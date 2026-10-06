const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const javascript = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../../FE/src/context/AppContext.tsx'), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX },
}).outputText;
const account = id => ({ id, name: id, role: 'driver' });
function deferred() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}
const settle = () => new Promise(resolve => setImmediate(resolve));

// Execute the real provider with deterministic hooks; these tests cover async state transitions, not DOM layout.
function fixture() {
  const restore = deferred(), logout = deferred();
  const slots = [], effects = [], listeners = new Map(), audit = [];
  let cursor = 0, mounted = false, cleared = 0, logoutCalls = 0, currentId = null;
  const react = {
    createContext: () => ({ Provider: 'provider' }),
    useState: initial => {
      const index = cursor++;
      if (!(index in slots)) slots[index] = initial;
      return [slots[index], value => { slots[index] = value; }];
    },
    useRef: initial => {
      const index = cursor++;
      return slots[index] ??= { current: initial };
    },
    useEffect: effect => { if (!mounted) effects.push(effect); },
  };
  const api = {
    currentUser: () => restore.promise, logout: () => { logoutCalls++; return logout.promise; },
    clearSession: () => { cleared++; }, expiresAt: () => Date.now() + 3600000,
  };
  const store = {
    init() {}, saveUser() {}, remindExpiringBookings() {},
    setCurrentUserId: id => { currentId = id; }, addAuditLog: entry => audit.push(entry),
  };
  const context = {
    exports: {}, Error,
    require: name => {
      if (name === 'react') return react;
      if (name === 'react/jsx-runtime') return { jsx: (type, props) => ({ type, props }) };
      if (name === '../lib/authApi') return { authApi: api, authExpiredEvent: 'sp-auth-expired' };
      if (name === '../lib/store') return { store };
      throw new Error(`Unexpected import: ${name}`);
    },
    localStorage: { getItem: () => null },
    document: { documentElement: { dataset: {}, classList: { add() {} } } },
    window: {
      addEventListener: (name, handler) => listeners.set(name, handler),
      removeEventListener: name => listeners.delete(name),
      setInterval() {}, clearInterval() {}, setTimeout() {}, clearTimeout() {},
    },
  };
  vm.runInNewContext(javascript, context);
  const render = () => { cursor = 0; return context.exports.AppProvider({ children: null }).props.value; };
  render();
  mounted = true;
  effects.forEach(effect => effect());
  return { render, restore, logout, audit, expire: () => listeners.get('sp-auth-expired')(),
    get cleared() { return cleared; }, get logoutCalls() { return logoutCalls; }, get currentId() { return currentId; } };
}

test('temporary restore failure keeps tokens available for retry', async () => {
  const f = fixture();
  f.restore.reject(new Error('Network unavailable'));
  await settle();
  assert.equal(f.cleared, 0);
  assert.equal(f.render().user, null);
  assert.equal(f.render().authReady, true);
  assert.equal(f.render().authError, 'Network unavailable');
});

for (const success of [true, false]) {
  test(`late restore ${success ? 'success' : 'failure'} cannot overwrite a new login`, async () => {
    const f = fixture();
    f.render().setUser(account('new-user'));
    f.render().setView('driver');
    if (success) f.restore.resolve(account('old-user'));
    else f.restore.reject(new Error('Old restore failed'));
    await settle();
    assert.equal(f.render().user.id, 'new-user');
    assert.equal(f.currentId, 'new-user');
    assert.equal(f.render().authError, '');
    assert.equal(f.cleared, 0);
  });
  test(`late logout ${success ? 'success' : 'failure'} cannot change a new login`, async () => {
    const f = fixture();
    f.restore.resolve(account('old-user'));
    await settle();
    const pending = f.render().signOut();
    f.render().setUser(account('new-user'));
    f.render().setView('driver');
    if (success) f.logout.resolve();
    else f.logout.reject(new Error('Old logout failed'));
    await pending;
    assert.equal(f.render().user.id, 'new-user');
    assert.equal(f.render().view, 'driver');
    assert.equal(f.currentId, 'new-user');
    assert.equal(f.render().authError, '');
    assert.equal(f.audit.length, 0);
  });
}

test('expiry during logout keeps the sign-in view and expiry message', async () => {
  const f = fixture();
  f.restore.resolve(account('old-user'));
  await settle();
  const pending = f.render().signOut();
  f.expire();
  f.logout.reject(new Error('Invalid token'));
  await pending;
  assert.equal(f.render().user, null);
  assert.equal(f.currentId, null);
  assert.equal(f.render().view, 'sign-in');
  assert.match(f.render().authError, /session has expired/);
});

test('late restore cannot bring back an expired session', async () => {
  const f = fixture();
  f.expire();
  f.restore.resolve(account('old-user'));
  await settle();
  assert.equal(f.render().user, null);
  assert.equal(f.currentId, null);
  assert.equal(f.render().view, 'sign-in');
  assert.equal(f.cleared, 1);
  assert.match(f.render().authError, /session has expired/);
});

test('successful logout clears UI identity and duplicate clicks send one request', async () => {
  const f = fixture();
  f.restore.resolve(account('old-user'));
  await settle();
  const pending = f.render().signOut();
  await f.render().signOut();
  f.logout.resolve();
  await pending;
  assert.equal(f.render().user, null);
  assert.equal(f.currentId, null);
  assert.equal(f.render().view, 'landing');
  assert.equal(f.audit.length, 1);
  assert.equal(f.logoutCalls, 1);
});

test('logout failure keeps the user visible and allows a retry', async () => {
  const f = fixture();
  f.restore.resolve(account('old-user'));
  await settle();
  const pending = f.render().signOut();
  f.logout.reject(new Error('Network unavailable'));
  await pending;
  assert.equal(f.render().user.id, 'old-user');
  assert.equal(f.render().authError, 'Network unavailable');
  f.logout.promise = Promise.resolve();
  await f.render().signOut();
  assert.equal(f.render().user, null);
});
