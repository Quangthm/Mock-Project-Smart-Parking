const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const orderId = 'a1b2c3d4-e5f6-4a8b-9c0d-1e2f3a4b5c6d';
const transactionId = 'f7d3a2b1-5e8c-4a3d-9c2b-1a2b3c4d5e6f';

function checkoutView(paymentApi, props) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/roles/driver/payments/PaymentCheckout.tsx'), 'utf8');
  const script = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  const hooks = [], effects = [];
  let index = 0;
  const jsx = (type, props) => ({ type, props });
  function memo(value, deps) {
    const i = index++;
    if (!hooks[i] || deps.some((dep, j) => dep !== hooks[i].deps[j])) {
      hooks[i] = { value, deps };
      return { value, changed: true };
    }
    return hooks[i];
  }
  const imports = {
    react: {
      useState(initial) {
        const i = index++;
        if (!(i in hooks)) hooks[i] = typeof initial === 'function' ? initial() : initial;
        return [hooks[i], value => { hooks[i] = value; }];
      },
      useRef(initial) { const i = index++; return hooks[i] ?? (hooks[i] = { current: initial }); },
      useCallback(fn, deps) { return memo(fn, deps).value; },
      useEffect(fn, deps) { if (memo(fn, deps).changed) effects.push(fn); },
    },
    'react/jsx-runtime': { jsx, jsxs: jsx, Fragment: 'fragment' },
    'qrcode.react': { QRCodeSVG: 'qr-svg' },
    '../../../lib/paymentApi': { paymentApi, isPaymentOrderId: id => id === orderId || id.toLowerCase() === orderId,
      classifyPaymentStatus: ({ status }) => status === 'PENDING' ? 'pending' : 'failed' },
    '../../../lib/serverRefresh': { useServerRefresh() {} },
    '../../../lib/authApi': { getSessionGeneration: () => 1 },
    '../../../assets/payment-qr.png': { default: '' },
    './payment-checkout.css': {},
  };
  const context = { exports: {}, URLSearchParams, window: { location: { search: '?orderId=' + orderId }, setInterval() {} },
    require: name => imports[name] };
  vm.runInNewContext(script, context);
  function flatten(node) {
    if (Array.isArray(node)) return node.flatMap(flatten);
    if (!node || typeof node !== 'object') return [];
    return [node, ...flatten(node.props?.children)];
  }
  return () => {
    index = 0;
    const tree = context.exports.PaymentCheckout(props);
    effects.splice(0).forEach(effect => effect());
    return flatten(tree);
  };
}

test('reservation generates QR once, standalone and expired orders do not auto-start', async () => {
  const calls = [];
  const link = { paymentTransactionId: transactionId, qrCode: 'test QR', amount: 25000, expiresAt: '2099-01-01T00:00:00Z' };
  const payment = { rememberedCheckout: () => null, checkout: async (...args) => { calls.push(args); return link; } };
  const props = { showMethodPicker: false, reservationOrderId: orderId, reservationRequestId: transactionId, holdExpiresAt: link.expiresAt };
  const render = checkoutView(payment, props);
  assert.equal(render().some(node => node.props?.children === 'Generate payment QR'), false);
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(render().find(node => node.type === 'qr-svg').props.value, link.qrCode);
  checkoutView(payment, { ...props, holdExpiresAt: '2000-01-01T00:00:00Z' })();
  checkoutView(payment, {})();
  await new Promise(resolve => setImmediate(resolve));
  assert.deepEqual(calls, [[orderId, transactionId]]);
});

test('a failed QR request is retried only on explicit submit', async () => {
  let calls = 0;
  const render = checkoutView({
    rememberedCheckout: () => null,
    checkout: async () => { calls++; throw new Error('Gateway unavailable'); },
  }, { showMethodPicker: false, reservationOrderId: orderId, holdExpiresAt: '2099-01-01T00:00:00Z' });
  render();
  await new Promise(resolve => setImmediate(resolve));
  const nodes = render();
  assert.equal(nodes.find(node => node.props?.children === 'Retry generating QR').type, 'button');
  assert.equal(calls, 1);
  nodes.find(node => node.type === 'form').props.onSubmit({ preventDefault() {} });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(calls, 2);
});

test('a confirmed reservation payment replaces the QR without a status request', async () => {
  const props = { showMethodPicker: false, reservationOrderId: orderId, holdExpiresAt: '2099-01-01T00:00:00Z' };
  const render = checkoutView({
    rememberedCheckout: () => null,
    checkout: async () => ({ paymentTransactionId: transactionId, qrCode:'QR', amount: 10000, expiresAt:props.holdExpiresAt }),
    status: () => assert.fail('Realtime confirmation must not query transaction status'),
  }, props);
  render();
  await new Promise(resolve => setImmediate(resolve));
  assert.ok(render().some(node => node.type === 'qr-svg'));
  props.paymentConfirmed = true;
  const confirmed = render();
  assert.equal(confirmed.some(node => node.type === 'qr-svg'), false);
  assert.equal(confirmed.some(node => node.props?.children === 'Check payment status'), false);
  assert.ok(confirmed.some(node => node.props?.role === 'status' && node.props.children === 'Payment confirmed'));
});

