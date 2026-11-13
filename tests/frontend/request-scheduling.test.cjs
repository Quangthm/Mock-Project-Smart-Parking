const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const settle = () => new Promise(resolve => setImmediate(resolve));
function events(extra = {}) {
  const handlers = new Map();
  return { ...extra, handlers,
    addEventListener(name, handler) { handlers.set(name, handler); },
    removeEventListener(name) { handlers.delete(name); },
    emit(name) { handlers.get(name)?.(); },
  };
}
function load(file, imports, globals) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib', file), 'utf8')
    .replace('import.meta.env.VITE_RESERVATION_API_BASE_URL', 'undefined');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const context = { exports: {}, ...globals, require(name) {
    if (!(name in imports)) throw new Error(`Unexpected import ${name}`);
    return imports[name];
  } };
  vm.runInNewContext(js, context);
  return context.exports;
}

test('ordinary resource refresh has no polling timer and coalesces resume events', async () => {
  let now = 0, calls = 0, intervals = 0, release;
  const window = events({ setInterval() { intervals++; }, clearInterval() {} });
  const document = events({ visibilityState: 'visible' });
  const { watchServerRefresh } = load('serverRefresh.ts', { react: {}, './authApi': {} }, {
    Date: { now: () => now }, window, document, navigator: { onLine: true },
  });
  const stop = watchServerRefresh(() => { calls++; return new Promise(resolve => { release = resolve; }); });
  assert.equal(intervals, 0);
  now = 6000;
  window.emit('focus'); window.emit('online'); document.emit('visibilitychange');
  assert.equal(calls, 1);
  now = 12000; window.emit('focus');
  assert.equal(calls, 1, 'no overlap while the callback is pending');
  release(); await settle();
  document.visibilityState = 'hidden'; window.emit('focus');
  assert.equal(calls, 1);
  stop();
  assert.equal(window.handlers.size + document.handlers.size, 0);
});

test('explicit existing checkout polling still schedules its requested interval', () => {
  const intervals = [], cleared = [];
  const window = events({ setInterval(callback, delay) { intervals.push(delay); return 7; }, clearInterval(id) { cleared.push(id); } });
  const { watchServerRefresh } = load('serverRefresh.ts', { react: {}, './authApi': {} }, {
    Date, window, document: events({ visibilityState: 'visible' }), navigator: { onLine: true },
  });
  const stop = watchServerRefresh(() => {}, 5000);
  assert.deepEqual(intervals, [5000]); stop(); assert.deepEqual(cleared, [7]);
});

function layoutFixture(options = {}) {
  const handlers = new Map(), timers = new Map();
  let id = 0, builds = 0, close, starts = 0, urlOptions, stopWait;
  let now = Date.now();
  class Clock extends Date { static now() { return now; } }
  const connection = { state: 'Disconnected',
    on(name, fn) { handlers.set(name, fn); }, off(name) { handlers.delete(name); },
    onclose(fn) { close = fn; },
    async start() { starts++; if (options.failure) throw options.failure; this.state = 'Connected'; },
    async stop() { this.state = 'Disconnected'; close?.(); if (stopWait) await stopWait; },
  };
  class HubConnectionBuilder {
    constructor() { builds++; }
    withUrl(url, config) { urlOptions = config; return this; } configureLogging() { return this; } build() { return connection; }
  }
  const window = events(), document = events({ visibilityState: 'visible' });
  const { watchOperatorLayout } = load('operatorLayoutLive.ts', {
    '@microsoft/signalr': { HubConnectionBuilder, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 } },
    './authApi': { authApi: { expiresAt: () => Date.now() + 900000 }, authExpiredEvent: 'expired', authRefreshedEvent: 'refreshed', ensureAccessToken: async () => 'token' },
    './realtimeIdempotency': { operatorRealtimeGuard: { decide: () => ({ apply: true }), commit() {}, reset() {} } },
  }, { window, document, Date: Clock,
    setTimeout(fn, delay) { timers.set(++id, { fn, delay }); return id; },
    clearTimeout(key) { timers.delete(key); },
  });
  return { watchOperatorLayout, handlers, timers, document, window, get builds() { return builds; },
    get starts() { return starts; }, get urlOptions() { return urlOptions; },
    run() { const [key, timer] = timers.entries().next().value; timers.delete(key); now += timer.delay; timer.fn(); },
    deferStop() { let release; stopWait = new Promise(resolve => { release = resolve; }); return release; },
  };
}
const siteId = 'b0000000-0000-4000-8000-000000000001';

