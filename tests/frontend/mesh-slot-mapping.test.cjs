const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

// Same minimal VM harness as parking-structure-rules.test.cjs: load a TypeScript module by
// transpiling it to CommonJS and running it in an isolated context whose `require` throws.
// This only works because FE/src/lib/3d/meshSlotMapping.ts declares zero runtime imports.
function load(file) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const context = { exports: {}, require: name => { throw new Error(`Unexpected import: ${name}`); } };
  vm.runInNewContext(js, context);
  return context.exports;
}

const mesh = load('lib/3d/meshSlotMapping.ts');

// Synthetic mesh/slot shapes only: the module reads `{ key, name }` candidates and
// `{ id, code }` slots, so no real THREE objects are needed.
const slots = (...codes) => codes.map((code, index) => ({ id: `slot-${index + 1}`, code }));
const candidates = (...names) => names.map((name, index) => ({ key: `mesh-${index + 1}`, name }));

// --- normalizeSlotIdentifier ---------------------------------------------

test('normalizeSlotIdentifier tolerates case, surrounding whitespace and template prefixes', () => {
  assert.equal(mesh.normalizeSlotIdentifier('CarParkingSpace_001'), '001');
  assert.equal(mesh.normalizeSlotIdentifier('carparkingspace 001'), '001');
  assert.equal(mesh.normalizeSlotIdentifier('car_parking_space_001'), '001');
  assert.equal(mesh.normalizeSlotIdentifier('  A-001  '), 'A-001');
  assert.equal(mesh.normalizeSlotIdentifier('a-001'), 'A-001');
  assert.equal(mesh.normalizeSlotIdentifier('Slot_A-001'), 'A-001');
  assert.equal(mesh.normalizeSlotIdentifier('A_001'), 'A-001');
  assert.equal(mesh.normalizeSlotIdentifier('mesh_C-003'), 'C-003');
  assert.equal(mesh.normalizeSlotIdentifier(''), '');
  assert.equal(mesh.normalizeSlotIdentifier('   '), '');
});

test('normalizeSlotIdentifier strips exactly three trailing Blender digits only', () => {
  assert.equal(mesh.normalizeSlotIdentifier('Slot_A-001.001'), 'A-001');
  assert.equal(mesh.normalizeSlotIdentifier('A001.001'), 'A001');
  assert.equal(mesh.normalizeSlotIdentifier('.001'), '');
  // Four digits is not the ".NNN" duplicate marker, so it is preserved (and fails validation).
  assert.equal(mesh.normalizeSlotIdentifier('Slot_A-001.0001'), 'A-001.0001');
});

test('normalizeSlotIdentifier keeps internal whitespace rather than collapsing it', () => {
  // Surprise: only the leading prefix separator and underscore-to-hyphen are handled; a
  // space between the area letter and the number is preserved, so "008 2"/"B 002" do not
  // normalize to a matchable code.
  assert.equal(mesh.normalizeSlotIdentifier('008 2'), '008 2');
  assert.equal(mesh.normalizeSlotIdentifier('B 002'), 'B 002');
  assert.equal(mesh.normalizeSlotIdentifier('A 001'), 'A 001');
});

test('normalizeSlotIdentifier strips only one leading template token', () => {
  // Surprise: "Car Parking Space 001" strips only the leading "Car " token, leaving
  // "PARKING SPACE 001". The multi-word form only collapses when written with underscores.
  assert.equal(mesh.normalizeSlotIdentifier('Car Parking Space 001'), 'PARKING SPACE 001');
  assert.equal(mesh.normalizeSlotIdentifier('car_parking_space_001'), '001');
});

// --- classification -------------------------------------------------------

test('isSlotCandidateName accepts slot-shaped names and rejects scene props', () => {
  for (const name of ['A-001', 'Slot_B-12', 'CarParkingSpace_003', 'parking_ev_car_01', 'b1_car_2', '001', 'M005']) {
    assert.equal(mesh.isSlotCandidateName(name), true, name);
  }
  for (const name of ['Sign_Exit', 'Light_Pole_1', 'Barrier', 'Ground', '', 'Baseline']) {
    assert.equal(mesh.isSlotCandidateName(name), false, name);
  }
});