function fixture(data = {}, response) {
  const storage = new Map();
  const calls = [];
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/paymentApi.ts'), 'utf8')
    .replace('import.meta.env.VITE_PAYMENT_API_BASE_URL', JSON.stringify('/payment-api'));
  const javascript = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const context = {
    exports: {}, URL, URLSearchParams, Date, Error,
    require: name => name === './authApi' ? { ensureAccessToken: async () => 'test-token' } : null,
    crypto: { randomUUID: () => 'c9b2c3d4-e5f6-4a8b-9c0d-1e2f3a4b5c6d' },
    window: {
      location: { origin: 'http://localhost:5173' },
      localStorage: {
        getItem: key => storage.get(key) ?? null,
        setItem: (key, value) => storage.set(key, value),
        removeItem: key => storage.delete(key),
      },
    },
    fetch: async (url, init) => {
      calls.push({ url, init });
      if (response) return typeof response === 'function' ? response(url, init) : response;
      return {
        ok: true,
        json: async () => ({ success: true, data: {
          paymentTransactionId: transactionId,
          orderCode: 123456,
          amount: 25000,
          checkoutUrl: 'https://pay.payos.vn/web/real-link',
          accountNumber: '9876543210',
          accountName: 'A REAL MERCHANT',
          status: 'PENDING',
          expiresAt: '2026-10-31T10:00:00Z',
          ...data,
        } }),
      };
    },
  };
  vm.runInNewContext(javascript, context);
  return { api: context.exports, storage, calls };
}

test('booking invoice resolves by session before fetching the authenticated official PDF', async () => {
  const pdf = new Blob(['%PDF'], { type: 'application/pdf' });
  const f = fixture({}, url => url.includes('/download')
    ? { ok: true, headers: new Headers({ 'content-type': 'application/pdf' }), blob: async () => pdf }
    : { ok: true, json: async () => ({ success: true, data: { invoiceIdentity: { invoiceId: transactionId } } }) });
  assert.equal(await f.api.paymentApi.reservationInvoicePdf({ sessionId: transactionId, orderId }), pdf);
  assert.deepEqual(f.calls.map(call => call.url), [
    `/payment-api/api/v1/payments/invoices?sessionId=${transactionId}`,
    `/payment-api/api/v1/payments/invoices/${transactionId}/download`,
  ]);
  assert.ok(f.calls.every(call => call.init.headers.Authorization === 'Bearer test-token'));
  const missing = fixture({}, { ok: false, status: 404 });
  await assert.rejects(missing.api.paymentApi.reservationInvoicePdf({ orderId }), /could not be found/);
  assert.equal(missing.calls[0].url, `/payment-api/api/v1/payments/orders/${orderId}/invoice`);
  assert.equal(missing.calls.length, 1);
});

test('booking invoice falls back to the order only when the session invoice is missing', async () => {
  const pdf = new Blob(['%PDF'], { type: 'application/pdf' });
  const f = fixture({}, url => {
    if (url.includes('?sessionId=')) return { ok: false, status: 404 };
    if (url.includes('/download')) return { ok: true, headers: new Headers({ 'content-type': 'application/pdf' }), blob: async () => pdf };
    return { ok: true, json: async () => ({ success: true, data: { invoiceIdentity: { invoiceId: transactionId } } }) };
  });
  assert.equal(await f.api.paymentApi.reservationInvoicePdf({ sessionId: transactionId, orderId }), pdf);
  assert.equal(f.calls[1].url, `/payment-api/api/v1/payments/orders/${orderId}/invoice`);
  for (const status of [403, 422]) {
    const denied = fixture({}, { ok: false, status });
    await assert.rejects(denied.api.paymentApi.reservationInvoicePdf({ sessionId: transactionId, orderId }));
    assert.equal(denied.calls.length, 1);
  }
});

test('invoice history uses the raw paged response and authenticated date/status filters', async () => {
  const page = { items: [], totalCount: 21, pageNumber: 2, totalPages: 3 };
  const f = fixture({}, { ok: true, json: async () => page });
  const result = await f.api.paymentApi.invoiceHistory({
    status: 'PAID', fromDate: '2026-11-01', toDate: '2026-11-11', pageNumber: 2,
  });
  assert.equal(result, page);
  const request = f.calls[0];
  const url = new URL(request.url, 'http://localhost');
  assert.equal(url.pathname, '/payment-api/api/v1/payments/invoices/my-history');
  assert.equal(url.searchParams.get('status'), 'PAID');
  assert.equal(url.searchParams.get('pageNumber'), '2');
  assert.equal(url.searchParams.get('fromDate'), '2026-11-01T00:00:00+07:00');
  assert.equal(url.searchParams.get('toDate'), '2026-11-11T23:59:59.9999999+07:00');
  assert.equal(request.init.headers.Authorization, 'Bearer test-token');
});

