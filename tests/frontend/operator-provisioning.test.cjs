const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function load(file, imports = {}, globals = {}) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8')
    .replace(/import\.meta\.env\.VITE_\w+/g, 'undefined');
  const javascript = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX },
  }).outputText;
  const context = { exports: {}, Error, ...globals, require(name) {
    if (name.endsWith("/ui/RoundedSelect")) return { RoundedSelect: "select" };
    if (name.endsWith("/ui/PageHeader")) return { PageHeader: "page-header" };
    if (name.endsWith("/ui/ParkingStatus")) return { ParkingStatus: "parking-status" };
    if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
    return imports[name];
  } };
  vm.runInNewContext(javascript, context);
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
    react: { useState(initial) {
      const i = cursor++;
      if (!(i in slots)) slots[i] = typeof initial === 'function' ? initial() : initial;
      return [slots[i], value => { slots[i] = typeof value === 'function' ? value(slots[i]) : value; }];
    }, useEffect(effect) { effects.push(effect); }, useMemo(compute) { return compute(); } },
    runtime: { jsx, jsxs: jsx },
    reset() { slots.length = 0; effects.length = 0; cursor = 0; },
    render(fn, props) { cursor = 0; return fn(props); }, effects,
  };
}
function nodes(tree) {
  if (!tree || typeof tree !== 'object') return [];
  const children = tree.props?.children;
  return [tree, ...[children].flat(Infinity).flatMap(nodes)];
}
const permissions = ['DEVICE_STATUS_VIEW', 'DEVICE_MANAGE', 'CASH_COLLECT', 'APPEAL_REVIEW', 'SLOT_OVERRIDE'];

function fixture({ sites = [site], existingOperators = [], failureMessage, pendingResponse } = {}) {
  const state = [], requests = [], created = [];
  let cursor = 0;
  const react = {
    useState(initial) {
      const index = cursor++;
      if (!(index in state)) state[index] = initial;
      return [state[index], value => { state[index] = typeof value === 'function' ? value(state[index]) : value; }];
    },
    useEffect() {},
  };
  const jsx = (type, props) => ({ type, props });
  const storage = new Map([['sp_access_token', 'owner-token'], ['sp_access_expires_at', '9999999999999']]);
  const auth = load('lib/authApi.ts', { './store': { store: {} } }, {
    AbortSignal,
    sessionStorage: { getItem: key => storage.get(key) ?? null },
    fetch: async (url, options) => {
      requests.push({ url, options });
      if (pendingResponse) await pendingResponse.promise;
      return {
        ok: !failureMessage, status: failureMessage ? 409 : 200,
        json: async () => failureMessage
          ? { success: false, message: failureMessage }
          : { success: true, data: { id: 'operator-id', ...JSON.parse(options.body) } },
      };
    },
  });
  const api = load('lib/parkingApi.ts', { './authApi': auth });
  const { CreateOperatorForm } = load('roles/owner/operators/CreateOperatorForm.tsx', {
    react, 'react/jsx-runtime': { jsx, jsxs: jsx },
    'react-dom': { createPortal: content => content },
    '../../../lib/parkingApi': api,
    '../../../lib/signInContact': load('lib/signInContact.ts'),
    '../../../components/icon/UntitledIcon': { UntitledIcon: 'icon' },
  }, { document: { body: {} } });
  const render = () => { cursor = 0; return CreateOperatorForm({ isOpen: true, sites, existingOperators, onCreated: value => created.push(value), onClose() {} }); };
  const change = (id, value) => nodes(render()).find(node => node.props?.id === id).props.onChange({ target: { value } });
  const submit = () => nodes(render()).find(node => node.type === 'form').props.onSubmit({ preventDefault() {} });
  const submitButton = () => nodes(render()).find(node => node.props?.type === 'submit');
  change('op-full-name', ' Staff ');
  change('op-email', ' STAFF@example.com ');
  return { render, change, submit, submitButton, requests, created };
}

