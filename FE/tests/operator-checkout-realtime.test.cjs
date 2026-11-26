const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');
const ts = require('../node_modules/typescript');
const settle = () => new Promise(resolve => setImmediate(resolve));

test('a realtime revision refreshes checkout history and verified payment without reload or polling', async () => {
  let index = 0, revision = 0, completed = false, tree, sessionReads = 0, paymentReads = 0;
  const slots = [], queued = [];
  const react = {
    useState(initial) { const i = index++; slots[i] ??= { value: typeof initial === 'function' ? initial() : initial };
      return [slots[i].value, value => { slots[i].value = typeof value === 'function' ? value(slots[i].value) : value; }]; },
    useEffect(fn, deps) { const i = index++, old = slots[i];
      if (!old || deps.some((value, n) => !Object.is(value, old.deps[n]))) {
        old?.cleanup?.(); slots[i] = { deps }; queued.push(() => { slots[i].cleanup = fn(); });
      }
    },
  };
  const lot = { id: 'assigned-site', name: 'Test site', slots: [] };
  const session = () => ({ id: 'session', siteId: lot.id, originalPlate: '51A12345', vehicleType: 'CAR',
    entryTime: '2026-11-16T01:00:00Z', exitTime: completed ? '2026-11-16T02:00:00Z' : null,
    status: completed ? 'COMPLETED' : 'PENDING_PAYMENT' });
  const storage = new Map([['smartpark.pendingCheckout', JSON.stringify({ siteId: lot.id, transactionId: 'transaction' })]]);
  const jsx = (type, props) => ({ type, props });
  const code = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../src/roles/operator/check-in-out/CheckInOut.tsx'), 'utf8').replace(/import\.meta\.env\.DEV/g, 'false'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX },
  }).outputText;
  const context = { exports: {}, Date, Error,
    localStorage: { getItem: key => storage.get(key) ?? null, removeItem: key => storage.delete(key) },
    window: { setInterval() { throw new Error('Checkout must not poll'); }, location: { reload() { throw new Error('Must not reload'); } } },
    require(name) {
      if (name === 'react') return react;
      if (name === 'react/jsx-runtime') return { jsx, jsxs: jsx };
      if (name.endsWith('/operatorApi')) return {
        reservationApi: { listParkingSessions: async () => { sessionReads++; return [session()]; } },
        paymentApi: { status: async () => { paymentReads++; return { status: completed ? 'SUCCESS' : 'PENDING', isAuthoritative: completed }; } },
      };
      if (name.endsWith('/formatStatus')) return { formatStatus: value => value };
      if (name.endsWith('/authApi')) return { AuthApiError: class extends Error {} };
      if (name.endsWith('/ParkingHistory')) return { ParkingHistory: 'parking-history' };
      return {};
    },
  };
  vm.runInNewContext(code, context);
  async function render() {
    for (let n = 0; n < 3; n++) {
      index = 0; tree = context.exports.CheckInOut({ mode: 'check-out', lot, realtimeRevision: revision });
      queued.splice(0).forEach(run => run()); await settle();
    }
  }
  function find(node, type) {
    if (!node || typeof node !== 'object') return null;
    if (node.type === type) return node;
    const children = [node.props?.children].flat(Infinity);
    for (const child of children) { const hit = find(child, type); if (hit) return hit; }
    return null;
  }
  await render();
  assert.equal(find(tree, 'parking-history').props.records[0].status, 'PENDING_PAYMENT');
  completed = true; revision++;
  await render();
  assert.equal(find(tree, 'parking-history').props.records[0].status, 'COMPLETED');
  assert.match(JSON.stringify(tree), /Payment confirmed/);
  assert.equal(sessionReads, 2); assert.equal(paymentReads, 2);
  await render(); assert.equal(sessionReads, 2, 'ordinary renders do not trigger requests');
  slots.forEach(slot => slot?.cleanup?.());
});
