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
    if (name === 'react-dom') return { createPortal: content => content };
    if (name.endsWith("/ui/RoundedSelect")) return { RoundedSelect: "select" };
    if (name.endsWith("/ui/PageHeader")) return { PageHeader: "page-header" };
    if (name.endsWith("/ui/ParkingStatus")) return { ParkingStatus: "parking-status" }; if (name.endsWith("/ui/Badge")) return { Badge: "badge" };
    if (name.endsWith("/forms/PasswordVisibilityIcon")) return { PasswordVisibilityIcon: "password-icon" };
    if (name.endsWith("/ui/Button")) return { Button: "button" };
    if (name.endsWith("/icon/UntitledIcon")) return { UntitledIcon: 'icon' };
    if (name.endsWith("/feedback/ErrorBoundary")) return { ErrorBoundary: ({ children }) => children };
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
    }, useEffect(effect) { effects.push(effect); }, useMemo(compute) { return compute(); },
    useRef(initial) { const i = cursor++; if (!(i in slots)) slots[i] = { current: initial }; return slots[i]; },
    useCallback(callback) { return callback; } },
    runtime: { jsx, jsxs: jsx },
    reset() { slots.length = 0; effects.length = 0; cursor = 0; },
    render(fn, props) { cursor = 0; return fn(props); }, effects,
  };
}
function nodes(tree) {
  if (!tree || typeof tree !== 'object') return [];
  let nested = [];
  if (typeof tree.type === 'function') {
    try { const res = tree.type(tree.props); if (res) nested = [res]; } catch {}
  }
  const children = tree.props?.children;
  return [tree, ...[children, tree.props?.actions, ...nested].flat(Infinity).flatMap(nodes)];
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

function operatorScreen(h, api) {
  return load('roles/owner/operators/OperatorManagement.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime,
    '../../../lib/operatorLive': { watchOperators: () => () => {} },
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/parkingApi': api,
    './CreateOperatorForm': { CreateOperatorForm: 'create-form' },
    '../../../lib/uiToast': { showUiToast() {} },
    '../../../lib/formatStatus': { formatStatus: value => value },
  }).OperatorManagement;
}

test('operator screen loads backend accounts and active sites, then displays a committed account', async () => {
  const h = hooks();
  const api = { operatorsApi: { list: async () => [operator] }, parkingApi: { list: async () => [site, { ...site, id: 'inactive', isActive: false }] } };
  const component = operatorScreen(h, api);
  const render = () => h.render(component, { selectedSiteId: 'all', onOperatorsChanged() {} });
  render(); h.effects.splice(0).forEach(effect => effect()); await settle();
  assert.ok(nodes(render()).some(node => node.props?.children === operator.fullName));
  nodes(render()).find(node => node.type === 'button' && node.props.className?.includes('btn-primary')).props.onClick();
  const form = nodes(render()).find(node => node.type === 'create-form');
  assert.deepEqual(Array.from(form.props.sites, value => value.id), [site.id]);
  form.props.onCreated({ ...operator, id: 'new-id', fullName: 'New Staff' });
  assert.ok(nodes(render()).some(node => node.props?.children === 'New Staff'));
  assert.equal(nodes(render()).find(node => node.type === 'create-form').props.isOpen, false);
});

test('Create Operator opens with zero backend sites and passes an empty list to its form', async () => {
  const h = hooks();
  const component = operatorScreen(h, { operatorsApi: { list: async () => [] }, parkingApi: { list: async () => [] } });
  const render = () => h.render(component, { selectedSiteId: 'all', onOperatorsChanged() {} });
  render(); h.effects.splice(0).forEach(effect => effect()); await settle();
  const button = nodes(render()).find(node => node.type === 'button' && node.props.className?.includes('btn-primary'));
  assert.equal(button.props.disabled, false);
  button.props.onClick();
  const form = nodes(render()).find(node => node.type === 'create-form');
  assert.equal(form.props.isOpen, true);
  assert.equal(form.props.sites.length, 0);
});

function siteManagementHook(h, parkingApi, operatorsApi) {
  return load('roles/owner/parking-lots/useSiteManagement.ts', {
    react: h.react,
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/parkingApi': { parkingApi, operatorsApi },
  }).useSiteManagement;
}

test('site management waits for backend sites and filters the committed list', async () => {
  const h = hooks(), pending = deferred(), saved = [];
  const hook = siteManagementHook(h, { list: () => pending.promise }, { list: async () => [operator] });
  const render = () => h.render(hook, (value, created) => saved.push([value, created]));
  assert.equal(render().loading, true);
  h.effects.splice(0).forEach(effect => effect());
  assert.equal(render().sites.length, 0);
  pending.resolve([site]); await settle();
  assert.deepEqual(Array.from(render().filteredSites, value => value.id), [site.id]);
  render().handleSavedSite({ ...site, id: 'new-site', name: 'New Site' });
  assert.deepEqual(Array.from(render().filteredSites, value => value.id), [ 'new-site', site.id ]);
  assert.equal(saved.length, 1);
  assert.equal(saved[0][1], true);
});

