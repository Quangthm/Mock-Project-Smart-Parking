const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function load(file, imports, globals = {}) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8')
    .replace('import.meta.env.VITE_API_BASE_URL', 'undefined');
  const js = ts.transpileModule(source, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  const context = { exports: {}, ...globals, require(name) {
    if (name.endsWith("/ui/RoundedSelect")) return { RoundedSelect: "select" };
    if (name.endsWith("/ui/PageHeader")) return { PageHeader: "page-header" };
    if (name.endsWith("/ui/ParkingStatus")) return { ParkingStatus: "parking-status" };
    if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
    return imports[name];
  } };
  vm.runInNewContext(js, context);
  return context.exports;
}
const settle = () => new Promise(resolve => setImmediate(resolve));
function events(extra = {}) {
  const handlers = new Map();
  return { ...extra,
    addEventListener(name, handler) { handlers.set(name, handler); },
    removeEventListener(name, handler) { if (handlers.get(name) === handler) handlers.delete(name); },
    emit(name) { handlers.get(name)?.(); },
    handlers,
  };
}
function liveFixture(failures = 0) {
  const timers = new Map();
  let timerId = 0, starts = 0, reloads = 0, onclose, expires = Date.now() + 900_000;
  const window = events(), document = events({ visibilityState: 'visible' });
  const connection = {
    state: 'Disconnected', on() {}, onclose(handler) { onclose = handler; },
    async start() {
      starts++;
      assert.equal(this.state, 'Disconnected', 'must never restart an active connection');
      if (failures-- > 0) throw new Error('offline');
      this.state = 'Connected';
    },
    async stop() { this.state = 'Disconnected'; onclose?.(); },
  };
  class HubConnectionBuilder {
    withUrl() { return this; } configureLogging() { return this; } build() { return connection; }
  }
  const { watchOperators } = load('lib/operatorLive.ts', {
    '@microsoft/signalr': { HubConnectionBuilder, LogLevel: { None: 6 } },
    './authApi': { authApi: { expiresAt: () => expires }, authExpiredEvent: 'expired',
      authRefreshedEvent: 'refreshed', ensureAccessToken: async () => 'token' },
  }, { window, document, Date,
    setTimeout(callback, delay) { const id = ++timerId; timers.set(id, { callback, delay }); return id; },
    clearTimeout(id) { timers.delete(id); },
  });
  const stop = watchOperators(() => reloads++);
  return { window, document, connection, timers, stop,
    get starts() { return starts; }, get reloads() { return reloads; },
    expire() { expires = 0; window.emit('expired'); },
    close() { connection.state = 'Disconnected'; onclose(); },
    runTimer() { const [id, timer] = timers.entries().next().value; timers.delete(id); timer.callback(); },
  };
}

test('focus reconnect cancels an earlier failed-start retry and never restarts Connected', async () => {
  const f = liveFixture(1);
  await settle();
  assert.equal(f.starts, 1);
  assert.equal(f.timers.size, 1);
  const staleTimer = [...f.timers.values()][0].callback;
  f.window.emit('focus');
  await settle();
  assert.equal(f.starts, 2);
  assert.equal(f.connection.state, 'Connected');
  assert.equal(f.timers.size, 0);
  staleTimer(); // Also tolerate a callback already queued before clearTimeout.
  await settle();
  assert.equal(f.starts, 2);
  assert.equal(f.timers.size, 0);
  assert.equal(f.reloads, 1);
  f.stop();
});

test('repeated closes schedule one retry and a successful retry reloads authoritative state', async () => {
  const f = liveFixture();
  await settle();
  f.close(); f.close();
  assert.equal(f.timers.size, 1);
  f.runTimer();
  await settle();
  assert.equal(f.starts, 2);
  assert.equal(f.reloads, 2);
  assert.equal(f.timers.size, 0);
  f.stop();
});

test('expiry cancels reconnect and cleanup removes resume listeners', async () => {
  const f = liveFixture(1);
  await settle();
  assert.equal(f.timers.size, 1);
  f.expire();
  await settle();
  assert.equal(f.timers.size, 0);
  f.stop();
  f.window.emit('focus'); f.window.emit('refreshed'); f.document.emit('visibilitychange');
  await settle();
  assert.equal(f.starts, 1);
  assert.equal(f.window.handlers.size, 0);
  assert.equal(f.document.handlers.size, 0);
});

function nodes(tree) {
  if (!tree || typeof tree !== 'object') return [];
  return [tree, ...[tree.props?.children].flat(Infinity).flatMap(nodes)];
}

test('late create response neither duplicates nor downgrades an Operator already loaded live', async () => {
  const states = [], effects = [];
  let cursor = 0;
  const jsx = (type, props) => ({ type, props });
  const operator = { id: 'operator-1', fullName: 'Staff', email: 'staff@example.com',
    status: 'active', siteIds: ['site-1'], permissions: [] };
  const component = load('roles/owner/operators/OperatorManagement.tsx', {
    react: {
      useState(initial) { const index = cursor++; if (!(index in states)) states[index] = typeof initial === 'function' ? initial() : initial;
        return [states[index], value => { states[index] = typeof value === 'function' ? value(states[index]) : value; }]; },
      useEffect(effect) { effects.push(effect); }, useMemo(calculate) { return calculate(); },
    },
    'react/jsx-runtime': { jsx, jsxs: jsx }, 'react-dom': { createPortal: value => value },
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-1' } }) },
    '../../../lib/parkingApi': { operatorsApi: { list: async () => [operator] },
      parkingApi: { list: async () => [{ id: 'site-1', name: 'Site', status: 'ACTIVE', isActive: true }] } },
    './CreateOperatorForm': { CreateOperatorForm: 'create-form' },
    '../../../components/icon/UntitledIcon': { UntitledIcon: 'icon' },
    '../../../lib/operatorLive': { watchOperators: () => () => {} },
  }).OperatorManagement;
  const render = () => { cursor = 0; return component({ selectedSiteId: 'all', onOperatorsChanged() {} }); };
  const form = () => nodes(render()).find(node => node.type === 'create-form');
  render();
  for (const effect of [...effects]) effect();
  await settle();
  form().props.onCreated({ ...operator, status: 'pending_verification' });
  const roster = form().props.existingOperators;
  assert.equal(roster.length, 1);
  assert.equal(roster[0].status, 'active');
  form().props.onCreated({ ...operator, id: 'operator-2', fullName: 'Another Staff' });
  assert.deepEqual(Array.from(form().props.existingOperators, item => item.id), ['operator-2', 'operator-1']);
});
