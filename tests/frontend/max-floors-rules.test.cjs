'use strict';

// Max-floor rules (Phase 1 item 2b).
//
// The lot-type static ceilings are outdoor=1, basement=2, multi-storey=3, and a
// 3D model's floor count lowers the ceiling further. The shared rule lives in
// useSiteForm.validateFloorCount, which the create wizard and site form both
// rely on.

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const ts = require('../../FE/node_modules/typescript');

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

// Same stub map the existing useSiteForm test uses (see
// tests/frontend/spark-238-lot-type-and-siteform.test.cjs). validateFloorCount is
// a pure helper, so the react hooks are never invoked.
const formModule = load('roles/owner/parking-lots/useSiteForm.ts', {
  react: {
    useState: initial => [typeof initial === 'function' ? initial() : initial, () => {}],
    useEffect: () => {},
    useMemo: compute => compute(),
    useId: () => 'form-id',
    useRef: initial => ({ current: initial }),
  },
  '../../../lib/parkingApi': {
    parkingApi: { list: async () => [], create: async () => ({}), edit: async () => {}, unit: async () => {} },
  },
  '../../../lib/authApi': { AuthApiError: class AuthApiError extends Error {} },
  '../data/data': { ownerData: { getLotsByOwner: () => [] } },
  '../../../context/AppContext': { useApp: () => ({ user: { id: 'owner-1' } }) },
  '../../../lib/3d/parking3DConfig': {
    ALLOWED_PARKING_MODELS: {},
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
  '../../../lib/vietnamAdministrativeUnits': { CITIES: [] },
  '../../../components/ui/ParkingLocationPicker': { validParkingCoordinates: () => true },
});

const { LOT_TYPE_INFO, validateFloorCount } = formModule;

test('static lot-type ceilings are outdoor=1, basement=2, multi-storey=3', () => {
  assert.equal(LOT_TYPE_INFO.outdoor.maxFloors, 1);
  assert.equal(LOT_TYPE_INFO.basement.maxFloors, 2);
  assert.equal(LOT_TYPE_INFO['multi-storey'].maxFloors, 3);
});

test('validateFloorCount accepts the valid bounds for each lot type', () => {
  assert.equal(validateFloorCount('outdoor', 1), null);
  assert.equal(validateFloorCount('basement', 1), null);
  assert.equal(validateFloorCount('basement', 2), null);
  assert.equal(validateFloorCount('multi-storey', 1), null);
  assert.equal(validateFloorCount('multi-storey', 2), null);
  assert.equal(validateFloorCount('multi-storey', 3), null);
});

test('validateFloorCount rejects outdoor>1, basement>2 and multi-storey>3', () => {
  assert.notEqual(validateFloorCount('outdoor', 2), null);
  assert.notEqual(validateFloorCount('basement', 3), null);
  assert.notEqual(validateFloorCount('multi-storey', 4), null);
  // The ceiling message carries the lot-type limit.
  assert.match(validateFloorCount('basement', 3), /at most 2 floor/);
  assert.match(validateFloorCount('multi-storey', 4), /at most 3 floor/);
});

test('validateFloorCount rejects non-integer and out-of-range counts', () => {
  assert.notEqual(validateFloorCount('basement', 0), null);
  assert.notEqual(validateFloorCount('basement', -1), null);
  assert.notEqual(validateFloorCount('basement', 1.5), null);
  assert.notEqual(validateFloorCount('basement', Number.NaN), null);
});

test('a 3D model lowers the ceiling below the static lot-type limit', () => {
  // basement allows 2, but the bound model only has 1 floor.
  assert.equal(validateFloorCount('basement', 1, 1), null);
  assert.notEqual(validateFloorCount('basement', 2, 1), null);
  // multi-storey allows 3, but the bound model only has 2 floors.
  assert.equal(validateFloorCount('multi-storey', 2, 2), null);
  assert.notEqual(validateFloorCount('multi-storey', 3, 2), null);
  assert.match(validateFloorCount('multi-storey', 3, 2), /at most 2 floor/);
});