test('layout bursts produce one snapshot and retain one trailing read without overlap', async () => {
  let calls = 0, release;
  const reasons = [];
  const f = layoutFixture();
  const stop = f.watchOperatorLayout(siteId, reason => {
    calls++; reasons.push(reason);
    return new Promise(resolve => { release = resolve; });
  });
  await settle();
  for (let i = 0; i < 40; i++) f.handlers.get('SlotStateChanged')({ eventId: String(i), siteId });
  assert.equal(f.timers.size, 1); f.run(); await settle();
  assert.equal(calls, 1); assert.equal(reasons[0], false);
  for (let i = 0; i < 40; i++) f.handlers.get('OccupancyChanged')({ eventId: `next-${i}`, siteId });
  assert.equal(f.timers.size, 0, 'do not start another read before completion');
  release(); await settle(); assert.equal(f.timers.size, 1);
  f.run(); await settle(); assert.equal(calls, 2); assert.equal(reasons[1], true);
  stop(); release(); await settle();
  assert.equal(f.timers.size, 0);
});

test('invalid site ids never connect; hidden layout changes wait until resume; cleanup cancels work', async () => {
  let calls = 0;
  const f = layoutFixture();
  f.watchOperatorLayout('demo-site', () => {} )(); assert.equal(f.builds, 0);
  const stop = f.watchOperatorLayout(siteId, () => { calls++; }); await settle();
  f.document.visibilityState = 'hidden';
  f.handlers.get('SnapshotRequired')(); assert.equal(f.timers.size, 0);
  f.document.visibilityState = 'visible'; f.document.emit('visibilitychange');
  assert.equal(f.timers.size, 1); f.run(); await settle(); assert.equal(calls, 1);
  f.handlers.get('SlotStateChanged')({ eventId: 'last', siteId });
  stop(); assert.equal(f.timers.size, 0);
  assert.equal(f.window.handlers.size + f.document.handlers.size, 0);
});

test('layout uses WebSockets only and transient failure cannot bypass cooldown or retry budget through focus', async () => {
  const f = layoutFixture({ failure: new Error('WebSocket connection failed') });
  const states = [];
  const stop = f.watchOperatorLayout(siteId, () => {}, state => states.push(state));
  await settle();
  assert.equal(f.urlOptions.transport, 1, 'SSE/LongPolling must not be selected');
  assert.equal(f.starts, 1);
  for (let i = 0; i < 30; i++) { f.window.emit('focus'); f.window.emit('refreshed'); f.document.emit('visibilitychange'); }
  await settle(); assert.equal(f.starts, 1);
  f.run(); await settle(); assert.equal(f.starts, 2);
  f.run(); await settle(); assert.equal(f.starts, 3); assert.equal(f.timers.size, 0);
  f.window.emit('focus'); await settle(); assert.equal(f.starts, 3);
  assert.equal(states.at(-1), 'unavailable'); stop();
});

test('nested authorization failures stop automatic reconnect without logging out', async () => {
  const f = layoutFixture({ failure: { message: 'Aggregate transport failure', innerErrors: [{ statusCode: 403 }] } });
  const stop = f.watchOperatorLayout(siteId, () => {}); await settle();
  assert.equal(f.starts, 1); assert.equal(f.timers.size, 0);
  f.window.emit('focus'); f.window.emit('refreshed'); await settle();
  assert.equal(f.starts, 1); stop();
});

test('replacement subscription waits for the previous connection shutdown', async () => {
  const f = layoutFixture();
  const stop = f.watchOperatorLayout(siteId, () => {}); await settle();
  const release = f.deferStop(); stop();
  const stopNext = f.watchOperatorLayout(siteId, () => {}); await settle();
  assert.equal(f.starts, 1);
  release(); await settle(); assert.equal(f.starts, 2); stopNext();
});
