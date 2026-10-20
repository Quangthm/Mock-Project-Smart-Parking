const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function load(file, imports) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8').replace('import.meta.env.VITE_PARKING_API_BASE_URL', 'undefined');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX } }).outputText;
  const context = {
    exports: {},
    Error,
    document: { body: {} },
    sessionStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    require: name => {
      if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
      return imports[name];
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}
const site = { id: '12345678-1234-1234-1234-123456789012', name: 'Real site', isActive: true, status: 'ACTIVE' };
const operator = { id: 'operator-id', fullName: 'Staff', email: 'staff@example.com', status: 'active', siteIds: [site.id], permissions: ['DEVICE_STATUS_VIEW'] };
const settle = () => new Promise(resolve => setImmediate(resolve));
function deferred() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}
function hooks() {
  const slots = [], effects = [];
  let cursor = 0;
  const jsx = (type, props) => ({ type, props });
  return {
    react: {
      useState(initial) {
        const i = cursor++;
        if (!(i in slots)) slots[i] = typeof initial === 'function' ? initial() : initial;
        return [slots[i], value => { slots[i] = typeof value === 'function' ? value(slots[i]) : value; }];
      },
      useEffect(effect) { effects.push(effect); },
      useRef(initial) { return { current: initial }; },
      useMemo(fn) { return fn(); },
      useId() { return 'mock-id'; },
    },
    runtime: { jsx, jsxs: jsx },
    reset() { slots.length = 0; effects.length = 0; cursor = 0; },
    render(fn, props) { cursor = 0; return fn(props); },
    effects,
  };
}
function nodes(tree) {
  if (!tree || typeof tree !== 'object') return [];
  const children = tree.props?.children;
  return [tree, ...[children].flat(Infinity).flatMap(nodes)];
}
function formFixture(sites = [site]) {
  const h = hooks(), pending = deferred(), calls = [], created = [];
  const api = load('lib/parkingApi.ts', { './authApi': { request: (...args) => { calls.push(args); return pending.promise; } } });
  const component = load('roles/owner/operators/CreateOperatorForm.tsx', {
    react: h.react,
    'react-dom': { createPortal: c => c },
    'react/jsx-runtime': h.runtime,
    '../../../lib/parkingApi': api,
    '../../../lib/signInContact': load('lib/signInContact.ts', {}),
    '../../../components/forms/PasswordVisibilityIcon': { PasswordVisibilityIcon: () => null },
    '../../../components/icon/UntitledIcon': { UntitledIcon: () => null },
  }).CreateOperatorForm;
  const render = () => h.render(component, { sites, isOpen: true, onCreated: value => created.push(value), onCancel() {}, onClose() {} });
  const change = (id, value) => {
    const target = nodes(render()).find(node =>
      node.props?.id === id ||
      node.props?.id === `op-${id.replace('operator-', '')}` ||
      (id === 'operator-name' && node.props?.id === 'op-full-name')
    );
    if (target) target.props.onChange({ target: { value } });
    if (id === 'operator-password' || id === 'op-password') {
      const confirm = nodes(render()).find(node => node.props?.id === 'op-confirm-password');
      if (confirm) confirm.props.onChange({ target: { value } });
    }
  };
  change('operator-name', ' Staff '); change('operator-email', ' staff@example.com '); change('operator-password', 'Password@123');
  const submit = () => {
    const root = render();
    const formNode = nodes(root).find(node => node.type === 'form') || root;
    return formNode.props.onSubmit({ preventDefault() {} });
  };
  return { render, change, calls, created, pending, submit };
}

test('owner form sends backend create request, awaits commit and never imports local user storage', async () => {
  const f = formFixture();
  const task = f.submit();
  assert.equal(f.created.length, 0);
  assert.equal(f.calls.length, 1);
  assert.equal(f.calls[0][1], 'POST'); assert.equal(f.calls[0][3], '/api/users');
  assert.deepEqual(JSON.parse(JSON.stringify(f.calls[0][2])), { fullName: 'Staff', email: 'staff@example.com', password: 'Password@123', siteIds: [site.id], permissions: ['DEVICE_STATUS_VIEW', 'DEVICE_MANAGE', 'CASH_COLLECT', 'APPEAL_REVIEW', 'SLOT_OVERRIDE'] });
  assert.equal(nodes(f.render()).find(node => node.type === 'fieldset').props.disabled, true);
  await f.submit(); assert.equal(f.calls.length, 1);
  f.pending.resolve({ success: true, data: operator }); await task;
  assert.equal(f.created[0].id, operator.id);
  assert.equal(nodes(f.render()).find(node => node.props?.id === 'op-password').props.value, '');
});

