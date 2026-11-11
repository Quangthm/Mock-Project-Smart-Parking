const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const fixturesDir = path.join(__dirname, '../../FE/src/fixtures');
const fixture = JSON.parse(
  fs.readFileSync(path.join(fixturesDir, 'driver-vehicle-location-fixture.json'), 'utf8'),
);

/**
 * Same harness as operational-layout-adapter.test.cjs: transpile the real TS
 * module to CommonJS, run it in a vm, and stub every import by name.
 */
function load(file, imports, replacements = {}) {
  let source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8')
    .replace(/import\.meta\.env\.\w+/g, 'undefined');
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

const authImports = {
  './authApi': {
    request: async () => { throw new Error('network must not be used in unit tests'); },
    AuthApiError: class AuthApiError extends Error {
      constructor(message, status) { super(message); this.status = status; }
    },
  },
};

const api = load('lib/driverVehicleLocationApi.ts', authImports);

test('AC-4 slot precision exposes the exact slot code and floor', () => {
  const data = fixture.slotPrecision.data;
  assert.equal(data.recordedLocation.precision, 'SLOT');
  assert.equal(data.recordedLocation.slotCode, 'B1-A-101');
  assert.equal(data.recordedLocation.floorCode, 'Floor_B1');
  assert.notEqual(data.recordedAt, null);
});

test('AC-5 zone-only precision never surfaces or fabricates a slot', () => {
  const data = fixture.zonePrecision.data;
  assert.equal(data.recordedLocation.precision, 'ZONE');
  assert.equal(data.recordedLocation.slotId, null);
  assert.equal(data.recordedLocation.slotCode, null);

  const lines = api.locationDetailLines(api.normalizeRecordedLocation({
    ...data.recordedLocation,
    slotCode: 'SHOULD-BE-IGNORED',
    slotId: 'fake-id',
  }));
  const joined = lines.join('\n');
  assert.doesNotMatch(joined, /slot/i);
  assert.equal(joined.includes('SHOULD-BE-IGNORED'), false);
  assert.equal(joined.includes('fake-id'), false);
});

test('AC-4 unknown precision nulls every location field and timestamp', () => {
  const data = fixture.unknownPrecision.data;
  assert.equal(data.recordedLocation.precision, 'UNKNOWN');
  assert.equal(data.recordedLocation.slotId, null);
  assert.equal(data.recordedLocation.slotCode, null);
  assert.equal(data.recordedLocation.zoneId, null);
  assert.equal(data.recordedLocation.zoneCode, null);
  assert.equal(data.recordedLocation.floorCode, null);
  assert.equal(data.recordedAt, null);
});

test('AC-9 stale variant reports isStale', () => {
  assert.equal(fixture.staleVariant.data.freshness.isStale, true);
});

test('AC-7/AC-8 allocation status only accepts RESERVED and OCCUPIED', () => {
  assert.equal(api.normalizeAllocationStatus('OCCUPIED'), 'OCCUPIED');
  assert.equal(api.normalizeAllocationStatus('RESERVED'), 'RESERVED');
  assert.equal(api.normalizeAllocationStatus('RELEASED'), null);
  assert.equal(api.normalizeAllocationStatus(null), null);
});

test('fixture mode serves the scenario without any network usage', async () => {
  let requestCalls = 0;
  const offlineApi = load('lib/driverVehicleLocationApi.ts', {
    './authApi': {
      request: async () => {
        requestCalls += 1;
        throw new Error('fixture mode must not use the network');
      },
      AuthApiError: authImports['./authApi'].AuthApiError,
    },
    '../fixtures/driver-vehicle-location-fixture.json': fixture,
  }, {
    'undefined === "true"': 'true',
  });

  const data = await offlineApi.getCurrentVehicleLocation('zonePrecision');
  assert.equal(data.recordedLocation.precision, 'ZONE');
  assert.equal(data.recordedLocation.slotCode, null);
  assert.equal(requestCalls, 0);
});