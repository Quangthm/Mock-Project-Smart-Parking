'use strict';

// SPARK-238/244 Phase 2 Slice 2C regression tests.
//
// Owner side: the maintenance panel schedules exactly ONE single-shot timer at the
// next window boundary (never `setInterval`), and the scope helpers describe
// site-wide, unit-scoped and slot-scoped closures.
//
// Operator side: the backend-overlaid `serviceAvailability` block plus the
// `MAINTENANCE` slot overlay must survive `siteFromOperationalLayout` and
// `useLotStatus` unchanged, so the 3D twin, grid, list and Maintenance tab all
// read the same state.

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const ts = require('../../FE/node_modules/typescript');

function loadModule(relativePath, imports = {}, globals = {}) {
  const fullPath = path.join(__dirname, '../../FE/src', relativePath);
  const source = fs
    .readFileSync(fullPath, 'utf8')
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
    Error,
    Map,
    Promise,
    JSON,
    Date,
    URL: { createObjectURL: () => 'blob:test' },
    fetch: async () => {
      throw new Error(`Unexpected fetch in ${relativePath}`);
    },
    ...globals,
    require(name) {
      if (name in imports) return imports[name];
      throw new Error(`Unexpected import '${name}' in ${relativePath}`);
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}

// Minimal, index-stable react shim — same shape as spark-395-zone-search.test.cjs.
function createReactHarness() {
  const state = [];
  const refs = [];
  let cursor = 0;
  let currentEffects = null;

  const react = {
    useState(initial) {
      const index = cursor++;
      if (!(index in state)) {
        state[index] = typeof initial === 'function' ? initial() : initial;
      }
      return [
        state[index],
        value => {
          state[index] = typeof value === 'function' ? value(state[index]) : value;
        },
      ];
    },
    useMemo(compute) {
      return compute();
    },
    useCallback(fn) {
      return fn;
    },
    useRef(initial) {
      const index = cursor++;
      if (!(index in refs)) refs[index] = { current: initial };
      return refs[index];
    },
    useEffect(fn) {
      currentEffects.push(fn);
    },
  };

  return {
    react,
    render(run) {
      cursor = 0;
      currentEffects = [];
      const result = run();
      return { result, effects: currentEffects };
    },
  };
}

const windowStub = {
  setInterval: () => 1,
  clearInterval: () => {},
  setTimeout: () => 1,
  clearTimeout: () => {},
  addEventListener: () => {},
  removeEventListener: () => {},
  localStorage: {
    getItem: () => null,
    setItem: () => {},
    removeItem: () => {},
  },
  dispatchEvent: () => {},
};

const documentStub = {
  addEventListener: () => {},
  removeEventListener: () => {},
  visibilityState: 'visible',
};

const HOUR = 60 * 60 * 1000;

// ---------------------------------------------------------------------------
// P2.6 — boundary timer and scope formatting (pure helpers)
// ---------------------------------------------------------------------------

test('nextMaintenanceBoundaryMs returns ms until the earliest future start or end', () => {
  const rules = loadModule('lib/structureRules.ts');
  const now = Date.parse('2026-11-22T10:00:00Z');

  const windows = [
    { start: '2026-11-22T09:00:00Z', end: '2026-11-22T12:00:00Z', cancelledAt: null },
    { start: '2026-11-23T08:00:00Z', end: '2026-11-23T09:00:00Z', cancelledAt: null },
  ];

  // The first window already started, so the only future instant of it is its end
  // (2h away); the tomorrow window is further out.
  assert.equal(rules.nextMaintenanceBoundaryMs(windows, now), 2 * HOUR);

  // A window that starts later wins over a later end of the current one.
  const soonStart = [
    { start: '2026-11-22T10:30:00Z', end: '2026-11-22T11:00:00Z', cancelledAt: null },
    { start: '2026-11-22T09:00:00Z', end: '2026-11-22T12:00:00Z', cancelledAt: null },
  ];
  assert.equal(rules.nextMaintenanceBoundaryMs(soonStart, now), 30 * 60 * 1000);
});

test('nextMaintenanceBoundaryMs ignores cancelled windows and past instants', () => {
  const rules = loadModule('lib/structureRules.ts');
  const now = Date.parse('2026-11-22T10:00:00Z');

  assert.equal(
    rules.nextMaintenanceBoundaryMs(
      [
        { start: '2026-11-22T10:15:00Z', end: '2026-11-22T11:15:00Z', cancelledAt: '2026-11-22T09:30:00Z' },
        { start: '2026-11-22T08:00:00Z', end: '2026-11-22T09:00:00Z', cancelledAt: null },
      ],
      now,
    ),
    null,
  );
  assert.equal(rules.nextMaintenanceBoundaryMs([], now), null);
  assert.equal(rules.nextMaintenanceBoundaryMs([{ start: '2026-11-22T08:00:00Z', end: '2026-11-22T09:00:00Z' }], now), null);
});

test('formatMaintenanceScope labels whole-site, unit-scoped and slot-scoped windows', () => {
  const rules = loadModule('lib/structureRules.ts');
  const units = [
    { id: 'floor-1', type: 'FLOOR', name: 'Level 1' },
    { id: 'zone-a', type: 'ZONE', name: 'Zone A' },
  ];
  const slots = [
    { id: 'slot-1', code: 'CarParkingSpace_001', unitId: 'zone-a' },
    { id: 'slot-2', code: 'CarParkingSpace_002', unitId: 'zone-a' },
  ];
  const label = code => operationalLayout.formatSlotLabel(code);

  assert.equal(rules.formatMaintenanceScope({ unitId: null, slotId: null }, units, slots), 'Whole site');
  assert.equal(rules.formatMaintenanceScope({ unitId: 'floor-1', slotId: null }, units, slots), 'FLOOR: Level 1');
  assert.equal(rules.formatMaintenanceScope({ unitId: 'zone-a', slotId: null }, units, slots), 'ZONE: Zone A');
  assert.equal(
    rules.formatMaintenanceScope({ unitId: 'zone-a', slotId: 'slot-1' }, units, slots, label),
    'CAR-001 (Zone A)',
  );
  assert.equal(
    rules.formatMaintenanceScope({ unitId: 'zone-a', slotId: 'missing' }, units, slots, label),
    'missing (Zone A)',
  );
});

// ---------------------------------------------------------------------------
// P2.7 — operator projection adapter and hook exposure
// ---------------------------------------------------------------------------

const operationalLayout = loadModule('lib/operationalLayout.ts');

function loadOperatorApi() {
  return loadModule('lib/operatorApi.ts', {
    './authApi': { request: async () => { throw new Error('unused request'); } },
    './operatorCheckoutReturn': { operatorCheckoutReturnUrls: () => ({}) },
    './store': { store: { get: () => null, set: () => {}, remove: () => {} } },
    './3d/ownerModelStorage': { loadOwnerModelUrl: () => null },
    './parkingApi': { operatorsApi: {}, parkingApi: {} },
    './operationalLayout': operationalLayout,
    './3d/parking3DConfig': {
      ALLOWED_PARKING_MODELS: { MULTI_LEVEL: { path: '/model.glb', fileName: 'model.glb' } },
      inferLotType: () => 'MULTI_LEVEL',
    },
    './types': {},
  }, { window: windowStub, document: documentStub });
}

function projectionWith(serviceAvailability) {
  const slot = (slotId, slotCode, physicalState, isBookable) => ({
    slotId,
    slotCode,
    floorId: 'floor-1',
    floorCode: 'G',
    zoneCode: 'Zone A',
    supportedVehicleType: 'CAR',
    slotType: 'STANDARD',
    physicalState,
    reservationState: 'UNRESERVED',
    isEvCharging: false,
    isBookable,
    updatedAt: '2026-11-22T10:00:00Z',
  });

  const projection = {
    siteId: 'site-1',
    siteCode: 'DEMO-1',
    siteName: 'Demo Site',
    contractVersion: '1.2.0',
    generatedAt: '2026-11-22T10:00:00Z',
    isFresh: true,
    stalenessThresholdSeconds: 30,
    metrics: {
      totalPhysicalCapacity: 2,
      occupiedCount: 1,
      reservedCount: 0,
      availableCarSlots: 0,
      motorcycleCapacity: { maxCapacity: 0, occupiedCount: 0, availableCount: 0 },
    },
    floors: [{ floorId: 'floor-1', floorCode: 'G', name: 'Ground', level: 1, maxCapacity: 2 }],
    slots: [
      slot('slot-1', 'CAR-001', 'MAINTENANCE', false),
      slot('slot-2', 'CAR-002', 'OCCUPIED', false),
    ],
  };

  if (serviceAvailability !== undefined) projection.serviceAvailability = serviceAvailability;
  return projection;
}

test('siteFromOperationalLayout preserves the serviceAvailability block', () => {
  const operatorApi = loadOperatorApi();
  const availability = {
    state: 'MAINTENANCE',
    closedUntil: '2026-11-22T12:00:00Z',
    reason: 'Repaving level 1',
    activeWindows: [
      {
        id: 'window-1',
        unitId: 'floor-1',
        slotId: null,
        start: '2026-11-22T09:30:00Z',
        end: '2026-11-22T12:00:00Z',
        reason: 'Repaving level 1',
        enforcementState: 'acknowledged',
      },
    ],
  };

  const site = operatorApi.siteFromOperationalLayout(projectionWith(availability), 'tenant-1');
  assert.equal(site.serviceAvailability.state, 'MAINTENANCE');
  assert.equal(site.serviceAvailability.closedUntil, '2026-11-22T12:00:00Z');
  assert.equal(site.serviceAvailability.activeWindows.length, 1);
  assert.equal(site.serviceAvailability.activeWindows[0].enforcementState, 'acknowledged');
});

test('siteFromOperationalLayout keeps serviceAvailability null on a v1.2.0 response', () => {
  const operatorApi = loadOperatorApi();
  const site = operatorApi.siteFromOperationalLayout(projectionWith(undefined), 'tenant-1');
  assert.equal(site.serviceAvailability, null);
});

test('siteFromOperationalLayout keeps the MAINTENANCE overlay and never rewrites OCCUPIED', () => {
  const operatorApi = loadOperatorApi();
  const site = operatorApi.siteFromOperationalLayout(projectionWith(undefined), 'tenant-1');
  const byId = new Map(site.slots.map(slot => [slot.id, slot]));

  assert.equal(byId.get('slot-1').physicalState, 'MAINTENANCE');
  assert.equal(byId.get('slot-1').isBookable, false);
  assert.equal(byId.get('slot-2').physicalState, 'OCCUPIED');
});

const DEMO_HQ = {
  id: 'demo-hq',
  tenantId: 'tenant-1',
  code: 'DEMO-HQ',
  name: 'DEMO-HQ',
  floors: 1,
  units: [
    { id: 'floor-g', type: 'FLOOR', name: 'Ground', parentId: null, maxCapacity: 4 },
    { id: 'zone-a', type: 'ZONE', name: 'Zone A', parentId: 'floor-g', maxCapacity: 0 },
  ],
  slots: [
    { id: 'slot-1', number: 'CarParkingSpace_001', unitId: 'zone-a', vehicleType: 'car', status: 'available', physicalState: 'MAINTENANCE', reservationState: 'UNRESERVED', isBookable: false },
    { id: 'slot-2', number: 'CarParkingSpace_002', unitId: 'zone-a', vehicleType: 'car', status: 'available', physicalState: 'AVAILABLE', reservationState: 'UNRESERVED', isBookable: true },
    { id: 'slot-3', number: 'CarParkingSpace_003', unitId: 'zone-a', vehicleType: 'car', status: 'occupied', physicalState: 'OCCUPIED', reservationState: 'UNRESERVED', isBookable: false },
  ],
  serviceAvailability: {
    state: 'MAINTENANCE_UNCERTAIN',
    closedUntil: '2026-11-22T12:00:00Z',
    reason: 'Repaving',
    activeWindows: [],
  },
};

function loadUseLotStatus(harness) {
  const module = loadModule(
    'roles/operator/parking-lot/useLotStatus.ts',
    {
      react: harness.react,
      '../../../lib/operationalLayout': operationalLayout,
      '../../../lib/operatorApi': {
        reservationApi: {
          listParkingSessions: async () => [],
          scanParkingSession: async () => ({}),
        },
      },
      '../../../lib/authApi': { AuthApiError: class AuthApiError extends Error {} },
      '../../../lib/operatorRealtimeResources': {
        invalidateOperatorGateResources() {},
        invalidateOperatorParkingSessions() {},
        reconcileOperatorLayout() {},
        subscribeOperatorLayout() {
          return () => {};
        },
        subscribeOperatorParkingSessions() {
          return () => {};
        },
      },
      '../../../lib/operatorLayoutLive': {},
      '../../../lib/types': {},
    },
    { window: windowStub, document: documentStub },
  );
  return module;
}

test('useLotStatus exposes serviceAvailability from the selected site', () => {
  const harness = createReactHarness();
  const module = loadUseLotStatus(harness);

  const rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  assert.equal(rendered.result.serviceAvailability.state, 'MAINTENANCE_UNCERTAIN');
  assert.equal(rendered.result.serviceAvailability.reason, 'Repaving');
});

test('useLotStatus filters statusFilter "maintenance" down to MAINTENANCE slots only', () => {
  const harness = createReactHarness();
  const module = loadUseLotStatus(harness);

  let rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  rendered.result.setStatusFilter('maintenance');
  rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));

  assert.deepEqual(
    rendered.result.filteredSlots.map(slot => slot.id),
    ['slot-1'],
  );
  assert.equal(rendered.result.counts.maintenance, 1);
});

// ---------------------------------------------------------------------------
// P2.6 — source hygiene guards
// ---------------------------------------------------------------------------

const FE_SRC = path.join(__dirname, '../../FE/src');

test('ServiceAvailabilityPanel never polls with setInterval', () => {
  const source = fs.readFileSync(path.join(FE_SRC, 'roles/owner/structure/ServiceAvailabilityPanel.tsx'), 'utf8');
  assert.equal(source.includes('setInterval'), false);
  assert.equal(source.includes('nextMaintenanceBoundaryMs'), true);
});

test('the dead localStorage SpaceDetailModal is gone', () => {
  assert.equal(fs.existsSync(path.join(FE_SRC, 'roles/owner/structure/SpaceDetailModal.tsx')), false);
});