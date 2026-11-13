const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const orderId = 'a1b2c3d4-e5f6-4a8b-9c0d-1e2f3a4b5c6d';
const transactionId = 'f7d3a2b1-5e8c-4a3d-9c2b-1a2b3c4d5e6f';

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
      if (response) return response;
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
});

test('checkout rejects a non-payOS redirect and the backend demo gateway', async () => {
  await assert.rejects(fixture({ checkoutUrl: 'https://example.com/steal' }).api.paymentApi.checkout(orderId), /untrusted checkout link/);
  await assert.rejects(fixture({ accountNumber: '0123456789', accountName: 'SMARTPARKING CORP' }).api.paymentApi.checkout(orderId), /demo gateway/);
});

test('return URL alone never marks a payment successful', () => {
  const { classifyPaymentStatus } = fixture().api;
  assert.equal(classifyPaymentStatus({ status: 'PENDING', isAuthoritative: false }), 'pending');
  assert.equal(classifyPaymentStatus({ status: 'SUCCESS', isAuthoritative: false }), 'pending');
  assert.equal(classifyPaymentStatus({ status: 'SUCCESS', isAuthoritative: true }), 'success');
  assert.equal(classifyPaymentStatus({ status: 'CANCELLED', isAuthoritative: true }), 'failed');
});