for (const message of ['Email is already registered.', 'Cannot reach the authentication server. Please try again.', 'Every assigned site must be active and owned by this Owner.']) {
  test(`create failure stays in form and exposes backend error: ${message}`, async () => {
    const f = formFixture(); const task = f.submit();
    f.pending.reject(new Error(message)); await task;
    assert.equal(f.created.length, 0);
    assert.ok(nodes(f.render()).some(node => node.props?.role === 'alert' || JSON.stringify(node.props?.children).includes(message)));
    assert.equal(nodes(f.render()).find(node => node.type === 'fieldset').props.disabled, false);
  });
}

for (const email of ['a@example', 'a..b@example.com', 'a@example..com', '0912345678']) {
  test(`operator creation rejects malformed email before sending request: ${email}`, async () => {
    const f = formFixture(); f.change('operator-email', email); await f.submit();
    assert.equal(f.calls.length, 0); assert.equal(f.created.length, 0);
    assert.ok(nodes(f.render()).some(node => JSON.stringify(node.props?.children).includes('valid email')));
    f.change('operator-email', ' staff+test@example.com ');
    const task = f.submit();
    assert.equal(f.calls[0][2].email, 'staff+test@example.com');
    f.pending.resolve({ success: true, data: operator }); await task;
    assert.equal(f.created.length, 1);
  });
}

test('All Sites expands to real IDs, never the mock all identifier', async () => {
  const second = { ...site, id: '22345678-1234-1234-1234-123456789012' };
  const f = formFixture([site, second]); f.change('operator-site', '');
  const task = f.submit();
  assert.deepEqual(Array.from(f.calls[0][2].siteIds), [site.id, second.id]);
  f.pending.resolve({ success: true, data: operator }); await task;
});

for (const password of ['weakpass', 'Password@12345678']) {
  test(`invalid password is rejected before sending request: ${password}`, async () => {
    const f = formFixture(); f.change('operator-password', password); await f.submit();
    assert.equal(f.calls.length, 0); assert.equal(f.created.length, 0);
    assert.ok(nodes(f.render()).some(node => node.props?.role === 'alert' || JSON.stringify(node.props?.children).includes('Password must be 8–15 characters')));
  });
}

test('mock or missing site cannot create an account', async () => {
  const f = formFixture([]);
  await f.submit();
  assert.equal(f.calls.length, 0);
});

test('operator screen loads backend list and active real sites, updates with committed account', async () => {
  const h = hooks();
  const api = load('lib/parkingApi.ts', { './authApi': { request: async (path, method, body, prefix) => ({ success: true, data: prefix === '/api/operators' ? [operator] : [site, { ...site, id: 'inactive', isActive: false }] }) } });
  const component = load('roles/owner/operators/OperatorManagement.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime,
    'react-dom': { createPortal: c => c },
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/parkingApi': api, './CreateOperatorForm': { CreateOperatorForm: 'create-form' }, '../parking-lots/SiteForm': { SiteForm: 'site-form' }, '../../../components/icon/UntitledIcon': { UntitledIcon: 'icon' },
  }).OperatorManagement;
  const render = () => h.render(component, { selectedSiteId: 'all', onOperatorsChanged() {} });
  render(); for (const effect of h.effects) effect(); await settle();
  assert.ok(nodes(render()).some(node => node.props?.children === operator.fullName));
  nodes(render()).find(node => node.type === 'button' && node.props?.className?.includes('btn-primary')).props.onClick();
  const form = nodes(render()).find(node => node.type === 'create-form');
  assert.deepEqual(Array.from(form.props.sites, value => value.id), [site.id]);
  form.props.onCreated({ ...operator, id: 'new-id', fullName: 'New Staff' });
  assert.ok(nodes(render()).some(node => node.props?.children === 'New Staff'));
  assert.equal(nodes(render()).find(node => node.type === 'create-form').props.isOpen, false);
});

