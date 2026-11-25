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
const selection = { vehicleId: 'vehicle', siteId: 'site', requestedSlotId: '917820fe-d339-4007-aa5f-2d5180e53fd4', start: '2026-12-01T03:00:00.000Z', end: '2026-12-01T05:00:00.000Z', paymentMethod: 'PAYOS' };
const quoteSelection = { licensePlate: '51A12345', requestedSlotId: selection.requestedSlotId, start: selection.start, end: selection.end, paymentMethod: 'PAYOS' };
const quoteIntent = { ...quoteSelection, quoteId: '90411434-635c-4526-b579-434d826ed2f8' };
const quote = { ...quoteIntent, canonicalPlate: quoteSelection.licensePlate, siteId: selection.siteId,
  validUntil: '2099-01-01T00:00:00Z', bookedAmount: 40000, depositAmount: 20000, currency: 'VND',
  disclosure: 'Reviewed quote', operationalPolicy: { values: {} } };

test('reservation adapter sends the reviewed intent and stable idempotency key to Operations', async () => {
  const calls = [];
  const { reservationsApi: api } = load('lib/reservationsApi.ts', { './authApi': { request: async (...args) => { calls.push(args); return { success: true, data: {} }; } } });
  await api.preview(selection);
  await api.reviewQuote(quoteSelection);
  await api.submitQuote(quoteIntent, 'quote-key');
  await api.submitQuote(quoteIntent, 'quote-key');
  assert.equal(calls[0][0], '/reservation-price-previews');
  assert.deepEqual(calls[1].slice(0, 3), ['/reservation-quotes', 'POST', quoteSelection]);
  assert.deepEqual(calls[2].slice(0, 5), ['/reservation-requests', 'POST', quoteIntent, '/api', '/operations']);
  assert.equal(calls[2][5]['Idempotency-Key'], 'quote-key');
  assert.deepEqual(calls[3], calls[2]);
  assert.equal(api.review, undefined);
  assert.equal(api.submit, undefined);
  await api.receipt('receipt'); await api.booking('booking'); await api.list(); await api.requestForOrder('order');
  assert.deepEqual(calls.slice(4).map(call => call[0]), ['/reservation-requests/receipt', '/me/reservations/booking', '/me/reservations?pageSize=100', '/me/reservations/by-order/order/request']);
});

test('reservation adapter propagates failed requests without fabricating a booking', async () => {
  const { reservationsApi: api } = load('lib/reservationsApi.ts', { './authApi': { request: async () => ({ success: false }) } });
  await assert.rejects(api.submitQuote(quoteIntent, 'key'), /no result/);
});

// A small hook harness exercises client transitions with mocked services; it is not browser/E2E evidence.
const requestKey = '0854e9bf-b070-47b9-ab2f-b8b294186acf';
const receiptId = '22680f58-0737-46fa-9a53-6d5201bf7bdd';

function checkout(api = {}, storage = new Map()) {
  const hooks = []; let index = 0; let snapshot = null; let financial = null; let generation = 1; let subscribed;
  const jsx = (type, props) => ({ type, props });
  const component = load('roles/driver/booking/ReservationCheckout.tsx', {
    react: {
      useState: initial => { const i = index++; if (!(i in hooks)) hooks[i] = typeof initial === 'function' ? initial() : initial; return [hooks[i], value => { hooks[i] = typeof value === 'function' ? value(hooks[i]) : value; }]; },
      useRef: initial => { const i = index++; return hooks[i] ?? (hooks[i] = { current: initial }); },
      useEffect: (effect, deps) => {
        const i = index++;
        if (!hooks[i] || deps.some((v,j) => hooks[i][j] !== v)) { hooks[i] = deps; effect(); }
      },
    },
    'react/jsx-runtime': { jsx, jsxs: jsx, Fragment: 'fragment' },
    '../../../lib/authApi': { getSessionGeneration: () => generation },
    '../../../lib/reservationsApi': { reservationsApi: { reviewQuote: async () => quote, submitQuote: api.submit } },
    '../../../lib/reservationProgress': { useReservationProgress: id => { subscribed = id; return { snapshot, checkout: financial, connected: true }; } },
    '../../../lib/formatStatus': { formatStatus: value => value },
    '../../../components/icon/UntitledIcon': { UntitledIcon: 'UntitledIcon' },
    '../payments/PaymentCheckout': { PaymentCheckout: 'PaymentCheckout' },
  }, {
    sessionStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
    crypto: { randomUUID: () => requestKey },
    window: { dispatchEvent() {}, setInterval() {} }, Event: class {},
  }).ReservationCheckout;
  let tree;
  function render() { index = 0; tree = component({ selection: quoteSelection, storageKey: 'driver-account', onDone() {} }); return tree; }
  function walk(node) {
    if (Array.isArray(node)) return node.flatMap(walk);
    if (!node || typeof node !== 'object') return [];
    return [node, ...walk(node.props?.children)];
  }
  function nodes() { return walk(tree); }
  function button(label) { return nodes().find(node => node.type === 'button' && node.props.children === label); }
  async function click(label) { button(label).props.onClick(); await new Promise(resolve => setImmediate(resolve)); render(); }
  async function accept() { await click('Review price and terms'); nodes().find(node => node.type === 'input').props.onChange({ target: { checked: true } }); render(); await click('Accept terms and reserve'); }
  render();
  return { render, nodes, button, click, accept, storage, payment: value => { financial = value; render(); }, requestId: () => subscribed, progress: value => { snapshot = value; render(); }, switchAccount: () => { generation++; } };
}