test('name, email and site submit through the real API serializer without a password', async () => {
  const secondSite = { ...site, id: 'second-site' };
  const pendingResponse = deferred();
  const form = fixture({ sites: [site, secondSite], pendingResponse });
  form.change('op-site', secondSite.id);
  assert.equal(form.submitButton().props.disabled, false);
  assert.equal(nodes(form.render()).filter(node => node.type === 'input').length, 2);
  assert.equal(nodes(form.render()).some(node => node.props?.type === 'password'), false);
  const submission = form.submit();
  assert.equal(form.submitButton().props.disabled, true);
  await form.submit();
  assert.equal(form.created.length, 0);
  pendingResponse.resolve();
  await submission;
  assert.equal(form.requests.length, 1);
  const { url, options } = form.requests[0];
  assert.equal(url, 'http://localhost:5035/api/users');
  assert.equal(options.method, 'POST');
  assert.deepEqual(JSON.parse(options.body), {
    fullName: 'Staff', email: 'staff@example.com', siteIds: [secondSite.id], permissions,
  });
  assert.equal(form.created.length, 1);
  assert.equal(nodes(form.render()).find(node => node.props?.id === 'op-full-name').props.value, '');
  assert.equal(form.submitButton().props.disabled, true);
});

test('missing name, malformed email, and unknown site still prevent requests', async () => {
  for (const [id, value] of [['op-full-name', ' '], ['op-email', 'a@example'], ['op-email', 'a..b@example.com'], ['op-email', 'a@example..com'], ['op-email', '0912345678'], ['op-site', 'unknown']]) {
    const form = fixture();
    form.change(id, value);
    await form.submit();
    assert.equal(form.requests.length, 0);
    assert.equal(form.created.length, 0);
  }
  assert.equal(fixture({ sites: [] }).submitButton().props.disabled, true);
});

test('duplicate email from loaded operators prevents a request', async () => {
  const form = fixture({ existingOperators: [{ email: 'staff@example.com' }] });
  await form.submit();
  assert.equal(form.requests.length, 0);
  assert.ok(nodes(form.render()).some(node => node.props?.children === 'This email is already assigned to an existing operator.'));
});

for (const failureMessage of ['Email is already registered.', 'Cannot reach the authentication server. Please try again.', 'Every assigned site must be active and owned by this Owner.']) {
  test(`server rejection keeps operator form editable: ${failureMessage}`, async () => {
    const form = fixture({ failureMessage });
    await form.submit();
    assert.equal(form.requests.length, 1);
    assert.equal(form.created.length, 0);
    assert.equal(form.submitButton().props.disabled, false);
    const expectedMessage = failureMessage === 'Email is already registered.'
      ? 'This email is already registered in the system.' : failureMessage;
    assert.ok(nodes(form.render()).some(node => node.props?.children === expectedMessage));
  });
}

test('operator form permits retry after correcting an invalid email', async () => {
  const form = fixture();
  form.change('op-email', 'invalid');
  await form.submit();
  assert.equal(form.requests.length, 0);
  form.change('op-email', ' staff+test@example.com ');
  await form.submit();
  assert.equal(JSON.parse(form.requests[0].options.body).email, 'staff+test@example.com');
  assert.equal(form.created.length, 1);
});

test('operator screen loads backend list and active real sites, updates with committed account', async () => {
  const h = hooks();
  const api = load('lib/parkingApi.ts', { './authApi': { request: async (path, method, body, prefix) => ({ success: true, data: prefix === '/api/operators' ? [operator] : [site, { ...site, id: 'inactive', isActive: false }] }) } });
  const component = load('roles/owner/operators/OperatorManagement.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime, 'react-dom': { createPortal: content => content }, '../../../lib/operatorLive': { watchOperators: () => () => {} }, '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/parkingApi': api, './CreateOperatorForm': { CreateOperatorForm: 'create-form' }, '../parking-lots/SiteForm': { SiteForm: 'site-form' }, '../../../components/icon/UntitledIcon': { UntitledIcon: 'icon' },
  }).OperatorManagement;
  const render = () => h.render(component, { selectedSiteId: 'all', onOperatorsChanged() {} });
  render(); h.effects.slice().forEach(effect => effect()); await settle();
  assert.ok(nodes(render()).some(node => node.props?.children === operator.fullName));
  nodes(render()).find(node => node.type === 'button' && node.props.className?.includes('btn-primary')).props.onClick();
  const form = nodes(render()).find(node => node.type === 'create-form');
  assert.deepEqual(Array.from(form.props.sites, value => value.id), [site.id]);
  form.props.onCreated({ ...operator, id: 'new-id', fullName: 'New Staff' });
  assert.ok(nodes(render()).some(node => node.props?.children === 'New Staff'));
  assert.equal(nodes(render()).find(node => node.type === 'create-form').props.isOpen, false);
});

