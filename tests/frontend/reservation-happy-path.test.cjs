const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const test = require('node:test');
const ts = require('../../FE/node_modules/typescript');

function load(file, mocks, globals = {}) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8')
    .replace('import.meta.env.VITE_RESERVATION_API_BASE_URL', JSON.stringify('/operations'));
  const js = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  const context = { exports: {}, require: name => {
    assert.ok(name in mocks, `Unexpected dependency: ${name}`);
    return mocks[name];
  }, ...globals };
  vm.runInNewContext(js, context);
  return context.exports;
}
const selection = { vehicleId: 'vehicle', siteId: 'site', requestedSlotId: 'slot', start: '2026-12-01T03:00:00.000Z', end: '2026-12-01T05:00:00.000Z', paymentMethod: 'PAYOS' };
const terms = { termsId: 'terms', termsVersion: 'v2', validUntil: '2099-01-01T00:00:00Z', commercial: { bookedAmount: 40000, depositAmount: 20000, disclosure: 'Reviewed price', prepaymentMode: 'PARTIAL' }, operationalPolicy: { values: {} } };

test('reservation adapter sends the reviewed intent and stable idempotency key to Operations', async () => {
  const calls = [];
  const { reservationsApi: api } = load('lib/reservationsApi.ts', { './authApi': { request: async (...args) => { calls.push(args); return { success: true, data: {} }; } } });
  await api.preview(selection);
  await api.review(selection);
  await api.submit(selection, terms, 'same-key');
  await api.submit(selection, terms, 'same-key');
  assert.equal(calls[0][0], '/reservation-price-previews');
  assert.equal(calls[1][0], '/reservation-terms');
  assert.equal(calls[2][3], '/api');
  assert.equal(calls[2][4], '/operations');
  assert.deepEqual(JSON.parse(JSON.stringify(calls[2][2])), { ...selection, mode: 'SPECIFIC_SLOT', termsId: 'terms', termsVersion: 'v2' });
  assert.equal(calls[2][5]['Idempotency-Key'], calls[3][5]['Idempotency-Key']);
  await api.receipt('receipt'); await api.booking('booking'); await api.list();
  assert.deepEqual(calls.slice(4).map(call => call[0]), ['/reservation-requests/receipt', '/me/reservations/booking', '/me/reservations?pageSize=100']);
});

test('reservation adapter propagates failed requests without fabricating a booking', async () => {
  const { reservationsApi: api } = load('lib/reservationsApi.ts', { './authApi': { request: async () => ({ success: false }) } });
  await assert.rejects(api.submit(selection, terms, 'key'), /no result/);
});

// A small hook harness exercises client transitions with mocked services; it is not browser/E2E evidence.
function checkout(api = {}, storage = new Map()) {
  const hooks = []; let index = 0; let refresh; let generation = 1;
  const jsx = (type, props) => ({ type, props });
  const component = load('roles/driver/booking/ReservationCheckout.tsx', {
    react: {
      useState: initial => { const i = index++; if (!(i in hooks)) hooks[i] = typeof initial === 'function' ? initial() : initial; return [hooks[i], value => { hooks[i] = typeof value === 'function' ? value(hooks[i]) : value; }]; },
      useRef: initial => { const i = index++; return hooks[i] ?? (hooks[i] = { current: initial }); },
      useEffect() {},
    },
    'react/jsx-runtime': { jsx, jsxs: jsx, Fragment: 'fragment' },
    '../../../lib/authApi': { getSessionGeneration: () => generation },
    '../../../lib/reservationsApi': { reservationsApi: { review: async () => terms, ...api } },
    '../../../lib/serverRefresh': { useServerRefresh: (_scope, callback) => { refresh = callback; } },
    '../payments/PaymentCheckout': { PaymentCheckout: 'PaymentCheckout' },
  }, {
    sessionStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
    crypto: { randomUUID: () => 'stable-key' },
    window: { dispatchEvent() {} }, Event: class {},
  }).ReservationCheckout;
  let tree;
  function render() { index = 0; tree = component({ selection, accountScope: 'driver-account', onDone() {} }); return tree; }
  function walk(node) {
    if (Array.isArray(node)) return node.flatMap(walk);
    if (!node || typeof node !== 'object') return [];
    return [node, ...walk(node.props?.children)];
  }
  function nodes() { return walk(tree); }
  function button(label) { return nodes().find(node => node.type === 'button' && node.props.children === label); }
  async function click(label) { button(label).props.onClick(); await new Promise(resolve => setImmediate(resolve)); render(); }
  async function accept() { await click('Review price and terms'); nodes().find(node => node.type === 'input').props.onChange({ target: { checked: true } }); render(); await click('Accept and reserve'); }
  render();
  return { render, nodes, button, click, accept, storage, refresh: async () => { await refresh(() => true); render(); }, switchAccount: () => { generation++; } };
}

test('happy path waits for a real order and never treats HOLD_CREATED as CONFIRMED', async () => {
  let booking = { reservationId: 'booking', state: 'PENDING_PAYMENT', orderId: null, holdExpiresAt: '2099-01-01T00:00:00Z', bookedAmount: 40000, depositAmount: 20000 };
  const receipt = { requestId: 'request', state: 'HOLD_CREATED', reservationId: 'booking' };
  const f = checkout({ submit: async () => receipt, receipt: async () => receipt, booking: async () => booking });
  await f.accept(); await f.refresh();
  assert.equal(f.nodes().some(node => node.type === 'PaymentCheckout'), false);
  assert.equal(f.button('Done'), undefined);
  booking = { ...booking, orderId: 'payment-order' };
  await f.refresh();
  assert.equal(f.nodes().find(node => node.type === 'PaymentCheckout').props.reservationOrderId, 'payment-order');
  assert.equal(f.button('Done'), undefined);
  booking = { ...booking, state: 'CONFIRMED' };
  await f.refresh();
  assert.ok(f.button('Done'));
});

test('ambiguous submission survives remount and retries exactly the same intent/key', async () => {
  let sent;
  const f = checkout({ submit: async (...args) => { sent = JSON.parse(JSON.stringify(args)); throw new Error('Network unavailable'); } });
  await f.accept();
  const resumed = checkout({ submit: async (...args) => { assert.deepEqual(JSON.parse(JSON.stringify(args)), sent); return { requestId: 'request', state: 'PREPARING' }; } }, f.storage);
  assert.ok(resumed.button('Retry same request'));
  await resumed.click('Retry same request');
  assert.equal(resumed.button('Retry same request'), undefined);
});

test('late admission responses after account switch do not persist foreign receipts', async () => {
  let resolve;
  const f = checkout({ submit: () => new Promise(done => { resolve = done; }) });
  await f.accept();
  f.switchAccount();
  resolve({ requestId: 'foreign-response', state: 'HOLD_CREATED', reservationId: 'foreign-booking' });
  await new Promise(done => setImmediate(done));
  assert.equal(JSON.parse([...f.storage.values()][0]).receipt, undefined);
});
