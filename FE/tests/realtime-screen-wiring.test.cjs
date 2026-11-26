const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');
const ts = require('../node_modules/typescript');
const settle = () => new Promise(resolve => setImmediate(resolve));
function screen(file, mocks, globals = {}, extra = '') {
  const slots = [], effects = [];
  let index = 0;
  const hooks = {
    useState(initial) { const i = index++; slots[i] ??= { value: typeof initial === 'function' ? initial() : initial };
      return [slots[i].value, value => { slots[i].value = typeof value === 'function' ? value(slots[i].value) : value; }]; },
    useRef(value) { const i = index++; slots[i] ??= { current: value }; return slots[i]; },
    useEffect(run, deps) { const i = index++, old = slots[i];
      if (!old || deps.some((value, n) => !Object.is(value, old.deps[n]))) {
        old?.cleanup?.(); slots[i] = { deps }; effects.push(() => { slots[i].cleanup = run(); });
      }
    },
  };
  const jsx = (type, props) => ({ type, props });
  const source = fs.readFileSync(path.join(__dirname, '../src', file), 'utf8') + extra;
  const context = { exports: {}, URLSearchParams, ...globals, require(name) {
    if (name === 'react') return hooks;
    if (name === 'react/jsx-runtime') return { jsx, jsxs: jsx };
    return mocks[name] ?? {};
  } };
  vm.runInNewContext(ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText, context);
  return {
    async render(name, props = {}) {
      let tree;
      for (let n = 0; n < 3; n++) {
        index = 0; tree = context.exports[name](props);
        effects.splice(0).forEach(run => run()); await settle();
      }
      return tree;
    },
    unmount() { slots.forEach(slot => slot?.cleanup?.()); },
  };
}
function find(node, type) {
  if (!node || typeof node !== 'object') return null;
  if (node.type === type) return node;
  for (const child of [node.props?.children].flat(Infinity)) { const match = find(child, type); if (match) return match; }
  return null;
}

test('Owner remote operator changes update dashboard and the managed roster without reloading sites', async () => {
  let changed, rosterReads = 0, siteReads = 0, stopped = 0;
  let accounts = [{ id: 'operator-1', status: 'pending_verification' }];
  const f = screen('roles/owner/dashboard/OwnerDashboard.tsx', {
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-1', role: 'owner', onboardingComplete: true } }) },
    '../../../lib/parkingApi': {
      parkingApi: { list: async () => { siteReads++; return []; } },
      operatorsApi: { list: async () => { rosterReads++; return accounts; } },
    },
    '../../../lib/operatorLive': { watchOperators: callback => { changed = callback; return () => stopped++; } },
    '../../../lib/ownerLotSetup': { reconcileOwnerLotSetups: async () => {} },
    './OwnerOverview': { OwnerOverview: 'overview' },
    '../operators/OperatorManagement': { OperatorManagement: 'roster' },
    '../../../components/layout/DashboardSidebar': { DashboardSidebar: 'sidebar' },
  }, { window: { addEventListener() {}, removeEventListener() {} } });
  let tree = await f.render('OwnerDashboard');
  assert.equal(find(tree, 'overview').props.operators[0].status, 'pending_verification');
  accounts = [{ id: 'operator-1', status: 'active' }, { id: 'operator-2', status: 'active' }];
  changed(); tree = await f.render('OwnerDashboard');
  assert.equal(find(tree, 'overview').props.operators.length, 2);
  assert.equal(find(tree, 'overview').props.operators[0].status, 'active');
  const nav = find(tree, 'sidebar').props.groups.flatMap(group => group.items).find(item => item.id === 'operators');
  nav.onClick(); tree = await f.render('OwnerDashboard');
  assert.equal(find(tree, 'roster').props.liveOperators.length, 2);
  assert.equal(rosterReads, 2); assert.equal(siteReads, 1);
  f.unmount(); assert.equal(stopped, 1);
});

test('Cashier refreshes a selected session on realtime change and removes its completed checkout action', async () => {
  let reads = 0, complete = false;
  const lot = { id: 'site-1', name: 'Site' };
  const f = screen('roles/operator/dashboard/OperatorDashboard.tsx', {
    '../../../lib/operatorApi': { reservationApi: { lookup: async () => {
      reads++; return { id: 'booking-1', lotId: lot.id, status: complete ? 'completed' : 'checked-in',
        licensePlate: '51A12345', slotNumber: 'CAR 001', amount: 20000, paymentStatus: complete ? 'SUCCESS' : 'PENDING' };
    } } },
  }, {}, '\nexport { CashierPOS };');
  let tree = await f.render('CashierPOS', { lot, realtimeRevision: 0, sitesLoading: false, sitesError: '', onDataChanged() {} });
  find(tree, 'input').props.onChange({ target: { value: '51A12345' } });
  tree = await f.render('CashierPOS', { lot, realtimeRevision: 0, sitesLoading: false, sitesError: '', onDataChanged() {} });
  await find(tree, 'form').props.onSubmit({ preventDefault() {} });
  tree = await f.render('CashierPOS', { lot, realtimeRevision: 0, sitesLoading: false, sitesError: '', onDataChanged() {} });
  assert.match(JSON.stringify(tree), /Confirm vehicle checkout/); assert.equal(reads, 1);
  complete = true;
  tree = await f.render('CashierPOS', { lot, realtimeRevision: 1, sitesLoading: false, sitesError: '', onDataChanged() {} });
  assert.match(JSON.stringify(tree), /completed/);
  assert.match(JSON.stringify(tree), /SUCCESS/);
  assert.doesNotMatch(JSON.stringify(tree), /Confirm vehicle checkout/);
  assert.equal(reads, 2); f.unmount();
});

test('an open slot inspection follows the refreshed slot instead of keeping its old object', async () => {
  let inspected = { id: 'slot-1', physicalState: 'OCCUPIED' };
  const latest = { id: 'slot-1', physicalState: 'AVAILABLE' }, allSlots = [latest];
  const f = screen('roles/operator/parking-lot/LotStatus.tsx', {
    './useLotStatus': { useLotStatus: () => ({ site: null, allSlots, inspectedSlot: inspected,
      setInspectedSlot: value => { inspected = value; } }) },
    '../../../lib/slotVisualState': { SLOT_VISUAL_STATE_HEX: {} },
  });
  await f.render('LotStatus');
  assert.equal(inspected, latest);
  f.unmount();
});