test('isSlotCandidateName rejects any name containing a non-slot keyword substring', () => {
  // Surprise: NON_SLOT_NAME is an unanchored substring test, so "Baseline" is rejected
  // because it contains "line", even though it is not an obvious scene prop.
  assert.equal(mesh.isSlotCandidateName('Baseline'), false);
  assert.equal(mesh.isSlotCandidateName('ParkingLine_12'), false);
});

test('isParkingSlotObjectName enforces the strict [AREA]-[NUMBER] shape', () => {
  for (const name of ['A-001', '001', 'A001', 'M005', 'A-1']) {
    assert.equal(mesh.isParkingSlotObjectName(name), true, name);
  }
  for (const name of ['ABCD-001', 'A-12345', '008 2', 'B 002', 'Ground', '']) {
    assert.equal(mesh.isParkingSlotObjectName(name), false, name);
  }
  // isSlotCandidateName is deliberately broader than the strict object-name check.
  assert.equal(mesh.isSlotCandidateName('parking_ev_car_01'), true);
  assert.equal(mesh.isParkingSlotObjectName('parking_ev_car_01'), false);
});

// --- mapMeshCandidates: exact + alias matching ----------------------------

test('mapMeshCandidates matches an exact normalized name to its slot code', () => {
  // An extra unmatched slot keeps the positional fallback out of play, isolating the exact pass.
  const mapping = mesh.mapMeshCandidates(candidates('Slot_A-001'), slots('A-001', 'Z-999'));
  assert.deepEqual(Array.from(mapping.matches, item => [item.key, item.slotId, item.code]), [
    ['mesh-1', 'slot-1', 'A-001'],
  ]);
  assert.equal(mapping.unregistered.length, 0);
  assert.deepEqual(Array.from(mapping.unmatchedSlots, item => item.code), ['Z-999']);
});

test('mapMeshCandidates links a hyphen-insensitive alias (A001 -> A-001)', () => {
  const mapping = mesh.mapMeshCandidates(candidates('A001'), slots('A-001', 'Z-999'));
  assert.deepEqual(Array.from(mapping.matches, item => [item.slotId, item.code]), [['slot-1', 'A-001']]);
  assert.equal(mapping.unregistered.length, 0);
});

test('mapMeshCandidates alias strips hyphens only, not spaces', () => {
  // Surprise: aliasIdentifier removes "-" but preserves " ", so "A 001" is unmatched even
  // though "A001" is. A single mesh against two slots keeps the fallback from masking it.
  const mapping = mesh.mapMeshCandidates(candidates('A 001'), slots('A-001', 'Z-999'));
  assert.equal(mapping.matches.length, 0);
  assert.deepEqual(Array.from(mapping.unregistered, item => item.name), ['A 001']);
  assert.deepEqual(Array.from(mapping.unmatchedSlots, item => item.code), ['A-001', 'Z-999']);
});

// --- mapMeshCandidates: A12 (unregistered meshes stay inert) --------------

test('A12: non-matching and unnamed meshes are returned as unregistered, never bound', () => {
  const extras = mesh.mapMeshCandidates(candidates('A-001', 'A-002', 'A-003'), slots('A-001'));
  assert.deepEqual(Array.from(extras.matches, item => item.code), ['A-001']);
  assert.deepEqual(Array.from(extras.unregistered, item => item.name), ['A-002', 'A-003']);

  const noSlots = mesh.mapMeshCandidates(candidates('Ground'), slots());
  assert.equal(noSlots.matches.length, 0);
  assert.deepEqual(Array.from(noSlots.unregistered, item => item.name), ['Ground']);

  const mixed = mesh.mapMeshCandidates(
    candidates('Sign_Exit', 'A-001'),
    slots('A-001', 'Z-999', 'Y-888'),
  );
  assert.deepEqual(Array.from(mixed.matches, item => item.code), ['A-001']);
  assert.deepEqual(Array.from(mixed.unregistered, item => item.name), ['Sign_Exit']);
  assert.deepEqual(Array.from(mixed.unmatchedSlots, item => item.code), ['Z-999', 'Y-888']);
});

