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
  let id = 0, builds = 0, close, starts = 0, stops = 0, urlOptions, stopWait, generation = 0;
  let now = Date.now();
  let currentFailure = options.failure;
  class Clock extends Date { static now() { return now; } }
  const connection = { state: 'Disconnected',
    on(name, fn) { handlers.set(name, fn); }, off(name) { handlers.delete(name); },
    onclose(fn) { close = fn; },
    async start() { starts++; if (currentFailure) throw currentFailure; this.state = 'Connected'; },
    async stop() { stops++; this.state = 'Disconnected'; close?.(); if (stopWait) await stopWait; },
  };
  class HubConnectionBuilder {
    constructor() { builds++; }
    withUrl(url, config) { urlOptions = config; return this; } configureLogging() { return this; } build() { return connection; }
  }
  const navigator = { onLine: options.onLine ?? true };
  const window = events(), document = events({ visibilityState: options.visibilityState ?? 'visible' });
  const guardModule = load('realtimeIdempotency.ts', {}, {});
  const idempotencyImport = options.guard
    ? { operatorRealtimeGuard: options.guard }
    : { RealtimeIdempotencyGuard: guardModule.RealtimeIdempotencyGuard, operatorRealtimeGuard: guardModule.operatorRealtimeGuard };
  const { watchOperatorLayout } = load('operatorLayoutLive.ts', {
    '@microsoft/signalr': { HubConnectionBuilder, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 } },
    './authApi': {
      authApi: { expiresAt: () => now + 900000 },
      authExpiredEvent: 'expired',
      authRefreshedEvent: 'refreshed',
      ensureAccessToken: async () => 'token',
      getSessionGeneration: () => generation,
    },
    './realtimeIdempotency': idempotencyImport,
  }, { window, document, navigator, Date: Clock,
    setTimeout(fn, delay) { timers.set(++id, { fn, delay }); return id; },
    clearTimeout(key) { timers.delete(key); },
  });
  return { watchOperatorLayout, handlers, timers, document, window, navigator,
    get builds() { return builds; },
    get starts() { return starts; },
    get stops() { return stops; },
    get urlOptions() { return urlOptions; },
    setGeneration(value) { generation = value; },
    setFailure(value) { currentFailure = value; },
    setOnline(value) { navigator.onLine = value; },
    close(error) { connection.state = 'Disconnected'; close?.(error); },
    advance(ms) { now += ms; },
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
  f.run(); await settle(); assert.equal(f.starts, 3); assert.equal(f.timers.size, 1, 'schedules a single bounded 60s recovery probe timer');
  f.window.emit('focus'); await settle(); assert.equal(f.starts, 3);
  assert.equal(states.at(-1), 'unavailable'); stop();
  assert.equal(f.timers.size, 0, 'unsubscribe cancels cooldown probe timer');
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

test('business SnapshotRequired envelopes deduplicate by eventId without resetting slot watermarks', async () => {
  const { RealtimeIdempotencyGuard } = load('realtimeIdempotency.ts', {}, {});
  const realGuard = new RealtimeIdempotencyGuard();
  let resets = 0;
  const spiedGuard = {
    decide: (event, slotId) => realGuard.decide(event, slotId),
    commit: (event, slotId) => realGuard.commit(event, slotId),
    reset: () => { resets++; realGuard.reset(); },
  };
  let calls = 0;
  const f = layoutFixture({ guard: spiedGuard });
  const stop = f.watchOperatorLayout(siteId, () => { calls++; });
  await settle();
  assert.equal(resets, 0, 'channel.start must not reset watermarks before connect SnapshotRequired');

  // Connect reconciliation with matching siteId resets watermarks and triggers initial read.
  f.handlers.get('SnapshotRequired')(siteId);
  assert.equal(resets, 1);
  f.run(); await settle();
  assert.equal(calls, 1);

  // Wrong-site SnapshotRequired strings and envelopes are ignored.
  const otherSiteId = 'b0000000-0000-4000-8000-000000000002';
  f.handlers.get('SnapshotRequired')(otherSiteId);
  f.handlers.get('SnapshotRequired')({ eventId: 'snap-other', siteId: otherSiteId });
  assert.equal(f.timers.size, 0);

  // Record a slot event with watermark 5, then deliver 100 duplicate business SnapshotRequired envelopes.
  f.handlers.get('SlotStateChanged')({ eventId: 'slot-ev-5', sequenceNumber: 5, siteId, payload: { slotId: 'slot-1' } });
  f.run(); await settle();
  assert.equal(calls, 2);

  for (let i = 0; i < 100; i++) {
    f.handlers.get('SnapshotRequired')({ eventId: 'snap-biz-1', eventType: 'SnapshotRequired', siteId, payload: {} });
  }
  assert.equal(resets, 1, 'business SnapshotRequired must not wipe deduplication/watermarks');
  assert.equal(f.timers.size, 1);
  f.run(); await settle();
  assert.equal(calls, 3);

  // Stale slot event <= watermark 5 is still suppressed after business SnapshotRequired.
  f.handlers.get('SlotStateChanged')({ eventId: 'slot-ev-stale', sequenceNumber: 4, siteId, payload: { slotId: 'slot-1' } });
  assert.equal(f.timers.size, 0);
  stop();
});

test('shared channel reference-counts subscribers and preserves retry budget across remounts until session changes', async () => {
  const f = layoutFixture({ failure: new Error('WebSocket connection failed') });
  const statesA = [], statesB = [];
  const stopA = f.watchOperatorLayout(siteId, () => {}, state => statesA.push(state));
  const stopB = f.watchOperatorLayout(siteId, () => {}, state => statesB.push(state));
  await settle();
  assert.equal(f.builds, 1);
  assert.equal(f.starts, 1);
  assert.equal(statesB.at(-1), 'unavailable');

  // Unmounting only one subscriber keeps the shared channel alive.
  stopA();
  assert.equal(f.stops, 0);

  // Remounting / clicking manual Retry cannot bypass backoff or reset the 3-attempt budget.
  stopB();
  assert.equal(f.stops, 1);
  const stopRemount1 = f.watchOperatorLayout(siteId, () => {});
  await settle();
  assert.equal(f.starts, 1, 'remount during backoff must not fire an immediate start');

  f.run(); await settle(); assert.equal(f.starts, 2);
  f.run(); await settle(); assert.equal(f.starts, 3);
  assert.equal(f.timers.size, 1);

  // Remounting right after exhausting 3 attempts stays in 60s cooldown with one probe timer.
  stopRemount1();
  const stopRemount2 = f.watchOperatorLayout(siteId, () => {});
  await settle();
  assert.equal(f.starts, 3);
  assert.equal(f.timers.size, 1);

  // After 60s cooldown passes, foreground resume allows at most 1 probe per minute.
  f.advance(60_000);
  f.window.emit('focus');
  await settle();
  assert.equal(f.starts, 4);
  f.window.emit('focus');
  await settle();
  assert.equal(f.starts, 4);

  // New login session generation receives a fresh budget.
  stopRemount2();
  f.setGeneration(1);
  const stopNewSession = f.watchOperatorLayout(siteId, () => {});
  await settle();
  assert.equal(f.starts, 5);
  stopNewSession();
});

test('[P1] short-lived successful handshakes closed by IDENTITY_UNAVAILABLE obey retry budget until accepted and reconciled', async () => {
  const f = layoutFixture();
  const states = [];
  let reads = 0;
  let stop = f.watchOperatorLayout(siteId, () => { reads++; }, state => states.push(state));
  await settle();

  // Attempt 1..3 resolve start() then immediately close before SnapshotRequired acceptance.
  assert.equal(f.starts, 1);
  assert.equal(states.includes('connected'), false, 'start() alone must not mark connected');

  const transientCloseError = new Error('Connection closed with an error. HubException: IDENTITY_UNAVAILABLE');
  f.close(transientCloseError);
  assert.equal(f.timers.size, 1);

  // Attempt 2 starts, resolves, closes immediately; budget must not replenish.
  f.run(); await settle();
  assert.equal(f.starts, 2);
  f.close(transientCloseError);
  assert.equal(f.timers.size, 1);

  // Attempt 3 starts, resolves, closes immediately -> hits 60s cooldown (3 total starts in initial minute).
  f.run(); await settle();
  assert.equal(f.starts, 3);
  f.close(transientCloseError);
  assert.equal(f.timers.size, 1);
  const [[, cooldownTimer]] = f.timers.entries();
  assert.ok(cooldownTimer.delay >= 59_000, '3rd close enters full 60s cooldown window');

  // Remount + manual Retry during cooldown cannot bypass the 3-attempt cap.
  stop();
  stop = f.watchOperatorLayout(siteId, () => { reads++; }, state => states.push(state));
  await settle();
  assert.equal(f.starts, 3);
  assert.equal(states.includes('connected'), false);

  // Simulate repeated cooldown probes (attempts 4..12) still closing immediately before acceptance.
  for (let probe = 4; probe <= 12; probe++) {
    f.run(); await settle();
    assert.equal(f.starts, probe);
    f.close(transientCloseError);
    const [[, probeTimer]] = f.timers.entries();
    assert.ok(probeTimer.delay >= 59_000, 'each short-lived cooldown probe waits a full 60s');
    assert.equal(states.includes('connected'), false);
  }

  // On probe 13, server accepts and sends SnapshotRequired; only after read settles does state become connected.
  f.run(); await settle();
  assert.equal(f.starts, 13);
  assert.equal(states.at(-1), 'connecting');
  f.handlers.get('SnapshotRequired')(siteId);
  assert.equal(states.at(-1), 'connecting', 'SnapshotRequired must wait for snapshot read reconciliation before connected');
  f.run(); await settle(); // 300ms invalidation read
  assert.equal(reads, 1);
  assert.equal(states.at(-1), 'connected');

  // After staying healthy >= 60s, a subsequent transient drop gets a fresh fast retry window.
  f.advance(61_000);
  f.close(new Error('Transient socket reset'));
  const [[, replenishedTimer]] = f.timers.entries();
  assert.ok(replenishedTimer.delay < 5_000, 'healthy >= 60s session replenishes fast backoff budget');
  stop();
});

test('[P2] cooldown probe timer recovers automatically in foreground and pauses while hidden or offline', async () => {
  const f = layoutFixture({ failure: new Error('WebSocket connection failed') });
  const states = [];
  const stop = f.watchOperatorLayout(siteId, () => {}, state => states.push(state));
  await settle();
  assert.equal(f.starts, 1);

  f.run(); await settle(); assert.equal(f.starts, 2);
  f.run(); await settle(); assert.equal(f.starts, 3);
  assert.equal(f.timers.size, 1, 'pending cooldown probe timer exists without user interaction');

  // Going hidden cancels timer and prevents probes.
  f.document.visibilityState = 'hidden';
  f.document.emit('visibilitychange');
  assert.equal(f.timers.size, 0);

  // Returning visible before cooldown completes reschedules the single remaining probe without immediate start.
  f.advance(20_000);
  f.document.visibilityState = 'visible';
  f.document.emit('visibilitychange');
  assert.equal(f.starts, 3);
  assert.equal(f.timers.size, 1);
  const [[, remaining]] = f.timers.entries();
  assert.ok(remaining.delay > 30_000 && remaining.delay <= 45_000);

  // Going offline cancels again; recovering online after cooldown finishes triggers 1 automatic probe.
  f.setOnline(false);
  f.window.emit('offline');
  assert.equal(f.timers.size, 0);
  f.advance(45_000);
  f.setFailure(undefined);
  f.setOnline(true);
  f.window.emit('online');
  await settle();
  assert.equal(f.starts, 4);

  // Or if no events happened, advancing the timer directly triggers automatic probe recovery.
  f.close(new Error('Brief glitch'));
  assert.equal(f.timers.size, 1);
  f.run(); await settle();
  assert.equal(f.starts, 5);
  f.handlers.get('SnapshotRequired')(siteId);
  f.run(); await settle();
  assert.equal(states.at(-1), 'connected');
  stop();
  assert.equal(f.timers.size, 0);
});

test('multi-site channels keep independent replay guards so reconnecting site B does not clear site A watermarks', async () => {
  const siteA = 'b0000000-0000-4000-8000-000000000001';
  const siteB = 'b0000000-0000-4000-8000-000000000002';
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src/lib/operatorLayoutLive.ts'), 'utf8')
    .replace('import.meta.env.VITE_RESERVATION_API_BASE_URL', 'undefined');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const guardModule = load('realtimeIdempotency.ts', {}, {});
  const connections = new Map();
  const timers = new Map();
  let timerId = 0;
  class HubConnectionBuilder {
    withUrl(url) { this.url = url; return this; }
    configureLogging() { return this; }
    build() {
      const siteHandlers = new Map();
      const conn = {
        state: 'Disconnected',
        handlers: siteHandlers,
        on(name, fn) { siteHandlers.set(name, fn); },
        off(name) { siteHandlers.delete(name); },
        onclose() {},
        async start() { this.state = 'Connected'; },
        async stop() { this.state = 'Disconnected'; },
      };
      connections.set(this.url, conn);
      return conn;
    }
  }
  const win = events(), doc = events({ visibilityState: 'visible' });
  const ctx = { exports: {}, window: win, document: doc, navigator: { onLine: true }, Date,
    setTimeout(fn, delay) { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimeout(key) { timers.delete(key); },
    require(name) {
      if (name === '@microsoft/signalr') return { HubConnectionBuilder, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 } };
      if (name === './authApi') return { authApi: { expiresAt: () => Date.now() + 900000 }, authExpiredEvent: 'expired', authRefreshedEvent: 'refreshed', ensureAccessToken: async () => 'token', getSessionGeneration: () => 0 };
      if (name === './realtimeIdempotency') return { RealtimeIdempotencyGuard: guardModule.RealtimeIdempotencyGuard, operatorRealtimeGuard: new guardModule.RealtimeIdempotencyGuard() };
      throw new Error(`Unexpected import ${name}`);
    },
  };
  vm.runInNewContext(js, ctx);
  let callsA = 0, callsB = 0;
  const stopA = ctx.exports.watchOperatorLayout(siteA, () => { callsA++; });
  const stopB = ctx.exports.watchOperatorLayout(siteB, () => { callsB++; });
  await settle();

  const connA = connections.get(`http://localhost:5055/ws/parking-lots/${siteA}/layout`);
  const connB = connections.get(`http://localhost:5055/ws/parking-lots/${siteB}/layout`);

  connA.handlers.get('SlotStateChanged')({ eventId: 'a-10', sequenceNumber: 10, siteId: siteA, payload: { slotId: 'slot-1' } });
  const flushTimers = async () => { for (const [k, t] of [...timers.entries()]) { timers.delete(k); t.fn(); } await settle(); };
  await flushTimers();
  assert.equal(callsA, 1);

  // Reconnect reconciliation on site B resets site B only.
  connB.handlers.get('SnapshotRequired')(siteB);
  await flushTimers();
  assert.equal(callsB, 1);

  // Site A must still suppress stale event sequence 9 <= 10.
  connA.handlers.get('SlotStateChanged')({ eventId: 'a-9-stale', sequenceNumber: 9, siteId: siteA, payload: { slotId: 'slot-1' } });
  assert.equal(timers.size, 0);
  assert.equal(callsA, 1);

  stopA(); stopB();
});

test('[P1] acceptance arriving during connection.start() reconciles to connected even before or after start() resolution, fenced across attempts', async () => {
  const handlers = new Map(), timers = new Map();
  let timerId = 0, closeCb, now = 1_000_000;
  let emitInStart = false;
  let startResolvesAsync = false;
  let releaseSecondStart;
  let startCount = 0;
  class Clock extends Date { static now() { return now; } }
  const connection = {
    state: 'Disconnected',
    on(name, fn) { handlers.set(name, fn); },
    off(name) { handlers.delete(name); },
    onclose(fn) { closeCb = fn; },
    async start() {
      startCount++;
      this.state = 'Connected';
      if (emitInStart) {
        // Simulates SignalR HubConnection._processIncomingData dispatching SnapshotRequired
        // from the handshake chunk before the awaited start() promise resolves.
        handlers.get('SnapshotRequired')(siteId);
      }
      if (startResolvesAsync) {
        await new Promise(resolve => { releaseSecondStart = resolve; });
      }
    },
    async stop() { this.state = 'Disconnected'; closeCb?.(); },
  };
  class HubConnectionBuilder {
    withUrl() { return this; }
    configureLogging() { return this; }
    build() { return connection; }
  }
  const win = events(), doc = events({ visibilityState: 'visible' });
  const guardModule = load('realtimeIdempotency.ts', {}, {});
  const { watchOperatorLayout, getOperatorLayoutSyncState } = load('operatorLayoutLive.ts', {
    '@microsoft/signalr': { HubConnectionBuilder, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 } },
    './authApi': {
      authApi: { expiresAt: () => now + 900_000 },
      authExpiredEvent: 'expired',
      authRefreshedEvent: 'refreshed',
      ensureAccessToken: async () => 'token',
      getSessionGeneration: () => 0,
    },
    './realtimeIdempotency': { RealtimeIdempotencyGuard: guardModule.RealtimeIdempotencyGuard, operatorRealtimeGuard: guardModule.operatorRealtimeGuard },
  }, {
    window: win, document: doc, navigator: { onLine: true }, Date: Clock,
    setTimeout(fn, delay) { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimeout(key) { timers.delete(key); },
  });

  emitInStart = true;
  const states = [];
  let calls = 0;
  const stop = watchOperatorLayout(siteId, () => { calls++; return 'synced'; }, s => states.push(s));
  await settle();
  assert.equal(startCount, 1);
  assert.equal(timers.size, 1, 'initial SnapshotRequired emitted during start() scheduled 300ms reconciliation');
  const [[k1, t1]] = timers.entries();
  timers.delete(k1); now += t1.delay; t1.fn();
  await settle();
  assert.equal(calls, 1);
  assert.deepEqual(states, ['connecting', 'connected']);
  const syncDiag = getOperatorLayoutSyncState(siteId);
  assert.equal(syncDiag.lastSyncOutcome, 'synced');
  assert.ok(syncDiag.lastSyncedAt > 0);
  assert.ok(syncDiag.acceptedAt > 0);

  // Attempt fence test: start attempt 1 snapshot read, then drop socket and start attempt 2;
  // resolving old attempt 1 read after attempt 2 started must NEVER mark attempt 2 connected.
  emitInStart = false;
  let releaseOldRead;
  let resolveOld = true;
  const states2 = [];
  stop();
  const stop2 = watchOperatorLayout(siteId, () => {
    if (resolveOld) { resolveOld = false; return new Promise(r => { releaseOldRead = r; }); }
    return 'synced';
  }, s => states2.push(s));
  await settle();
  handlers.get('SnapshotRequired')(siteId);
  const [[k2, t2]] = timers.entries();
  timers.delete(k2); now += t2.delay; t2.fn(); // old read now in flight

  connection.state = 'Disconnected';
  closeCb(new Error('Transient close before old read completed'));
  await settle();
  const [[kRetry, tRetry]] = timers.entries();
  timers.delete(kRetry); now += tRetry.delay;
  startResolvesAsync = true;
  tRetry.fn(); // attempt 2 now connecting (not accepted yet)
  await settle();
  releaseOldRead(); // stale read from attempt 1 finishes while attempt 2 is connecting
  await settle();
  assert.equal(states2.at(-1), 'connecting', 'stale read from fenced attempt 1 must not mark attempt 2 connected');
  releaseSecondStart();
  await settle();
  stop2();
});

test('[P1] real dashboard reconciliation helper handles HTTP 503 error and stale scope without marking connected, then recovers on 200', async () => {
  const handlers = new Map(), timers = new Map();
  let timerId = 0, now = 2_000_000;
  class Clock extends Date { static now() { return now; } }
  const connection = {
    state: 'Disconnected',
    on(name, fn) { handlers.set(name, fn); },
    off(name) { handlers.delete(name); },
    onclose() {},
    async start() { this.state = 'Connected'; },
    async stop() { this.state = 'Disconnected'; },
  };
  class HubConnectionBuilder {
    withUrl() { return this; } configureLogging() { return this; } build() { return connection; }
  }
  const win = events(), doc = events({ visibilityState: 'visible' });
  const guardModule = load('realtimeIdempotency.ts', {}, {});
  const { watchOperatorLayout, reconcileOperatorSiteSnapshot, getOperatorLayoutSyncState } = load('operatorLayoutLive.ts', {
    '@microsoft/signalr': { HubConnectionBuilder, HttpTransportType: { WebSockets: 1 }, LogLevel: { None: 6 } },
    './authApi': {
      authApi: { expiresAt: () => now + 900_000 },
      authExpiredEvent: 'expired',
      authRefreshedEvent: 'refreshed',
      ensureAccessToken: async () => 'token',
      getSessionGeneration: () => 0,
    },
    './realtimeIdempotency': { RealtimeIdempotencyGuard: guardModule.RealtimeIdempotencyGuard, operatorRealtimeGuard: guardModule.operatorRealtimeGuard },
  }, {
    window: win, document: doc, navigator: { onLine: true }, Date: Clock,
    setTimeout(fn, delay) { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimeout(key) { timers.delete(key); },
  });

  const states = [];
  const uiErrors = [];
  let currentSite = { id: siteId, totalSlots: 10 };
  let active = true;
  let httpMode = '503';
  let appliedCount = 0;
  let bookingsInvalidated = 0;

  const dashboardSubscriber = (refreshBookings, details) =>
    reconcileOperatorSiteSnapshot({
      expectedSiteId: siteId,
      isActive: () => active,
      getCurrentSite: () => currentSite,
      refreshSite: async site => {
        if (httpMode === '503') throw new Error('HTTP 503 Service Unavailable');
        return { ...site, totalSlots: 48 };
      },
      applySite: updated => { currentSite = updated; appliedCount++; },
      onError: err => { uiErrors.push(err instanceof Error ? err.message : 'err'); },
      onClearError: () => { uiErrors.length = 0; },
      onBookingsInvalidated: () => { bookingsInvalidated++; },
      refreshBookings,
      details,
    });

  const stop = watchOperatorLayout(siteId, dashboardSubscriber, s => states.push(s));
  await settle();
  handlers.get('SnapshotRequired')(siteId);
  const [[k1, t1]] = timers.entries();
  timers.delete(k1); now += t1.delay; t1.fn();
  await settle();

  // Swallowed 503 in caller still stores UI error once and MUST NOT report connected/synced.
  assert.deepEqual(states, ['connecting']);
  assert.equal(uiErrors.length, 1);
  assert.equal(appliedCount, 0);
  let diag = getOperatorLayoutSyncState(siteId);
  assert.equal(diag.lastSyncOutcome, 'error');
  assert.equal(diag.lastSyncedAt, 0);
  assert.equal(diag.acceptedAt, 0);
  assert.ok(diag.transportConnectedAt > 0, 'transport connection tracked separately from sync timestamp');

  // Stale scope switch must return 'stale' without mutating or marking connected.
  currentSite = { id: 'b0000000-0000-4000-8000-000000000002', totalSlots: 5 };
  httpMode = '200';
  handlers.get('SnapshotRequired')({ eventId: 'evt-stale-check', siteId, payload: {} });
  const [[k2, t2]] = timers.entries();
  timers.delete(k2); now += t2.delay; t2.fn();
  await settle();
  assert.deepEqual(states, ['connecting']);
  assert.equal(getOperatorLayoutSyncState(siteId).lastSyncOutcome, 'stale');

  // Restoring active matching site and receiving valid snapshot reconciles and becomes connected.
  currentSite = { id: siteId, totalSlots: 10 };
  handlers.get('SnapshotRequired')({ eventId: 'evt-recover-200', siteId, payload: { affectedResources: ['full-layout', 'capacity-summary'] } });
  const [[k3, t3]] = timers.entries();
  timers.delete(k3); now += t3.delay; t3.fn();
  await settle();
  assert.deepEqual(states, ['connecting', 'connected']);
  assert.equal(appliedCount, 1);
  assert.equal(currentSite.totalSlots, 48);
  assert.equal(uiErrors.length, 0);
  diag = getOperatorLayoutSyncState(siteId);
  assert.equal(diag.lastSyncOutcome, 'synced');
  assert.ok(diag.lastSyncedAt >= diag.transportConnectedAt);

  // Operations-only bookings invalidation refreshes bookings without invoking layout HTTP snapshot.
  handlers.get('SnapshotRequired')({ eventId: 'evt-bookings-only', siteId, payload: { affectedResources: ['bookings'] } });
  const [[k4, t4]] = timers.entries();
  timers.delete(k4); now += t4.delay; t4.fn();
  await settle();
  assert.equal(appliedCount, 1, 'bookings-only invalidation skips full-layout GET');
  assert.equal(bookingsInvalidated, 2);

  stop();
});

test('shared RealtimeResourceCoordinator enforces budgets, fairness, selected-detail cascade, and Owner/Driver contracts', async () => {
  const timers = new Map();
  let timerId = 0, now = 500_000;
  const {
    RealtimeResourceCoordinator,
    mergeLiveStructureProjection,
    selectOwnerSubscriptionSites,
    selectDriverSubscriptionSites,
    createDriverViewportDebouncer,
  } = load('realtimeResourceCoordinator.ts', {}, {
    setTimeout(fn, delay) { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimeout(key) { timers.delete(key); },
  });

  const coordinator = new RealtimeResourceCoordinator({
    now: () => now,
    setTimer: (fn, delay) => { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimer: key => { timers.delete(key); },
  });

  // 1. Sliding window (max 4 starts/s), max 4 concurrent, and selected-detail cascade from capacity-summary.
  const executed = [];
  const resolvers = [];
  const driverSites = Array.from({ length: 6 }, (_, i) => `b0000000-0000-4000-8000-00000000010${i}`);
  coordinator.setSelectedSiteId(driverSites[0]);
  const unsubs = [];

  for (const s of driverSites) {
    unsubs.push(coordinator.registerResource(
      { sessionGeneration: 1, role: 'driver', userId: 'drv-1', siteId: s, resourceClass: 'capacity-summary' },
      () => new Promise(resolve => { executed.push(`summary:${s.slice(-2)}`); resolvers.push(resolve); }),
    ));
  }
  const detailScope = { sessionGeneration: 1, role: 'driver', userId: 'drv-1', siteId: driverSites[0], resourceClass: 'sanitized-detail' };
  unsubs.push(coordinator.registerResource(
    detailScope,
    () => new Promise(resolve => { executed.push(`detail:${driverSites[0].slice(-2)}`); resolvers.push(resolve); }),
  ));

  // Burst 6 summary invalidations + 1 selected-site cascade -> at most 4 start immediately.
  for (const s of driverSites) {
    coordinator.invalidateResource({ sessionGeneration: 1, role: 'driver', userId: 'drv-1', siteId: s, resourceClass: 'capacity-summary' });
  }
  await settle();
  assert.equal(executed.length, 4, 'capped at 4 concurrent / 4 starts per second');
  assert.ok(executed.includes(`detail:${driverSites[0].slice(-2)}`), 'selected-site capacity-summary cascade triggered sanitized-detail');

  // Complete first 4 reads within same second -> sliding window holds remaining reads until 1000ms elapses.
  while (resolvers.length) resolvers.shift()({ outcome: 'synced', data: { ok: true } });
  await settle();
  assert.equal(executed.length, 4, 'sliding 1s window prevents 5th start before 1000ms elapsed');
  now += 1000;
  for (const [k, t] of [...timers.entries()]) { timers.delete(k); t.fn(); }
  await settle();
  while (resolvers.length) resolvers.shift()({ outcome: 'synced', data: { ok: true } });
  await settle();
  assert.equal(executed.length, 7);
  assert.equal(coordinator.getMetrics().totalReadsStarted, 7);

  // Full-layout / sanitized-detail 2000ms invalidation pacing holds immediate repeat invalidation.
  coordinator.invalidateResource(detailScope, { reason: 'invalidation' });
  await settle();
  assert.equal(executed.length, 7, '2s pacing holds repeat detail invalidation');
  now += 2000;
  for (const [k, t] of [...timers.entries()]) { timers.delete(k); t.fn(); }
  await settle();
  while (resolvers.length) resolvers.shift()({ outcome: 'synced', data: { ok: true } });
  await settle();
  assert.equal(executed.length, 8);
  assert.equal(coordinator.getMetrics().totalReadsStarted, 8);

  // 2. Owner 20-site union cap and unsaved form + 3D modelUrl merge preservation.
  const ownerCandidates = Array.from({ length: 25 }, (_, i) => `owner-site-${i}`);
  const ownerSelection = selectOwnerSubscriptionSites(ownerCandidates, 'owner-site-24', 20);
  assert.equal(ownerSelection.length, 20);
  assert.equal(ownerSelection[0], 'owner-site-24', 'selected detail site stays included within 20-site cap');

  const unsavedDraft = { tariffName: 'Edited Unsaved Draft', backupCount: 4 };
  const merged = mergeLiveStructureProjection(
    { formDraft: unsavedDraft, modelUrl: 'blob://kept-3d-asset', slots: [{ id: 's1', code: 'A01', status: 'available' }] },
    { slots: [{ id: 's1', code: 'A01', status: 'occupied' }], syncedAt: 9999 },
  );
  assert.equal(merged.formDraft, unsavedDraft, 'unsaved form draft object preserved untouched');
  assert.equal(merged.modelUrl, 'blob://kept-3d-asset', '3D model asset reference preserved untouched');
  assert.equal(merged.slots[0].status, 'occupied');

  // 3. Driver 10+1 cap & 400ms viewport debounce.
  const drvSelected = selectDriverSubscriptionSites(
    Array.from({ length: 14 }, (_, i) => `drv-site-${i}`),
    'selected-extra',
    10,
  );
  assert.equal(drvSelected.visibleSiteIds.length, 10);
  assert.equal(drvSelected.selectedSiteId, 'selected-extra');

  let viewportCommits = 0;
  const debouncer = createDriverViewportDebouncer(() => { viewportCommits++; }, 400, {
    setTimer: (fn, delay) => { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimer: key => { timers.delete(key); },
  });
  debouncer.schedule(['a', 'b'], 'a');
  debouncer.schedule(['c', 'd'], 'c');
  assert.equal(viewportCommits, 0);
  for (const [k, t] of [...timers.entries()]) { assert.equal(t.delay, 400); timers.delete(k); t.fn(); }
  assert.equal(viewportCommits, 1);

  for (const u of unsubs) u();
  coordinator.dispose();
});

test('[P1] coordinator shares one authoritative fetcher across 5 subscribers per key and enforces exact accounting across 4+ multi-subscriber resources', async () => {
  const timers = new Map();
  let timerId = 0, now = 900_000;
  const { RealtimeResourceCoordinator } = load('realtimeResourceCoordinator.ts', {}, {
    setTimeout(fn, delay) { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimeout(key) { timers.delete(key); },
  });

  const coordinator = new RealtimeResourceCoordinator({
    now: () => now,
    setTimer: (fn, delay) => { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimer: key => { timers.delete(key); },
  });

  // Scenario A: 5 subscribers register on the SAME full-layout key; invalidating once runs ONE fetcher only.
  const layoutScope = { sessionGeneration: 1, role: 'operator', userId: 'op-1', siteId: 'site-1', resourceClass: 'full-layout' };
  let actualFetches = 0;
  let releaseLayout;
  const authoritativeFetcher = () => {
    actualFetches++;
    return new Promise(resolve => { releaseLayout = resolve; });
  };
  const observerSnapshots = [[], [], [], [], []];
  const layoutUnsubs = observerSnapshots.map(log =>
    coordinator.registerResource(layoutScope, authoritativeFetcher, snap => log.push(snap)),
  );

  coordinator.invalidateResource(layoutScope, { reason: 'initial' });
  await settle();
  assert.equal(actualFetches, 1, '5 subscribers of same key must execute 1 authoritative fetcher');
  assert.equal(coordinator.getMetrics().activeConcurrentReads, 1);
  assert.equal(coordinator.getMetrics().totalReadsStarted, 1);

  // One subscriber unmounts mid-flight + another invalidation arrives (trailing dirty); remaining 4 still receive shared result.
  layoutUnsubs[0]();
  coordinator.invalidateResource(layoutScope, { reason: 'invalidation' });
  releaseLayout({ slots: 48, rev: 1 });
  await settle();

  assert.equal(actualFetches, 1);
  for (let i = 1; i < 5; i++) {
    const last = observerSnapshots[i].at(-1);
    assert.equal(last.lastOutcome, 'synced');
    assert.deepEqual(last.data, { slots: 48, rev: 1 });
  }
  assert.equal(observerSnapshots[0].at(-1).inFlight, true, 'unmounted subscriber does not receive post-unmount updates');

  // 6th late observer joining an already-synced resource receives cached snapshot immediately with 0 extra fetches.
  const lateSnapshots = [];
  const unsubLate = coordinator.registerResource(layoutScope, authoritativeFetcher, snap => lateSnapshots.push(snap));
  assert.equal(actualFetches, 1);
  assert.deepEqual(lateSnapshots[0].data, { slots: 48, rev: 1 });

  // Trailing dirty invalidation obeys 2000ms full-layout pacing, then runs 1 follow-up fetch shared across active observers.
  assert.equal(actualFetches, 1);
  now += 2000;
  for (const [k, t] of [...timers.entries()]) { timers.delete(k); t.fn(); }
  await settle();
  assert.equal(actualFetches, 2);
  releaseLayout({ slots: 47, rev: 2 });
  await settle();
  assert.deepEqual(lateSnapshots.at(-1).data, { slots: 47, rev: 2 });
  assert.equal(coordinator.getMetrics().totalReadsStarted, 2);

  unsubLate();
  for (const u of layoutUnsubs) u();

  // Scenario B: 4 distinct resources with multiple subscribers each + 1 extra -> exact fetch counts match metrics, scope switch ignores stale reads.
  coordinator.dispose();
  const c2 = new RealtimeResourceCoordinator({
    now: () => now,
    setTimer: (fn, delay) => { timers.set(++timerId, { fn, delay }); return timerId; },
    clearTimer: key => { timers.delete(key); },
  });
  now += 5000;

  let multiFetches = 0;
  const pendingResolvers = [];
  const multiScopes = [1, 2, 3, 4, 5].map(i => ({
    sessionGeneration: 2, role: 'owner', userId: 'own-1', siteId: `site-${i}`, resourceClass: 'capacity-summary',
  }));
  const receivedBySite = new Map(multiScopes.map(s => [s.siteId, []]));
  const unsubs2 = [];

  for (const scope of multiScopes) {
    for (let copy = 0; copy < 3; copy++) {
      unsubs2.push(c2.registerResource(
        scope,
        () => {
          multiFetches++;
          return new Promise(resolve => { pendingResolvers.push({ siteId: scope.siteId, resolve }); });
        },
        snap => { if (snap.lastOutcome === 'synced') receivedBySite.get(scope.siteId).push(snap.data); },
      ));
    }
  }

  for (const scope of multiScopes) c2.invalidateResource(scope);
  await settle();
  assert.equal(multiFetches, 4, 'max 4 starts across 5 multi-subscriber resources');
  assert.equal(c2.getMetrics().activeConcurrentReads, 4);
  assert.equal(c2.getMetrics().totalReadsStarted, 4);

  // Scope switch while reads are in flight drops stale completions and purges generation 2 keys.
  c2.setSessionGeneration(3);
  while (pendingResolvers.length) {
    const item = pendingResolvers.shift();
    item.resolve({ site: item.siteId, count: 10 });
  }
  await settle();
  for (const scope of multiScopes) {
    assert.equal(receivedBySite.get(scope.siteId).length, 0, 'stale generation completions must not be applied');
  }
  assert.equal(c2.getMetrics().trackedResources, 0);

  for (const u of unsubs2) u();
  c2.dispose();
});

function createSlice4Harness() {
  let now = 1_000_000;
  let expiresAt = now + 600_000;
  let sessionGeneration = 1;
  let timerId = 0;
  const timers = new Map();

  const FakeDate = class extends Date {
    constructor(...args) {
      if (args.length === 0) super(now);
      else super(...args);
    }
    static now() { return now; }
  };

  function setTimer(fn, delay) {
    const id = ++timerId;
    timers.set(id, { fn, delay: delay ?? 0, due: now + Math.max(0, delay ?? 0) });
    return id;
  }
  function clearTimer(id) { timers.delete(id); }

  function multiEvents(extra = {}) {
    const handlers = new Map();
    return {
      ...extra,
      handlers,
      addEventListener(name, handler) {
        if (!handlers.has(name)) handlers.set(name, new Set());
        handlers.get(name).add(handler);
      },
      removeEventListener(name, handler) {
        handlers.get(name)?.delete(handler);
      },
      dispatchEvent(ev) {
        const name = typeof ev === 'string' ? ev : ev.type;
        for (const fn of handlers.get(name) ?? []) fn(ev);
      },
      emit(name, ev) {
        for (const fn of handlers.get(name) ?? []) fn(ev);
      },
    };
  }

  const win = multiEvents({ setTimeout: setTimer, clearTimeout: clearTimer });
  const doc = multiEvents({ visibilityState: 'visible' });

  const hubHandlers = new Map();
  let onCloseCallback = null;
  let connectCount = 0;
  const connection = {
    state: 'Disconnected',
    on(name, fn) { hubHandlers.set(name, fn); },
    off(name) { hubHandlers.delete(name); },
    onclose(fn) { onCloseCallback = fn; },
    async start() {
      connectCount++;
      connection.state = 'Connected';
    },
    async stop() {
      connection.state = 'Disconnected';
    },
  };

  const storedModels = new Map();
  let layoutCalls = 0;
  let bookingsCalls = 0;
  let sessionCalls = 0;
  let ticketCalls = 0;
  let notificationCalls = 0;
  let assignmentsImpl = async () => [{
    siteId: '11111111-1111-1111-1111-111111111111',
    tenantId: 'tenant-1',
    permissions: ['SLOT_OVERRIDE'],
  }];
  let fetchLayoutImpl = async ({ siteId }) => {
    layoutCalls++;
    return {
      id: siteId,
      tenantId: 'tenant-1',
      code: 'LOT-A',
      name: 'Lot Alpha',
      address: '1 Main St',
      latitude: null,
      longitude: null,
      isActive: true,
      totalSlots: 10,
      floors: 1,
      configuredBackupCount: 1,
      slots: [],
      units: [],
      freshness: { generatedAt: new Date(now).toISOString(), isFresh: true, stalenessThresholdSeconds: 60 },
      operationalLayout: { siteId, generatedAt: new Date(now).toISOString(), isFresh: true, stalenessThresholdSeconds: 60, slots: [] },
    };
  };
  let fetchBookingsImpl = async () => { bookingsCalls++; return []; };
  let fetchSessionsImpl = async () => { sessionCalls++; return []; };
  let fetchLookupImpl = async (_siteId, ref) => ({ id: ref, lotId: '11111111-1111-1111-1111-111111111111', status: 'checked-in' });

  const globals = {
    Date: FakeDate,
    setTimeout: setTimer,
    clearTimeout: clearTimer,
    window: win,
    document: doc,
    navigator: { onLine: true },
    Event: class { constructor(type) { this.type = type; } },
  };

  const idempotency = load('realtimeIdempotency.ts', {}, globals);
  const coordModule = load('realtimeResourceCoordinator.ts', {}, globals);
  const liveModule = load('operatorLayoutLive.ts', {
    '@microsoft/signalr': {
      HubConnectionBuilder: class {
        withUrl() { return this; }
        configureLogging() { return this; }
        build() { return connection; }
      },
      HttpTransportType: { WebSockets: 1 },
      LogLevel: { None: 6 },
    },
    './authApi': {
      authApi: { expiresAt: () => expiresAt },
      authExpiredEvent: 'auth-expired',
      authRefreshedEvent: 'auth-refreshed',
      ensureAccessToken: async () => 'token-1',
      getSessionGeneration: () => sessionGeneration,
    },
    './realtimeIdempotency': idempotency,
  }, globals);

  const opsResources = load('operatorRealtimeResources.ts', {
    './authApi': { authExpiredEvent: 'auth-expired', getSessionGeneration: () => sessionGeneration },
    './3d/ownerModelStorage': { loadOwnerModelUrl: async (siteId) => storedModels.get(siteId) ?? null },
    './operatorApi': {
      loadOperatorSites: async () => [],
      readAssignedOperatorSite: async (args) => fetchLayoutImpl(args),
      reservationApi: {
        list: async (siteId) => fetchBookingsImpl(siteId),
        listParkingSessions: async (siteId) => fetchSessionsImpl(siteId),
        lookup: async (siteId, ref) => fetchLookupImpl(siteId, ref),
      },
    },
    './parkingApi': { operatorsApi: { assignments: async () => assignmentsImpl() } },
    './realtimeResourceCoordinator': coordModule,
  }, globals);

  async function advanceMs(ms) {
    const target = now + ms;
    while (true) {
      let nextKey = null;
      let nextTimer = null;
      for (const [key, t] of timers.entries()) {
        if (t.due <= target && (!nextTimer || t.due < nextTimer.due)) {
          nextKey = key;
          nextTimer = t;
        }
      }
      if (!nextTimer) break;
      timers.delete(nextKey);
      if (nextTimer.due > now) now = nextTimer.due;
      nextTimer.fn();
      await settle();
    }
    now = target;
    await settle();
  }

  return {
    get now() { return now; },
    set now(v) { now = v; },
    get sessionGeneration() { return sessionGeneration; },
    set sessionGeneration(v) { sessionGeneration = v; },
    get layoutCalls() { return layoutCalls; },
    get bookingsCalls() { return bookingsCalls; },
    get sessionCalls() { return sessionCalls; },
    get ticketCalls() { return ticketCalls; },
    get notificationCalls() { return notificationCalls; },
    get connectCount() { return connectCount; },
    setFetchLayout(fn) { fetchLayoutImpl = fn; },
    setFetchBookings(fn) { fetchBookingsImpl = fn; },
    setFetchSessions(fn) { fetchSessionsImpl = fn; },
    storedModels,
    win,
    connection,
    hubHandlers,
    emitHub(event, payload) { hubHandlers.get(event)?.(payload); },
    dropHub() { connection.state = 'Disconnected'; onCloseCallback?.(new Error('drop')); },
    advanceMs,
    coordModule,
    liveModule,
    opsResources,
  };
}

test('Slice 4: single-fetcher deduplication across 3 operator observers + connect reconcile + unmount resilience', async () => {
  const h = createSlice4Harness();
  const siteId = '11111111-1111-1111-1111-111111111111';
  const user = { id: 'op-1', role: 'operator' };

  const dashObs = [];
  const lotStatusObs = [];
  const backupObs = [];

  const sites = await h.opsResources.loadAssignedOperatorSitesCoordinated(user);
  assert.equal(sites.length, 1);
  assert.equal(h.layoutCalls, 1);

  const unsubDash = h.opsResources.subscribeOperatorLayout(siteId, s => dashObs.push(s));
  const unsubLot = h.opsResources.subscribeOperatorLayout(siteId, s => lotStatusObs.push(s));
  const unsubBackup = h.opsResources.subscribeOperatorLayout(siteId, s => backupObs.push(s));
  const backupReconcile = await h.opsResources.reconcileOperatorLayout(siteId, 'initial');
  assert.equal(backupReconcile.lastOutcome, 'synced');
  assert.equal(h.layoutCalls, 1, '3 observers + backup initial mount must share 1 fetch within initial window');

  let currentSite = sites[0];
  const stopWatch = h.liveModule.watchOperatorLayout(
    siteId,
    (refreshBookings, details) => h.liveModule.reconcileOperatorSiteSnapshot({
      expectedSiteId: siteId,
      isActive: () => true,
      getCurrentSite: () => currentSite,
      refreshSite: async (site) => {
        const snap = await h.opsResources.reconcileOperatorLayout(site.id, details?.refreshReason ?? 'invalidation');
        if (snap.lastOutcome === 'error') throw snap.lastError;
        return snap.lastOutcome === 'stale' || !snap.data ? null : snap.data;
      },
      applySite: (updated) => { currentSite = updated; },
      onError: () => {},
    }),
  );
  await settle();
  h.emitHub('SnapshotRequired', siteId);
  await h.advanceMs(350);

  assert.equal(h.layoutCalls, 1, 'initial WS SnapshotRequired reuses fresh <2000ms coordinated fetch');
  assert.equal(h.opsResources.getOperatorRealtimeMetrics().totalReadsStarted, 1);

  // Unmount one observer and send an invalidation after >2000ms -> remaining 2 stay active.
  unsubBackup();
  await h.advanceMs(2200);
  h.emitHub('SnapshotRequired', {
    eventId: 'evt-layout-2',
    siteId,
    payload: { affectedResources: ['full-layout'] },
  });
  await h.advanceMs(350);

  assert.equal(h.layoutCalls, 2);
  assert.equal(h.opsResources.getOperatorRealtimeMetrics().totalReadsStarted, 2);
  assert.equal(dashObs.at(-1).lastOutcome, 'synced');
  assert.equal(lotStatusObs.at(-1).lastOutcome, 'synced');

  stopWatch();
  unsubDash();
  unsubLot();
  h.opsResources.clearOperatorRealtimeResources();
});

test('Slice 4: StrictMode double-mount dedup and modelUrl identity preservation across invalidations', async () => {
  const h = createSlice4Harness();
  const siteId = '11111111-1111-1111-1111-111111111111';
  const user = { id: 'op-strict', role: 'operator' };
  h.storedModels.set(siteId, 'blob:site-model-v1');

  const [first, second] = await Promise.all([
    h.opsResources.loadAssignedOperatorSitesCoordinated(user),
    h.opsResources.loadAssignedOperatorSitesCoordinated(user),
  ]);
  assert.equal(h.layoutCalls, 1, 'StrictMode double mount shares 1 initial fetch');
  assert.equal(first[0].modelUrl, 'blob:site-model-v1');
  assert.equal(second[0].modelUrl, 'blob:site-model-v1');

  await h.advanceMs(2100);
  const refreshed = await h.opsResources.reconcileOperatorLayout(siteId, 'invalidation');
  assert.equal(h.layoutCalls, 2);
  assert.equal(refreshed.data.modelUrl, 'blob:site-model-v1', 'modelUrl identity preserved after backend reload without modelUrl');

  h.opsResources.clearOperatorRealtimeResources();
});

test('Slice 4: resource isolation (bookings-only never fetches layout/tickets/notifications and never clears layout error)', async () => {
  const h = createSlice4Harness();
  const siteId = '11111111-1111-1111-1111-111111111111';
  const user = { id: 'op-iso', role: 'operator' };

  const [site] = await h.opsResources.loadAssignedOperatorSitesCoordinated(user);
  let currentSite = site;
  const diagLog = [];
  const bookingsObs = [];
  const sessionsObs = [];

  const unsubBookings = h.opsResources.subscribeOperatorBookings(siteId, s => bookingsObs.push(s));
  const unsubSessions = h.opsResources.subscribeOperatorParkingSessions(siteId, s => sessionsObs.push(s));

  const stopWatch = h.liveModule.watchOperatorLayout(
    siteId,
    (refreshBookings, details) => h.liveModule.reconcileOperatorSiteSnapshot({
      expectedSiteId: siteId,
      isActive: () => true,
      getCurrentSite: () => currentSite,
      refreshSite: async (s) => {
        const snap = await h.opsResources.reconcileOperatorLayout(s.id, details?.refreshReason ?? 'invalidation');
        if (snap.lastOutcome === 'error') throw snap.lastError;
        return snap.lastOutcome === 'stale' || !snap.data ? null : snap.data;
      },
      applySite: (updated) => { currentSite = updated; },
      onError: () => {},
      onBookingsInvalidated: () => {
        h.opsResources.invalidateOperatorBookings(siteId, 'invalidation');
        h.opsResources.invalidateOperatorParkingSessions(siteId, 'invalidation');
      },
      refreshBookings,
      details,
    }),
    undefined,
    diag => diagLog.push(diag),
  );
  await settle();
  h.emitHub('SnapshotRequired', siteId);
  await h.advanceMs(350);
  assert.equal(h.layoutCalls, 1);

  // Cause a layout 503 failure while WS stays connected.
  h.setFetchLayout(async () => { layoutFailureCount++; throw new Error('HTTP 503'); });
  let layoutFailureCount = 0;
  await h.advanceMs(2100);
  h.emitHub('SnapshotRequired', {
    eventId: 'evt-layout-fail',
    siteId,
    payload: { affectedResources: ['full-layout'] },
  });
  await h.advanceMs(350);
  assert.equal(diagLog.at(-1).lastSyncOutcome, 'error');

  // Now send a bookings-only envelope -> only bookings + parking-sessions refresh, 0 layout/tickets/notifications calls, error state preserved!
  const callsBeforeBookings = h.layoutCalls;
  const bookingsBefore = h.bookingsCalls;
  const sessionsBefore = h.sessionCalls;
  h.emitHub('SnapshotRequired', {
    eventId: 'evt-bookings-only',
    siteId,
    payload: { affectedResources: ['bookings'] },
  });
  await h.advanceMs(350);

  assert.equal(layoutFailureCount, 1);
  assert.equal(h.layoutCalls, callsBeforeBookings, 'bookings-only notification must not trigger layout fetch');
  assert.equal(h.bookingsCalls - bookingsBefore, 1);
  assert.equal(h.sessionCalls - sessionsBefore, 1);
  assert.equal(h.ticketCalls, 0);
  assert.equal(h.notificationCalls, 0);
  assert.equal(bookingsObs.at(-1).lastOutcome, 'synced');
  assert.equal(sessionsObs.at(-1).lastOutcome, 'synced');
  assert.equal(diagLog.at(-1).lastSyncOutcome, 'error', 'bookings-only flush must not wipe prior layout error');

  stopWatch();
  unsubBookings();
  unsubSessions();
  h.opsResources.clearOperatorRealtimeResources();
});

test('Slice 4: connected-but-sync-error surfaces error while preserving last layout and recovers on 200', async () => {
  const h = createSlice4Harness();
  const siteId = '11111111-1111-1111-1111-111111111111';
  const user = { id: 'op-err', role: 'operator' };

  const layoutSnapshots = [];
  const connStates = [];
  const syncStates = [];

  const [initialSite] = await h.opsResources.loadAssignedOperatorSitesCoordinated(user);
  let currentSite = initialSite;
  const unsub = h.opsResources.subscribeOperatorLayout(siteId, s => layoutSnapshots.push(s));

  const stopWatch = h.liveModule.watchOperatorLayout(
    siteId,
    (refreshBookings, details) => h.liveModule.reconcileOperatorSiteSnapshot({
      expectedSiteId: siteId,
      isActive: () => true,
      getCurrentSite: () => currentSite,
      refreshSite: async (s) => {
        const snap = await h.opsResources.reconcileOperatorLayout(s.id, details?.refreshReason ?? 'invalidation');
        if (snap.lastOutcome === 'error') throw snap.lastError;
        return snap.lastOutcome === 'stale' || !snap.data ? null : snap.data;
      },
      applySite: (updated) => { currentSite = updated; },
      onError: () => {},
      refreshBookings,
      details,
    }),
    s => connStates.push(s),
    d => syncStates.push(d),
  );
  await settle();
  h.emitHub('SnapshotRequired', siteId);
  await h.advanceMs(350);
  assert.equal(connStates.at(-1), 'connected');
  assert.equal(syncStates.at(-1).lastSyncOutcome, 'synced');

  // Inject HTTP 503 on next invalidation while transport remains connected.
  h.setFetchLayout(async () => { throw new Error('HTTP 503'); });
  await h.advanceMs(2100);
  h.emitHub('SlotStateChanged', { eventId: 'evt-slot-503', siteId, payload: { slotId: 's1' } });
  await h.advanceMs(350);

  assert.equal(connStates.at(-1), 'connected', 'WebSocket remains connected');
  assert.equal(syncStates.at(-1).lastSyncOutcome, 'error', 'sync diagnostics reports error, not synced');
  assert.equal(layoutSnapshots.at(-1).lastOutcome, 'error');
  assert.equal(layoutSnapshots.at(-1).data.id, siteId, 'last synchronized layout data preserved on error');

  // Restore HTTP 200 and recover.
  h.setFetchLayout(async ({ siteId: id }) => ({
    ...initialSite,
    id,
    freshness: { generatedAt: new Date(h.now).toISOString(), isFresh: true, stalenessThresholdSeconds: 60 },
  }));
  await h.advanceMs(2100);
  const recovered = await h.opsResources.reconcileOperatorLayout(siteId, 'mutation', { force: true });
  assert.equal(recovered.lastOutcome, 'synced');
  assert.equal(layoutSnapshots.at(-1).lastOutcome, 'synced');

  stopWatch();
  unsub();
  h.opsResources.clearOperatorRealtimeResources();
});

test('Slice 4: authExpiredEvent / identity change purges cache and fences stale in-flight fetches', async () => {
  const h = createSlice4Harness();
  const siteId = '11111111-1111-1111-1111-111111111111';
  const user1 = { id: 'op-user-1', role: 'operator' };

  const bookingsReceived = [];
  h.opsResources.registerOperatorRealtimeIdentity('operator', user1.id);
  const unsub = h.opsResources.subscribeOperatorBookings(siteId, s => bookingsReceived.push(s));

  let resolveSlowFetch;
  h.setFetchBookings(() => new Promise(resolve => { resolveSlowFetch = resolve; }));
  h.opsResources.invalidateOperatorBookings(siteId, 'initial');
  await settle();

  // Expire session while booking fetch is in flight -> purges cache and clears resources.
  h.sessionGeneration = 2;
  h.win.dispatchEvent({ type: 'auth-expired' });
  h.opsResources.registerOperatorRealtimeIdentity('operator', 'op-user-2');

  resolveSlowFetch([{ id: 'stale-booking' }]);
  await settle();

  assert.equal(bookingsReceived.filter(s => s.lastOutcome === 'synced').length, 0, 'stale fetch after authExpired must never sync');
  assert.equal(h.opsResources.getOperatorRealtimeMetrics().trackedResources, 0);

  unsub();
  h.opsResources.clearOperatorRealtimeResources();
});

test('Slice 4: 10-duplicate layout burst and 12-distinct sustained window obey pacing, 4/s and concurrency=4', async () => {
  const h = createSlice4Harness();
  const siteId = '11111111-1111-1111-1111-111111111111';
  const user = { id: 'op-burst', role: 'operator' };
  const coord = h.coordModule.sharedRealtimeCoordinator;

  let inflightRelease = null;
  let actualFetches = 0;
  const startTimes = [];
  h.setFetchLayout(async ({ siteId: id }) => {
    actualFetches++;
    startTimes.push(h.now);
    return new Promise(resolve => {
      inflightRelease = () => resolve({
        id,
        tenantId: 'tenant-1',
        code: 'LOT-A',
        name: 'Lot Alpha',
        address: '1 Main St',
        latitude: null,
        longitude: null,
        isActive: true,
        totalSlots: 10,
        floors: 1,
        configuredBackupCount: 1,
        slots: [],
        units: [],
      });
    });
  });

  h.opsResources.registerOperatorRealtimeIdentity('operator', user.id);
  const unsub = h.opsResources.subscribeOperatorLayout(siteId, () => {});

  // First read starts; 9 duplicate invalidations arrive during/before completion.
  h.opsResources.invalidateOperatorLayout(siteId, 'invalidation');
  await settle();
  for (let i = 0; i < 9; i++) {
    h.opsResources.invalidateOperatorLayout(siteId, 'invalidation');
  }
  assert.equal(actualFetches, 1);
  assert.ok(coord.getMetrics().totalCoalescedInvalidations >= 8);
  inflightRelease();
  await settle();

  // Trailing dirty invalidation obeys >=2000ms pacing.
  assert.equal(actualFetches, 1);
  await h.advanceMs(2000);
  assert.equal(actualFetches, 2);
  inflightRelease();
  await settle();
  unsub();
  h.opsResources.clearOperatorRealtimeResources();
  await h.advanceMs(1000);

  // Sustained 12-distinct resource invalidations: <=4 starts/sec and <=4 concurrent reads.
  let maxConcurrent = 0;
  let currentConcurrent = 0;
  const distinctStartTimes = [];
  const pendingResolvers = [];
  const unsubs = [];

  for (let i = 1; i <= 12; i++) {
    const distinctScope = {
      sessionGeneration: h.sessionGeneration,
      role: 'operator',
      userId: user.id,
      siteId: `00000000-0000-0000-0000-0000000000${String(i).padStart(2, '0')}`,
      resourceClass: 'bookings',
      filterKey: 'reservations',
    };
    unsubs.push(coord.registerResource(distinctScope, () => {
      distinctStartTimes.push(h.now);
      currentConcurrent++;
      maxConcurrent = Math.max(maxConcurrent, currentConcurrent);
      return new Promise(resolve => {
        pendingResolvers.push(() => {
          currentConcurrent--;
          resolve([{ id: `b-${i}` }]);
        });
      });
    }));
  }

  for (let i = 1; i <= 12; i++) {
    coord.invalidateResource({
      sessionGeneration: h.sessionGeneration,
      role: 'operator',
      userId: user.id,
      siteId: `00000000-0000-0000-0000-0000000000${String(i).padStart(2, '0')}`,
      resourceClass: 'bookings',
      filterKey: 'reservations',
    });
  }
  await settle();
  assert.equal(distinctStartTimes.length, 4, 'initial batch caps at 4 concurrent reads');
  assert.equal(maxConcurrent, 4);

  // Finish batch 1 before 1000ms window expires -> sliding rate limit holds batch 2 until +1000ms.
  while (pendingResolvers.length) pendingResolvers.shift()();
  await h.advanceMs(500);
  assert.equal(distinctStartTimes.length, 4, 'rate limit holds batch 2 inside 1000ms window');

  await h.advanceMs(500);
  assert.equal(distinctStartTimes.length, 8, 'batch 2 starts once 1000ms window slides');
  while (pendingResolvers.length) pendingResolvers.shift()();
  await h.advanceMs(1000);
  assert.equal(distinctStartTimes.length, 12, 'batch 3 starts after next 1000ms window');
  while (pendingResolvers.length) pendingResolvers.shift()();
  await settle();

  assert.ok(maxConcurrent <= 4);
  assert.ok(coord.getMetrics().maxQueueDelayMs >= 1000, 'queue delay recorded for throttled reads');
  for (const u of unsubs) u();
  h.opsResources.clearOperatorRealtimeResources();
});

function assertSlidingWindowMaxFour(startTimes, context) {
  for (let i = 4; i < startTimes.length; i++) {
    const span = startTimes[i] - startTimes[i - 4];
    assert.ok(span >= 1000, `${context}: 5th start in sliding window (${startTimes[i]} - ${startTimes[i - 4]} = ${span}ms) must be >= 1000ms`);
  }
}

test('R1: clearAll() and dispose() keep outstanding permits and fence stale resolve/reject across generations', async () => {
  for (const clearMethod of ['clearAll', 'dispose']) {
    for (const staleOutcome of ['resolve', 'reject']) {
      let now = 1_000_000;
      let timerId = 0;
      const timers = new Map();
      const { RealtimeResourceCoordinator } = load('realtimeResourceCoordinator.ts', {}, { Date: { now: () => now } });
      const coord = new RealtimeResourceCoordinator({
        now: () => now,
        setTimer: (fn, delay) => {
          const id = ++timerId;
          timers.set(id, { fn, due: now + Math.max(0, delay ?? 0) });
          return id;
        },
        clearTimer: id => { timers.delete(id); },
      });

      let realOutstanding = 0;
      let peakRealOutstanding = 0;
      let actualFetchesStarted = 0;
      const startTimes = [];
      const gen2Events = [];
      const deferredGen1 = [];
      const deferredGen2 = [];
      const unsubs = [];

      const gen1Scope = {
        sessionGeneration: 1, role: 'operator', userId: 'u1',
        siteId: '10000000-0000-0000-0000-000000000001', resourceClass: 'capacity-summary',
      };
      unsubs.push(coord.registerResource(gen1Scope, () => {
        realOutstanding++;
        peakRealOutstanding = Math.max(peakRealOutstanding, realOutstanding);
        actualFetchesStarted++;
        startTimes.push(now);
        return new Promise((resolve, reject) => {
          deferredGen1.push({ resolve, reject });
        }).finally(() => { realOutstanding--; });
      }));

      coord.invalidateResource(gen1Scope);
      await settle();
      assert.equal(realOutstanding, 1);
      assert.equal(coord.getMetrics().activeConcurrentReads, 1);
      assert.equal(coord.getMetrics().totalReadsStarted, actualFetchesStarted);

      // Purge cache while gen-1 read is still outstanding.
      coord[clearMethod]();
      assert.equal(coord.getMetrics().trackedResources, 0);
      assert.equal(coord.getMetrics().activeConcurrentReads, realOutstanding, `${clearMethod}/${staleOutcome}: active permit preserved across purge`);

      coord.setSessionGeneration(2);
      const gen2Scopes = [1, 2, 3, 4, 5].map(i => ({
        sessionGeneration: 2, role: 'operator', userId: 'u2',
        siteId: `20000000-0000-0000-0000-00000000000${i}`, resourceClass: 'capacity-summary',
      }));
      for (const s of gen2Scopes) {
        unsubs.push(coord.registerResource(
          s,
          () => {
            realOutstanding++;
            peakRealOutstanding = Math.max(peakRealOutstanding, realOutstanding);
            actualFetchesStarted++;
            startTimes.push(now);
            return new Promise(resolve => {
              deferredGen2.push(() => resolve({ siteId: s.siteId }));
            }).finally(() => { realOutstanding--; });
          },
          snap => {
            if (snap.lastOutcome === 'synced' || snap.lastOutcome === 'error') {
              gen2Events.push({ siteId: s.siteId, outcome: snap.lastOutcome, data: snap.data, err: snap.lastError });
            }
          },
        ));
      }

      for (const s of gen2Scopes) coord.invalidateResource(s);
      await settle();
      assert.equal(deferredGen2.length, 3, `${clearMethod}/${staleOutcome}: only 3 gen-2 reads may start while gen-1 holds 1 permit`);
      assert.equal(realOutstanding, 4);
      assert.equal(coord.getMetrics().activeConcurrentReads, realOutstanding);
      assert.equal(coord.getMetrics().totalReadsStarted, actualFetchesStarted);

      // Advance >1000ms without settling gen-1 or the 3 gen-2 reads -> still capped at 4 outstanding.
      now += 1001;
      for (const [k, t] of [...timers.entries()]) {
        if (t.due <= now) { timers.delete(k); t.fn(); }
      }
      await settle();
      assert.equal(deferredGen2.length, 3, 'clock advance alone cannot bypass active concurrency cap of 4');
      assert.equal(realOutstanding, 4);
      assert.equal(coord.getMetrics().activeConcurrentReads, 4);

      // Settle gen-1 read (resolve or reject) -> stale outcome is fenced, frees 1 permit, starts 4th gen-2 read only.
      if (staleOutcome === 'resolve') deferredGen1[0].resolve({ siteId: gen1Scope.siteId, stale: true });
      else deferredGen1[0].reject(new Error('gen-1 stale error'));
      await settle();

      assert.equal(gen2Events.length, 0, 'stale gen-1 result/error must never reach gen-2 observers');
      assert.equal(deferredGen2.length, 4, 'releasing old permit starts exactly 1 queued read (total 4 outstanding)');
      assert.equal(realOutstanding, 4);
      assert.equal(coord.getMetrics().activeConcurrentReads, 4);
      assert.equal(coord.getMetrics().totalReadsStarted, actualFetchesStarted);

      // Drain remaining 4 reads -> 5th gen-2 read starts and completes cleanly.
      while (deferredGen2.length > 0) {
        const next = deferredGen2.shift();
        next();
        await settle();
        assert.equal(coord.getMetrics().activeConcurrentReads, realOutstanding);
      }

      assert.equal(realOutstanding, 0);
      assert.equal(peakRealOutstanding, 4);
      assert.equal(actualFetchesStarted, 6);
      assert.equal(coord.getMetrics().totalReadsStarted, 6);
      assert.equal(gen2Events.filter(e => e.outcome === 'synced').length, 5);
      assert.equal(gen2Events.filter(e => e.outcome === 'error').length, 0);
      assertSlidingWindowMaxFour(startTimes, `${clearMethod}/${staleOutcome}`);

      for (const u of unsubs) u();
      coord.dispose();
    }
  }
});

test('R1: rolling 4/sec start budget survives clearAll() across logout/identity switch', async () => {
  let now = 0;
  let timerId = 0;
  const timers = new Map();
  const { RealtimeResourceCoordinator } = load('realtimeResourceCoordinator.ts', {}, { Date: { now: () => now } });
  const coord = new RealtimeResourceCoordinator({
    now: () => now,
    setTimer: (fn, delay) => {
      const id = ++timerId;
      timers.set(id, { fn, due: now + Math.max(0, delay ?? 0) });
      return id;
    },
    clearTimer: id => { timers.delete(id); },
  });

  let actualFetchesStarted = 0;
  let realOutstanding = 0;
  const startTimes = [];
  const unsubs = [];

  for (let i = 1; i <= 4; i++) {
    const scope = { sessionGeneration: 1, role: 'operator', userId: 'user-a', siteId: `site-a-${i}`, resourceClass: 'bookings' };
    unsubs.push(coord.registerResource(scope, () => {
      actualFetchesStarted++;
      realOutstanding++;
      startTimes.push(now);
      return Promise.resolve([]).finally(() => { realOutstanding--; });
    }));
    coord.invalidateResource(scope);
  }
  await settle();
  assert.equal(actualFetchesStarted, 4);
  assert.equal(realOutstanding, 0);

  // At t=200 (inside same 1000ms window), clearAll + switch to user-b -> next 4 starts wait until t=1000.
  now = 200;
  coord.clearAll();
  assert.equal(coord.getMetrics().totalReadsStarted, actualFetchesStarted);

  const userBSites = [1, 2, 3, 4].map(i => ({
    sessionGeneration: 2, role: 'operator', userId: 'user-b', siteId: `site-b-${i}`, resourceClass: 'bookings',
  }));
  coord.setSessionGeneration(2);
  for (const scope of userBSites) {
    unsubs.push(coord.registerResource(scope, () => {
      actualFetchesStarted++;
      realOutstanding++;
      startTimes.push(now);
      return Promise.resolve([]).finally(() => { realOutstanding--; });
    }));
    coord.invalidateResource(scope);
  }
  await settle();
  assert.equal(actualFetchesStarted, 4, 'user-b reads must respect user-a rolling 4/s start window at t=200');

  now = 999;
  for (const [k, t] of [...timers.entries()]) {
    if (t.due <= now) { timers.delete(k); t.fn(); }
  }
  await settle();
  assert.equal(actualFetchesStarted, 4, 'still throttled at t=999');

  now = 1000;
  for (const [k, t] of [...timers.entries()]) {
    if (t.due <= now) { timers.delete(k); t.fn(); }
  }
  await settle();
  assert.equal(actualFetchesStarted, 8);
  assert.equal(realOutstanding, 0);
  assert.equal(coord.getMetrics().activeConcurrentReads, 0);
  assert.equal(coord.getMetrics().totalReadsStarted, 8);
  assertSlidingWindowMaxFour(startTimes, 'rolling-budget-across-clear');

  for (const u of unsubs) u();
  coord.dispose();
});

test('R1: same-generation role/user switch, consecutive purges, real concurrency/rate caps & metadata fencing', async () => {
  // Part A: Same sessionGeneration=1 role switches (operator/u1 -> owner/u1 -> operator/u2 + double clearAll)
  let now = 5_000;
  let timerId = 0;
  const timers = new Map();
  const { RealtimeResourceCoordinator } = load('realtimeResourceCoordinator.ts', {}, { Date: { now: () => now } });
  const coord = new RealtimeResourceCoordinator({
    now: () => now,
    setTimer: (fn, delay) => {
      const id = ++timerId;
      timers.set(id, { fn, due: now + Math.max(0, delay ?? 0) });
      return id;
    },
    clearTimer: id => { timers.delete(id); },
  });

  let realOutstanding = 0;
  let peakRealOutstanding = 0;
  let actualFetchesStarted = 0;
  const startTimes = [];
  const oldDeferred = [];
  const u2Deferred = [];
  const u2Events = [];
  const unsubs = [];

  for (let i = 1; i <= 2; i++) {
    const scope = { sessionGeneration: 1, role: 'operator', userId: 'u1', siteId: `s-${i}`, resourceClass: 'bookings' };
    unsubs.push(coord.registerResource(scope, () => {
      realOutstanding++; peakRealOutstanding = Math.max(peakRealOutstanding, realOutstanding);
      actualFetchesStarted++; startTimes.push(now);
      return new Promise((resolve, reject) => oldDeferred.push({ resolve, reject })).finally(() => { realOutstanding--; });
    }));
    coord.invalidateResource(scope);
  }
  await settle();

  // Same-generation role switch: operator -> owner (calls clearAll), launches 2 more deferred reads.
  coord.clearAll();
  for (let i = 3; i <= 4; i++) {
    const scope = { sessionGeneration: 1, role: 'owner', userId: 'u1', siteId: `s-${i}`, resourceClass: 'bookings' };
    unsubs.push(coord.registerResource(scope, () => {
      realOutstanding++; peakRealOutstanding = Math.max(peakRealOutstanding, realOutstanding);
      actualFetchesStarted++; startTimes.push(now);
      return new Promise((resolve, reject) => oldDeferred.push({ resolve, reject })).finally(() => { realOutstanding--; });
    }));
    coord.invalidateResource(scope);
  }
  await settle();
  assert.equal(realOutstanding, 4);
  assert.equal(coord.getMetrics().activeConcurrentReads, 4);

  // Same-generation user switch + consecutive clearAll calls.
  coord.clearAll();
  coord.clearAll();

  const u2Scopes = [5, 6, 7].map(i => ({
    sessionGeneration: 1, role: 'operator', userId: 'u2', siteId: `s-${i}`, resourceClass: 'bookings',
  }));
  for (const scope of u2Scopes) {
    unsubs.push(coord.registerResource(
      scope,
      () => {
        realOutstanding++; peakRealOutstanding = Math.max(peakRealOutstanding, realOutstanding);
        actualFetchesStarted++; startTimes.push(now);
        return new Promise(resolve => u2Deferred.push(() => resolve([{ id: scope.siteId }]))).finally(() => { realOutstanding--; });
      },
      snap => { if (snap.lastOutcome === 'synced' || snap.lastOutcome === 'error') u2Events.push(snap); },
    ));
    coord.invalidateResource(scope);
  }
  await settle();
  assert.equal(u2Deferred.length, 0, 'u2 cannot start while 4 prior requests still hold permits');
  assert.equal(realOutstanding, 4);

  // Even after >1000ms window passes, u2 stays blocked until prior permits settle.
  now += 1100;
  for (const [k, t] of [...timers.entries()]) {
    if (t.due <= now) { timers.delete(k); t.fn(); }
  }
  await settle();
  assert.equal(u2Deferred.length, 0);

  // Settle 1st old read (success) + 2nd old read (error) -> 2 permits freed, 2 u2 reads start (no leaks to u2!).
  oldDeferred[0].resolve([{ stale: true }]);
  oldDeferred[1].reject(new Error('old user failure'));
  await settle();
  assert.equal(u2Events.length, 0, 'stale resolve/reject in same generation must never fire u2 observers');
  assert.equal(u2Deferred.length, 2);
  assert.equal(realOutstanding, 4);
  assert.equal(coord.getMetrics().activeConcurrentReads, 4);

  oldDeferred[2].resolve([]);
  oldDeferred[3].reject(new Error('old owner failure'));
  await settle();
  assert.equal(u2Deferred.length, 3);
  assert.equal(realOutstanding, 3);
  assert.equal(coord.getMetrics().activeConcurrentReads, 3);

  while (u2Deferred.length) { u2Deferred.shift()(); await settle(); }
  assert.equal(realOutstanding, 0);
  assert.equal(peakRealOutstanding, 4);
  assert.equal(coord.getMetrics().activeConcurrentReads, 0);
  assert.equal(coord.getMetrics().totalReadsStarted, actualFetchesStarted);
  assert.equal(u2Events.length, 3);
  assertSlidingWindowMaxFour(startTimes, 'same-gen-role-user-purge');
  for (const u of unsubs) u();
  coord.dispose();

  // Part B: operatorRealtimeResources metadata fencing across identity change + consecutive purges.
  const h = createSlice4Harness();
  const siteId = '11111111-1111-1111-1111-111111111111';
  let resolveOpASite;
  let actualOpCalls = 0;
  h.setFetchLayout(({ siteId: id }) => {
    actualOpCalls++;
    if (actualOpCalls === 1) {
      return new Promise(resolve => { resolveOpASite = resolve; });
    }
    return Promise.resolve({
      id, tenantId: 'tenant-1', code: 'LOT-A', name: 'Lot Alpha', address: '1 Main St',
      latitude: null, longitude: null, isActive: true, totalSlots: 10, floors: 1,
      configuredBackupCount: 1, slots: [], units: [], modelUrl: undefined,
    });
  });

  const opAPromise = h.opsResources.loadAssignedOperatorSitesCoordinated({ id: 'op-a', role: 'operator' }).catch(err => err);
  await settle();
  assert.equal(actualOpCalls, 1);
  assert.equal(h.opsResources.getOperatorRealtimeMetrics().activeConcurrentReads, 1);

  // Switch user within same sessionGeneration + extra consecutive clearOperatorRealtimeResources().
  h.opsResources.registerOperatorRealtimeIdentity('operator', 'op-b');
  h.opsResources.clearOperatorRealtimeResources();
  assert.equal(h.opsResources.getOperatorRealtimeMetrics().activeConcurrentReads, 1);

  // Resolve op-a's late fetch carrying a secret modelUrl -> must NOT leak into op-b metadata or permits.
  resolveOpASite({
    id: siteId, tenantId: 'tenant-1', code: 'LOT-A', name: 'Lot Alpha', address: '1 Main St',
    latitude: null, longitude: null, isActive: true, totalSlots: 10, floors: 1,
    configuredBackupCount: 1, slots: [], units: [], modelUrl: 'blob:op-a-model-leak',
  });
  await settle();
  await opAPromise;
  assert.equal(h.opsResources.getOperatorRealtimeMetrics().activeConcurrentReads, 0);

  const opBSites = await h.opsResources.loadAssignedOperatorSitesCoordinated({ id: 'op-b', role: 'operator' });
  assert.equal(opBSites.length, 1);
  assert.equal(opBSites[0].modelUrl, undefined, 'stale op-a modelUrl must not pollute op-b metadata');
  assert.equal(h.opsResources.getOperatorRealtimeMetrics().totalReadsStarted, actualOpCalls);
  h.opsResources.clearOperatorRealtimeResources();
});