test('New Operator stays clickable with zero sites and shows warning to create active site', async () => {
  const h = hooks(), calls = [];
  const api = load('lib/parkingApi.ts', { './authApi': { request: async (...args) => { calls.push(args); return { success: true, data: [] }; } } });
  const component = load('roles/owner/operators/OperatorManagement.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime,
    'react-dom': { createPortal: c => c },
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/parkingApi': api, './CreateOperatorForm': { CreateOperatorForm: 'create-form' },
    '../../../components/icon/UntitledIcon': { UntitledIcon: () => null },
  }).OperatorManagement;
  let changed = 0;
  const render = () => h.render(component, { selectedSiteId: 'all', onOperatorsChanged() { changed++; } });
  render(); for (const effect of h.effects) effect(); await settle();
  const button = nodes(render()).find(node => node.type === 'button' && node.props?.className?.includes('btn-primary'));
  assert.equal(button.props.disabled, false);
  button.props.onClick();
  assert.equal(nodes(render()).find(node => node.type === 'create-form').props.isOpen, true);
  assert.ok(nodes(render()).some(node => JSON.stringify(node.props?.children).includes('Create an active site in Sites before assigning an operator.')));
});

for (const success of [true, false]) {
  test(`site form ${success ? 'waits for committed backend site' : 'keeps form and shows server failure'}`, async () => {
    const h = hooks(), pending = deferred(), calls = [], saved = [];
    const api = load('lib/parkingApi.ts', { './authApi': { request: (...args) => { calls.push(args); return pending.promise; } } });
    const component = load('roles/owner/parking-lots/SiteForm.tsx', {
      react: h.react,
      'react/jsx-runtime': h.runtime,
      '../../../lib/parkingApi': api,
      '../../../lib/authApi': { AuthApiError: Error },
      '../data/data': { ownerData: { addAuditLog() {}, saveLot() {}, getLotsByOwner: () => [] } },
      '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
      '../../../components/icon/UntitledIcon': { UntitledIcon: () => null },
      '../../../lib/3d/parking3DConfig': { ALLOWED_PARKING_MODELS: { outdoor: { path: '', fileName: '' } } },
    }).SiteForm;
    const render = () => h.render(component, { onSaved: site => saved.push(site), onCancel() {} });
    nodes(render()).find(node => node.props?.id?.endsWith('site-name')).props.onChange({ target: { value: ' abc ' } });
    nodes(render()).find(node => node.props?.id?.endsWith('street')).props.onChange({ target: { value: ' Address ' } });
    const task = render().props.onSubmit({ preventDefault() {} });
    assert.equal(saved.length, 0);
    assert.equal(calls[0][1], 'POST'); assert.equal(calls[0][3], '/api/parking-lots');
    assert.deepEqual(JSON.parse(JSON.stringify(calls[0][2])), { code: 'SPK-HCM-ABC', name: 'abc', address: 'Address, Quận 1, TP. Hồ Chí Minh' });
    assert.equal(nodes(render()).find(node => node.type === 'fieldset').props.disabled, true);
    if (success) pending.resolve({ success: true, data: site });
    else pending.reject(new Error('Site code already exists.'));
    await task;
    assert.equal(saved.length, success ? 1 : 0);
    if (success) assert.equal(saved[0].id, site.id);
    else assert.ok(JSON.stringify(nodes(render()).find(node => node.props?.role === 'alert').props.children).includes('Site code already exists.'));
  });
}

test('Sites screen reads real sites and operator memberships instead of local fixtures', async () => {
  const h = hooks();
  const realSite = { ...site, code: 'ABC', address: 'Address', totalPhysicalCapacity: 0 };
  const api = load('lib/parkingApi.ts', { './authApi': { request: async (path, method, body, prefix) => ({ success: true, data: prefix === '/api/operators' ? [operator] : [realSite] }) } });
  const component = load('roles/owner/parking-lots/SiteManagement.tsx', {
    react: h.react,
    'react-dom': { createPortal: c => c },
    'react/jsx-runtime': h.runtime,
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/parkingApi': api,
    './SiteForm': { SiteForm: 'site-form' },
    '../../../components/icon/UntitledIcon': { UntitledIcon: () => null },
  }).SiteManagement;
  const render = () => h.render(component, { onSiteCreated() {} });
  render(); for (const effect of h.effects) effect(); await settle();
  assert.ok(nodes(render()).some(node => JSON.stringify(node.props?.children).includes('Staff')));
  assert.ok(nodes(render()).some(node => node.type === 'td' && node.props.children === realSite.address));
  nodes(render()).find(node => node.type === 'button' && node.props?.className?.includes('btn-primary')).props.onClick();
  const form = nodes(render()).find(node => node.type === 'site-form');
  form.props.onSaved({ ...realSite, id: 'new-site', address: 'New address' });
  assert.ok(nodes(render()).some(node => node.props?.children === 'New address'));
});

