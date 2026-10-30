const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function load(file, imports = {}) {
  const source = fs
    .readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8')
    .replace(/import\.meta\.env/g, '({})');
  const js = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
      jsx: ts.JsxEmit.ReactJSX,
    },
  }).outputText;
  const context = {
    exports: {},
    Error,
    console,
    require: name => {
      if (!(name in imports)) throw new Error(`Unexpected import: ${name}`);
      return imports[name];
    },
  };
  vm.runInNewContext(js, context);
  return context.exports;
}

const layout = load('lib/operationalLayout.ts');
const config = load('lib/3d/parking3DConfig.ts');
const materials = load('lib/3d/slotMaterialManager.ts', {
  three: {},
  './parking3DConfig': config,
  '../operationalLayout': layout,
});
const banner = load('components/ui/StaleDataBanner.tsx', {
  'react/jsx-runtime': { jsx: (type, props) => ({ type, props }), jsxs: (type, props) => ({ type, props }) },
  '../icon/UntitledIcon': { UntitledIcon: 'icon' },
});
const lotStatus = load('roles/operator/parking-lot/useLotStatus.ts', {
  react: {
    useCallback: fn => fn,
    useEffect: () => {},
    useMemo: fn => fn(),
    useRef: value => ({ current: value }),
    useState: initial => [typeof initial === 'function' ? initial() : initial, () => {}],
  },
  '../../../lib/operationalLayout': layout,
  '../../../lib/parkingApi': { parkingApi: { operationalLayout: async () => null } },
});

const VISUAL_STATES = ['available', 'occupied', 'reserved', 'backup', 'maintenance', 'unavailable', 'unknown'];
const generatedAt = '2026-10-26T12:00:00.000Z';
const generatedMs = Date.parse(generatedAt);

test('the 3D vocabulary covers all seven visual states with matching number/hex values', () => {
  for (const state of VISUAL_STATES) {
    const num = config.SLOT_VISUAL_STATE_COLORS[state];
    assert.equal(typeof num, 'number', `${state} must have a numeric colour`);
    assert.equal(config.SLOT_VISUAL_STATE_HEX[state], `#${num.toString(16).padStart(6, '0')}`);
    assert.equal(typeof config.SLOT_VISUAL_STATE_LABELS[state], 'string', `${state} must have a label`);
  }
  assert.equal(typeof config.SLOT_VISUAL_STATE_COLORS.selected, 'number');
  assert.equal(typeof config.SLOT_VISUAL_STATE_COLORS.default, 'number');
});

test('backup reads as its own state, never as reserved amber or unknown', () => {
  const colors = config.SLOT_VISUAL_STATE_COLORS;
  assert.equal(colors.backup, 0x0891b2);
  assert.equal(config.SLOT_VISUAL_STATE_HEX.backup, '#0891b2');
  assert.equal(config.SLOT_VISUAL_STATE_LABELS.backup, 'Backup');

  // BACKUP is a separate appearance from every state it could be confused with.
  for (const other of ['reserved', 'occupied', 'available', 'maintenance', 'unknown', 'unavailable', 'selected']) {
    assert.notEqual(colors.backup, colors[other], `backup must differ from ${other}`);
    assert.notEqual(config.SLOT_VISUAL_STATE_HEX.backup, config.SLOT_VISUAL_STATE_HEX[other]);
  }

  // The reservation overlay keeps reserved/protected on amber and moves only BACKUP.
  assert.equal(layout.RESERVATION_STATE_VISUAL.RESERVED, 'reserved');
  assert.equal(layout.RESERVATION_STATE_VISUAL.PROTECTED, 'reserved');
  assert.equal(layout.RESERVATION_STATE_VISUAL.BACKUP, 'backup');
  assert.notEqual(
    config.SLOT_VISUAL_STATE_HEX[layout.RESERVATION_STATE_VISUAL.BACKUP],
    config.SLOT_VISUAL_STATE_HEX[layout.RESERVATION_STATE_VISUAL.RESERVED],
  );
});

test('unknown cannot be mistaken for available, maintenance or unavailable', () => {
  const colors = config.SLOT_VISUAL_STATE_COLORS;
  assert.notEqual(colors.unknown, colors.available);
  assert.notEqual(colors.unknown, colors.maintenance);
  assert.notEqual(colors.unknown, colors.unavailable);
  assert.notEqual(colors.unavailable, colors.maintenance);
  assert.notEqual(colors.unavailable, colors.available);
});

test('slot material resolution fails closed for missing and drifted values', () => {
  const resolve = materials.resolveSlotVisualState;
  assert.equal(resolve(null), 'unknown');
  assert.equal(resolve(undefined), 'unknown');
  assert.equal(resolve({}), 'unknown');
  assert.equal(resolve({ physicalState: 'BROKEN', reservationState: 'UNRESERVED' }), 'unknown');
  assert.equal(resolve({ physicalState: 'AVAILABLE', reservationState: 'BROKEN' }), 'unknown');
  assert.equal(resolve({ physicalState: 'UNKNOWN', reservationState: 'UNRESERVED' }), 'unknown');
  assert.equal(resolve({ physicalState: 'UNAVAILABLE', reservationState: 'RESERVED' }), 'unavailable');
  assert.equal(resolve({ physicalState: 'AVAILABLE', reservationState: 'BACKUP' }), 'backup');
  assert.equal(resolve({ physicalState: 'AVAILABLE', reservationState: 'RESERVED' }), 'reserved');
  assert.equal(resolve({ physicalState: 'AVAILABLE', reservationState: 'PROTECTED' }), 'reserved');
  // A physical state other than AVAILABLE still wins over the reservation hold.
  assert.equal(resolve({ physicalState: 'OCCUPIED', reservationState: 'BACKUP' }), 'occupied');
  assert.equal(resolve({ physicalState: 'OCCUPIED', reservationState: 'RESERVED' }), 'occupied');
});

