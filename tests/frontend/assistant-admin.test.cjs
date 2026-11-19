const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function load(request, base = 'http://localhost:5095') {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/assistantAdminApi.ts'), 'utf8')
    .replace('import.meta.env.VITE_ASSISTANT_API_BASE_URL', JSON.stringify(base));
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const context = { exports: {}, URL, require: name => {
    assert.equal(name, './authApi'); return { request };
  } };
  vm.runInNewContext(js, context);
  return context.exports;
}
function view() {
  return { secretsConfigured: true, runtimeReleaseApproved: false, settings: {
    revision: 2, enabled: false, maintenanceMessage: '',
    providers: ['gemini', 'openai'].map(id => ({ id, model: 'model-test', enabled: false, priority: 1 })),
    contractVersion: 2, configurations: [{ model: 'model-test', credentialVersion: '22222222-2222-4222-8222-222222222222', id: '11111111-1111-4111-8111-111111111111', provider: 'openai', label: 'Test', enabled: false, priority: 1, createdAt: '2026-11-03T00:00:00Z', revokedAt: null }],
  } };
}
test('uses baseline authenticated request and projects only public credential metadata', async () => {
  const data = view(); data.settings.configurations[0].secret = 'must-not-retain';
  const api = load(async (...args) => {
    assert.deepEqual(args.slice(0, 5), ['/settings', 'GET', undefined, '/api/assistant/admin', 'http://localhost:5095']);
    assert.equal(args[6].expireSessionOnUnauthorized, false);
    return { success: true, data };
  });
  const result = await api.assistantAdminApi.read();
  assert.equal(result.settings.configurations[0].secret, undefined);
  assert.equal(result.settings.revision, 2);
});
test('mutations preserve operation ID and revision with one dispatch', async () => {
  let calls = 0;
  const mutation = { action: 'save-config', mutationId: 'fixed-id', expectedRevision: 2, enabled: false };
  const api = load(async (route, method, body) => {
    calls++; assert.equal(route, '/mutations'); assert.equal(method, 'POST'); assert.equal(body, mutation);
    return { success: true, data: view() };
  });
  await api.assistantAdminApi.change(mutation);
  assert.equal(calls, 1);
});
test('conflicts and unknown failures are propagated without retry', async () => {
  for (const status of [409, 500]) {
    let calls = 0;
    const error = Object.assign(new Error('rejected'), { status });
    const api = load(async () => { calls++; throw error; });
    await assert.rejects(api.assistantAdminApi.change({ action: 'save-key' }), e => e === error);
    assert.equal(calls, 1);
  }
});
test('invalid management configuration never sends credentials to a fallback', async () => {
  for (const base of ['', 'invalid', 'https://user:pass@example.test', 'https://example.test?key=value']) {
    const api = load(() => assert.fail('must not dispatch'), base);
    assert.equal(api.assistantAdminConfigured, false);
    await assert.rejects(api.assistantAdminApi.read(), /ASSISTANT_NOT_CONFIGURED/);
  }
});
test('malformed or duplicate provider responses are rejected', async () => {
  const bad = [null, { settings: {} }, view(), view()];
  bad[2].settings.providers[1].id = 'gemini';
  bad[3].settings.configurations[0].priority = '1';
  for (const data of bad) {
    await assert.rejects(load(async () => ({ success: true, data })).assistantAdminApi.read(), /INVALID_ASSISTANT_ADMIN_RESPONSE/);
  }
});

// Exercise the real shared auth transport too: a mocked request alone cannot
// detect an Assistant response erasing the application's login.
function authenticated(respond) {
  const storage = new Map([
    ['sp_access_token', 'access-a'], ['sp_refresh_token', 'refresh-a'],
    ['sp_access_expires_at', '9999999999999'],
  ]);
  const events = [], calls = [];
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/authApi.ts'), 'utf8')
    .replace('import.meta.env.VITE_API_BASE_URL', JSON.stringify('http://localhost:5035'));
  const js = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const context = {
    exports: {}, AbortSignal, Event,
    require: name => {
      assert.equal(name, './store');
      return { store: { findUserById: () => undefined } };
    },
    sessionStorage: {
      getItem: key => storage.get(key) ?? null,
      setItem: (key, value) => storage.set(key, value),
      removeItem: key => storage.delete(key),
    },
    window: { dispatchEvent: event => events.push(event.type) },
    fetch: async (url, options) => {
      calls.push({ url, options });
      return respond(url, options);
    },
  };
  vm.runInNewContext(js, context);
  return { ...load(context.exports.request), auth: context.exports.authApi, storage, events, calls };
}
const http = (status, data = { success: false, code: 'INVALID_TOKEN', message: 'Rejected.' }) =>
  ({ status, ok: status >= 200 && status < 300, json: async () => data });