test('Create Operator opens with zero sites and passes the empty list to the form', async () => {
  const h = hooks();
  const api = load('lib/parkingApi.ts', { './authApi': { request: async () => ({ success: true, data: [] }) } });
  const component = load('roles/owner/operators/OperatorManagement.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime,
    'react-dom': { createPortal: content => content },
    '../../../lib/operatorLive': { watchOperators: () => () => {} },
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/parkingApi': api, './CreateOperatorForm': { CreateOperatorForm: 'create-form' },
    '../../../components/icon/UntitledIcon': { UntitledIcon: 'icon' },
  }).OperatorManagement;
  const render = () => h.render(component, { selectedSiteId: 'all', onOperatorsChanged() {} });
  render(); h.effects.slice().forEach(effect => effect()); await settle();
  const button = nodes(render()).find(node => node.type === 'button' && node.props.className?.includes('btn-primary'));
  assert.equal(button.props.disabled, false);
  button.props.onClick();
  const form = nodes(render()).find(node => node.type === 'create-form');
  assert.equal(form.props.isOpen, true);
  assert.equal(form.props.sites.length, 0);
});

for (const success of [true, false]) {
  test(`site form ${success ? 'waits for committed backend site' : 'keeps form and shows server failure'}`, async () => {
    const h = hooks(), pending = deferred(), calls = [], saved = [];
    const api = load('lib/parkingApi.ts', { './authApi': { request: (...args) => { calls.push(args); return pending.promise; } } });
    const component = load('roles/owner/parking-lots/SiteForm.tsx', { react: h.react, 'react/jsx-runtime': h.runtime, '../../../lib/parkingApi': api }).SiteForm;
    const render = () => h.render(component, { onSaved: site => saved.push(site), onCancel() {} });
    for (const [id, value] of [['site-code', ' ABC '], ['site-name', ' abc '], ['site-address', ' Address ']]) {
      nodes(render()).find(node => node.props?.id === id).props.onChange({ target: { value } });
    }
    const task = render().props.onSubmit({ preventDefault() {} });
    assert.equal(saved.length, 0);
    assert.equal(calls[0][1], 'POST'); assert.equal(calls[0][3], '/api/parking-lots');
    assert.deepEqual(JSON.parse(JSON.stringify(calls[0][2])), { code: 'ABC', name: 'abc', address: 'Address' });
    assert.equal(nodes(render()).find(node => node.type === 'fieldset').props.disabled, true);
    if (success) pending.resolve({ success: true, data: site });
    else pending.reject(new Error('Site code already exists.'));
    await task;
    assert.equal(saved.length, success ? 1 : 0);
    if (success) assert.equal(saved[0].id, site.id);
    else assert.equal(nodes(render()).find(node => node.props?.role === 'alert').props.children, 'Site code already exists.');
  });
}

test('Sites screen reads real sites and operator memberships instead of local fixtures', async () => {
  const h = hooks();
  const realSite = { ...site, code: 'ABC', address: 'Address', totalPhysicalCapacity: 0 };
  const api = load('lib/parkingApi.ts', { './authApi': { request: async (path, method, body, prefix) => ({ success: true, data: prefix === '/api/operators' ? [operator] : [realSite] }) } });
  const component = load('roles/owner/parking-lots/SiteManagement.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime, '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/parkingApi': api, './SiteForm': { SiteForm: 'site-form' },
  }).SiteManagement;
  const render = () => h.render(component, { onSiteCreated() {} });
  render(); h.effects[0](); await settle();
  assert.ok(nodes(render()).some(node => node.type === 'td' && node.props.children === 1));
  assert.ok(nodes(render()).some(node => node.type === 'td' && node.props.children === realSite.address));
  nodes(render()).find(node => node.type === 'button' && node.props.className === 'btn-primary').props.onClick();
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
  const component = load('roles/owner/structure/ParkingStructure.tsx', { react: h.react, 'react/jsx-runtime': h.runtime, '../../../lib/parkingApi': api }).ParkingStructure;
  const outer = outerHooks.render(component, { sites: [site], selectedSiteId: 'all' });
  const editor = nodes(outer).find(node => typeof node.type === 'function');
  h.reset();
  let changed = 0;
  const render = () => h.render(editor.type, { ...editor.props, onChanged() { changed++; } });
  render(); h.effects[0]();
  const form = index => nodes(render()).filter(node => node.type === 'form')[index];
  const fields = index => nodes(form(index)).filter(node => node.type === 'input' || node.type === 'select');
  return { render, form, fields, calls, get changed() { return changed; } };
}

