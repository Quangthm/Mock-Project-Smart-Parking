const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function fixture(failFirst = false) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/accessRealtime.ts'), 'utf8')
    .replace('import.meta.env.VITE_API_BASE_URL', 'undefined');
  const code = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022,
  } }).outputText;
  const listeners = new Map(), timers = new Map(), received = [];
  let nextTimer = 0, starts = 0, stops = 0, url, options, close, token = 'current-token';
  const connection = {
    state: 'Disconnected',
    on(name, callback) { listeners.set(name, callback); },
    off(name, callback) { if (listeners.get(name) === callback) listeners.delete(name); },
    onclose(callback) { close = callback; },
    async start() {
      starts++;
      if (failFirst && starts === 1) throw new Error('offline');
      this.state = 'Connected';
    },
    async stop() { stops++; this.state = 'Disconnected'; close?.(); },
  };
  class HubConnectionBuilder {
    withUrl(value, config) { url = value; options = config; return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return connection; }
  }
  const browser = () => ({ addEventListener() {}, removeEventListener() {} });
  const context = { exports: {}, console: { warn() {} }, window: browser(),
    document: { ...browser(), visibilityState: 'visible' },
    setTimeout(callback) { const id = ++nextTimer; timers.set(id, callback); return id; },
    clearTimeout(id) { timers.delete(id); },
    require(name) {
      if (name === '@microsoft/signalr') return { HubConnectionBuilder, LogLevel: { None: 6 } };
      if (name === './authApi') return { ensureAccessToken: async () => token };
      throw new Error(name);
    },
  };
  vm.runInNewContext(code, context);
  const stop = context.exports.watchAccessRevocation(reason => received.push(reason));
  return { connection, listeners, timers, received, stop, get starts() { return starts; },
    get stops() { return stops; }, get url() { return url; }, get options() { return options; },
    setToken(value) { token = value; },
    close() { connection.state = 'Disconnected'; close(); },
    retry() { const [id, callback] = timers.entries().next().value; timers.delete(id); callback(); },
  };
}
const settle = () => new Promise(resolve => setImmediate(resolve));

test('uses the current token factory and handles one targeted revocation', async () => {
  const f = fixture();
  await settle();
  assert.equal(f.url, 'http://localhost:5035/hubs/access');
  assert.equal(await f.options.accessTokenFactory(), 'current-token');
  const event = f.listeners.get('AccessRevoked');
  event({ reasonCode: 'USER_LOCKED' });
  event({ reasonCode: 'USER_LOCKED' });
  assert.deepEqual(f.received, ['USER_LOCKED']);
  assert.equal(f.stops, 1);
  assert.equal(f.listeners.has('AccessRevoked'), false);
  assert.equal(f.timers.size, 0);
});

test('temporary startup failure retries without revoking the session', async () => {
  const f = fixture(true);
  await settle();
  assert.deepEqual(f.received, []);
  assert.equal(f.timers.size, 1);
  f.retry();
  await settle();
  assert.equal(f.starts, 2);
  assert.equal(f.connection.state, 'Connected');
  assert.equal(f.timers.size, 0);
  f.stop();
});

test('cleanup cancels reconnection and stale callbacks', async () => {
  const f = fixture();
  await settle();
  f.close();
  assert.equal(f.timers.size, 1);
  const stale = [...f.timers.values()][0];
  f.stop();
  stale();
  await settle();
  assert.equal(f.starts, 1);
  assert.deepEqual(f.received, []);
});

for (const reason of ['ACCOUNT_DISABLED', 'SESSION_REVOKED']) {
  test(`${reason} is delivered once and stops the connection`, async () => {
    const f = fixture();
    await settle();
    const event = f.listeners.get('AccessRevoked');
    event({ reasonCode: reason });
    event({ reasonCode: reason });
    assert.deepEqual(f.received, [reason]);
    assert.equal(f.stops, 1);
  });
}

test('reconnect uses the latest token and does not log out on a disconnect', async () => {
  const f = fixture();
  await settle();
  f.close();
  assert.deepEqual(f.received, []);
  f.setToken('refreshed-token');
  f.retry();
  await settle();
  assert.equal(await f.options.accessTokenFactory(), 'refreshed-token');
  assert.equal(f.starts, 2);
  f.stop();
});

test('separate authenticated tabs own independent connections', async () => {
  const first = fixture(), second = fixture();
  await settle();
  first.listeners.get('AccessRevoked')({ reasonCode: 'USER_LOCKED' });
  assert.equal(first.stops, 1);
  assert.equal(second.stops, 0);
  assert.deepEqual(second.received, []);
  second.stop();
});
