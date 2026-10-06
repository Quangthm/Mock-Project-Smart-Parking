const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

// Exercise the actual browser module with isolated storage and HTTP responses, without a DOM framework.
const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/authApi.ts'), 'utf8')
  .replace("import { store } from './store';", 'const store = { findUserById: () => undefined };')
  .replace('import.meta.env.VITE_API_BASE_URL', 'undefined');
const javascript = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

function fixture(respond) {
  const storage = new Map([
    ['sp_access_token', 'access-a'], ['sp_refresh_token', 'refresh-a'], ['sp_access_expires_at', '1234'],
  ]);
  const calls = [], events = [];
  const context = {
    exports: {}, AbortSignal, Event,
    sessionStorage: {
      getItem: key => storage.get(key) ?? null,
      removeItem: key => storage.delete(key),
      setItem: (key, value) => storage.set(key, value),
    },
    window: { dispatchEvent: event => events.push(event.type) },
    fetch: async (url, options) => {
      calls.push({ url, options });
      return respond(url, options, storage);
    },
  };
  vm.runInNewContext(javascript, context);
  return { api: context.exports.authApi, storage, calls, events };
}
const response = (status, payload = { success: false, code: 'INVALID_TOKEN', message: 'Invalid token.' }) =>
  ({ status, ok: status >= 200 && status < 300, json: async () => payload });

const apiUser = { userId: 'driver-id', fullName: 'Driver', email: 'driver@example.com', role: 'driver' };
test('login trims email, preserves password and stores the server session and identity', async () => {
  const f = fixture(() => response(200, { success: true, data: {
    accessToken: 'access-new', refreshToken: 'refresh-new', expiresIn: 3600, user: apiUser,
  } }));
  const started = Date.now();
  const user = await f.api.login('  driver@example.com  ', ' Password@123 ');
  assert.deepEqual(JSON.parse(f.calls[0].options.body), { email: 'driver@example.com', password: ' Password@123 ' });
  assert.equal(user.id, 'driver-id');
  assert.equal(user.role, 'driver');
  assert.equal(user.password, '');
  assert.equal(f.storage.get('sp_access_token'), 'access-new');
  assert.equal(f.storage.get('sp_refresh_token'), 'refresh-new');
  assert.ok(f.api.expiresAt() >= started + 3600000);
  assert.ok(f.api.expiresAt() <= Date.now() + 3600000);
});

for (const [status, code] of [[400, 'VALIDATION_ERROR'], [401, 'AUTH_FAILED'], [403, 'ACCOUNT_LOCKED']]) {
  test(`login HTTP ${status} reports the server error without creating a session`, async () => {
    const f = fixture(() => response(status, { success: false, code, message: code }));
    f.api.clearSession();
    await assert.rejects(f.api.login('driver@example.com', 'wrong'), error => error.status === status && error.message === code);
    assert.equal(f.storage.size, 0);
    assert.deepEqual(f.events, []);
  });
}

test('unsupported login role does not store tokens', async () => {
  const f = fixture(() => response(200, { success: true, data: {
    accessToken: 'access-new', refreshToken: 'refresh-new', expiresIn: 3600, user: { ...apiUser, role: 'unknown' },
  } }));
  f.api.clearSession();
  await assert.rejects(f.api.login('driver@example.com', 'password'), /unsupported account role/);
  assert.equal(f.storage.size, 0);
});

test('currentUser restores the backend identity using the bearer token', async () => {
  const f = fixture(() => response(200, { success: true, data: apiUser }));
  assert.equal((await f.api.currentUser()).id, 'driver-id');
  assert.equal(f.calls[0].options.headers.Authorization, 'Bearer access-a');
});

for (const status of [401, 500]) {
  test(`currentUser HTTP ${status} ${status === 401 ? 'expires' : 'preserves'} the session`, async () => {
    const f = fixture(() => response(status));
    await assert.rejects(f.api.currentUser(), error => error.status === status);
    assert.equal(f.storage.size, status === 401 ? 0 : 3);
    assert.deepEqual(f.events, status === 401 ? ['sp-auth-expired'] : []);
  });
}