test('Owner dashboard shares backend sites with overview, structure and refreshes after site creation', async () => {
  const h = hooks();
  let listed = [site];
  const api = load('lib/parkingApi.ts', { './authApi': { request: async (path, method, body, prefix) => ({ success: true, data: prefix === '/api/operators' ? [operator] : listed }) } });
  const imports = {
    react: h.react, 'react/jsx-runtime': h.runtime,
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id', onboardingComplete: true } }) },
    '../../../lib/parkingApi': api,
    '../parking-lots/CreateParkingLotForOwner': { OnboardingWizard: 'onboarding' },
    './OwnerOverview': { OwnerOverview: 'overview' },
    '../operators/OperatorManagement': { OperatorManagement: 'operators' },
    '../policies/PolicySettings': { PolicySettings: 'policies' },
    '../parking-lots/SiteManagement': { SiteManagement: 'sites' },
    '../parking-lots/LotManagement': { LotManagement: 'lots' },
    '../revenue/RevenueDashboard': { RevenueDashboard: 'revenue' },
    '../structure/ParkingStructure': { ParkingStructure: 'structure' },
    '../../../components/layout/DashboardSidebar': { DashboardSidebar: 'sidebar' },
  };
  const component = load('roles/owner/dashboard/OwnerDashboard.tsx', imports).OwnerDashboard;
  const render = () => h.render(component, {});
  render(); h.effects[0](); await settle();
  const overview = nodes(render()).find(node => node.type === 'overview');
  assert.deepEqual(Array.from(overview.props.sites, value => value.id), [site.id]);
  assert.equal(overview.props.operators[0].id, operator.id);
  const sidebar = render();
  sidebar.props.groups.flatMap(group => group.items).find(item => item.id === 'sites').onClick();
  const sitesPage = nodes(render()).find(node => node.type === 'sites');
  listed = [site, { ...site, id: 'new-site', name: 'New Site' }];
  sitesPage.props.onSiteCreated();
  render(); h.effects[h.effects.length - 2](); await settle();
  render().props.groups.flatMap(group => group.items).find(item => item.id === 'structure').onClick();
  const structure = nodes(render()).find(node => node.type === 'structure');
  assert.deepEqual(Array.from(structure.props.sites, value => value.id), [site.id, 'new-site']);
});

function structureFixture(failUnit = false) {
  const outerHooks = hooks(), h = hooks(), calls = [];
  const layout = { site: { ...site, totalPhysicalCapacity: 0 }, units: [], slots: [], paths: [], capacityViews: [] };
  const api = load('lib/parkingApi.ts', { './authApi': { request: async (path, method, body, prefix) => {
    calls.push({ path, method, body, prefix });
    if (path.endsWith('/units')) {
      if (failUnit) throw new Error('Capacity is invalid.');
      layout.units.push({ id: 'unit-id', name: body.name, type: body.type, parentId: body.parentId, maxCapacity: body.capacity });
      return { success: true, data: 'unit-id' };
    }
    if (path.endsWith('/slots')) {
      layout.slots.push({ ...body, id: 'slot-id', isPhysicallyOccupied: false, operationalStatus: 'OPERATIONAL' });
      return { success: true, data: 'slot-id' };
    }
    return { success: true, data: JSON.parse(JSON.stringify(layout)) };
  } } });
  const structureImports = {
    react: h.react,
    'react/jsx-runtime': h.runtime,
    '../../../lib/parkingApi': api,
    '../dashboard/BackupCapacityPanel': { BackupCapacityPanel: () => null },
    '../../../components/icon/UntitledIcon': { UntitledIcon: () => null },
  };
  const component = load('roles/owner/structure/ParkingStructure.tsx', structureImports).ParkingStructure;
  let changed = 0;
  const render = () => h.render(component, { sites: [site], selectedSiteId: site.id, onChanged() { changed++; } });
  render();
  h.effects[0]();
  render();
  h.effects[3]();
  const openModal = type => {
    const text = type === 'unit' ? 'Add unit' : 'Add slot';
    const button = nodes(render()).find(node => node.type === 'button' && JSON.stringify(node.props?.children).includes(text));
    button.props.onClick();
  };
  const form = () => nodes(render()).find(node => node.type === 'form');
  const fields = () => nodes(form()).filter(node => node.type === 'input' || node.type === 'select');
  return { render, openModal, form, fields, calls, get changed() { return changed; } };
}

