const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

// Exercise the actual browser module with isolated storage and HTTP responses, without a DOM framework.
const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/authApi.ts'), 'utf8')
  .replace('import.meta.env.VITE_API_BASE_URL', 'undefined');
const javascript = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

function fixture(respond) {
  const storage = new Map([
    ['sp_access_token', 'access-a'], ['sp_refresh_token', 'refresh-a'], ['sp_access_expires_at', '9999999999999'],
  ]);
  const calls = [], events = [];
  const context = {
    exports: {}, AbortSignal, Event,
    require: name => {
      if (name === './store') return { store: { findUserById: () => undefined } };
      throw new Error(`Unexpected import: ${name}`);
    },
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
  return { api: context.exports.authApi, request: context.exports.request, ensureAccessToken: context.exports.ensureAccessToken, storage, calls, events };
}
const response = (status, payload = { success: false, code: 'INVALID_TOKEN', message: 'Invalid token.' }) =>
  ({ status, ok: status >= 200 && status < 300, json: async () => payload });

const apiUser = { userId: 'driver-id', fullName: 'Driver', email: 'driver@example.com', role: 'driver' };
const refreshedSession = { success: true, data: { accessToken: 'access-rotated', refreshToken: 'refresh-rotated', expiresIn: 900, user: apiUser } };

test('concurrent protected requests rotate an expired access token once before using the new bearer', async () => {
  const f = fixture(url => url.endsWith('/refresh') ? response(200, refreshedSession) : response(200, { success: true, data: apiUser }));
  f.storage.set('sp_access_expires_at', '1');
  const users = await Promise.all([f.api.currentUser(), f.api.currentUser()]);
  assert.equal(users.length, 2);
  assert.equal(f.calls.filter(c => c.url.endsWith('/refresh')).length, 1);
  assert.ok(f.calls.filter(c => c.url.endsWith('/me')).every(c => c.options.headers.Authorization === 'Bearer access-rotated'));
  assert.equal(f.storage.get('sp_refresh_token'), 'refresh-rotated');
});

test('logout waits for in-flight refresh and revokes the resulting pair', async () => {
  let release;
  const f = fixture(url => url.endsWith('/refresh') ? new Promise(resolve => { release = resolve; }) : response(200, { success: true }));
  f.storage.set('sp_access_expires_at', '1');
  const refresh = f.ensureAccessToken();
  const logout = f.api.logout();
  release(response(200, refreshedSession));
  await Promise.all([refresh, logout]);
  assert.deepEqual(f.calls.map(c => c.url.split('/').at(-1)), ['refresh', 'logout']);
  assert.equal(f.calls[1].options.headers.Authorization, 'Bearer access-rotated');
  assert.equal(JSON.parse(f.calls[1].options.body).refreshToken, 'refresh-rotated');
  assert.equal(f.storage.size, 0);
});

test('late refresh cannot resurrect a cleared or replaced session', async () => {
  for (const replace of [false, true]) {
    let release;
    const f = fixture(() => new Promise(resolve => { release = resolve; }));
    f.storage.set('sp_access_expires_at', '1');
    const refresh = f.ensureAccessToken();
    f.api.clearSession();
    if (replace) { f.storage.set('sp_access_token', 'new-login'); f.storage.set('sp_refresh_token', 'new-refresh'); }
    release(response(200, refreshedSession));
    await refresh;
    assert.equal(f.storage.get('sp_access_token'), replace ? 'new-login' : undefined);
    assert.deepEqual(f.events, []);
  }
});

for (const operation of ['logout', 'mutation', 'currentUser']) {
  test(`waiting ${operation} cannot switch to a new login while refresh completes`, async () => {
    let release;
    const f = fixture(url => url.endsWith('/refresh')
      ? new Promise(resolve => { release = resolve; })
      : response(200, { success: true, data: {
        accessToken: 'login-b', refreshToken: 'refresh-b', expiresIn: 900, user: apiUser,
      } }));
    f.storage.set('sp_access_expires_at', '1');
    const pending = operation === 'mutation'
      ? f.request('/users/operators', 'POST', { fullName: 'Operator' }, '/api')
      : f.api[operation]();
    const rejected = assert.rejects(pending, /sign-in changed/);
    await f.api.login('another@example.com', 'Password@123');
    release(response(200, refreshedSession));
    await rejected;
    assert.equal(f.storage.get('sp_access_token'), 'login-b');
    assert.equal(f.storage.get('sp_refresh_token'), 'refresh-b');
    assert.deepEqual(f.calls.map(c => c.url.split('/').at(-1)), ['refresh', 'login']);
  });
}

test('invalid activation proof does not erase an unrelated authenticated session', async () => {
  const f = fixture(() => response(401));
  await assert.rejects(f.request('/bootstrap-password', 'POST', { challengeId: 'expired' }), error => error.status === 401);
  assert.equal(f.storage.get('sp_access_token'), 'access-a');
  assert.deepEqual(f.events, []);
});

test('completed protected mutation from an old login cannot reach the new login UI', async () => {
  let release;
  const f = fixture(url => url.endsWith('/login')
    ? response(200, { success: true, data: {
      accessToken: 'login-b', refreshToken: 'refresh-b', expiresIn: 900, user: apiUser,
    } })
    : new Promise(resolve => { release = resolve; }));
  const pending = f.api.disableMfa({ password: 'Password@123', totp: '123456' });
  const rejected = assert.rejects(pending, /sign-in changed/);
  await new Promise(resolve => setImmediate(resolve));
  await f.api.login('another@example.com', 'Password@123');
  release(response(200, { success: true }));
  await rejected;
  assert.equal(f.storage.get('sp_access_token'), 'login-b');
});

for (const replacement of ['clear', 'login']) {
  test(`late sign-in cannot overwrite a subsequent ${replacement}`, async () => {
    let release;
    const f = fixture((_url, options) => JSON.parse(options.body).email === 'old@example.com'
      ? new Promise(resolve => { release = resolve; })
      : response(200, { success: true, data: {
        accessToken: 'login-b', refreshToken: 'refresh-b', expiresIn: 900, user: apiUser,
      } }));
    const pending = f.api.login('old@example.com', 'Password@123');
    const rejected = assert.rejects(pending, /sign-in changed/);
    if (replacement === 'clear') f.api.clearSession();
    else await f.api.login('new@example.com', 'Password@123');
    release(response(200, refreshedSession));
    await rejected;
    assert.equal(f.storage.get('sp_access_token'), replacement === 'clear' ? undefined : 'login-b');
  });
}

test('refresh transport failure preserves the pair and does not retry automatically', async () => {
  const f = fixture(() => { throw new Error('connection lost'); });
  f.storage.set('sp_access_expires_at', '1');
  await assert.rejects(f.ensureAccessToken(), /Cannot refresh/);
  assert.equal(f.calls.length, 1);
  assert.equal(f.storage.get('sp_refresh_token'), 'refresh-a');
  assert.deepEqual(f.events, []);
});

test('password verification returns a challenge without creating a session or storing its proof', async () => {
  const challenge = { challengeId: 'challenge', challengeToken: 'private-proof', expiresAt: 'future' };
  const f = fixture(() => response(200, { success: true, mfaRequired: true, data: challenge }));
  f.api.clearSession();
  const result = await f.api.login('driver@example.com', 'Password@123');
  assert.equal(result.mfaRequired, true);
  assert.equal(result.challenge.challengeToken, challenge.challengeToken);
  assert.equal(f.storage.size, 0);
});

test('MFA completion sends challenge proof and recovery code, then stores only tokens and remaining count', async () => {
  const f = fixture(() => response(200, { success: true, data: {
    accessToken: 'mfa-access', refreshToken: 'mfa-refresh', expiresIn: 3600, user: apiUser, recoveryCodesRemaining: 7,
  } }));
  f.api.clearSession();
  await f.api.completeMfaLogin({ challengeId: 'challenge', challengeToken: 'proof', expiresAt: 'future' }, undefined, 'saved-code');
  assert.deepEqual(JSON.parse(f.calls[0].options.body), { challengeId: 'challenge', challengeToken: 'proof', recoveryCode: 'saved-code' });
  assert.equal(f.storage.get('sp_mfa_recovery_remaining'), '7');
  assert.ok(!JSON.stringify([...f.storage.values()]).includes('saved-code'));
  assert.ok(!JSON.stringify([...f.storage.values()]).includes('proof'));
});

for (const operation of ['setupMfa', 'disableMfa', 'regenerateRecoveryCodes']) {
  test(`${operation}: incorrect verification reports error without expiring the authenticated session`, async () => {
    const f = fixture(() => response(401, { success: false, code: 'MFA_INVALID', message: 'Invalid code.' }));
    await assert.rejects(f.api[operation]({ password: 'Password@123', totp: '000000' }), error => error.code === 'MFA_INVALID');
    assert.equal(f.storage.get('sp_access_token'), 'access-a');
    assert.deepEqual(f.events, []);
  });
}

for (const operation of ['setupMfa', 'disableMfa', 'regenerateRecoveryCodes']) {
  test(`${operation}: revoked bearer expires the session and clears tokens`, async () => {
    const f = fixture(() => response(401, { success: false, code: 'INVALID_TOKEN', message: 'Invalid token.' }));
    await assert.rejects(f.api[operation]({ password: 'Password@123', totp: '123456' }), error => error.code === 'INVALID_TOKEN');
    assert.equal(f.storage.size, 0);
    assert.deepEqual(f.events, ['sp-auth-expired']);
  });
}

test('protected MFA failure from an earlier session cannot clear a replacement session', async () => {
  const f = fixture((_url, _options, storage) => {
    storage.set('sp_access_token', 'replacement');
    return response(401);
  });
  await assert.rejects(f.api.setupMfa({ password: 'Password@123' }));
  assert.equal(f.storage.get('sp_access_token'), 'replacement');
  assert.deepEqual(f.events, []);
});

test('OTP sign-in supports a recovery code instead of TOTP', async () => {
  const f = fixture(() => response(200, { success: true, data: {
    accessToken: 'otp-access', refreshToken: 'otp-refresh', expiresIn: 3600, user: apiUser, recoveryCodesRemaining: 0,
  } }));
  await f.api.loginOtp('challenge', '123456', undefined, 'saved-code');
  assert.deepEqual(JSON.parse(f.calls[0].options.body), { challengeId: 'challenge', code: '123456', recoveryCode: 'saved-code' });
  assert.equal(f.storage.get('sp_mfa_recovery_remaining'), '0');
});

test('OTP login stores server identity and never stores the code or authenticator secret', async () => {
  const f = fixture(() => response(200, { success: true, data: { accessToken: 'otp-access', refreshToken: 'otp-refresh', expiresIn: 86400, user: apiUser } }));
  const user = await f.api.loginOtp('challenge', '123456', '654321');
  assert.deepEqual(JSON.parse(f.calls[0].options.body), { challengeId: 'challenge', code: '123456', totp: '654321' });
  assert.equal(user.password, ''); assert.equal(f.storage.get('sp_access_token'), 'otp-access');
  assert.ok(!JSON.stringify([...f.storage.values()]).includes('123456'));
});
test('OTP authentication failure preserves an existing independent session', async () => {
  const f = fixture(() => response(401, { success: false, message: 'MFA_REQUIRED' }));
  await assert.rejects(f.api.loginOtp('challenge', '123456'), /MFA_REQUIRED/);
  assert.equal(f.storage.get('sp_access_token'), 'access-a'); assert.deepEqual(f.events, []);
});
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
  assert.equal(f.storage.get('sp_access_expires_at'), '9999999999999');
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