test('legacy single-axis payloads stay readable but fail closed when unrecognised', () => {
  const resolve = materials.resolveSlotVisualState;
  assert.equal(resolve({ status: 'available' }), 'available');
  assert.equal(resolve({ status: 'occupied' }), 'occupied');
  assert.equal(resolve({ status: 'reserved' }), 'reserved');
  assert.equal(resolve({ status: 'maintenance' }), 'maintenance');
  assert.equal(resolve({ status: 'disabled' }), 'unavailable');
  assert.equal(resolve({ status: 'not-a-status' }), 'unknown');
  assert.equal(resolve({ status: '' }), 'unknown');
});

test('staleness uses the server threshold and fails closed on a bad snapshot', () => {
  const fresh = { generatedAt, isFresh: true, stalenessThresholdSeconds: 300 };
  assert.equal(layout.isLayoutStale(fresh, generatedMs + 299000), false);
  assert.equal(layout.isLayoutStale(fresh, generatedMs + 301000), true);

  const narrow = { ...fresh, stalenessThresholdSeconds: 30 };
  assert.equal(layout.isLayoutStale(narrow, generatedMs + 29000), false);
  assert.equal(layout.isLayoutStale(narrow, generatedMs + 31000), true);

  assert.equal(layout.isLayoutStale(null, generatedMs), true);
  assert.equal(layout.isLayoutStale({ ...fresh, isFresh: false }, generatedMs), true);
  assert.equal(layout.isLayoutStale({ ...fresh, stalenessThresholdSeconds: 0 }, generatedMs), true);
  assert.equal(layout.isLayoutStale({ ...fresh, generatedAt: 'not-a-date' }, generatedMs), true);
  assert.equal(layout.layoutAgeSeconds(fresh, generatedMs + 120000), 120);
  assert.equal(layout.layoutAgeSeconds(null, generatedMs), null);
});

test('banner copy is factual and the dim is exactly 30 percent', () => {
  assert.equal(banner.STALE_DIM_OPACITY, 0.7);
  assert.equal(banner.staleBannerMessage(120), 'Live view is stale — last update 2 min ago');
  assert.match(banner.staleBannerMessage(null), /age is unknown/);
  assert.equal(banner.formatLayoutAge(12), '12s');
  assert.equal(banner.formatLayoutAge(120), '2 min');
  assert.equal(banner.formatLayoutAge(7200), '2 h');
  assert.equal(banner.formatLayoutAge(172800), '2 d');
  assert.equal(banner.formatLayoutAge(null), 'unknown');
});

const baseSlot = {
  id: 's1',
  number: 'A-001',
  floor: 1,
  status: 'available',
  unitId: 'u1',
  vehicleType: 'CAR',
  operationalStatus: 'OPERATIONAL',
  isPhysicallyOccupied: false,
  reservationState: null,
};

test('two-axis filter predicates extend the existing record pattern', () => {
  const predicates = lotStatus.createStatusFilterPredicates(() => undefined);

  assert.equal(predicates.available(baseSlot), true);
  assert.equal(predicates.unknown(baseSlot), false);
  assert.equal(predicates.occupied(baseSlot), false);
  assert.equal(predicates.maintenance(baseSlot), false);
  assert.equal(predicates.unavailable(baseSlot), false);
  assert.equal(predicates.backup(baseSlot), false);

  const unknownSlot = { ...baseSlot, operationalStatus: 'UNKNOWN' };
  assert.equal(predicates.unknown(unknownSlot), true);
  assert.equal(predicates.available(unknownSlot), false);

  const unavailableSlot = { ...baseSlot, operationalStatus: 'BLOCKED' };
  assert.equal(predicates.unavailable(unavailableSlot), true);

  const disabledSlot = { ...baseSlot, status: 'disabled' };
  assert.equal(predicates.unavailable(disabledSlot), true);

  const backupSlot = { ...baseSlot, status: 'reserved', reservationState: 'BACKUP' };
  assert.equal(predicates.backup(backupSlot), true);
  assert.equal(predicates.reserved(backupSlot), false);
  assert.equal(predicates.available(backupSlot), false);

  const protectedSlot = { ...baseSlot, isPhysicallyOccupied: true, reservationState: 'PROTECTED' };
  assert.equal(predicates.reserved(protectedSlot), true);
  assert.equal(predicates.occupied(protectedSlot), true);
});

test('projection axes override legacy fields and drifted axes fail closed', () => {
  const projectedFor = slot =>
    slot.id === baseSlot.id
      ? { slotId: slot.id, slotCode: slot.number, physicalState: 'UNKNOWN', reservationState: 'UNRESERVED' }
      : undefined;
  const predicates = lotStatus.createStatusFilterPredicates(projectedFor);

  assert.equal(predicates.unknown(baseSlot), true);
  assert.equal(predicates.available(baseSlot), false);

  const axes = lotStatus.resolveSlotAxes(
    { ...baseSlot, physicalState: 'UNAVAILABLE' },
    { physicalState: 'AVAILABLE', reservationState: 'RESERVED' },
  );
  assert.equal(axes.physicalState, 'AVAILABLE');
  assert.equal(axes.reservationState, 'RESERVED');
});

test('layout is read from either the data-lane field name', () => {
  const projection = {
    generatedAt,
    isFresh: true,
    stalenessThresholdSeconds: 30,
    slots: [],
  };
  assert.equal(lotStatus.resolveLotLayout({ operationalLayout: projection }), projection);
  assert.equal(lotStatus.resolveLotLayout({ layout: projection }), projection);
  assert.equal(lotStatus.resolveLotLayout(null), null);
});