test('happy path waits for a real order and never treats HOLD_CREATED as CONFIRMED', async () => {
  let booking = { requestId: 'request', admissionState: 'HOLD_CREATED', checks: {account:'SUCCEEDED',vehicle:'SUCCEEDED',facility:'SUCCEEDED',booking:'SUCCEEDED'}, reservationId: 'booking', reservationState: 'PENDING_PAYMENT', orderId: null, holdExpiresAt: '2099-01-01T00:00:00Z', bookedAmount: 40000, depositAmount: 20000 };
  const receipt = { requestId: 'request', state: 'HOLD_CREATED', reservationId: 'booking' };
  const f = checkout({ submit: async () => receipt });
  await f.accept(); f.progress(booking);
  assert.equal(f.nodes().some(node => node.type === 'PaymentCheckout'), false);
  assert.equal(f.button('Done'), undefined);
  f.payment({ state: 'PAYABLE', orderId: 'payment-order', holdExpiresAt:booking.holdExpiresAt });
  f.progress(booking);
  assert.equal(f.nodes().find(node => node.type === 'PaymentCheckout').props.reservationOrderId, 'payment-order');
  assert.equal(f.button('Done'), undefined);
  f.progress({ ...booking, reservationState: 'CONFIRMED' });
  assert.ok(f.button('Done'));
});

test('ambiguous submission survives remount and retries exactly the same intent/key', async () => {
  let sent;
  const f = checkout({ submit: async (...args) => { sent = JSON.parse(JSON.stringify(args)); throw new Error('Network unavailable'); } });
  await f.accept();
  assert.deepEqual(sent, [quoteIntent, requestKey]);
  const resumed = checkout({ submit: async (...args) => { assert.deepEqual(JSON.parse(JSON.stringify(args)), sent); return { requestId: receiptId, state: 'CHECKING' }; } }, f.storage);
  assert.ok(resumed.button('Retry same request'));
  await resumed.click('Retry same request');
  assert.equal(resumed.button('Retry same request'), undefined);
});

test('saved terms and malformed intents cannot resubmit, but a known request can recover', () => {
  const legacy = { key: requestKey, intent: { ...selection, termsId: 'terms', termsVersion: 'v1' } };
  const submit = () => assert.fail('opening storage must not create a reservation');
  for (const attempt of [legacy, { ...legacy, intent: { ...quoteIntent, quoteId: null } },
    { ...legacy, intent: quoteIntent, key: 'bad-key' }, { ...legacy, intent: quoteIntent, requestId: 'bad-id' }]) {
    const f = checkout({ submit }, new Map([['driver-account', JSON.stringify(attempt)]]));
    assert.equal(f.requestId(), undefined);
    assert.equal(f.button('Retry same request'), undefined);
    assert.ok(f.button('Review price and terms'));
  }
  const f = checkout({ submit }, new Map([['driver-account', JSON.stringify({ ...legacy, requestId: receiptId })]]));
  assert.equal(f.requestId(), receiptId);
  assert.equal(f.button('Retry same request'), undefined);
  assert.equal(f.nodes().some(node => node.type === 'PaymentCheckout'), false);
  assert.equal(f.button('Done'), undefined);
});