test('A12 caveat: the positional fallback binds non-slot/unnamed meshes when counts are equal', () => {
  // Surprise / explicit semantics: when the remaining meshes and remaining slots are equal
  // in count, mapMeshCandidates pairs them positionally even if the names are not slot-shaped.
  // This means a non-slot or even unnamed mesh IS bound in a strict 1:1 template. The module
  // documents this as intentional ("keep a fully registered template interactive"), but it is
  // a deliberate exception to a literal reading of "unregistered meshes are inert".
  const twoProps = mesh.mapMeshCandidates(candidates('Ground', 'Sign_Exit'), slots('A-001', 'B-002'));
  assert.deepEqual(Array.from(twoProps.matches, item => [item.key, item.code]), [
    ['mesh-1', 'A-001'],
    ['mesh-2', 'B-002'],
  ]);
  assert.equal(twoProps.unregistered.length, 0);
  assert.equal(twoProps.unmatchedSlots.length, 0);

  const unnamed = mesh.mapMeshCandidates(candidates(''), slots('A-001'));
  assert.deepEqual(Array.from(unnamed.matches, item => item.code), ['A-001']);
});

test('positional fallback applies only when the remaining counts are exactly equal', () => {
  const complete = mesh.mapMeshCandidates(candidates('Bay one', 'Bay two'), slots('X1', 'X2'));
  assert.deepEqual(Array.from(complete.matches, item => item.code), ['X1', 'X2']);
  assert.equal(complete.unregistered.length, 0);

  const partial = mesh.mapMeshCandidates(candidates('Bay one', 'Bay two', 'Bay three'), slots('X1', 'X2'));
  assert.equal(partial.matches.length, 0);
  assert.equal(partial.unregistered.length, 3);
  assert.deepEqual(Array.from(partial.unmatchedSlots, item => item.code), ['X1', 'X2']);
});

// --- mapMeshCandidates: determinism / exclusivity -------------------------

test('mapMeshCandidates binds each slot and mesh at most once', () => {
  const duplicateNames = mesh.mapMeshCandidates(candidates('A-001', 'A-001'), slots('A-001'));
  assert.equal(duplicateNames.matches.length, 1);
  assert.equal(duplicateNames.unregistered.length, 1);
});

test('mapMeshCandidates deduplicates repeated candidate keys', () => {
  const mapping = mesh.mapMeshCandidates(
    [{ key: 'k', name: 'A-001' }, { key: 'k', name: 'B-002' }],
    slots('A-001', 'B-002'),
  );
  assert.deepEqual(Array.from(mapping.matches, item => [item.key, item.code]), [['k', 'A-001']]);
  assert.equal(mapping.unregistered.length, 0);
  assert.deepEqual(Array.from(mapping.unmatchedSlots, item => item.code), ['B-002']);
});

test('mapMeshCandidates is deterministic for the same spatial candidate order', () => {
  const first = mesh.mapMeshCandidates(candidates('A-002', 'A-001'), slots('A-001', 'A-002'));
  const second = mesh.mapMeshCandidates(candidates('A-002', 'A-001'), slots('A-001', 'A-002'));
  assert.deepEqual(JSON.parse(JSON.stringify(first)), JSON.parse(JSON.stringify(second)));
  assert.deepEqual(Array.from(first.matches, item => item.code), ['A-002', 'A-001']);
});

// --- suggestSlotCode ------------------------------------------------------

test('suggestSlotCode derives the registration code from a mesh name', () => {
  assert.equal(mesh.suggestSlotCode('Slot_A-001.001'), 'A-001');
  assert.equal(mesh.suggestSlotCode('CarParkingSpace_012'), '012');
  assert.equal(mesh.suggestSlotCode('A_001'), 'A-001');
  assert.equal(mesh.suggestSlotCode('B 002'), 'B 002');
});

test('suggestSlotCode falls back to the raw trimmed name when normalization is empty', () => {
  assert.equal(mesh.suggestSlotCode('   '), '');
  // ".001" normalizes to "" but the trimmed raw name is kept as the suggestion.
  assert.equal(mesh.suggestSlotCode('.001'), '.001');
});

test('suggestSlotCode caps the suggestion at 50 characters', () => {
  assert.equal(mesh.suggestSlotCode('X'.repeat(60)), 'X'.repeat(50));
  assert.equal(mesh.suggestSlotCode(`Slot_${'A'.repeat(60)}`), 'A'.repeat(50));
});