test('site management shows a backend failure without inventing fixture sites', async () => {
  const h = hooks();
  const hook = siteManagementHook(h, { list: async () => { throw new Error('Site service unavailable.'); } }, { list: async () => [] });
  const render = () => h.render(hook, () => {});
  render(); h.effects.splice(0).forEach(effect => effect()); await settle();
  assert.equal(render().loading, false);
  assert.equal(render().error, 'Site service unavailable.');
  assert.equal(render().filteredSites.length, 0);
});

test('Sites screen displays backend site address and assigned operator, and opens site creation', () => {
  const h = hooks();
  const realSite = { ...site, code: 'ABC', address: 'Address', totalPhysicalCapacity: 8 };
  const model = {
    sites: [realSite], operators: [operator], loading: false, error: '', message: '',
    searchTerm: '', statusFilter: 'all', filteredSites: [realSite],
    setSearchTerm() {}, setStatusFilter() {}, setReload() {},
    editingSite: undefined, showFormModal: false, inspectSite: null, assignModal: null,
    assignBusy: false, assignError: '',
  };
  const component = load('roles/owner/parking-lots/SiteManagement.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime,
    '../../../lib/parkingApi': { parkingApi: { list: async () => [realSite] } },
    './useSiteManagement': { useSiteManagement: () => model },
    './SiteManagementDialogs': { SiteManagementDialogs: 'dialogs' },
    './CreateParkingLotForOwner': { OnboardingWizard: 'wizard' },
  }, { document: { body: {} } }).SiteManagement;
  const render = () => h.render(component, { onSiteCreated() {} });
  assert.ok(nodes(render()).some(node => node.type === 'td' && node.props.children === realSite.address));
  assert.ok(nodes(render()).some(node => node.props?.title === `${operator.fullName} (${operator.email})`));
  nodes(render()).find(node => node.type === 'button' && node.props.className?.includes('btn-primary')).props.onClick();
  assert.ok(nodes(render()).some(node => node.type === 'wizard'));
});

test('Owner dashboard shares backend sites with overview and refreshes after site creation', async () => {
  const h = hooks(); let listed = [site];
  const api = { parkingApi: { list: async () => listed }, operatorsApi: { list: async () => [operator] } };
  const component = load('roles/owner/dashboard/OwnerDashboard.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime,
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id', onboardingComplete: true } }) },
    '../../../lib/parkingApi': api,
    '../../../lib/ownerLotSetup': { reconcileOwnerLotSetups: async () => {} },
    '../parking-lots/CreateParkingLotForOwner': { OnboardingWizard: 'onboarding' },
    './OwnerOverview': { OwnerOverview: 'overview' },
    '../operators/OperatorManagement': { OperatorManagement: 'operators' },
    '../policies/PolicySettings': { PolicySettings: 'policies' },
    '../parking-lots/SiteManagement': { SiteManagement: 'sites' },
    '../parking-lots/LotManagement': { LotManagement: 'lots' },
    '../revenue/RevenueDashboard': { RevenueDashboard: 'revenue' },
    '../structure/ParkingStructure': { ParkingStructure: 'structure' },
    '../../../components/layout/DashboardSidebar': { DashboardSidebar: 'sidebar' },
  }, { window: { addEventListener() {}, removeEventListener() {} } }).OwnerDashboard;
  const render = () => h.render(component, {});
  render(); h.effects.splice(0).forEach(effect => effect()); await settle();
  const overview = nodes(render()).find(node => node.type === 'overview');
  assert.deepEqual(Array.from(overview.props.sites, value => value.id), [site.id]);
  assert.equal(overview.props.operators[0].id, operator.id);
  render().props.groups.flatMap(group => group.items).find(item => item.id === 'sites').onClick();
  const sitesPage = nodes(render()).find(node => node.type === 'sites');
  listed = [site, { ...site, id: 'new-site', name: 'New Site' }];
  sitesPage.props.onSiteCreated(listed[1], true);
  render(); h.effects.splice(0).forEach(effect => effect()); await settle();
  render().props.groups.flatMap(group => group.items).find(item => item.id === 'structure').onClick();
  const structure = nodes(render()).find(node => node.type === 'structure');
  assert.deepEqual(Array.from(structure.props.sites, value => value.id), [site.id, 'new-site']);
});