test('late admission responses after account switch do not persist foreign receipts', async () => {
  let resolve;
  const f = checkout({ submit: () => new Promise(done => { resolve = done; }) });
  await f.accept();
  f.switchAccount();
  resolve({ requestId: 'foreign-response', state: 'HOLD_CREATED', reservationId: 'foreign-booking' });
  await new Promise(done => setImmediate(done));
  assert.equal(JSON.parse([...f.storage.values()][0]).requestId, undefined);
});

test('SignalR joins before one recovery read, merges by request revision and fences a changed login', async () => {
  let generation = 4, tokenProvider, handler, hint, reconnect, disconnect, finishRead;
  const pending = [], checkoutStates = [];
  const calls = [], versions = [], availability = [];
  const connection = {
    on(name, callback) { if (name === 'ReservationProgress') handler = callback; else if (name === 'ReservationCheckoutChanged') hint = callback; },
    off() {}, onreconnecting(callback) { disconnect = callback; }, onreconnected(callback) { reconnect = callback; }, onclose() {},
    async start() { calls.push('start'); }, async invoke(method, id) { calls.push([method,id]); },
    async stop() { calls.push('stop'); },
  };
  class Builder {
    withUrl(url, options) { assert.equal(url, '/operations/hubs/reservation-progress'); tokenProvider = options.accessTokenFactory; return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return connection; }
  }
  const fixture = load('lib/reservationProgress.ts', {
    react: {}, '@microsoft/signalr': {HubConnectionBuilder: Builder, HttpTransportType: {WebSockets: 1, ServerSentEvents: 2}, LogLevel: {None: 6}},
    './paymentApi': {paymentApi:{reservationCheckout: () => new Promise(resolve => pending.push(resolve))}},
    './authApi': {getSessionGeneration: () => generation, ensureAccessToken: async () => 'driver-token'},
    './reservationsApi': {reservationServer: '/operations', reservationsApi: { progress: id => {
      calls.push(['read', id]); return new Promise(resolve => { finishRead = resolve; });
    }}},
  });
  const snapshot = (version, requestId = 'ours') => ({requestId, version, checks: {}, eventId: 'event-' + version});
  const flush = () => new Promise(resolve => setImmediate(resolve));
  const stop = fixture.watchReservationProgress('ours', s => versions.push(s.version), v => availability.push(v), v => checkoutStates.push(v?.state ?? v));
  await flush();
  assert.equal(await tokenProvider(), 'driver-token');
  assert.deepEqual(calls, ['start',['Subscribe','ours'],['read','ours']]);
  handler(snapshot(4)); handler(snapshot(4)); handler(snapshot(9,'foreign')); finishRead(snapshot(2)); await flush();
  assert.deepEqual(versions, [4]); assert.deepEqual(availability, [true]);
  assert.equal(pending.length, 1);
  hint('foreign');
  assert.equal(pending.length, 1);
  hint('ours');
  pending[1]({state:'PAYABLE', reservationId:'ours'});
  pending[0]({state:'MISSING', reservationId:null});
  await flush();
  assert.deepEqual(checkoutStates, ['PAYABLE']);
  disconnect();
  const recovering = reconnect();
  await flush();
  assert.deepEqual(calls.slice(3), [['Subscribe','ours'],['read','ours']]);
  assert.equal(pending.length, 3);
  pending[2]({state:'PAID', reservationId:'ours'});
  finishRead(snapshot(6)); await recovering;
  assert.deepEqual(checkoutStates, ['PAYABLE',null,'PAID']);
  handler(snapshot(5));
  assert.deepEqual(versions, [4,6]); assert.deepEqual(availability, [true,false,true]);
  generation++;
  handler(snapshot(7)); await assert.rejects(tokenProvider, /sign-in changed/);
  stop(); handler(snapshot(8));
  assert.deepEqual(versions, [4,6]); assert.equal(calls.at(-1), 'stop');
});

