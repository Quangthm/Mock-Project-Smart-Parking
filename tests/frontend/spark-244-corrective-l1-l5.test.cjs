const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const settle = () => new Promise(resolve => setImmediate(resolve));

function events(extra = {}) {
  const handlers = new Map();
  return {
    ...extra,
    handlers,
    addEventListener(name, handler) { handlers.set(name, handler); },
    removeEventListener(name) { handlers.delete(name); },
    emit(name) { handlers.get(name)?.(); },
  };
}

function loadModule(relativePath, imports = {}, globals = {}) {
  const fullPath = path.join(__dirname, '../../FE/src', relativePath);
  let source = fs.readFileSync(fullPath, 'utf8')
    .replace(/import\.meta\.env\.\w+/g, 'undefined');
  const js = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.React,
    },
  }).outputText;
  const context = {
    exports: {},
    console,
    process,
    setTimeout,
    clearTimeout,
    setInterval,
    clearInterval,
    ...globals,
    require(name) {
      if (name in imports) return imports[name];
      for (const [k, v] of Object.entries(imports)) {
        if (name.endsWith(k) || k.endsWith(name)) return v;
      }
      throw new Error(`Unexpected import '${name}' in ${relativePath}`);
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}

// -----------------------------------------------------------------------------
// L1 REGRESSION: Production useLotStatus helpers reconcile authoritative physical
// state, pre-checkout snapshots, slot release, slot reuse, and checkout filtering
// -----------------------------------------------------------------------------
test('L1 Regression: Production useLotStatus helpers reconcile authoritative physical state, checkout releases, and slot reuse', () => {
  const opLayout = loadModule('lib/operationalLayout.ts');
  const useLotStatusModule = loadModule('roles/operator/parking-lot/useLotStatus.ts', {
    '../../../lib/operationalLayout': opLayout,
    '../../../lib/operatorApi': {},
    '../../../lib/authApi': { AuthApiError: class extends Error {} },
    '../../../lib/operatorRealtimeResources': {
      invalidateOperatorGateResources() {},
      invalidateOperatorParkingSessions() {},
      reconcileOperatorLayout() {},
      subscribeOperatorLayout() { return () => {}; },
      subscribeOperatorParkingSessions() { return () => {}; },
    },
    '../../../lib/operatorLayoutLive': {},
    '../../../lib/types': {},
    react: {
      useCallback: fn => fn,
      useEffect() {},
      useMemo: fn => fn(),
      useRef: init => ({ current: init }),
      useState: init => [typeof init === 'function' ? init() : init, () => {}],
    },
  });

  const {
    resolveSlotAxes,
    slotVisualState,
    resolveOperatorSlotView,
    reconcileSlotAssignment,
    filterAssignmentsOnCheckout,
  } = useLotStatusModule;

  assert.equal(typeof resolveOperatorSlotView, 'function', 'resolveOperatorSlotView must be exported from useLotStatus');
  assert.equal(typeof reconcileSlotAssignment, 'function', 'reconcileSlotAssignment must be exported from useLotStatus');
  assert.equal(typeof filterAssignmentsOnCheckout, 'function', 'filterAssignmentsOnCheckout must be exported from useLotStatus');

  // 1. Initial slot: occupied by session overlay
  const baseSlot = {
    id: 'slot-91b7c1b5',
    number: 'A-01',
    status: 'occupied',
    physicalState: 'OCCUPIED',
    isPhysicallyOccupied: true,
    isBookable: false,
    vehicleType: 'car',
    reservationState: 'UNRESERVED',
  };

  const activeSession = {
    plate: '51Z-24499',
    imageUrl: 'data:image/svg+xml,img',
    session: {
      id: 'session-bca785b6',
      originalPlate: '51Z-24499',
      normalizedPlate: '51Z24499',
      status: 'ACTIVE',
      entryTime: new Date().toISOString(),
    },
  };

  const projectedOccupied = {
    slotId: 'slot-91b7c1b5',
    slotCode: 'A-01',
    physicalState: 'OCCUPIED',
    reservationState: 'UNRESERVED',
    isBookable: false,
    updatedAt: new Date().toISOString(),
  };

  const slotReconciledOccupied = reconcileSlotAssignment(baseSlot, activeSession, undefined, projectedOccupied);
  assert.equal(slotReconciledOccupied.physicalState, 'OCCUPIED');
  assert.equal(slotReconciledOccupied.demoPlate, '51Z-24499');

  const viewOccupied = resolveOperatorSlotView(slotReconciledOccupied, projectedOccupied);
  assert.equal(viewOccupied.physicalState, 'OCCUPIED');
  assert.equal(viewOccupied.visualState, 'occupied');

  // 2. Checkout happens: Facility projection releases slot -> physicalState becomes AVAILABLE
  const projectedReleased = {
    slotId: 'slot-91b7c1b5',
    slotCode: 'A-01',
    physicalState: 'AVAILABLE',
    reservationState: 'UNRESERVED',
    isBookable: true,
    updatedAt: new Date().toISOString(),
  };

  // Even if stale activeSession is passed (e.g. from late pre-checkout snapshot)
  const slotAfterCheckout = reconcileSlotAssignment(baseSlot, activeSession, undefined, projectedReleased);
  assert.equal(slotAfterCheckout.demoPlate, undefined, 'Released slot must not retain stale demo session');

  const viewAfterRelease = resolveOperatorSlotView(slotAfterCheckout, projectedReleased);
  assert.equal(viewAfterRelease.physicalState, 'AVAILABLE', 'Authoritative physicalState AVAILABLE wins over stale demoSession');
  assert.equal(viewAfterRelease.visualState, 'available', 'Visual state must be available after checkout release');

  // 3. Late pre-checkout response with status: 'COMPLETED'
  const completedSession = {
    ...activeSession,
    session: {
      ...activeSession.session,
      status: 'COMPLETED',
      exitTime: new Date().toISOString(),
    },
  };
  const slotWithCompletedSession = reconcileSlotAssignment(baseSlot, completedSession, undefined, projectedOccupied);
  assert.equal(slotWithCompletedSession.demoPlate, undefined, 'COMPLETED session is never attached to slot');

  // 4. Slot reuse by a new vehicle / different session after release
  const newVehicleSession = {
    plate: '29A-88888',
    imageUrl: 'data:image/svg+xml,img2',
    session: {
      id: 'session-new-777',
      originalPlate: '29A-88888',
      normalizedPlate: '29A88888',
      status: 'ACTIVE',
      entryTime: new Date().toISOString(),
    },
  };
  const slotReused = reconcileSlotAssignment(baseSlot, newVehicleSession, undefined, projectedOccupied);
  assert.equal(slotReused.demoPlate, '29A-88888');
  assert.equal(slotReused.demoSession?.id, 'session-new-777');

  // 5. Reservation axes preserved: BACKUP reservation remains on reservation axis
  const projectedBackup = {
    slotId: 'slot-91b7c1b5',
    slotCode: 'A-01',
    physicalState: 'AVAILABLE',
    reservationState: 'BACKUP',
    isBookable: false,
    updatedAt: new Date().toISOString(),
  };
  const viewBackup = resolveOperatorSlotView(baseSlot, projectedBackup);
  assert.equal(viewBackup.physicalState, 'AVAILABLE');
  assert.equal(viewBackup.reservationState, 'BACKUP');
  assert.equal(viewBackup.visualState, 'backup', 'Reservation axis BACKUP must be preserved');

  // 6. filterAssignmentsOnCheckout removes by sessionId, slotId, and normalized plate
  const assignments = {
    'slot-1': activeSession,
    'slot-2': newVehicleSession,
  };

  // Remove by sessionId
  const filteredBySession = filterAssignmentsOnCheckout(assignments, { sessionId: 'session-bca785b6' });
  assert.equal(filteredBySession['slot-1'], undefined);
  assert.ok(filteredBySession['slot-2'], 'Other sessions must be preserved');

  // Remove by slotId
  const filteredBySlot = filterAssignmentsOnCheckout(assignments, { slotId: 'slot-1' });
  assert.equal(filteredBySlot['slot-1'], undefined);
  assert.ok(filteredBySlot['slot-2']);

  // Remove by plate (with normalization: spaces/hyphens stripped)
  const filteredByPlate = filterAssignmentsOnCheckout(assignments, { plate: '51z24499' });
  assert.equal(filteredByPlate['slot-1'], undefined);
  assert.ok(filteredByPlate['slot-2']);
});

// -----------------------------------------------------------------------------
// D1 REGRESSION: Authoritative session identity protects reused slot / plate
// from old checkout cleanup in both assignments state and pending localStorage
// -----------------------------------------------------------------------------
test('D1 Regression: Production filterAssignmentsOnCheckout and filterPendingCheckInsOnCheckout fence old checkout identity from reused slot/plate', () => {
  const opLayout = loadModule('lib/operationalLayout.ts');
  const useLotStatusModule = loadModule('roles/operator/parking-lot/useLotStatus.ts', {
    '../../../lib/operationalLayout': opLayout,
    '../../../lib/operatorApi': {},
    '../../../lib/authApi': { AuthApiError: class extends Error {} },
    '../../../lib/operatorRealtimeResources': {
      invalidateOperatorGateResources() {},
      invalidateOperatorParkingSessions() {},
      reconcileOperatorLayout() {},
      subscribeOperatorLayout() { return () => {}; },
      subscribeOperatorParkingSessions() { return () => {}; },
    },
    '../../../lib/operatorLayoutLive': {},
    '../../../lib/types': {},
    react: {
      useCallback: fn => fn,
      useEffect() {},
      useMemo: fn => fn(),
      useRef: init => ({ current: init }),
      useState: init => [typeof init === 'function' ? init() : init, () => {}],
    },
  });

  const {
    filterAssignmentsOnCheckout,
    filterPendingCheckInsOnCheckout,
  } = useLotStatusModule;

  assert.equal(typeof filterAssignmentsOnCheckout, 'function');
  assert.equal(typeof filterPendingCheckInsOnCheckout, 'function');

  // 1. Old session-A checkout arrives late after session-B reused slot-1 with a new plate
  const sessionBSlotReuse = {
    plate: 'NEW-002',
    imageUrl: 'data:image/svg+xml,b',
    session: {
      id: 'session-B',
      originalPlate: 'NEW-002',
      normalizedPlate: 'NEW002',
      status: 'ACTIVE',
      entryTime: new Date().toISOString(),
    },
  };
  const unrelatedSessionC = {
    plate: '30K-99999',
    imageUrl: 'data:image/svg+xml,c',
    session: {
      id: 'session-C',
      originalPlate: '30K-99999',
      normalizedPlate: '30K99999',
      status: 'ACTIVE',
      entryTime: new Date().toISOString(),
    },
  };

  const stateAfterOldCheckoutSameSlot = filterAssignmentsOnCheckout(
    { 'slot-1': sessionBSlotReuse, 'slot-2': unrelatedSessionC },
    { sessionId: 'session-A', slotId: 'slot-1', plate: 'OLD-001' },
  );
  assert.ok(stateAfterOldCheckoutSameSlot['slot-1'], 'D1: old session-A checkout must NOT erase session-B on reused slot-1');
  assert.equal(stateAfterOldCheckoutSameSlot['slot-1'].session.id, 'session-B');
  assert.ok(stateAfterOldCheckoutSameSlot['slot-2'], 'Unrelated session-C must remain intact');

  const pendingAfterOldCheckoutSameSlot = filterPendingCheckInsOnCheckout(
    [
      { plate: 'NEW-002', slotId: 'slot-1', session: sessionBSlotReuse.session },
      { plate: '30K-99999', slotId: 'slot-2', session: unrelatedSessionC.session },
    ],
    { sessionId: 'session-A', slotId: 'slot-1', plate: 'OLD-001' },
  );
  assert.equal(pendingAfterOldCheckoutSameSlot.length, 2, 'D1 localStorage: must preserve session-B on reused slot-1');
  assert.equal(pendingAfterOldCheckoutSameSlot[0].session.id, 'session-B');

  // 2. Old session-A checkout arrives late after a new session-B checked in with the SAME plate
  const sessionBSamePlate = {
    plate: '51Z-24499',
    imageUrl: 'data:image/svg+xml,b2',
    session: {
      id: 'session-B',
      originalPlate: '51Z-24499',
      normalizedPlate: '51Z24499',
      status: 'ACTIVE',
      entryTime: new Date().toISOString(),
    },
  };
  const stateSamePlate = filterAssignmentsOnCheckout(
    { 'slot-1': sessionBSamePlate },
    { sessionId: 'session-A', slotId: 'slot-1', plate: '51z24499' },
  );
  assert.ok(stateSamePlate['slot-1'], 'D1: old session-A checkout must NOT erase new session-B with identical plate');

  const pendingSamePlate = filterPendingCheckInsOnCheckout(
    [{ plate: '51Z-24499', slotId: 'slot-1', session: sessionBSamePlate.session }],
    { sessionId: 'session-A', slotId: 'slot-1', plate: '51z24499' },
  );
  assert.equal(pendingSamePlate.length, 1, 'D1 localStorage: must preserve new session-B of identical plate');

  // 3. Matching session-A IS removed cleanly (and matching reservationId removes reservation session, preserving others)
  const sessionA = {
    plate: 'OLD-001',
    imageUrl: 'data:image/svg+xml,a',
    session: {
      id: 'session-A',
      originalPlate: 'OLD-001',
      normalizedPlate: 'OLD001',
      status: 'ACTIVE',
      entryTime: new Date().toISOString(),
    },
  };
  const stateMatchingA = filterAssignmentsOnCheckout(
    { 'slot-1': sessionA, 'slot-2': unrelatedSessionC },
    { sessionId: 'session-A', slotId: 'slot-1', plate: 'OLD-001' },
  );
  assert.equal(stateMatchingA['slot-1'], undefined, 'Matching session-A must be removed');
  assert.ok(stateMatchingA['slot-2'], 'Other session-C remains intact');

  const reservationBoundSession = {
    plate: 'RES-777',
    imageUrl: 'data:image/svg+xml,res',
    session: {
      id: 'parking-sess-9',
      reservationId: 'booking-9',
      originalPlate: 'RES-777',
      normalizedPlate: 'RES777',
      status: 'ACTIVE',
      entryTime: new Date().toISOString(),
    },
  };
  const matchedViaReservation = filterAssignmentsOnCheckout(
    { 'slot-3': reservationBoundSession },
    { sessionId: 'booking-9', reservationId: 'booking-9', slotId: 'slot-3', plate: 'RES-777' },
  );
  assert.equal(matchedViaReservation['slot-3'], undefined, 'Reservation checkout must remove session via reservationId match');

  // 4. Legacy fallback still works when session identity is absent on event OR on legacy entry
  const legacyNoSessionEntry = {
    plate: 'OLD-001',
    imageUrl: 'data:image/svg+xml,legacy',
    session: {
      originalPlate: 'OLD-001',
      normalizedPlate: 'OLD001',
      status: 'ACTIVE',
      entryTime: new Date().toISOString(),
    },
  };
  const plateOnlyEventRemovesB = filterAssignmentsOnCheckout(
    { 'slot-1': sessionBSamePlate, 'slot-2': unrelatedSessionC },
    { plate: '51z24499' },
  );
  assert.equal(plateOnlyEventRemovesB['slot-1'], undefined, 'Legacy plate-only checkout falls back to normalized plate');
  assert.ok(plateOnlyEventRemovesB['slot-2']);

  const slotOnlyEventRemoves = filterAssignmentsOnCheckout(
    { 'slot-1': legacyNoSessionEntry, 'slot-2': unrelatedSessionC },
    { slotId: 'slot-1' },
  );
  assert.equal(slotOnlyEventRemoves['slot-1'], undefined, 'Legacy slot-only checkout falls back to slotId');
  assert.ok(slotOnlyEventRemoves['slot-2']);

  const pendingLegacyRemoved = filterPendingCheckInsOnCheckout(
    [
      { plate: 'OLD-001', slotId: 'slot-9' },
      { plate: '30K-99999', slotId: 'slot-2', session: unrelatedSessionC.session },
    ],
    { sessionId: 'session-A', slotId: 'slot-9', plate: 'OLD-001' },
  );
  assert.equal(pendingLegacyRemoved.length, 1, 'Legacy localStorage item lacking session.id falls back cleanly');
  assert.equal(pendingLegacyRemoved[0].session.id, 'session-C');
});

// -----------------------------------------------------------------------------
// L2 REGRESSION: Production operatorRealtimeResources invalidates and coalesces
// parking sessions with coordinator 2s reuse window
// -----------------------------------------------------------------------------
test('L2 Regression: Real operatorRealtimeResources invalidates and coalesces parking sessions with 2s reuse window', async () => {
  let now = 100_000;
  let timerId = 0;
  const timers = new Map();

  const { RealtimeResourceCoordinator } = loadModule('lib/realtimeResourceCoordinator.ts', {}, {
    Date: { now: () => now },
  });

  const coordinator = new RealtimeResourceCoordinator({
    now: () => now,
    setTimer: (fn, delay) => {
      const id = ++timerId;
      timers.set(id, { fn, due: now + Math.max(0, delay ?? 0) });
      return id;
    },
    clearTimer: id => { timers.delete(id); },
  });

  let sessionReadsStarted = 0;
  let finishInFlight;
  let makeInFlight = false;
  const mockSessions = [{ id: 'sess-1', originalPlate: '51Z-24499', status: 'ACTIVE' }];

  const mockReservationApi = {
    listParkingSessions: async () => {
      sessionReadsStarted++;
      if (makeInFlight) {
        return new Promise(resolve => { finishInFlight = resolve; });
      }
      return mockSessions;
    },
    lookup: async () => ({ id: 'booking-1' }),
  };

  const realtimeResourcesModule = loadModule('lib/operatorRealtimeResources.ts', {
    './authApi': { authExpiredEvent: 'expired', getSessionGeneration: () => 1 },
    './3d/ownerModelStorage': { loadOwnerModelUrl: async () => null },
    './operatorApi': {
      loadOperatorSites: async () => [],
      readAssignedOperatorSite: () => null,
      reservationApi: mockReservationApi,
    },
    './parkingApi': { operatorsApi: {} },
    './realtimeResourceCoordinator': {
      FULL_LAYOUT_MIN_INTERVAL_MS: 2000,
      sharedRealtimeCoordinator: coordinator,
    },
    './types': {},
  }, {
    Date: { now: () => now },
  });

  const {
    subscribeOperatorParkingSessions,
    invalidateOperatorParkingSessions,
  } = realtimeResourcesModule;

  const siteId = 'site-test-123';

  // 1. Initial subscription and invalidation
  const unsub1 = subscribeOperatorParkingSessions(siteId, () => {});
  invalidateOperatorParkingSessions(siteId, 'initial');
  // StrictMode/multiple readers can invalidate before the first fetch settles.
  // Initial observers need that same snapshot, not a second trailing request.
  for (let i = 0; i < 5; i++) {
    invalidateOperatorParkingSessions(siteId, 'initial');
  }
  await settle();

  assert.equal(sessionReadsStarted, 1, 'First initial read starts');

  // 2. Second component / remount within 2s reuse window (< 2000ms, e.g. 300ms later)
  now += 300;
  const unsub2 = subscribeOperatorParkingSessions(siteId, () => {});
  invalidateOperatorParkingSessions(siteId, 'initial');
  await settle();

  assert.equal(sessionReadsStarted, 1, 'Initial invalidation within 2s reuse window does NOT start a new network read');

  // 3. Remount after 2s reuse window (e.g. 2100ms later)
  now += 2100;
  invalidateOperatorParkingSessions(siteId, 'initial');
  await settle();

  assert.equal(sessionReadsStarted, 2, 'Initial invalidation after 2s reuse window DOES start a fresh network read');

  // 4. Overlapping burst of WS invalidations during an in-flight read
  makeInFlight = true;
  invalidateOperatorParkingSessions(siteId, 'mutation');
  for (const [k, t] of [...timers.entries()]) {
    if (t.due <= now) { timers.delete(k); t.fn(); }
  }
  await settle();
  assert.equal(sessionReadsStarted, 3, 'Mutation read is in flight');

  // While in flight, 5 duplicate SignalR WS invalidations arrive
  for (let i = 0; i < 5; i++) {
    now += 10;
    invalidateOperatorParkingSessions(siteId, 'invalidation');
  }

  // A new initial observer must not erase the real invalidations queued above.
  invalidateOperatorParkingSessions(siteId, 'initial');

  // Complete the in-flight read
  makeInFlight = false;
  if (typeof finishInFlight === 'function') {
    finishInFlight(mockSessions);
  }
  await settle();

  // Exactly 4 total reads: initial #1, initial #2 (after 2s), mutation #3, and exactly 1 coalesced trailing read for all 5 WS invalidations
  assert.equal(sessionReadsStarted, 4, 'All 5 overlapping WS invalidations coalesce into exactly one trailing read');

  unsub1();
  unsub2();
  coordinator.dispose();
});

// -----------------------------------------------------------------------------
// L3 REGRESSION: Real operatorLayoutLive module handles token refresh, prevents
// socket start while page is hidden (C2 fix), and cleanly fences auth rejection
// -----------------------------------------------------------------------------
function createLayoutHarness(options = {}) {
  const handlers = new Map(), timers = new Map();
  let id = 0, builds = 0, close, starts = 0, stops = 0, generation = 1;
  let now = options.now ?? 500_000;
  let currentFailure = options.failure;
  let expiresAt = options.expiresAt ?? (now + 900_000);
  let refreshDeferred = options.refreshDeferred;
  let refreshCalls = 0;

  class Clock extends Date {
    static now() { return now; }
  }

  const connection = {
    state: 'Disconnected',
    on(name, fn) { handlers.set(name, fn); },
    off(name) { handlers.delete(name); },
    onclose(fn) { close = fn; },
    async start() {
      starts++;
      if (currentFailure) throw currentFailure;
      this.state = 'Connected';
    },
    async stop() {
      stops++;
      this.state = 'Disconnected';
      close?.();
    },
  };

  class HubConnectionBuilder {
    constructor() { builds++; }
    withUrl() { return this; }
    configureLogging() { return this; }
    build() { return connection; }
  }

  const navigator = { onLine: options.onLine ?? true };
  const window = events();
  const document = events({ visibilityState: options.visibilityState ?? 'visible' });

  const guardModule = loadModule('lib/realtimeIdempotency.ts');
  const { watchOperatorLayout, getOperatorLayoutSyncState } = loadModule('lib/operatorLayoutLive.ts', {
    '@microsoft/signalr': {
      HubConnectionBuilder,
      HttpTransportType: { WebSockets: 1 },
      LogLevel: { None: 6 },
    },
    './authApi': {
      authApi: { expiresAt: () => expiresAt },
      authExpiredEvent: 'expired',
      authRefreshedEvent: 'refreshed',
      ensureAccessToken: async () => {
        refreshCalls++;
        if (options.refreshFails) {
          throw new Error('401 Unauthorized');
        }
        if (refreshDeferred) {
          await refreshDeferred.promise;
        }
        expiresAt = now + 900_000;
        return 'fresh-token';
      },
      getSessionGeneration: () => generation,
    },
    './realtimeIdempotency': {
      RealtimeIdempotencyGuard: guardModule.RealtimeIdempotencyGuard,
      operatorRealtimeGuard: guardModule.operatorRealtimeGuard,
    },
  }, {
    window,
    document,
    navigator,
    Date: Clock,
    setTimeout(fn, delay) { timers.set(++id, { fn, delay }); return id; },
    clearTimeout(key) { timers.delete(key); },
  });

  return {
    watchOperatorLayout,
    getOperatorLayoutSyncState,
    handlers,
    timers,
    document,
    window,
    navigator,
    connection,
    get starts() { return starts; },
    get stops() { return stops; },
    get refreshCalls() { return refreshCalls; },
    setExpiresAt(val) { expiresAt = val; },
    setGeneration(val) { generation = val; },
    advance(ms) { now += ms; },
  };
}

const siteUuid = 'b0000000-0000-4000-8000-000000000001';

test('L3 Regression: Real operatorLayoutLive refreshes expired token before connect, prevents start while hidden (C2), and fences rejection', async () => {
  // Case 1: Foreground connect with expired token -> refreshes first, then connects SignalR
  {
    const harness = createLayoutHarness({
      expiresAt: 500_000 - 10_000, // expired
    });

    let connectionState = '';
    const stop = harness.watchOperatorLayout(
      siteUuid,
      () => 'synced',
      state => { connectionState = state; },
    );
    await settle();

    assert.equal(harness.refreshCalls, 1, 'ensureAccessToken must be called before SignalR start');
    assert.equal(harness.starts, 1, 'SignalR must start after token refresh');
    assert.equal(harness.connection.state, 'Connected');
    stop();
  }

  // Case 2: C2 Reproduction — refresh completes while page is hidden -> must NOT start SignalR!
  {
    let resolveRefresh;
    const refreshDeferred = {
      promise: new Promise(resolve => { resolveRefresh = resolve; }),
    };

    const harness = createLayoutHarness({
      expiresAt: 500_000 - 10_000, // expired
      refreshDeferred,
      visibilityState: 'visible',
    });

    const stop = harness.watchOperatorLayout(
      siteUuid,
      () => 'synced',
    );
    await settle();

    assert.equal(harness.refreshCalls, 1, 'ensureAccessToken started');
    assert.equal(harness.starts, 0, 'SignalR has not started yet');

    // While refresh is pending in flight, page becomes hidden
    harness.document.visibilityState = 'hidden';
    harness.document.emit('visibilitychange');

    // Complete token refresh
    resolveRefresh();
    await settle();

    // C2 Verification: SignalR must NOT start while page is hidden!
    assert.equal(harness.starts, 0, 'SignalR must NOT start after refresh completion while document is hidden (fixes C2!)');

    // Now resume page to foreground / visible
    harness.document.visibilityState = 'visible';
    harness.document.emit('visibilitychange');
    await settle();

    // Now SignalR connects cleanly!
    assert.equal(harness.starts, 1, 'SignalR connects cleanly after page is resumed to visible');
    stop();
  }

  // Case 3: Refresh rejection (401 / expired refresh token)
  {
    let notifiedState = '';
    const harness = createLayoutHarness({
      expiresAt: 500_000 - 10_000,
      refreshFails: true,
    });

    const stop = harness.watchOperatorLayout(
      siteUuid,
      () => 'synced',
      state => { notifiedState = state; },
    );
    await settle();

    assert.equal(harness.refreshCalls, 1);
    assert.equal(harness.starts, 0, 'SignalR must not start when token refresh rejects');
    assert.equal(notifiedState, 'unavailable', 'Failed refresh notifies unavailable');
    stop();
  }

  // Case 4: Lifecycle change during await (unmount while refresh is in flight)
  {
    let resolveRefresh;
    const refreshDeferred = {
      promise: new Promise(resolve => { resolveRefresh = resolve; }),
    };

    const harness = createLayoutHarness({
      expiresAt: 500_000 - 10_000,
      refreshDeferred,
    });

    const stop = harness.watchOperatorLayout(
      siteUuid,
      () => 'synced',
    );
    await settle();

    assert.equal(harness.refreshCalls, 1);
    assert.equal(harness.starts, 0);

    // Unmount before refresh completes
    stop();

    resolveRefresh();
    await settle();

    assert.equal(harness.starts, 0, 'SignalR must not start if unmounted during token refresh await');
  }

  // Case 5: Offline during pending refresh -> 0 starts; later online resume -> 1 start
  {
    let resolveRefresh;
    const refreshDeferred = {
      promise: new Promise(resolve => { resolveRefresh = resolve; }),
    };

    const harness = createLayoutHarness({
      expiresAt: 500_000 - 10_000,
      refreshDeferred,
      onLine: true,
    });

    const stop = harness.watchOperatorLayout(siteUuid, () => 'synced');
    await settle();

    assert.equal(harness.refreshCalls, 1);
    assert.equal(harness.starts, 0);

    // Go offline while refresh is in flight
    harness.navigator.onLine = false;
    harness.window.emit('offline');

    resolveRefresh();
    await settle();
    assert.equal(harness.starts, 0, 'SignalR must NOT start after refresh completion while offline');

    // Come back online -> recovers and connects cleanly
    harness.navigator.onLine = true;
    harness.window.emit('online');
    await settle();
    assert.equal(harness.starts, 1, 'SignalR connects after browser comes back online');
    stop();
  }

  // Case 6: Generation / user replacement during pending refresh -> 0 starts
  {
    let resolveRefresh;
    const refreshDeferred = {
      promise: new Promise(resolve => { resolveRefresh = resolve; }),
    };
    const states = [];
    const harness = createLayoutHarness({
      expiresAt: 500_000 - 10_000,
      refreshDeferred,
    });

    const stop = harness.watchOperatorLayout(
      siteUuid,
      () => 'synced',
      state => states.push(state),
    );
    await settle();

    assert.equal(harness.refreshCalls, 1);
    assert.equal(harness.starts, 0);

    // Session generation advances (account replaced / logged out) while refresh is pending
    harness.setGeneration(2);

    resolveRefresh();
    await settle();

    assert.equal(harness.starts, 0, 'SignalR must NOT start after session generation changed during refresh');
    assert.equal(states.includes('connected'), false, 'Old subscriber must never receive connected after generation change');
    stop();
  }
});

test('L3 Integration: Real authApi enforces single-flight HTTP refresh on concurrent resumes and fences late refresh after clearSession()', async () => {
  const createEventBus = (extra = {}) => {
    const listeners = new Map();
    return {
      ...extra,
      addEventListener(name, fn) {
        if (!listeners.has(name)) listeners.set(name, new Set());
        listeners.get(name).add(fn);
      },
      removeEventListener(name, fn) {
        listeners.get(name)?.delete(fn);
      },
      dispatchEvent(event) {
        const type = typeof event === 'string' ? event : event?.type;
        for (const fn of listeners.get(type) ?? []) fn(event);
        return true;
      },
    };
  };

  // Scenario A: Concurrent focus/online/visibilitychange during expired token refresh -> 1 HTTP /api/auth/refresh, 1 SignalR start
  {
    let now = 1_000_000;
    const timers = new Map();
    let timerId = 0;
    let starts = 0;
    let httpRefreshRequests = 0;
    let resolveHttpRefresh;

    class Clock extends Date {
      static now() { return now; }
    }

    const storage = new Map([
      ['sp_access_token', 'old-expired-jwt'],
      ['sp_refresh_token', 'valid-refresh-1'],
      ['sp_access_expires_at', String(now - 5_000)],
    ]);
    const sessionStorage = {
      getItem: key => (storage.has(key) ? storage.get(key) : null),
      setItem: (key, val) => storage.set(key, String(val)),
      removeItem: key => storage.delete(key),
    };

    const windowBus = createEventBus();
    const documentBus = createEventBus({ visibilityState: 'visible' });
    const navigator = { onLine: true };

    const connection = {
      state: 'Disconnected',
      handlers: new Map(),
      on(name, fn) { this.handlers.set(name, fn); },
      off(name) { this.handlers.delete(name); },
      onclose(fn) { this.close = fn; },
      async start() { starts++; this.state = 'Connected'; },
      async stop() { this.state = 'Disconnected'; this.close?.(); },
    };
    class HubConnectionBuilder {
      withUrl() { return this; }
      configureLogging() { return this; }
      build() { return connection; }
    }

    const fetchMock = async (url) => {
      if (String(url).endsWith('/api/auth/refresh')) {
        httpRefreshRequests++;
        return await new Promise(resolve => {
          resolveHttpRefresh = resolve;
        });
      }
      throw new Error(`Unexpected fetch URL: ${url}`);
    };

    const authModule = loadModule('lib/authApi.ts', {
      './types': {},
      './store': { store: { findUserById: () => undefined } },
    }, {
      fetch: fetchMock,
      sessionStorage,
      window: windowBus,
      Event: class Event { constructor(type) { this.type = type; } },
      AbortSignal: { timeout: () => undefined },
      Date: Clock,
    });

    const guardModule = loadModule('lib/realtimeIdempotency.ts');
    const { watchOperatorLayout } = loadModule('lib/operatorLayoutLive.ts', {
      '@microsoft/signalr': {
        HubConnectionBuilder,
        HttpTransportType: { WebSockets: 1 },
        LogLevel: { None: 6 },
      },
      './authApi': authModule,
      './realtimeIdempotency': {
        RealtimeIdempotencyGuard: guardModule.RealtimeIdempotencyGuard,
        operatorRealtimeGuard: guardModule.operatorRealtimeGuard,
      },
    }, {
      window: windowBus,
      document: documentBus,
      navigator,
      Date: Clock,
      setTimeout(fn, delay) { timers.set(++timerId, { fn, delay }); return timerId; },
      clearTimeout(key) { timers.delete(key); },
    });

    const stop = watchOperatorLayout(siteUuid, () => 'synced');
    await settle();

    // Concurrent resume storms while refresh HTTP request is in flight
    windowBus.dispatchEvent({ type: 'focus' });
    windowBus.dispatchEvent({ type: 'online' });
    documentBus.dispatchEvent({ type: 'visibilitychange' });
    await settle();

    assert.equal(httpRefreshRequests, 1, 'Real authApi single-flight must issue strictly 1 HTTP /api/auth/refresh across concurrent resumes');
    assert.equal(starts, 0, 'SignalR must wait until HTTP refresh completes');

    resolveHttpRefresh({
      ok: true,
      status: 200,
      json: async () => ({
        success: true,
        data: {
          accessToken: 'new-fresh-jwt',
          refreshToken: 'new-refresh-2',
          expiresIn: 900,
          user: { userId: 'op-1', fullName: 'Operator', email: 'op@test', role: 'operator' },
        },
      }),
    });
    await settle();
    await settle();

    assert.equal(starts, 1, 'Exactly 1 SignalR start after real single-flight HTTP refresh completes');
    stop();
  }

  // Scenario B: Real authApi.clearSession() during in-flight /api/auth/refresh fences token restoration and SignalR start
  {
    let now = 2_000_000;
    let starts = 0;
    let httpRefreshRequests = 0;
    let resolveHttpRefresh;

    class Clock extends Date {
      static now() { return now; }
    }

    const storage = new Map([
      ['sp_access_token', 'old-expired-jwt-2'],
      ['sp_refresh_token', 'valid-refresh-2'],
      ['sp_access_expires_at', String(now - 5_000)],
    ]);
    const sessionStorage = {
      getItem: key => (storage.has(key) ? storage.get(key) : null),
      setItem: (key, val) => storage.set(key, String(val)),
      removeItem: key => storage.delete(key),
    };

    const windowBus = createEventBus();
    const documentBus = createEventBus({ visibilityState: 'visible' });
    const navigator = { onLine: true };

    const connection = {
      state: 'Disconnected',
      on() {},
      off() {},
      onclose() {},
      async start() { starts++; this.state = 'Connected'; },
      async stop() { this.state = 'Disconnected'; },
    };
    class HubConnectionBuilder {
      withUrl() { return this; }
      configureLogging() { return this; }
      build() { return connection; }
    }

    const fetchMock = async () => {
      httpRefreshRequests++;
      return await new Promise(resolve => { resolveHttpRefresh = resolve; });
    };

    const authModule = loadModule('lib/authApi.ts', {
      './types': {},
      './store': { store: { findUserById: () => undefined } },
    }, {
      fetch: fetchMock,
      sessionStorage,
      window: windowBus,
      Event: class Event { constructor(type) { this.type = type; } },
      AbortSignal: { timeout: () => undefined },
      Date: Clock,
    });

    const guardModule = loadModule('lib/realtimeIdempotency.ts');
    const { watchOperatorLayout } = loadModule('lib/operatorLayoutLive.ts', {
      '@microsoft/signalr': {
        HubConnectionBuilder,
        HttpTransportType: { WebSockets: 1 },
        LogLevel: { None: 6 },
      },
      './authApi': authModule,
      './realtimeIdempotency': {
        RealtimeIdempotencyGuard: guardModule.RealtimeIdempotencyGuard,
        operatorRealtimeGuard: guardModule.operatorRealtimeGuard,
      },
    }, {
      window: windowBus,
      document: documentBus,
      navigator,
      Date: Clock,
      setTimeout,
      clearTimeout,
    });

    const stop = watchOperatorLayout(siteUuid, () => 'synced');
    await settle();
    assert.equal(httpRefreshRequests, 1);

    // User logs out / clears session while /api/auth/refresh is still in flight
    authModule.authApi.clearSession();

    resolveHttpRefresh({
      ok: true,
      status: 200,
      json: async () => ({
        success: true,
        data: {
          accessToken: 'stale-refreshed-jwt',
          refreshToken: 'stale-refresh-2',
          expiresIn: 900,
          user: { userId: 'op-1', fullName: 'Operator', email: 'op@test', role: 'operator' },
        },
      }),
    });
    await settle();
    await settle();

    assert.equal(sessionStorage.getItem('sp_access_token'), null, 'Late refresh response must never restore cleared session');
    assert.equal(starts, 0, 'SignalR must NOT start when session was cleared during refresh');
    stop();
  }
});

// -----------------------------------------------------------------------------
// L4 REGRESSION: Release Backup confirmation dialog component structure
// (Live browser UI interaction marked [UNVERIFIED] per Tech Lead review)
// -----------------------------------------------------------------------------
test('L4 Regression: Release Backup modal renders with proper accessible dialog attributes and portal container', () => {
  const slotManagementSource = fs.readFileSync(
    path.join(__dirname, '../../FE/src/roles/operator/parking-slots/SlotManagement.tsx'),
    'utf8',
  );

  assert.ok(
    slotManagementSource.includes('createPortal('),
    'SlotManagement must import and call createPortal',
  );
  assert.ok(
    slotManagementSource.includes('document.body'),
    'SlotManagement confirmation modal must portal to document.body',
  );
  assert.ok(
    slotManagementSource.includes('role="dialog"'),
    'Confirmation modal must have role="dialog"',
  );
  assert.ok(
    slotManagementSource.includes('aria-modal="true"'),
    'Confirmation modal must have aria-modal="true"',
  );
  assert.ok(
    slotManagementSource.includes('aria-labelledby="release-backup-title"'),
    'Confirmation modal must have aria-labelledby',
  );
  assert.ok(
    slotManagementSource.includes('handleReleaseBackup(releasingSlot.id)'),
    'Confirmation button must call handleReleaseBackup with releasingSlot.id',
  );
  assert.ok(
    slotManagementSource.includes('setReleasingSlot(null)'),
    'Cancel button must close dialog by setting releasingSlot to null',
  );
  assert.ok(
    slotManagementSource.includes('e.key === "Escape"'),
    'Modal must support Escape key dismissal',
  );
});

// -----------------------------------------------------------------------------
// L5 REGRESSION: Production evaluateOperatorFreshness separates healthy idle,
// malformed metadata (C3 fix), explicit source-stale, and sync error
// -----------------------------------------------------------------------------
test('L5 Regression: Production evaluateOperatorFreshness separates healthy idle, malformed metadata (C3), and source-stale', () => {
  const opLayout = loadModule('lib/operationalLayout.ts');
  const useLotStatusModule = loadModule('roles/operator/parking-lot/useLotStatus.ts', {
    '../../../lib/operationalLayout': opLayout,
    '../../../lib/operatorApi': {},
    '../../../lib/authApi': { AuthApiError: class extends Error {} },
    '../../../lib/operatorRealtimeResources': {
      invalidateOperatorGateResources() {},
      invalidateOperatorParkingSessions() {},
      reconcileOperatorLayout() {},
      subscribeOperatorLayout() { return () => {}; },
      subscribeOperatorParkingSessions() { return () => {}; },
    },
    '../../../lib/operatorLayoutLive': {},
    '../../../lib/types': {},
    react: {
      useCallback: fn => fn,
      useEffect() {},
      useMemo: fn => fn(),
      useRef: init => ({ current: init }),
      useState: init => [typeof init === 'function' ? init() : init, () => {}],
    },
  });

  const { evaluateOperatorFreshness } = useLotStatusModule;
  assert.equal(typeof evaluateOperatorFreshness, 'function', 'evaluateOperatorFreshness must be exported from useLotStatus');

  const now = Date.now();
  // 5 minutes ago (> 60s server stalenessThresholdSeconds)
  const generated5MinAgo = new Date(now - 300_000).toISOString();

  // Valid snapshot from 5 minutes ago during idle
  const idleFreshLayout = {
    generatedAt: generated5MinAgo,
    isFresh: true,
    stalenessThresholdSeconds: 60,
  };

  // 1. Healthy idle: connected + synced + valid metadata + isFresh: true -> NOT stale!
  const healthyIdleResult = evaluateOperatorFreshness(idleFreshLayout, {
    now,
    connectionState: 'connected',
    syncState: 'synced',
    refreshFailed: false,
  });
  assert.equal(healthyIdleResult.isHealthyIdleLive, true);
  assert.equal(healthyIdleResult.isMalformedMetadata, false);
  assert.equal(healthyIdleResult.isStale, false, 'Healthy connected idle beyond threshold must NOT show stale banner');

  // 2. C3 Fix: Malformed timestamp ('invalid-timestamp') with isFresh: true, connected, synced -> MUST BE STALE!
  const malformedTimestampLayout = {
    generatedAt: 'not-a-valid-timestamp',
    isFresh: true,
    stalenessThresholdSeconds: 60,
  };
  const malformedTimestampResult = evaluateOperatorFreshness(malformedTimestampLayout, {
    now,
    connectionState: 'connected',
    syncState: 'synced',
    refreshFailed: false,
  });
  assert.equal(malformedTimestampResult.isMalformedMetadata, true, 'Malformed timestamp must be flagged as malformed');
  assert.equal(malformedTimestampResult.isHealthyIdleLive, false, 'Malformed metadata must NEVER qualify for healthy idle (fixes C3!)');
  assert.equal(malformedTimestampResult.isStale, true, 'Malformed timestamp must fail closed and show stale banner');

  // 3. C3 Fix: Non-positive / invalid threshold (0, -10, NaN, undefined) with isFresh: true, connected, synced -> MUST BE STALE!
  for (const badThreshold of [0, -10, NaN, undefined, null]) {
    const malformedThresholdLayout = {
      generatedAt: generated5MinAgo,
      isFresh: true,
      stalenessThresholdSeconds: badThreshold,
    };
    const malformedThresholdResult = evaluateOperatorFreshness(malformedThresholdLayout, {
      now,
      connectionState: 'connected',
      syncState: 'synced',
      refreshFailed: false,
    });
    assert.equal(malformedThresholdResult.isMalformedMetadata, true, `Threshold ${badThreshold} must be flagged as malformed`);
    assert.equal(malformedThresholdResult.isHealthyIdleLive, false, `Threshold ${badThreshold} must not be healthy idle`);
    assert.equal(malformedThresholdResult.isStale, true, `Threshold ${badThreshold} must fail closed and show stale banner`);
  }

  // 4. Server explicit isFresh: false (source stale) -> MUST be stale!
  const sourceStaleLayout = {
    generatedAt: new Date(now - 10_000).toISOString(),
    isFresh: false,
    stalenessThresholdSeconds: 60,
  };
  const sourceStaleResult = evaluateOperatorFreshness(sourceStaleLayout, {
    now,
    connectionState: 'connected',
    syncState: 'synced',
    refreshFailed: false,
  });
  assert.equal(sourceStaleResult.isSourceStale, true);
  assert.equal(sourceStaleResult.isStale, true, 'Explicit server isFresh=false must show stale banner even if connected');

  // 5. Sync error (failed reconciliation) -> MUST be stale!
  const syncErrorResult = evaluateOperatorFreshness(idleFreshLayout, {
    now,
    connectionState: 'connected',
    syncState: 'error',
    refreshFailed: false,
  });
  assert.equal(syncErrorResult.isSyncFailed, true);
  assert.equal(syncErrorResult.isStale, true, 'Sync error must show stale banner');

  // 6. Disconnected / unavailable while age > threshold -> MUST be stale!
  const disconnectedResult = evaluateOperatorFreshness(idleFreshLayout, {
    now,
    connectionState: 'unavailable',
    syncState: 'synced',
    refreshFailed: false,
  });
  assert.equal(disconnectedResult.isHealthyIdleLive, false);
  assert.equal(disconnectedResult.isStale, true, 'Disconnected with age > threshold must show stale banner');

  // 7. Recovery: reconnects to connected + synced -> clears stale banner
  const recoveredResult = evaluateOperatorFreshness(idleFreshLayout, {
    now,
    connectionState: 'connected',
    syncState: 'synced',
    refreshFailed: false,
  });
  assert.equal(recoveredResult.isStale, false, 'Reconnecting and syncing clears stale banner');

  // 8. Legacy / uninstrumented owner path (freshness: null)
  const legacyResult = evaluateOperatorFreshness(null, {
    now,
    connectionState: 'connected',
    syncState: 'synced',
    refreshFailed: false,
  });
  assert.equal(legacyResult.isStale, false, 'Legacy uninstrumented path does not flag stale');
});