for (const status of [200, 401]) {
  test(`late currentUser HTTP ${status} cannot restore or expire a replacement session`, async () => {
    const f = fixture((url, options, storage) => {
      storage.set('sp_access_token', 'access-b');
      return response(status, { success: true, data: apiUser });
    });
    if (status === 200) assert.equal(await f.api.currentUser(), null);
    else await assert.rejects(f.api.currentUser(), error => error.status === 401);
    assert.equal(f.storage.get('sp_access_token'), 'access-b');
    assert.deepEqual(f.events, []);
  });
}

test('mismatched refresh token reports logout failure and preserves a valid session', async () => {
  const f = fixture(url => url.endsWith('/logout') ? response(401) : response(200, { success: true }));
  await assert.rejects(f.api.logout(), error => error.status === 401);
  assert.equal(f.storage.get('sp_access_token'), 'access-a');
  assert.equal(f.storage.get('sp_refresh_token'), 'refresh-a');
  assert.equal(f.storage.get('sp_access_expires_at'), '1234');
  assert.deepEqual(f.events, []);
  assert.deepEqual(f.calls.map(call => call.url.split('/').at(-1)), ['logout', 'me']);
  assert.equal(f.calls[0].options.headers.Authorization, 'Bearer access-a');
  assert.deepEqual(JSON.parse(f.calls[0].options.body), { refreshToken: 'refresh-a' });
});

test('invalid bearer expires the session without reporting successful revocation', async () => {
  const f = fixture(() => response(401));
  await assert.rejects(f.api.logout(), error => error.status === 401);
  assert.equal(f.storage.size, 0);
  assert.deepEqual(f.events, ['sp-auth-expired']);
});

test('successful logout clears both tokens and expiry', async () => {
  const f = fixture(() => response(200, { success: true }));
  await f.api.logout();
  assert.equal(f.storage.size, 0);
  assert.equal(f.calls.length, 1);
  assert.deepEqual(f.events, []);
});

for (const status of [400, 500]) {
  test(`logout HTTP ${status} preserves the session for retry`, async () => {
    const f = fixture(() => response(status));
    await assert.rejects(f.api.logout(), error => error.status === status);
    assert.equal(f.storage.get('sp_access_token'), 'access-a');
    assert.equal(f.storage.get('sp_refresh_token'), 'refresh-a');
    assert.equal(f.calls.length, 1);
  });
}

test('failed bearer probe preserves tokens and the original logout error', async () => {
  const f = fixture(url => {
    if (url.endsWith('/me')) throw new Error('Network unavailable');
    return response(401);
  });
  await assert.rejects(f.api.logout(), error => error.status === 401);
  assert.equal(f.storage.get('sp_access_token'), 'access-a');
  assert.deepEqual(f.events, []);
});

test('an unexpected HTTP 200 body does not clear the session', async () => {
  const f = fixture(() => response(200, { success: false }));
  await assert.rejects(f.api.logout(), /Sign out failed/);
  assert.equal(f.storage.get('sp_refresh_token'), 'refresh-a');
});

for (const status of [200, 401]) {
  test(`a late logout HTTP ${status} cannot clear a replacement session`, async () => {
    const f = fixture((url, options, storage) => {
      storage.set('sp_access_token', 'access-b');
      storage.set('sp_refresh_token', 'refresh-b');
      return response(status, { success: true });
    });
    if (status === 200) await f.api.logout();
    else await assert.rejects(f.api.logout(), error => error.status === 401);
    assert.equal(f.storage.get('sp_access_token'), 'access-b');
    assert.equal(f.storage.get('sp_refresh_token'), 'refresh-b');
    assert.equal(f.calls.length, 1);
    assert.deepEqual(f.events, []);
  });
}
