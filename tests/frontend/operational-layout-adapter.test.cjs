const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const fixturesDir = path.join(__dirname, '../../FE/src/fixtures');
const freshEnvelope = JSON.parse(fs.readFileSync(path.join(fixturesDir, 'slots-operational-fixture.json'), 'utf8'));
const staleEnvelope = JSON.parse(fs.readFileSync(path.join(fixturesDir, 'slots-operational-stale-fixture.json'), 'utf8'));
const fresh = freshEnvelope.data;
const stale = staleEnvelope.data;

/**
 * Same harness as operator-provisioning.test.cjs: transpile the real TS module
 * to CommonJS, run it in a vm, and stub every import by name.
 */
function load(file, imports, replacements = {}) {
  let source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8');
  for (const [from, to] of Object.entries(replacements)) source = source.split(from).join(to);
  const js = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const context = {
    exports: {},
    Error,
    Promise,
    require: name => {
      if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
      return imports[name];
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}

const layout = load('lib/operationalLayout.ts', {}, {
  'import.meta.env.VITE_OPERATIONAL_LAYOUT_FIXTURE': '"false"',
  'import.meta.env.DEV': 'true',
});

const PHYSICAL = ['AVAILABLE', 'OCCUPIED', 'UNKNOWN', 'MAINTENANCE', 'UNAVAILABLE'];
const RESERVATION = ['UNRESERVED', 'RESERVED', 'PROTECTED', 'BACKUP'];

// Independent expectation table, deliberately not imported from the module under test.
const EXPECTED_VISUAL = {
  AVAILABLE: { UNRESERVED: 'available', RESERVED: 'reserved', PROTECTED: 'reserved', BACKUP: 'backup' },
  OCCUPIED: { UNRESERVED: 'occupied', RESERVED: 'occupied', PROTECTED: 'occupied', BACKUP: 'occupied' },
  UNKNOWN: { UNRESERVED: 'unknown', RESERVED: 'unknown', PROTECTED: 'unknown', BACKUP: 'unknown' },
  MAINTENANCE: { UNRESERVED: 'maintenance', RESERVED: 'maintenance', PROTECTED: 'maintenance', BACKUP: 'maintenance' },
  UNAVAILABLE: { UNRESERVED: 'unavailable', RESERVED: 'unavailable', PROTECTED: 'unavailable', BACKUP: 'unavailable' },
};

test('the real fixture covers the full 20/20 physical x reservation matrix and maps each cell', () => {
  const physicalSeen = new Set(fresh.slots.map(slot => slot.physicalState));
  const reservationSeen = new Set(fresh.slots.map(slot => slot.reservationState));
  assert.deepEqual([...physicalSeen].sort(), [...PHYSICAL].sort());
  assert.deepEqual([...reservationSeen].sort(), [...RESERVATION].sort());

  const cells = new Set();
  for (const slot of fresh.slots) {
    cells.add(`${slot.physicalState}:${slot.reservationState}`);
    assert.equal(
      layout.mapSlotVisualState(slot),
      EXPECTED_VISUAL[slot.physicalState][slot.reservationState],
      `${slot.physicalState} + ${slot.reservationState}`,
    );
  }
  assert.equal(cells.size, 20);
});

test('no non-bookable, UNKNOWN or BACKUP input is ever rendered as available', () => {
  for (const physicalState of PHYSICAL) {
    for (const reservationState of RESERVATION) {
      const slot = { physicalState, reservationState };
      const visual = layout.mapSlotVisualState(slot);
      if (physicalState === 'UNKNOWN') assert.equal(visual, 'unknown');
      if (reservationState === 'BACKUP') assert.notEqual(visual, 'available');
      if (!layout.isSlotBookable(slot)) assert.notEqual(visual, 'available');
    }
  }
});

test('BACKUP has its own visual state while RESERVED and PROTECTED stay reserved', () => {
  assert.equal(layout.mapSlotVisualState({ physicalState: 'AVAILABLE', reservationState: 'BACKUP' }), 'backup');
  assert.equal(layout.mapSlotVisualState({ physicalState: 'AVAILABLE', reservationState: 'RESERVED' }), 'reserved');
  assert.equal(layout.mapSlotVisualState({ physicalState: 'AVAILABLE', reservationState: 'PROTECTED' }), 'reserved');
  assert.notEqual(
    layout.mapSlotVisualState({ physicalState: 'AVAILABLE', reservationState: 'BACKUP' }),
    layout.mapSlotVisualState({ physicalState: 'AVAILABLE', reservationState: 'RESERVED' }),
  );

  // The overlay record is closed and fail-closed values are untouched.
  assert.deepEqual({ ...layout.RESERVATION_STATE_VISUAL }, {
    UNRESERVED: 'available',
    RESERVED: 'reserved',
    PROTECTED: 'reserved',
    BACKUP: 'backup',
  });
  assert.deepEqual({ ...layout.PHYSICAL_STATE_VISUAL }, {
    AVAILABLE: 'available',
    OCCUPIED: 'occupied',
    UNKNOWN: 'unknown',
    MAINTENANCE: 'maintenance',
    UNAVAILABLE: 'unavailable',
  });

  // A backup hold is never bookable, and a non-AVAILABLE physical axis still wins.
  assert.equal(layout.isSlotBookable({ physicalState: 'AVAILABLE', reservationState: 'BACKUP' }), false);
  assert.equal(layout.mapSlotVisualState({ physicalState: 'OCCUPIED', reservationState: 'BACKUP' }), 'occupied');
  assert.equal(layout.mapSlotVisualState({ physicalState: 'BROKEN', reservationState: 'BACKUP' }), 'unknown');
});

test('the visual mapper fails closed for null, undefined, unknown and missing axes', () => {
  assert.equal(layout.mapSlotVisualState(null), 'unknown');
  assert.equal(layout.mapSlotVisualState(undefined), 'unknown');
  assert.equal(layout.mapSlotVisualState({}), 'unknown');
  assert.equal(layout.mapSlotVisualState({ physicalState: 'BROKEN', reservationState: 'UNRESERVED' }), 'unknown');
  assert.equal(layout.mapSlotVisualState({ physicalState: 'AVAILABLE', reservationState: 'BROKEN' }), 'unknown');
  assert.equal(layout.mapSlotVisualState({ physicalState: undefined, reservationState: undefined }), 'unknown');
});

test('staleness uses the fixture timestamp and server threshold, never Date.now()', () => {
  const freshNow = Date.parse(fresh.generatedAt) + 5_000;
  const staleNow = Date.parse(stale.generatedAt) + 1_000;
  assert.equal(layout.isLayoutStale(fresh, freshNow), false);
  assert.equal(layout.isLayoutStale(stale, staleNow), true);
  // The stale file would still be stale even if the server lied about freshness.
  assert.equal(layout.isLayoutStale({ ...stale, isFresh: true }, Date.parse(fresh.generatedAt) + 1_000), true);
  // 31s after a server threshold of 30s is stale even when isFresh says true.
  assert.equal(layout.isLayoutStale({ ...fresh, isFresh: true }, Date.parse(fresh.generatedAt) + 31_000), true);
});

test('a missing, zero or invalid staleness threshold always fails closed', () => {
  const fixedNow = Date.parse(fresh.generatedAt) + 1_000;
  for (const threshold of [undefined, null, 0, -1, NaN, Infinity]) {
    assert.equal(
      layout.isLayoutStale({ ...fresh, isFresh: true, stalenessThresholdSeconds: threshold }, fixedNow),
      true,
      `threshold ${String(threshold)}`,
    );
  }
  const { stalenessThresholdSeconds, ...absent } = fresh;
  assert.equal(layout.isLayoutStale(absent, fixedNow), true);
  assert.equal(layout.isLayoutStale(null, fixedNow), true);
});

test('booking eligibility is true only for AVAILABLE + UNRESERVED when the server flag is absent', () => {
  for (const physicalState of PHYSICAL) {
    for (const reservationState of RESERVATION) {
      assert.equal(
        layout.isSlotBookable({ physicalState, reservationState }),
        physicalState === 'AVAILABLE' && reservationState === 'UNRESERVED',
        `${physicalState} + ${reservationState}`,
      );
    }
  }
  assert.equal(layout.isSlotBookable(null), false);
  assert.equal(layout.isSlotBookable(undefined), false);
  assert.equal(layout.isSlotBookable({ physicalState: 'BROKEN', reservationState: 'UNRESERVED' }), false);
  assert.equal(layout.isSlotBookable({ physicalState: 'AVAILABLE', reservationState: 'UNRESERVED', isBookable: false }), false);
});

test('slotCode matching is exact and raw, including the Blender-suffixed fixture codes', () => {
  const codes = fresh.slots.map(slot => slot.slotCode);
  assert.ok(codes.includes('CarParkingSpace_008 2'));
  assert.ok(codes.includes('CarParkingSpace_012 3'));

  assert.equal(layout.matchSlotCode(codes, 'CarParkingSpace_001'), 'exact');
  assert.equal(layout.matchSlotCode(codes, 'CarParkingSpace_008 2'), 'exact');
  assert.equal(layout.matchSlotCode(codes, 'CarParkingSpace_012 3'), 'exact');
  assert.equal(layout.matchSlotCode(codes, 'CarParkingSpace_008'), 'none');
  assert.equal(layout.matchSlotCode(codes, 'carparkingspace_008'), 'none');
  assert.equal(layout.matchSlotCode(codes, 'carparkingspace_008 2'), 'none');
});

test('matchSlotCodes reports unmatched meshes and unmatched contract codes separately', () => {
  const codes = fresh.slots.map(slot => slot.slotCode);
  const report = layout.matchSlotCodes([...codes, 'Stray_Mesh'], [...codes, 'Missing_Slot']);
  assert.deepEqual([...report.unmatchedMeshNames], ['Stray_Mesh']);
  assert.deepEqual([...report.unmatchedSlotCodes], ['Missing_Slot']);
  assert.equal(report.matched.get('CarParkingSpace_008 2'), 'CarParkingSpace_008 2');
  assert.equal(report.matched.has('CarParkingSpace_008'), false);
});

function fakeThree() {
  let nextId = 1;
  class Vector3 {
    constructor(x = 0, y = 0, z = 0) { this.x = x; this.y = y; this.z = z; }
    set(x, y, z) { this.x = x; this.y = y; this.z = z; return this; }
  }
  class Object3D {
    constructor(name = '') {
      this.id = nextId++;
      this.name = name;
      this.children = [];
      this.userData = {};
      this.position = new Vector3();
    }
    add(child) { this.children.push(child); return this; }
    traverse(callback) {
      callback(this);
      for (const child of this.children) child.traverse(callback);
    }
    getWorldPosition(target) {
      return target.set(this.position.x, this.position.y, this.position.z);
    }
  }
  class Mesh extends Object3D {}
  return { Vector3, Object3D, Mesh };
}

test('mapModelSlots binds only exact raw slotCodes and never invents slot-auto placeholders', () => {
  const THREE = fakeThree();
  const slotMatcher = load('lib/3d/slotMatcher.ts', {
    three: THREE,
    '@/lib/operationalLayout': layout,
  });

  const scene = new THREE.Object3D('root');
  const meshA = new THREE.Mesh('CarParkingSpace_008 2');
  const meshB = new THREE.Mesh('CarParkingSpace_012 3');
  const stray = new THREE.Mesh('CarParkingSpace_008');
  scene.add(meshA); scene.add(meshB); scene.add(stray);
  const slots = [
    { id: 'slot-a', number: 'CarParkingSpace_008 2', floor: 1, status: 'available' },
    { id: 'slot-b', number: 'CarParkingSpace_012 3', floor: 1, status: 'available' },
  ];

  const result = slotMatcher.mapModelSlots(scene, slots);
  assert.deepEqual([...result.detectedSlotKeys], ['CarParkingSpace_008 2', 'CarParkingSpace_012 3']);
  assert.equal(result.meshBySlotKey.get('CarParkingSpace_008 2'), meshA);
  assert.equal(result.meshBySlotKey.get('CarParkingSpace_012 3'), meshB);
  assert.equal(result.meshBySlotKey.has('CarParkingSpace_008'), false);
  assert.deepEqual([...result.unmatchedMeshNames], ['CarParkingSpace_008']);
  assert.deepEqual([...result.unmatchedSlotCodes], []);
  assert.equal(result.slotByObjectId.get(meshA.id).id, 'slot-a');
  assert.equal(result.slotByObjectId.has(stray.id), false);
  assert.equal(result.allSlotMeshes.length, 3);
  for (const slot of result.slotByObjectId.values()) {
    assert.equal(/^slot-auto-/.test(slot.id), false);
  }

  assert.equal(slotMatcher.findSlotMesh(result.meshBySlotKey, 'CarParkingSpace_008 2'), meshA);
  assert.equal(slotMatcher.findSlotMesh(result.meshBySlotKey, { number: 'CarParkingSpace_012 3' }), meshB);
  assert.equal(slotMatcher.findSlotMesh(result.meshBySlotKey, 'CarParkingSpace_008'), null);
  assert.equal(slotMatcher.findSlotMesh(result.meshBySlotKey, 'carparkingspace_008 2'), null);
  assert.equal(slotMatcher.findSlotMesh(result.meshBySlotKey, '008'), null);
});

test('an empty slot array leaves every mesh unmatched instead of fabricating slots', () => {
  const THREE = fakeThree();
  const slotMatcher = load('lib/3d/slotMatcher.ts', {
    three: THREE,
    '@/lib/operationalLayout': layout,
  });
  const scene = new THREE.Object3D('root');
  scene.add(new THREE.Mesh('CarParkingSpace_001'));
  scene.add(new THREE.Mesh('CarParkingSpace_002'));

  const result = slotMatcher.mapModelSlots(scene, []);
  assert.deepEqual([...result.detectedSlotKeys], []);
  assert.equal(result.meshBySlotKey.size, 0);
  assert.equal(result.slotByObjectId.size, 0);
  assert.equal(result.allSlotMeshes.length, 2);
  assert.deepEqual([...result.unmatchedSlotCodes], []);
  assert.deepEqual([...result.unmatchedMeshNames].sort(), ['CarParkingSpace_001', 'CarParkingSpace_002']);
});

const operatorApiImports = {
  './authApi': { request: async () => ({ success: true, data: null }) },
  './store': { store: { getLots: () => [] } },
  './3d/ownerModelStorage': { loadOwnerModelUrl: async () => null },
  './operationalLayout': layout,
};

const operatorApiReplacements = {
  'import.meta.env.VITE_RESERVATION_API_BASE_URL': 'undefined',
  'import.meta.env.VITE_PARKING_API_BASE_URL': 'undefined',
};

test('operator sites map the authoritative projection and keep both axes on OCCUPIED + PROTECTED', async () => {
  const operatorApi = load('lib/operatorApi.ts', {
    ...operatorApiImports,
    './parkingApi': {
      operatorsApi: { assignments: async () => [{ siteId: fresh.siteId, tenantId: 'tenant-1', permissions: ['SLOT_OVERRIDE'] }] },
      parkingApi: {
        list: async () => { throw new Error('operator path must not call parkingApi.list'); },
        structure: async () => { throw new Error('operator path must not call parkingApi.structure'); },
        operationalLayout: async id => { assert.equal(id, fresh.siteId); return fresh; },
      },
    },
  }, operatorApiReplacements);

  const sites = await operatorApi.loadOperatorSites({ id: 'operator-1', role: 'operator' });
  assert.equal(sites.length, 1);
  const site = sites[0];
  assert.equal(site.id, fresh.siteId);
  assert.equal(site.tenantId, 'tenant-1');
  assert.equal(site.code, fresh.siteCode);
  assert.equal(site.name, fresh.siteName);
  assert.equal(site.totalSlots, fresh.slots.length);
  assert.equal(site.floors, fresh.floors.length);

  const protectedSlot = site.slots.find(slot => slot.number === 'CarParkingSpace_007');
  assert.equal(protectedSlot.physicalState, 'OCCUPIED');
  assert.equal(protectedSlot.reservationState, 'PROTECTED');
  assert.equal(protectedSlot.status, 'occupied');
  assert.equal(protectedSlot.isBookable, false);
  assert.equal(protectedSlot.isPhysicallyOccupied, true);
  assert.equal(protectedSlot.operationalStatus, 'OPERATIONAL');

  const unknownSlot = site.slots.find(slot => slot.number === 'CarParkingSpace_009');
  assert.equal(unknownSlot.physicalState, 'UNKNOWN');
  assert.equal(unknownSlot.status, 'disabled');
  assert.notEqual(unknownSlot.status, 'available');

  const bookableSlot = site.slots.find(slot => slot.number === 'CarParkingSpace_021');
  assert.equal(bookableSlot.isBookable, true);
  assert.equal(bookableSlot.status, 'available');
});

// F3: the owner path now reads the same authoritative projection as the operator
// path, and only borrows site metadata (address/lat/lng/isActive/tenantId) from
// `parkingApi.list()`.
test('owner sites read the operational-layout projection too, keeping list() metadata', async () => {
  const ownerSite = {
    id: fresh.siteId, tenantId: 'tenant-1', code: fresh.siteCode, name: fresh.siteName,
    address: '123 Owner Street', latitude: 10.5, longitude: 106.5,
    status: 'ACTIVE', isActive: true, totalPhysicalCapacity: 48,
  };
  let structureCalls = 0;
  const operatorApi = load('lib/operatorApi.ts', {
    ...operatorApiImports,
    './parkingApi': {
      operatorsApi: { assignments: async () => { throw new Error('owner path must not read assignments'); } },
      parkingApi: {
        list: async () => [ownerSite],
        structure: async () => { structureCalls += 1; throw new Error('owner path must not call parkingApi.structure'); },
        operationalLayout: async id => { assert.equal(id, fresh.siteId); return fresh; },
      },
    },
  }, operatorApiReplacements);

  const sites = await operatorApi.loadOperatorSites({ id: 'owner-1', role: 'owner' });
  assert.equal(structureCalls, 0);
  assert.equal(sites.length, 1);
  const site = sites[0];

  // metadata comes from list(), not from the projection
  assert.equal(site.tenantId, 'tenant-1');
  assert.equal(site.address, '123 Owner Street');
  assert.equal(site.latitude, 10.5);
  assert.equal(site.longitude, 106.5);
  assert.equal(site.isActive, true);
  assert.equal(site.code, fresh.siteCode);
  assert.equal(site.name, fresh.siteName);

  // axes + freshness come from the projection
  assert.equal(site.totalSlots, fresh.slots.length);
  assert.equal(site.floors, fresh.floors.length);
  assert.ok(site.freshness);
  assert.equal(site.freshness.generatedAt, fresh.generatedAt);
  assert.equal(site.freshness.stalenessThresholdSeconds, fresh.stalenessThresholdSeconds);

  const backup = site.slots.find(slot => slot.number === 'CarParkingSpace_004');
  assert.equal(backup.physicalState, 'AVAILABLE');
  assert.equal(backup.reservationState, 'BACKUP');
  assert.equal(backup.isBookable, false);

  const unknownSlot = site.slots.find(slot => slot.number === 'CarParkingSpace_009');
  assert.equal(unknownSlot.physicalState, 'UNKNOWN');
  assert.notEqual(unknownSlot.status, 'available');
});

function loadParkingApi(flag, requestImpl) {
  const calls = [];
  const api = load('lib/parkingApi.ts', {
    './authApi': { request: (...args) => { calls.push(args); return requestImpl(...args); } },
    './operationalLayout': {
      readOperationalLayoutFixture: async () => (flag ? fresh : null),
    },
  }, {
    'import.meta.env.VITE_PARKING_API_BASE_URL': 'undefined',
  }).parkingApi;
  return { api, calls };
}

test('the feature flag serves the local fixture without issuing HTTP', async () => {
  let called = false;
  const { api, calls } = loadParkingApi(true, async () => {
    called = true;
    throw new Error('fixture mode must not use the network');
  });
  const projection = await api.operationalLayout(fresh.siteId);
  assert.equal(called, false);
  assert.equal(calls.length, 0);
  assert.deepEqual(projection, fresh);
});

test('with the flag off operationalLayout unwraps the real envelope and passes filters', async () => {
  const { api, calls } = loadParkingApi(false, async () => ({ success: true, data: fresh }));
  const projection = await api.operationalLayout(fresh.siteId);
  assert.deepEqual(projection, fresh);
  assert.equal(calls.length, 1);
  assert.equal(calls[0][0], `/${fresh.siteId}/operational-layout`);
  assert.equal(calls[0][1], 'GET');
  assert.equal(calls[0][3], '/api/parking-lots');

  await api.operationalLayout(fresh.siteId, { vehicleType: 'CAR', floor: 'Floor_G' });
  assert.equal(calls[1][0], `/${fresh.siteId}/operational-layout?vehicleType=CAR&floor=Floor_G`);
});

test('operationalLayout throws when the envelope reports success: false', async () => {
  const { api } = loadParkingApi(false, async () => ({ success: false, data: fresh }));
  await assert.rejects(api.operationalLayout(fresh.siteId), /Parking request failed\./);
});