for (const action of ['read', 'change']) {
  for (const status of [401, 403, 503]) {
    test(`Assistant ${action} HTTP ${status} preserves the login and reports the error without retry`, async () => {
      const f = authenticated(() => http(status));
      const before = [...f.storage.entries()];
      await assert.rejects(f.assistantAdminApi[action]({ action: 'save-config' }), e => e.status === status);
      assert.deepEqual([...f.storage.entries()], before);
      assert.deepEqual(f.events, []);
      assert.equal(f.calls.length, 1);
      assert.equal(f.calls[0].options.headers.Authorization, 'Bearer access-a');
    });
  }
}

test('Assistant rejection after successful UserService refresh keeps the rotated login', async () => {
  const f = authenticated(url => url.endsWith('/api/auth/refresh') ? http(200, {
    success: true, data: {
      accessToken: 'access-b', refreshToken: 'refresh-b', expiresIn: 900,
      user: { userId: 'admin-id', fullName: 'Admin', email: 'admin@example.test', role: 'admin' },
    },
  }) : http(401));
  f.storage.set('sp_access_expires_at', '1');
  await assert.rejects(f.assistantAdminApi.read(), e => e.status === 401);
  assert.equal(f.storage.get('sp_access_token'), 'access-b');
  assert.equal(f.storage.get('sp_refresh_token'), 'refresh-b');
  assert.deepEqual(f.events, ['sp-auth-refreshed']);
  assert.deepEqual(f.calls.map(c => c.url), [
    'http://localhost:5035/api/auth/refresh', 'http://localhost:5095/api/assistant/admin/settings',
  ]);
  assert.equal(f.calls[1].options.headers.Authorization, 'Bearer access-b');
});

test('UserService refresh rejection still expires the login before any Assistant request', async () => {
  const f = authenticated(() => http(401));
  f.storage.set('sp_access_expires_at', '1');
  await assert.rejects(f.assistantAdminApi.read(), e => e.status === 401);
  assert.equal(f.storage.size, 0);
  assert.deepEqual(f.events, ['sp-auth-expired']);
  assert.deepEqual(f.calls.map(c => c.url), ['http://localhost:5035/api/auth/refresh']);
});

test('Assistant response cannot update a replacement login', async () => {
  let complete;
  const f = authenticated(() => new Promise(resolve => { complete = resolve; }));
  const pending = f.assistantAdminApi.change({ action: 'save-config' });
  const rejected = assert.rejects(pending, /sign-in changed/);
  await new Promise(resolve => setImmediate(resolve));
  f.auth.clearSession();
  f.storage.set('sp_access_token', 'replacement');
  f.storage.set('sp_refresh_token', 'replacement-refresh');
  complete(http(200, { success: true, data: view() }));
  await rejected;
  assert.equal(f.storage.get('sp_access_token'), 'replacement');
  assert.deepEqual(f.events, []);
});
test('discovery uses POST with transient credentials and preserves opaque pagination', async () => {
  const body = { provider: 'gemini', secret: 'synthetic-only', cursor: 'opaque' };
  const api = load(async (route, method, payload) => {
    assert.equal(route, '/models/discover'); assert.equal(method, 'POST'); assert.equal(payload, body);
    return { success: true, data: { items: [{ id: 'sample', displayName: 'Sample', supportsChat: true, secret: 'drop' }],
      nextCursor: 'next', asOf: '2026-11-05T00:00:00Z', secret: 'drop' } };
  });
  const result = await api.assistantAdminApi.discover(body);
  assert.equal(result.nextCursor, 'next');
  assert.equal(result.secret, undefined);
  assert.equal(result.items[0].secret, undefined);
});
test('running and terminal test responses retain identity and safe metadata', async () => {
  const id = '11111111-1111-4111-8111-111111111111';
  const body = { expectedRevision: 3, testRequestId: '22222222-2222-4222-8222-222222222222' };
  for (const outcome of ['running', 'passed', 'failed', 'unknown']) {
    const api = load(async (route, method, payload, _prefix, _server, _headers, options) => {
      assert.equal(route, '/configurations/' + id + '/test'); assert.equal(method, 'POST'); assert.equal(payload, body);
      assert.equal(options.timeoutMs, 35_000);
      return { success: true, data: { configurationId: id, testRequestId: body.testRequestId, testedRevision: 3,
        outcome, model: 'sample', latencyMs: 42, current: false, checkedAt: '2026-11-05T00:00:00Z', code: null, secret: 'drop' } };
    });
    const result = await api.assistantAdminApi.test(id, body);
    assert.equal(result.outcome, outcome); assert.equal(result.secret, undefined);
    if (outcome !== 'running') assert.equal(result.current, false);
  }
});
test('rejects mismatched diagnostics and old settings contracts', async () => {
  const id = '11111111-1111-4111-8111-111111111111';
  await assert.rejects(load(async () => ({ success: true, data: { outcome: 'running', configurationId: id, testRequestId: 'foreign' } }))
    .assistantAdminApi.test(id, { expectedRevision: 1, testRequestId: 'expected' }), /INVALID_ASSISTANT_ADMIN_RESPONSE/);
  const old = view(); delete old.settings.configurations; delete old.settings.contractVersion;
  await assert.rejects(load(async () => ({ success: true, data: old })).assistantAdminApi.read(), /INVALID_ASSISTANT_ADMIN_RESPONSE/);
});
