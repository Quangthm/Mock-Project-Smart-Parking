const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const test = require('node:test');
const ts = require('../../FE/node_modules/typescript');
const uuid = '11111111-1111-4111-8111-111111111111';
function load({ request = async () => { throw Error('Unexpected private request'); }, fetch = async () => { throw Error('Unexpected public request'); }, base = 'https://localhost:5075' } = {}) {
  const source = fs.readFileSync(require('node:path').join(__dirname, '../../FE/src/lib/assistantApi.ts'), 'utf8')
    .replace('import.meta.env.VITE_ASSISTANT_API_BASE_URL', JSON.stringify(base));
  const context = { exports: {}, URL, AbortSignal, crypto: { randomUUID: () => uuid }, fetch, require: name => {
    assert.equal(name, './authApi'); return { request };
  } };
  vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, context);
  return context.exports.assistantApi;
}
test('guest uses correlation ID and sends no account token or cookie', async () => {
  let calls = 0;
  const api = load({ fetch: async (url, options) => {
    calls++; assert.equal(url, 'https://localhost:5075/api/assistant/guest/messages');
    assert.equal(options.credentials, 'omit'); assert.equal(options.redirect, 'error');
    assert.equal(options.headers.Authorization, undefined);
    assert.deepEqual(JSON.parse(options.body), { requestId: uuid, message: 'SmartPark là gì?', locale: 'auto' });
    return { ok: true, json: async () => ({ success: true, data: { reply: 'synthetic' } }) };
  } });
  assert.equal((await api.guest(' SmartPark là gì? ', 'auto')).reply, 'synthetic'); assert.equal(calls, 1);
});
test('private sends preserve request identity, auth handling and dedicated budget', async () => {
  let calls = 0; const controller = new AbortController();
  const body = { clientMessageId: uuid, expectedRevision: 2, message: 'my vehicles' };
  const api = load({ request: async (...args) => {
    calls++; assert.equal(args[0], `/conversations/${uuid}/messages`); assert.equal(args[1], 'POST');
    assert.equal(args[2], body); assert.equal(args[3], '/api/assistant'); assert.equal(args[4], 'https://localhost:5075');
    assert.equal(args[5], undefined); assert.equal(args[6].signal, controller.signal);
    assert.equal(args[6].timeoutMs, 30000); assert.equal(args[6].expireSessionOnUnauthorized, false);
    assert.equal(args[6].redirect, 'error'); assert.equal(args[6].credentials, 'omit');
    return { success: true, data: { state: 'running', clientMessageId: uuid } };
  } });
  assert.equal((await api.send(uuid, body, controller.signal)).state, 'running'); assert.equal(calls, 1);
});
test('unknown POST outcome propagates once; recovery explicitly looks up same ID', async () => {
  let calls = 0; const failure = new Error('synthetic network failure');
  const api = load({ request: async (path, method) => {
    calls++; if (method === 'POST') throw failure;
    assert.equal(path, `/conversations/${uuid}/requests/${uuid}`);
    return { success: true, data: { state: 'completed' } };
  } });
  await assert.rejects(api.send(uuid, { clientMessageId: uuid, expectedRevision: 0, message: 'my vehicles' }), e => e === failure);
  assert.equal(calls, 1); assert.equal((await api.result(uuid, uuid)).state, 'completed'); assert.equal(calls, 2);
});
test('explicit base required, unsafe origins never fall back to Demo/User', async () => {
  for (const base of ['', 'https://user:secret@example.test', 'https://example.test/path', 'https://example.test?key=x']) {
    const api = load({ base }); await assert.rejects(api.status(), /ASSISTANT_NOT_CONFIGURED/);
  }
});
test('invalid selectors/revisions/messages rejected before network', () => {
  const api = load();
  assert.throws(() => api.history('../private'), /INVALID_ASSISTANT_ID/);
  assert.throws(() => api.send(uuid, { clientMessageId: uuid, expectedRevision: -1, message: 'x' }), /INVALID_ASSISTANT_MESSAGE/);
  assert.throws(() => api.guest(' ', 'vi'), /INVALID_ASSISTANT_MESSAGE/);
});
test('public outage stays failure without retry or private fallback', async () => {
  let calls = 0;
  const api = load({ fetch: async () => { calls++; return { ok: false, status: 503 }; } });
  await assert.rejects(api.status(), e => e.status === 503); assert.equal(calls, 1);
});