test('invoice download requires PDF content and handles unsettled payments', async () => {
  const pdf = new Blob(['%PDF'], { type: 'application/pdf' });
  const f = fixture({}, { ok: true, headers: new Headers({ 'content-type': 'application/pdf' }), blob: async () => pdf });
  assert.equal(await f.api.paymentApi.downloadInvoice(orderId), pdf);
  assert.equal(f.calls[0].url, `/payment-api/api/v1/payments/invoices/${orderId}/download`);
  assert.equal(f.calls[0].init.headers.Authorization, 'Bearer test-token');
  const pending = fixture({}, { ok: false, status: 422 });
  await assert.rejects(pending.api.paymentApi.downloadInvoice(orderId), /until payment is settled/);
});

test('checkout sends payOS request, idempotency key and same-origin return routes', async () => {
  const f = fixture();
  const checkout = await f.api.paymentApi.checkout(orderId);
  assert.equal(checkout.paymentTransactionId, transactionId);
  assert.equal(f.calls.length, 1);
  assert.equal(f.calls[0].url, `/payment-api/api/v1/payments/orders/${orderId}/checkout`);
  assert.equal(f.calls[0].init.headers['Idempotency-Key'], 'c9b2c3d4-e5f6-4a8b-9c0d-1e2f3a4b5c6d');
  assert.equal(f.calls[0].init.headers.Authorization, 'Bearer test-token');
  const body = JSON.parse(f.calls[0].init.body);
  assert.equal(body.paymentMethod, 'PAYOS');
  assert.equal(body.returnUrl, `http://localhost:5173/payment/success?orderId=${orderId}`);
  assert.equal(body.cancelUrl, `http://localhost:5173/payment/fail?orderId=${orderId}`);
  assert.equal(f.api.paymentApi.rememberedCheckout(orderId).paymentTransactionId, transactionId);
  assert.equal(f.api.paymentApi.rememberedCheckout(orderId).standalone, true);
  f.api.paymentApi.bindReservationCheckout(orderId, transactionId);
  assert.equal(f.api.paymentApi.rememberedCheckout(orderId).standalone, false);
});

test('checkout rejects a non-payOS redirect and the backend demo gateway', async () => {
  await assert.rejects(fixture({ checkoutUrl: 'https://example.com/steal' }).api.paymentApi.checkout(orderId), /untrusted checkout link/);
  await assert.rejects(fixture({ accountNumber: '0123456789', accountName: 'SMARTPARKING CORP' }).api.paymentApi.checkout(orderId), /demo gateway/);
});

test('checkout provides safe error references and keeps the same retry key for an unresolved payment', async () => {
  const f = fixture({}, { ok: false, status: 409, json: async () => ({
    success: false, code: 'PAYMENT_TRANSACTION_AMBIGUOUS', traceId: 'checkout:42', message: 'Internal provider details',
  }) });
  for (let attempt = 0; attempt < 2; attempt++) {
    await assert.rejects(f.api.paymentApi.checkout(orderId), error => {
      assert.match(error.message, /still unresolved.*PAYMENT_TRANSACTION_AMBIGUOUS.*checkout:42/);
      assert.doesNotMatch(error.message, /Internal provider details/);
      return true;
    });
  }
  assert.equal(f.calls[0].init.headers['Idempotency-Key'], f.calls[1].init.headers['Idempotency-Key']);
  const unknown = fixture({}, { ok: false, status: 502, json: async () => null });
  await assert.rejects(unknown.api.paymentApi.checkout(orderId), /HTTP 502/);
});

test('reservation checkout carries its request reference through both provider returns', async () => {
  const f = fixture();
  const requestId = '3a584ece-c27b-481d-9e92-4e9bb64a5a6a';
  await f.api.paymentApi.checkout(orderId, requestId);
  assert.equal(f.api.paymentApi.rememberedCheckout(orderId).standalone, false);
  const body = JSON.parse(f.calls[0].init.body);
  for (const url of [body.returnUrl, body.cancelUrl]) {
    const params = new URL(url).searchParams;
    assert.equal(params.get('requestId'), requestId);
    assert.equal(params.get('orderId'), orderId);
  }
});

test('return URL alone never marks a payment successful', () => {
  const { classifyPaymentStatus } = fixture().api;
  assert.equal(classifyPaymentStatus({ status: 'PENDING', isAuthoritative: false }), 'pending');
  assert.equal(classifyPaymentStatus({ status: 'SUCCESS', isAuthoritative: false }), 'pending');
  assert.equal(classifyPaymentStatus({ status: 'SUCCESS', isAuthoritative: true }), 'success');
  assert.equal(classifyPaymentStatus({ status: 'CANCELLED', isAuthoritative: true }), 'failed');
});