test('Parking Structure creates units and slots through backend and reloads the persisted layout', async () => {
  const f = structureFixture(); await settle();
  f.fields(0)[1].props.onChange({ target: { value: 'Floor 1' } });
  f.fields(0)[3].props.onChange({ target: { value: '10' } });
  await f.form(0).props.onSubmit({ preventDefault() {} });
  const createUnit = f.calls.find(call => call.method === 'POST');
  assert.equal(createUnit.path, `/${site.id}/units`);
  assert.deepEqual(JSON.parse(JSON.stringify(createUnit.body)), { type: 'FLOOR', name: 'Floor 1', parentId: null, capacity: 10 });
  assert.equal(f.changed, 1);
  f.fields(1)[0].props.onChange({ target: { value: 'unit-id' } });
  f.fields(1)[1].props.onChange({ target: { value: ' A01 ' } });
  await f.form(1).props.onSubmit({ preventDefault() {} });
  const createSlot = f.calls.find(call => call.path.endsWith('/slots'));
  assert.deepEqual(JSON.parse(JSON.stringify(createSlot.body)), { unitId: 'unit-id', code: 'A01', vehicleType: 'CAR', type: 'STANDARD' });
  assert.equal(f.changed, 2);
  assert.equal(f.calls.filter(call => call.path.endsWith('/structure')).length, 3);
  assert.ok(nodes(f.render()).some(node => node.type === 'span' && JSON.stringify(node.props.children).includes('A01')));
});

test('failed structure mutation reports error and does not report saved state', async () => {
  const f = structureFixture(true); await settle();
  f.fields(0)[1].props.onChange({ target: { value: 'Floor 1' } });
  await f.form(0).props.onSubmit({ preventDefault() {} });
  assert.equal(f.changed, 0);
  assert.equal(nodes(f.render()).find(node => node.props?.role === 'alert').props.children, 'Capacity is invalid.');
  assert.equal(nodes(f.render()).some(node => node.props?.children === 'Changes saved.'), false);
});

test('empty backend site list never displays mock parking structure', () => {
  const h = hooks();
  const component = load('roles/owner/structure/ParkingStructure.tsx', { react: h.react, 'react/jsx-runtime': h.runtime, '../../../lib/parkingApi': {} }).ParkingStructure;
  const tree = h.render(component, { sites: [], selectedSiteId: 'all' });
  assert.ok(nodes(tree).some(node => node.props?.children === 'No sites found. Create a site in Sites first.'));
  assert.equal(nodes(tree).some(node => typeof node.type === 'function'), false);
});

test('structure site selector can change sites after entering from a filtered dashboard', () => {
  const h = hooks();
  const component = load('roles/owner/structure/ParkingStructure.tsx', { react: h.react, 'react/jsx-runtime': h.runtime, '../../../lib/parkingApi': {} }).ParkingStructure;
  const props = { sites: [site, { ...site, id: 'second-site' }], selectedSiteId: site.id };
  let tree = h.render(component, props);
  const select = nodes(tree).find(node => node.type === 'select');
  assert.equal(select.props.value, site.id);
  select.props.onChange({ target: { value: 'second-site' } });
  tree = h.render(component, props);
  assert.equal(nodes(tree).find(node => typeof node.type === 'function').props.siteId, 'second-site');
});
