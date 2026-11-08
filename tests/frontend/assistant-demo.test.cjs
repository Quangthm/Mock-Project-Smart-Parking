const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function load(fetch, base = 'http://localhost:5095', timers = {}) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/assistantDemoApi.ts'), 'utf8')
    .replace('import.meta.env.VITE_ASSISTANT_DEMO_API_BASE_URL', JSON.stringify(base));
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const context = { exports: {}, fetch, URL, AbortController, Error, TypeError, SyntaxError, setTimeout, clearTimeout, ...timers };
  vm.runInNewContext(js, context);
  return context.exports.sendDemoMessage;
}
const controller = () => new AbortController();

test('sends trimmed input without credentials or auth and requires a demo reply', async () => {
  let calls = 0;
  const send = load(async (url, options) => {
    calls++;
    assert.equal(url, 'http://localhost:5095/api/demo/assistant/messages');
    assert.equal(options.credentials, 'omit');
    assert.equal(options.redirect, 'error');
    assert.equal(options.headers.Authorization, undefined);
    assert.equal(options.body, '{"message":"xin chào"}');
    return { ok: true, json: async () => ({ success: true, data: { isDemo: true, reply: '<b>plain</b>', notice: 'demo', mode: 'mock', sources: [] } }) };
  });
  assert.equal((await send('  xin chào  ', controller().signal)).reply, '<b>plain</b>');
  assert.equal(calls, 1);
});

test('missing configuration and invalid input do not call any API', async () => {
  const fetch = () => { throw new Error('must not fetch'); };
  await assert.rejects(load(fetch, '')('hello', controller().signal), /cấu hình/);
  await assert.rejects(load(fetch)(' ', controller().signal), /4000/);
  await assert.rejects(load(fetch)('😀'.repeat(2001), controller().signal), /4000/);
});

test('HTTP and invalid payload failures never retry or become mock answers', async () => {
  for (const response of [
    { ok: false, status: 500 },
    { ok: true, json: async () => ({ success: true, data: { isDemo: false, reply: 'real', notice: 'real' } }) },
  ]) {
    let calls = 0;
    const send = load(async () => { calls++; return response; });
    await assert.rejects(send('hello', controller().signal));
    assert.equal(calls, 1);
  }
});

test('external cancellation reaches fetch and clears the deadline', async () => {
  let cleared = false;
  const abort = controller();
  const send = load((_url, options) => new Promise((_resolve, reject) => {
    options.signal.addEventListener('abort', () => reject(new Error('aborted')));
  }), undefined, { setTimeout: () => 1, clearTimeout: () => { cleared = true; } });
  const result = send('hello', abort.signal);
  abort.abort();
  await assert.rejects(result, /aborted/);
  assert.equal(cleared, true);
});

test('deadline abort is surfaced as a retryable timeout message', async () => {
  let timeout;
  const send = load((_url, options) => new Promise((_resolve, reject) => {
    options.signal.addEventListener('abort', () => reject(new Error('aborted')));
  }), undefined, { setTimeout: (callback, delay) => { assert.equal(delay, 30000); timeout = callback; return 1; }, clearTimeout: () => {} });
  const result = send('hello', controller().signal);
  timeout();
  await assert.rejects(result, /quá lâu/);
});

test('provider reply preserves plain-text citations and explicit mode', async () => {
  const source = { articleId: 'booking-guide', version: '1.0', title: '<b>Đặt chỗ</b>', excerpt: 'Nội dung tham khảo' };
  const data = { isDemo: true, mode: 'provider', notice: 'AI demo', reply: 'Hướng dẫn', sources: [source], provider: 'OpenAI', model: 'configured-model' };
  const result = await load(async () => ({ ok: true, json: async () => ({ success: true, data }) }))('hello', controller().signal);
  assert.equal(result.mode, 'provider');
  assert.deepEqual(result.sources, [source]);
});

test('knowledge guidance is explicitly labeled and never pretends to be a provider response', async () => {
  const data = { isDemo: true, mode: 'knowledge', notice: 'Original documents, not AI', reply: 'Read guidance',
    sources: [{ articleId: 'booking', version: '2', title: 'Đặt chỗ', excerpt: 'Full policy, including final conditions.' }], provider: null, model: null };
  const result = await load(async () => ({ ok: true, json: async () => ({ success: true, data }) }))('booking', controller().signal);
  assert.equal(result.mode, 'knowledge');
  assert.equal(result.provider, null);
  for (const patch of [{ provider: 'gemini' }, { model: 'some-model' }]) {
    await assert.rejects(load(async () => ({ ok: true, json: async () => ({ success: true, data: { ...data, ...patch } }) }))('booking', controller().signal), /không hợp lệ/);
  }
});

test('invalid source fields, source collection or mode reject the response', async () => {
  const data = { isDemo: true, mode: 'provider', notice: 'demo', reply: 'reply', sources: [] };
  for (const patch of [
    { sources: null }, { sources: {} }, { sources: [null] },
    { sources: [{ articleId: 'a', version: '1', title: 'Title' }] },
    { sources: [{ articleId: 'a', version: '1', title: 'Title', excerpt: 42 }] },
    { sources: [{ articleId: ' ', version: '1', title: 'Title', excerpt: 'text' }] },
    { mode: 'unknown' }, { provider: {} }, { model: ' ' },
  ]) {
    await assert.rejects(load(async () => ({ ok: true, json: async () => ({ success: true, data: { ...data, ...patch } }) }))('hello', controller().signal), /không hợp lệ/);
  }
});

for (const [code, expected] of [
  ['RETRIEVAL_UNAVAILABLE', /hướng dẫn/],
  ['ASSISTANT_NOT_CONFIGURED', /sẵn sàng/],
  ['MODEL_TIMEOUT', /quá lâu/],
  ['INVALID_MODEL_OUTPUT', /chưa hợp lệ/],
  ['MODEL_UNAVAILABLE', /tạm gián đoạn/],
  ['MANAGEMENT_UNAVAILABLE', /sẵn sàng/],
]) {
  test(`${code} shows the appropriate error and correlation ID, without exposing arbitrary server details or retrying`, async () => {
    let calls = 0;
    const send = load(async () => {
      calls++;
      return { ok: false, status: 503, json: async () => ({ code, message: 'provider-sensitive-value', traceId: 'trace-123' }) };
    });
    await assert.rejects(send('hello', controller().signal), error => {
      assert.match(error.message, expected);
      assert.match(error.message, /Mã tra cứu: trace-123/);
      assert.doesNotMatch(error.message, /provider-sensitive-value|Settings|API key|provider\/model/);
      return true;
    });
    assert.equal(calls, 1);
  });
}

test('unknown error codes and invalid correlation values cannot inject server details', async () => {
  for (const code of ['MODEL_UNAVAILABLE', 'UNKNOWN', '__proto__', 'toString']) {
    const send = load(async () => ({ ok: false, status: 503,
      json: async () => ({ code, message: 'provider-sensitive-value', traceId: '<script>not-an-id</script>' }) }));
    await assert.rejects(send('hello', controller().signal), error => {
      assert.doesNotMatch(error.message, /provider-sensitive-value|script|Mã tra cứu/);
      return true;
    });
  }
});

test('local rate limiting explains waiting and never resends automatically', async () => {
  let calls = 0;
  const send = load(async () => { calls++; return { ok: false, status: 429 }; });
  await assert.rejects(send('hello', controller().signal), /gửi quá nhanh/);
  assert.equal(calls, 1);
});
