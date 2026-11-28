'use strict';

// SPARK-395 + SPARK-394 regression tests.
//
// SPARK-395: the operator zone dropdown must bind each option to the zone id
// (`value={z.id}`), not `z.name.slice(0, 1)`. The old value selected the first
// letter of the zone name, which matched no unit id and filtered the whole site
// down to 0/48 slots. Selecting the real zone id must keep all 48 DEMO-HQ
// "Zone A" slots visible.
//
// SPARK-394: the slot search box must match either the raw slot number or its
// human-readable label (formatSlotLabel), so searching "CAR-1" finds
// CarParkingSpace_001.

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
    ...globals,
    require(name) {
      if (name in imports) return imports[name];
      throw new Error(`Unexpected import '${name}' in ${relativePath}`);
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}

// Minimal, index-stable react shim. State and refs persist across renders; the
// caller decides when to flush the captured effects (so a filter test can read
// filteredSlots with and without the reconciling effects applied).
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

function slotFixtures(count, unitId, prefix) {
  return Array.from({ length: count }, (_, index) => ({
    id: `${prefix}-slot-${index + 1}`,
    number: `${prefix}-${String(index + 1).padStart(2, '0')}`,
    unitId,
    vehicleType: 'car',
    status: 'available',
  }));
}

// DEMO-HQ: one FLOOR holding one ZONE ("Zone A") with 48 car slots.
const DEMO_HQ = {
  id: 'demo-hq',
  name: 'DEMO-HQ',
  floors: 1,
  units: [
    { id: 'floor-g', type: 'FLOOR', name: 'Ground', parentId: null },
    { id: 'zone-a', type: 'ZONE', name: 'Zone A', parentId: 'floor-g' },
  ],
  slots: slotFixtures(48, 'zone-a', 'A'),
};

// A site whose slot numbers exercise the raw-vs-label search paths. The slots
// are stored as raw asset names: their human label ("CAR-001" / "EV-001") is
// only reachable through formatSlotLabel, never through the raw string.
const SEARCH_SITE = {
  id: 'demo-hq-search',
  name: 'DEMO-HQ Search',
  floors: 1,
  units: [{ id: 'floor-g', type: 'FLOOR', name: 'Ground', parentId: null }],
  slots: [
    { id: 's-car', number: 'CarParkingSpace_001', unitId: 'floor-g', vehicleType: 'car', status: 'available' },
    { id: 's-ev', number: 'PARKING_EV_CAR_001', unitId: 'floor-g', vehicleType: 'car', status: 'available' },
    { id: 's-other', number: 'B-099', unitId: 'floor-g', vehicleType: 'car', status: 'available' },
  ],
};

function loadUseLotStatus(harness) {
  const operationalLayout = loadModule('lib/operationalLayout.ts');
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
  return { module, operationalLayout };
}

test('formatSlotLabel renders the human label that search must match', () => {
  const operationalLayout = loadModule('lib/operationalLayout.ts');
  assert.equal(operationalLayout.formatSlotLabel('CarParkingSpace_001'), 'CAR-001');
  assert.equal(operationalLayout.formatSlotLabel('PARKING_EV_CAR_007'), 'EV-007');
  assert.equal(operationalLayout.formatSlotLabel('CAR-001'), 'CAR-001');
});

test('SPARK-395: selecting DEMO-HQ "Zone A" keeps all 48 slots visible (48/48, not 0/48)', () => {
  const harness = createReactHarness();
  const { module } = loadUseLotStatus(harness);

  let rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  assert.equal(rendered.result.allSlots.length, 48, 'the site has 48 slots');
  assert.deepEqual(
    rendered.result.availableZones.map(zone => ({ id: zone.id, name: zone.name })),
    [{ id: 'zone-a', name: 'Zone A' }],
    'the ZONE unit is exposed by id',
  );

  // Selecting the zone by its real id (the fixed dropdown value) keeps everything.
  rendered.result.setSelectedZone('zone-a');
  rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  assert.equal(rendered.result.selectedZone, 'zone-a');
  assert.equal(rendered.result.filteredSlots.length, 48, 'selecting Zone A must show 48/48 slots');

  // The legacy `z.name.slice(0,1)` value ("Z") matches no unit id and empties the
  // board — this is the bug SPARK-395 fixes by binding the option to `z.id`.
  rendered.result.setSelectedZone('Z');
  rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  assert.equal(rendered.result.filteredSlots.length, 0, 'the first-letter value filters everything out');
});