test('payment return schedules status checks only for standalone orders', async () => {
  const order = 'f7e1ff6b-c768-48c6-9c78-64d381aba8ba';
  const requestId = 'a0e97ab1-6222-4cd4-a6f7-cf960334344f';
  for (const route of ['reservation', 'legacy-reservation', 'standalone', 'legacy-standalone', 'lookup-unavailable']) {
    let index = 0, changed = false, reads = 0, timers = 0, lookups = 0, subscribed;
    let snapshot = null, tree;
    const slots = [], effects = [];
    const react = {
      useState(initial) {
        const i = index++;
        if (!(i in slots)) slots[i] = initial;
        return [slots[i], value => { if (slots[i] !== value) { slots[i] = value; changed = true; } }];
      },
      useEffect(effect, deps) {
        const i = index++;
        if (!slots[i] || deps.some((v,j) => slots[i][j] !== v)) { slots[i] = deps; effects.push(effect); }
      },
      useRef(initial) { const i = index++; return slots[i] ?? (slots[i] = {current: initial}); },
    };
    const jsx = (type, props) => ({type, props});
    const search = '?orderId=' + order + (route === 'reservation' ? '&requestId=' + order : '');
    const { PaymentResultPage } = load('roles/driver/payments/PaymentResultPage.tsx', {
      react, 'react/jsx-runtime': { jsx, jsxs: jsx },
      'react-router-dom': {useNavigate() {}}, '../../../components/brand/BrandLogo': {},
      '../../../context/AppContext': {useApp: () => ({authReady:true, user:{id:'me',role:'driver'}})},
      '../../../lib/authApi': {getSessionGeneration: () => 1}, '../../../lib/formatStatus': {formatStatus: v => v},
      './payment-checkout.css': {},
      '../../../lib/reservationsApi': {reservationsApi:{requestForOrder:async () =>
        route === 'legacy-reservation' ? {requestId, reservationId:requestId} : null}},
      '../../../lib/paymentApi': {isPaymentOrderId: v => typeof v === 'string' && v.length === 36,
        classifyPaymentStatus: () => 'pending', paymentApi: {
          orderReservation: async id => {
            assert.equal(id, order); lookups++;
            if (route === 'lookup-unavailable') throw new Error('Unavailable');
            if (route === 'legacy-reservation') return {state:'HISTORICAL', reservationId:requestId, orderId:order};
            return route.endsWith('reservation') ? { state:'PAID', reservationId:requestId } : {state:'STANDALONE', reservationId:null};
          },
          rememberedCheckout: () => ({paymentTransactionId: order, standalone: route === 'standalone'}),
          status: async () => { reads++; return {orderId:order,status:'PENDING',amount:1}; },
        }},
      '../../../lib/reservationProgress': {useReservationProgress: id => {
        subscribed = id; return {snapshot, connected:true};
      }},
    }, {URLSearchParams, window:{location:{search},setTimeout() {timers++;}, clearTimeout() {}},
      sessionStorage:{getItem: () => null}});
    const component = PaymentResultPage().type;
    do {
      index = 0; changed = false; tree = component();
      for (const effect of effects.splice(0)) effect();
      await new Promise(resolve => setImmediate(resolve));
    } while (changed);
    assert.equal(reads, 1, route);
    assert.equal(lookups, 1, route);
    assert.equal(subscribed ?? null, route.endsWith('reservation') ? requestId : null, route);
    assert.equal(timers, route.endsWith('standalone') ? 1 : 0, route);
    if (route === 'reservation') {
      const heading = node => Array.isArray(node) ? node.map(heading).find(Boolean) :
        node?.type === 'h1' ? node.props.children : node?.props && heading(node.props.children);
      assert.equal(heading(tree), 'Payment is still processing');
      snapshot = {requestId, reservationId:order, orderId:null, reservationState:'CONFIRMED'};
      index = 0; assert.equal(heading(component()), 'Payment is still processing');
      snapshot = {...snapshot, reservationId:requestId};
      index = 0; tree = component();
      assert.equal(heading(tree), 'Reservation confirmed');
      assert.doesNotMatch(JSON.stringify(tree), /Payment successful/);
      snapshot = {...snapshot, reservationState:'EXPIRED'};
      index = 0; assert.equal(heading(component()), 'Reservation expired');
    }
  }
});
