'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const ts = require('../../FE/node_modules/typescript');

// Transpile a FE/src module to CommonJS and run it in an isolated VM context,
// resolving imports from the supplied stub map (mirrors the other frontend tests).
function load(file, imports = {}) {
  const source = fs
    .readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8')
    .replace(/import\.meta\.env\.\w+/g, 'undefined');
  const js = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const context = {
    exports: {},
    Error,
    Map,
    Promise,
    JSON,
    console,
    URL: { createObjectURL: () => 'blob:test' },
    sessionStorage: { getItem: () => null, setItem: () => {}, removeItem: () => {} },
    require: name => {
      if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
      return imports[name];
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}

const config = load('lib/3d/parking3DConfig.ts');

test('inferLotType classifies basements from names, slots, floors and units', () => {
  assert.equal(config.inferLotType({ name: 'Bãi xe tầng hầm B1' }), 'basement');
  assert.equal(config.inferLotType({ name: 'Underground Parking' }), 'basement');
  assert.equal(config.inferLotType({ slots: [{ slotCode: 'B1-01' }] }), 'basement');
  assert.equal(config.inferLotType({ slots: [{ number: 'B2-14' }] }), 'basement');
  assert.equal(config.inferLotType({ floors: 2 }), 'basement');
  assert.equal(config.inferLotType({ units: [{ name: 'Hầm B1' }] }), 'basement');
});

test('inferLotType classifies multi-storey lots from names, slots, floors and units', () => {
  assert.equal(config.inferLotType({ name: 'Multi-storey Parking Tower' }), 'multi-storey');
  assert.equal(config.inferLotType({ name: 'Tòa nhà tầng 1-3' }), 'multi-storey');
  assert.equal(config.inferLotType({ slots: [{ slotCode: 'L1-01' }] }), 'multi-storey');
  assert.equal(config.inferLotType({ slots: [{ slotCode: 'PARKING_L2_08' }] }), 'multi-storey');
  assert.equal(config.inferLotType({ floors: 3 }), 'multi-storey');
  assert.equal(config.inferLotType({ units: [{ name: 'L2' }] }), 'multi-storey');
});

test('inferLotType falls back to outdoor for ground-level lots', () => {
  assert.equal(config.inferLotType({}), 'outdoor');
  assert.equal(config.inferLotType({ name: 'Central open car park', floors: 1 }), 'outdoor');
  assert.equal(config.inferLotType({ units: [{ name: 'Khu vực ngoài trời' }] }), 'outdoor');
  assert.equal(config.inferLotType({ units: [{ name: 'Ground' }] }), 'outdoor');
});

test('inferModelUrl returns the bound model path for each lot type', () => {
  assert.equal(config.inferModelUrl('basement'), config.ALLOWED_PARKING_MODELS.basement.path);
  assert.equal(config.inferModelUrl('multi-storey'), config.ALLOWED_PARKING_MODELS['multi-storey'].path);
  assert.equal(config.inferModelUrl('outdoor'), config.ALLOWED_PARKING_MODELS.outdoor.path);
});

test('useSiteForm mounts an edit form without a temporal-dead-zone ReferenceError', () => {
  const state = [];
  const effects = [];
  let cursor = 0;
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
    useEffect(effect) {
      effects.push(effect);
    },
    useMemo(compute) {
      return compute();
    },
    useId() {
      return 'form-id';
    },
    useRef(initial) {
      return { current: initial };
    },
  };

  const site = {
    id: 'site-1',
    code: 'LOT-1',
    name: 'Registered lot',
    address: 'Main Street',
    isActive: true,
    latitude: 10.7,
    longitude: 106.7,
    totalPhysicalCapacity: 48,
  };

  const formModule = load('roles/owner/parking-lots/useSiteForm.ts', {
    react,
    '../../../lib/parkingApi': {
      parkingApi: { list: async () => [], create: async () => ({}), edit: async () => {}, unit: async () => {} },
    },
    '../../../lib/authApi': {
      AuthApiError: class AuthApiError extends Error {
        constructor(message, status) {
          super(message);
          this.status = status;
        }
      },
    },
    '../data/data': { ownerData: { getLotsByOwner: () => [] } },
    '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-1' } }) },
    '../../../lib/3d/parking3DConfig': {
      ALLOWED_PARKING_MODELS: {
        outdoor: { path: '/models/parking/outdoor_parking_lot.glb', fileName: 'outdoor_parking_lot.glb' },
        basement: { path: '/models/parking/underground_parking_lot.glb', fileName: 'underground_parking_lot.glb' },
        'multi-storey': { path: '/models/parking/indoor_parking_lot.glb', fileName: 'indoor_parking_lot.glb' },
      },
      validateModelUpload: () => ({ isValid: true }),
    },
    '../../../lib/3d/modelSlotCapacity': {
      useModelCarCapacity: () => ({ capacity: 48, error: null }),
      useModelStructure: () => ({ analysis: { floors: [] } }),
    },
    '../../../lib/3d/ownerModelStorage': {
      deleteOwnerModel: async () => {},
      loadOwnerModelUrl: async () => null,
      saveOwnerModel: async () => 'blob:test',
    },
    '../../../lib/vietnamAdministrativeUnits': {
      CITIES: [{ code: 'HCM', name: 'HCM', defaultLat: 10.7, defaultLng: 106.7 }],
    },
    '../../../components/ui/ParkingLocationPicker': { validParkingCoordinates: () => true },
  });

  let result;
  assert.doesNotThrow(() => {
    result = formModule.useSiteForm(site, () => {}, () => {});
  });
  assert.equal(result.form.name, 'Registered lot');
  assert.equal(result.form.code, 'LOT-1');
  assert.equal(result.lotType, 'outdoor');

  // The effects (e.g. the auto-calc that previously read `form` in the TDZ) must run cleanly.
  assert.doesNotThrow(() => {
    effects.splice(0).forEach(effect => effect());
  });
});