test('Parking Structure creates units and slots through backend and reloads the persisted layout', async () => {
  const f = structureFixture(); await settle();
  f.openModal('unit');
  f.fields()[1].props.onChange({ target: { value: 'Floor 1' } });
  f.fields()[2].props.onChange({ target: { value: '10' } });
  await f.form().props.onSubmit({ preventDefault() {} });
  const createUnit = f.calls.find(call => call.method === 'POST');
  assert.equal(createUnit.path, `/${site.id}/units`);
  assert.deepEqual(JSON.parse(JSON.stringify(createUnit.body)), { type: 'FLOOR', name: 'Floor 1', parentId: null, capacity: 10 });
  assert.equal(f.changed, 1);
  f.openModal('slot');
  f.fields()[0].props.onChange({ target: { value: 'unit-id' } });
  f.fields()[1].props.onChange({ target: { value: ' A01 ' } });
  await f.form().props.onSubmit({ preventDefault() {} });
  const createSlot = f.calls.find(call => call.path.endsWith('/slots'));
  assert.deepEqual(JSON.parse(JSON.stringify(createSlot.body)), { unitId: 'unit-id', code: 'A01', vehicleType: 'CAR', type: 'STANDARD' });
  assert.equal(f.changed, 2);
  assert.equal(f.calls.filter(call => call.path.endsWith('/structure')).length, 3);
  assert.ok(nodes(f.render()).some(node => JSON.stringify(node.props?.children).includes('A01')));
});

test('failed structure mutation reports error and does not report saved state', async () => {
  const f = structureFixture(true); await settle();
  f.openModal('unit');
  f.fields()[1].props.onChange({ target: { value: 'Floor 1' } });
  await f.form().props.onSubmit({ preventDefault() {} });
  assert.equal(f.changed, 0);
  assert.ok(nodes(f.render()).some(node => node.props?.role === 'alert' && JSON.stringify(node.props?.children).includes('Capacity is invalid.')));
  assert.equal(nodes(f.render()).some(node => node.props?.children === 'Changes saved.'), false);
});

test('empty backend site list never displays mock parking structure', () => {
  const h = hooks();
  const component = load('roles/owner/structure/ParkingStructure.tsx', {
    react: h.react,
    'react/jsx-runtime': h.runtime,
    '../../../lib/parkingApi': {},
    '../dashboard/BackupCapacityPanel': { BackupCapacityPanel: () => null },
    '../../../components/icon/UntitledIcon': { UntitledIcon: () => null },
  }).ParkingStructure;
  const tree = h.render(component, { sites: [], selectedSiteId: 'all' });
  assert.ok(nodes(tree).some(node => node.props?.children === 'No parking sites are available for this account.'));
  assert.equal(nodes(tree).some(node => node.type === 'form'), false);
});

test('structure site selector can change sites after entering from a filtered dashboard', () => {
  const h = hooks();
  const component = load('roles/owner/structure/ParkingStructure.tsx', {
    react: h.react,
    'react/jsx-runtime': h.runtime,
    '../../../lib/parkingApi': {},
    '../dashboard/BackupCapacityPanel': { BackupCapacityPanel: () => null },
    '../../../components/icon/UntitledIcon': { UntitledIcon: () => null },
  }).ParkingStructure;
  const props = { sites: [site, { ...site, id: 'second-site' }], selectedSiteId: site.id };
  let tree = h.render(component, props);
  for (const effect of h.effects) effect();
  tree = h.render(component, props);
  const select = nodes(tree).find(node => node.type === 'select');
  assert.equal(select.props.value, site.id);
  select.props.onChange({ target: { value: 'second-site' } });
  tree = h.render(component, props);
  assert.equal(nodes(tree).find(node => node.type === 'select').props.value, 'second-site');
});