function structureScreen(h, parkingApi = {}) {
  return load('roles/owner/structure/ParkingStructure.tsx', {
    react: h.react, 'react/jsx-runtime': h.runtime,
    '../../../lib/parkingApi': { parkingApi },
    '../dashboard/BackupCapacityPanel': { BackupCapacityPanel: 'backup' },
    '../../../components/3d/Parking3DViewer': { Parking3DViewer: '3d-viewer' },
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-id' } }) },
    '../../../lib/uiToast': { showUiToast() {} },
    '../data/data': { ownerData: { getLotsByOwner: () => [] } },
    '../../../lib/3d/parking3DConfig': { ALLOWED_PARKING_MODELS: { outdoor: { path: '/models/outdoor.glb' } } },
    '../../../lib/3d/modelSlotCapacity': { useModelStructure: () => ({ url: '', analysis: null, loading: false, error: '' }) },
    '../../../lib/3d/ownerModelStorage': { loadOwnerModelUrl: async () => null },
    '../../../lib/operationalLayout': { formatSlotLabel: value => value, mapSlotVisualState: () => 'available' },
    '../../../lib/3d/meshSlotMapping': { suggestSlotCode: () => '' },
    '../../../lib/structureRules': load('lib/structureRules.ts'),
    './AccessPathsPanel': { AccessPathsPanel: () => null },
    './CapacitySummary': { CapacitySummary: () => null },
    './RegisteredSlotsPanel': { RegisteredSlotsPanel: props => h.runtime.jsx('div', { 'aria-label': `Space ${props.slots?.[0]?.code}` }) },
    './ServiceAvailabilityPanel': { ServiceAvailabilityPanel: () => null },
    './SlotCompatibilityDialog': { SlotCompatibilityDialog: () => null },
    './SlotRegistrationPanel': { SlotRegistrationPanel: () => null },
    './StructureTree': { StructureTree: () => null },
    './StructureFeedback': { structureErrorMessage: err => err?.message || String(err) },
  }).ParkingStructure;
}

test('Parking Structure reads persisted layout and exposes live slot details without creation controls', async () => {
  const h = hooks(), calls = [];
  const layout = { site, units: [], slots: [{ id: 'slot-id', code: 'A01', unitId: 'unit-id', vehicleType: 'CAR', type: 'STANDARD', isPhysicallyOccupied: false, operationalStatus: 'OPERATIONAL' }], paths: [], capacityViews: [] };
  const component = structureScreen(h, { structure: async id => { calls.push(id); return layout; } });
  const render = () => h.render(component, { sites: [site], selectedSiteId: site.id });
  render(); h.effects.splice(0).forEach(effect => effect());
  render(); h.effects.splice(0).forEach(effect => effect()); await settle();
  const tree = render();
  assert.deepEqual(calls, [site.id]);
  assert.ok(nodes(tree).some(node => node.props?.['aria-label']?.includes('Space A01')));
  assert.equal(nodes(tree).some(node => node.props?.children === 'Add unit' || node.props?.children === 'Add slot'), false);
});

test('failed structure load reports the backend error and shows no saved state', async () => {
  const h = hooks();
  const component = structureScreen(h, { structure: async () => { throw new Error('Capacity service unavailable.'); } });
  const render = () => h.render(component, { sites: [site], selectedSiteId: site.id });
  render(); h.effects.splice(0).forEach(effect => effect());
  render(); h.effects.splice(0).forEach(effect => effect()); await settle();
  assert.ok(nodes(render()).some(node => node.props?.children === 'Capacity service unavailable.'));
  assert.equal(nodes(render()).some(node => node.props?.children === 'Changes saved.'), false);
});

test('empty backend site list never displays mock parking structure', () => {
  const h = hooks();
  const component = structureScreen(h);
  const tree = h.render(component, { sites: [], selectedSiteId: 'all' });
  assert.ok(nodes(tree).some(node => node.props?.children === 'No parking sites are available for this account.'));
  assert.equal(nodes(tree).some(node => node.props?.['aria-label']?.startsWith('Space ')), false);
});

test('structure site selector changes the selected site', () => {
  const h = hooks();
  const component = structureScreen(h);
  const props = { sites: [site, { ...site, id: 'second-site' }], selectedSiteId: site.id };
  h.render(component, props); h.effects.splice(0)[0]();
  let tree = h.render(component, props);
  const select = nodes(tree).find(node => node.type === 'select' && node.props.value === site.id);
  assert.ok(select);
  select.props.onChange({ target: { value: 'second-site' } });
  tree = h.render(component, props);
  assert.equal(nodes(tree).find(node => node.type === 'select' && node.props.value === 'second-site').props.value, 'second-site');
});