test('useLotStatus resets a stale zone selection to "all" when the zone is no longer offered', () => {
  const harness = createReactHarness();
  const { module } = loadUseLotStatus(harness);

  let rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  // Flush the reconciling effects once on mount (no-op for selectedZone === 'all').
  rendered.effects.forEach(effect => effect());

  rendered.result.setSelectedZone('ghost-zone');
  rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  assert.equal(rendered.result.selectedZone, 'ghost-zone');
  // The zone-validity effect must fall back to "all" rather than show 0 slots.
  rendered.effects.forEach(effect => effect());

  rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  assert.equal(rendered.result.selectedZone, 'all', 'an unknown zone falls back to all');
  assert.equal(rendered.result.filteredSlots.length, 48);
});

test('useLotStatus resets the zone selection when the operator switches sites', () => {
  const harness = createReactHarness();
  const { module } = loadUseLotStatus(harness);

  let rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  rendered.effects.forEach(effect => effect());
  rendered.result.setSelectedZone('zone-a');
  rendered = harness.render(() => module.useLotStatus(DEMO_HQ, '', 'all', {}));
  assert.equal(rendered.result.selectedZone, 'zone-a');

  // Switching to another site must not carry the previous site's zone forward.
  rendered = harness.render(() => module.useLotStatus(SEARCH_SITE, '', 'all', {}));
  rendered.effects.forEach(effect => effect());
  rendered = harness.render(() => module.useLotStatus(SEARCH_SITE, '', 'all', {}));
  assert.equal(rendered.result.selectedZone, 'all', 'a site switch clears the zone selection');
});

test('SPARK-394: the slot search matches raw numbers, labels, case and whitespace', () => {
  const harness = createReactHarness();
  const { module } = loadUseLotStatus(harness);

  const search = query => {
    let rendered = harness.render(() => module.useLotStatus(SEARCH_SITE, '', 'all', {}));
    rendered.result.setSearchQuery(query);
    rendered = harness.render(() => module.useLotStatus(SEARCH_SITE, '', 'all', {}));
    return rendered.result.filteredSlots.map(slot => slot.number);
  };

  assert.deepEqual(search('CarParkingSpace_001'), ['CarParkingSpace_001'], 'raw generated slot code matches');
  // Only the label path can connect "CAR-001" to the generated code.
  assert.deepEqual(search('CAR-001'), ['CarParkingSpace_001'], 'the formatted car label matches');
  assert.deepEqual(search('EV-001'), ['PARKING_EV_CAR_001'], 'the formatted EV label matches');
  // Case and surrounding whitespace are normalised before matching.
  assert.deepEqual(search('  car-001  '), ['CarParkingSpace_001'], 'trim + lowercase');
  assert.deepEqual(search('b-099'), ['B-099']);
  assert.deepEqual(search('nomatch'), []);
});

test('SPARK-395: LotStatus.tsx binds each zone option to the zone id (not its first letter)', () => {
  const source = fs.readFileSync(
    path.join(__dirname, '../../FE/src/roles/operator/parking-lot/LotStatus.tsx'),
    'utf8',
  );

  assert.match(source, /<option\s+key=\{z\.id\}\s+value=\{z\.id\}/, 'option binds key and value to z.id');
  assert.doesNotMatch(source, /z\.name\.slice\(0,\s*1\)/, 'the legacy first-letter value is gone');
});
