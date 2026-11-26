const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

const fixturesDir = path.join(__dirname, '../../FE/src/fixtures');
const envelope = JSON.parse(fs.readFileSync(path.join(fixturesDir, 'driver-discovery-fixture.json'), 'utf8'));

/**
 * Same harness as operational-layout-adapter.test.cjs: transpile the real TS
 * module to CommonJS, run it in a vm, and stub every import by name.
 */
function load(file, imports, replacements = {}, globals = {}) {
  let source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8')
    .replace(/import\.meta\.env\.\w+/g, 'undefined');
  for (const [from, to] of Object.entries(replacements)) source = source.split(from).join(to);
  const js = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX },
  }).outputText;
  const context = {
    exports: {},
    Error,
    Promise,
    ...globals,
    require: name => {
      if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
      return imports[name];
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}

const api = load('lib/driverDiscoveryApi.ts', {
  './authApi': { request: async () => { throw new Error('network must not be used in unit tests'); } },
});

test('parses and maps the discovery response fixture', () => {
  assert.equal(envelope.success, true);
  const { items } = envelope.data;
  assert.equal(items.length, 2);
  for (const item of items) {
    assert.equal(typeof item.siteId, 'string');
    assert.equal(typeof item.coordinates.latitude, 'number');
    assert.equal(typeof item.coordinates.longitude, 'number');
    assert.equal(typeof item.distanceMeters, 'number');
    assert.equal(typeof item.availability.status, 'string');
    assert.equal(typeof item.tariff.currentAmountMinor, 'number');
    assert.equal(item.tariff.currency, 'VND');
  }
});

test('AC-1 manual origin is serialized and required', () => {
  const query = api.buildDiscoveryQuery({
    originSource: 'MANUAL',
    latitude: 10.77,
    longitude: 106.69,
    manualOrigin: 'Ben Thanh',
  });
  assert.ok(query.includes('originSource=MANUAL'));
  assert.ok(query.includes('manualOrigin=Ben%20Thanh'));
  assert.throws(
    () => api.buildDiscoveryQuery({ originSource: 'MANUAL', latitude: 10.77, longitude: 106.69 }),
    /manualOrigin/,
  );
  assert.throws(
    () => api.buildDiscoveryQuery({ originSource: 'MANUAL', latitude: 10.77, longitude: 106.69, manualOrigin: '   ' }),
    /manualOrigin/,
  );
});

test('AC-1 external directions url is deterministic', () => {
  assert.equal(
    api.buildExternalDirectionsUrl(10.7721, 106.6980),
    'https://www.google.com/maps/dir/?api=1&destination=10.7721,106.698',
  );
  assert.equal(
    api.buildExternalDirectionsUrl(10.7721, 106.6980, 'Ben Thanh Tower'),
    'https://www.google.com/maps/dir/?api=1&destination=10.7721,106.698',
  );
});

test('AC-2 handoff context preserves the authoritative siteId', () => {
  const item = envelope.data.items[0];
  const ctx = api.createHandoffContext(item);
  assert.strictEqual(ctx.siteId, item.siteId);
  assert.equal(ctx.siteCode, item.siteCode);
  assert.equal(ctx.layoutApiPath, '/api/parking-lots/' + item.siteId + '/operational-layout');
});

test('serializes every defined filter and omits undefined ones', () => {
  const query = api.buildDiscoveryQuery({
    originSource: 'CURRENT_GPS',
    latitude: 10.77,
    longitude: 106.69,
    vehicleType: 'CAR',
    lotType: 'MULTI_STOREY',
    radiusKm: 5,
    sort: 'PRICE',
    currency: 'VND',
  });
  for (const fragment of ['vehicleType=CAR', 'lotType=MULTI_STOREY', 'radiusKm=5', 'sort=PRICE', 'currency=VND']) {
    assert.ok(query.includes(fragment), fragment);
  }
  assert.ok(!query.includes('manualOrigin='));
  assert.ok(!query.includes('minPrice='));
});

test('fixture mode serves the bundled envelope without touching the network', async () => {
  let networkCalls = 0;
  // The harness replaces `import.meta.env.*` with `undefined` before applying
  // replacements, so the flag condition is `undefined === "true"` at this point.
  const fixtureApi = load('lib/driverDiscoveryApi.ts', {
    '../fixtures/driver-discovery-fixture.json': envelope,
    './authApi': { request: async () => { networkCalls += 1; throw new Error('fixture mode must not use the network'); } },
  }, {
    'undefined === "true"': 'true',
  });

  const result = await fixtureApi.searchDiscoveryLots({
    originSource: 'CURRENT_GPS',
    latitude: 10.77,
    longitude: 106.69,
  });

  assert.equal(networkCalls, 0);
  assert.deepEqual(result, envelope.data);
  assert.equal(result.items.length, 2);
});

const slotsOperationalEnvelope = JSON.parse(
  fs.readFileSync(path.join(fixturesDir, 'slots-operational-fixture.json'), 'utf8'),
);

test('maps an operational-layout projection into a bookable driver lot (Note 3)', () => {
  // Pure mapper only: no imports to stub, so the Node harness loads it directly.
  const mapping = load('lib/driverSlotMapping.ts', {});
  const projection = slotsOperationalEnvelope.data;
  const mapped = mapping.mapOperationalLayoutSlots(projection.slots);

  assert.equal(mapped.length, projection.slots.length);

  // id/number/floor/status come from the contract axes, never formatSlotLabel.
  const first = mapped[0];
  const firstSource = projection.slots[0];
  assert.equal(first.id, firstSource.slotId);
  assert.equal(first.number, firstSource.slotCode);
  assert.equal(first.number, 'CarParkingSpace_001');
  assert.equal(first.floor, 1); // "Floor_G" has no digit -> default 1
  assert.equal(first.status, 'available'); // AVAILABLE + UNRESERVED

  // floorCode digits drive the floor number, default 1.
  assert.equal(mapping.driverSlotFloor('Floor 3'), 3);
  assert.equal(mapping.driverSlotFloor('Floor_G'), 1);
  assert.equal(mapping.driverSlotFloor(undefined), 1);

  // Reservation/physical axes follow the operatorApi fail-closed convention.
  assert.equal(mapped.find((slot) => slot.number === 'CarParkingSpace_002').status, 'reserved');
  assert.equal(mapped.find((slot) => slot.number === 'CarParkingSpace_004').status, 'reserved'); // BACKUP
  assert.equal(mapped.find((slot) => slot.number === 'CarParkingSpace_009').status, 'disabled'); // UNKNOWN

  // A lot resolved from the projection must report availability and be bookable.
  const availableCount = mapped.filter((slot) => slot.status === 'available').length;
  assert.ok(availableCount > 0);

  const lot = {
    id: projection.siteId,
    status: 'active',
    slots: mapped,
    totalSlots: projection.metrics.totalPhysicalCapacity,
  };
  const canBook = lot.status === 'active' && availableCount > 0;
  assert.equal(canBook, true);
});

test('Find Parking resolves registered capacity before opening details, with at most three reads in flight', async () => {
  const source = slotsOperationalEnvelope.data;
  const sites = Array.from({ length: 4 }, (_, index) => ({
    id: `a0000000-0000-4000-8000-00000000000${index}`,
    name: `Site ${index}`, address: 'Address', type: 'outdoor', status: 'active', slots: [],
    totalSlots: 0, lat: 10.7, lng: 106.7,
  }));
  const requests = [];
  const states = [], effects = [];
  let cursor = 0;
  const jsx = (type, props) => ({ type, props });
  const react = {
    useState(initial) {
      const index = cursor++;
      if (!(index in states)) states[index] = typeof initial === 'function' ? initial() : initial;
      return [states[index], next => { states[index] = typeof next === 'function' ? next(states[index]) : next; }];
    },
    useRef(initial) {
      const index = cursor++;
      states[index] ??= { current: initial };
      return states[index];
    },
    useMemo(compute, deps) {
      const index = cursor++, previous = states[index];
      if (!previous || deps.some((dep, i) => !Object.is(dep, previous.deps[i]))) states[index] = { value: compute(), deps };
      return states[index].value;
    },
    useEffect(effect) { effects.push(effect); },
  };
  const { FindParking } = load('roles/driver/parking/FindParking.tsx', {
    react, 'react/jsx-runtime': { jsx, jsxs: jsx },
    '@maptiler/sdk': {}, '@maptiler/sdk/dist/maptiler-sdk.css': {}, animejs: {},
    '../data/data': { driverData: { getLots: () => sites, saveLot() { assert.fail('Preloading must not write to the store'); } } },
    './ParkingDetailsPanel': { ParkingDetailsPanel: 'details' },
    '../../../components/ui/RoundedSelect': { RoundedSelect: 'select' },
    '../../../components/ui/PageHeader': { PageHeader: 'header' },
    '../../../components/icon/UntitledIcon': { UntitledIcon: 'icon' },
    '../../../context/AppContext': { useApp: () => ({ theme: 'light' }) },
    '../../../lib/authApi': { getSessionGeneration: () => 1 },
    '../../../lib/driverDiscoveryApi': api, '../../../lib/routingApi': {},
    '../../../lib/driverSlotMapping': load('lib/driverSlotMapping.ts', {}),
    '../../../lib/parkingCoordinates': load('lib/parkingCoordinates.ts', {}),
    '../../../lib/parkingApi': { parkingApi: { operationalLayout: id => new Promise((resolve, reject) => requests.push({ id, resolve, reject })) } },
  }, {}, { window: { addEventListener() {}, setTimeout() {} }, requestAnimationFrame() {} });
  const render = () => {
    cursor = 0;
    const tree = FindParking({ onBook() {} });
    effects.splice(0).forEach(effect => effect());
    return tree;
  };
  function copy(node) {
    if (Array.isArray(node)) return node.map(copy).join(' ');
    return typeof node === 'object' && node !== null ? copy(node.props?.children) : String(node ?? '');
  }
  const settle = () => new Promise(resolve => setImmediate(resolve));
  assert.match(copy(render()), /Availability unknown/);
  assert.equal(requests.length, 3);
  requests[0].resolve({ ...source, siteId: sites[0].id, slots: source.slots.slice(0, 2) });
  await settle();
  assert.match(copy(render()), /1 spaces available\s+· 2 registered/);
  assert.equal(requests.length, 4);
  requests[1].resolve({ ...source, siteId: sites[1].id, slots: [] });
  requests[2].reject(new Error('offline'));
  requests[3].resolve({ ...source, siteId: sites[3].id, slots: [] });
  await settle();
  const labels = copy(render());
  assert.match(labels, /0 spaces available\s+· 0 registered/);
  assert.match(labels, /Availability unknown/);
  assert.equal(requests.length, 4);
});
