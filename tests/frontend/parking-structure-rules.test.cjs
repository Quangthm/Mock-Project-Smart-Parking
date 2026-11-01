const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');
const ts = require('../../FE/node_modules/typescript');

function load(file) {
  const source = fs.readFileSync(path.join(__dirname, '../../FE/src', file), 'utf8');
  const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const context = { exports: {}, require: name => { throw new Error(`Unexpected import: ${name}`); } };
  vm.runInNewContext(js, context);
  return context.exports;
}

const mesh = load('lib/3d/meshSlotMapping.ts');
const rules = load('lib/structureRules.ts');
const slots = (...codes) => codes.map((code, index) => ({ id: `slot-${index + 1}`, code }));
const meshes = (...names) => names.map((name, index) => ({ key: `mesh-${index + 1}`, name }));

// --- mesh matching -------------------------------------------------------

test('registered slot codes match template mesh names after normalisation', () => {
  const mapping = mesh.mapMeshCandidates(
    meshes('Slot_A-001', 'slot-B-002', 'mesh_C-003'),
    slots('A-001', 'B-002', 'C-003'),
  );
  assert.deepEqual(Array.from(mapping.matches, item => [item.key, item.code]), [
    ['mesh-1', 'A-001'], ['mesh-2', 'B-002'], ['mesh-3', 'C-003'],
  ]);
  assert.equal(mapping.unregistered.length, 0);
  assert.equal(mapping.unmatchedSlots.length, 0);
});

test('hyphen-insensitive alias links A001 mesh to A-001 slot', () => {
  const mapping = mesh.mapMeshCandidates(meshes('A001'), slots('A-001'));
  assert.deepEqual(Array.from(mapping.matches, item => item.slotId), ['slot-1']);
});

test('extra template meshes stay unregistered and are never matchable', () => {
  const mapping = mesh.mapMeshCandidates(
    meshes('A-001', 'A-002', 'A-003'),
    slots('A-001'),
  );
  assert.deepEqual(Array.from(mapping.matches, item => item.code), ['A-001']);
  assert.deepEqual(Array.from(mapping.unregistered, item => item.name), ['A-002', 'A-003']);
});

test('a mesh is never assigned to two slots and a slot never to two meshes', () => {
  const mapping = mesh.mapMeshCandidates(meshes('A-001', 'A-001'), slots('A-001'));
  assert.equal(mapping.matches.length, 1);
  assert.equal(mapping.unregistered.length, 1);
});

test('positional fallback applies only when the pairing is unambiguous', () => {
  const complete = mesh.mapMeshCandidates(meshes('Bay one', 'Bay two'), slots('X1', 'X2'));
  assert.deepEqual(Array.from(complete.matches, item => item.code), ['X1', 'X2']);
  assert.equal(complete.unregistered.length, 0);

  const partial = mesh.mapMeshCandidates(meshes('Bay one', 'Bay two', 'Bay three'), slots('X1', 'X2'));
  assert.equal(partial.matches.length, 0);
  assert.equal(partial.unregistered.length, 3);
  assert.deepEqual(Array.from(partial.unmatchedSlots, item => item.code), ['X1', 'X2']);
});

test('matching is deterministic for the same spatial candidate order', () => {
  const first = mesh.mapMeshCandidates(meshes('A-002', 'A-001'), slots('A-001', 'A-002'));
  const second = mesh.mapMeshCandidates(meshes('A-002', 'A-001'), slots('A-001', 'A-002'));
  assert.deepEqual(JSON.parse(JSON.stringify(first)), JSON.parse(JSON.stringify(second)));
  assert.deepEqual(Array.from(first.matches, item => item.code), ['A-002', 'A-001']);
});

test('suggested registration code strips template prefixes and Blender suffixes', () => {
  assert.equal(mesh.suggestSlotCode('Slot_A-001.001'), 'A-001');
  assert.equal(mesh.suggestSlotCode('CarParkingSpace_012'), '012');
  assert.equal(mesh.suggestSlotCode('   '), '');
});

test('non-slot template names are not candidates', () => {
  for (const name of ['Sign_Exit', 'Light_Pole_1', 'Barrier', 'Ground', '']) {
    assert.equal(mesh.isSlotCandidateName(name), false, name);
  }
  for (const name of ['A-001', 'Slot_B-12', 'CarParkingSpace_003']) {
    assert.equal(mesh.isSlotCandidateName(name), true, name);
  }
});

// --- hierarchy -----------------------------------------------------------

test('parent selection follows FLOOR -> ZONE -> BLOCK order', () => {
  assert.deepEqual(Array.from(rules.allowedParentTypes('FLOOR')), []);
  assert.deepEqual(Array.from(rules.allowedParentTypes('ZONE')), ['FLOOR']);
  assert.deepEqual(Array.from(rules.allowedParentTypes('BLOCK')), ['FLOOR', 'ZONE']);
  const units = [
    { id: 'f', type: 'FLOOR' }, { id: 'z', type: 'ZONE' }, { id: 'b', type: 'BLOCK' },
  ];
  assert.deepEqual(Array.from(rules.parentUnitOptions('ZONE', units), unit => unit.id), ['f']);
  assert.deepEqual(Array.from(rules.parentUnitOptions('BLOCK', units), unit => unit.id), ['f', 'z']);
  assert.deepEqual(Array.from(rules.parentUnitOptions('FLOOR', units), unit => unit.id), []);
});

test('sibling names are unique case-insensitively and rename excludes itself', () => {
  const siblings = [{ id: 'a', name: 'Zone A' }, { id: 'b', name: 'Zone B' }];
  assert.match(rules.validateUnitName('  zone a ', siblings), /already exists/);
  assert.equal(rules.validateUnitName('Zone A', siblings, 'a'), null);
  assert.match(rules.validateUnitName('Zone A', siblings, 'b'), /already exists/);
  assert.equal(rules.validateUnitName('   ', siblings), 'Enter a unit name.');
  assert.match(rules.validateUnitName('x'.repeat(101), siblings), /at most 100/);
});

test('removal explains children, slots and paths instead of a generic error', () => {
  const units = [{ id: 'root', parentId: null, name: 'F1' }, { id: 'zone', parentId: 'root', name: 'Z1' }];
  const blockers = rules.unitRemovalBlockers(
    'root',
    units,
    [{ unitId: 'root' }, { unitId: 'zone' }],
    [{ fromUnitId: 'root', toUnitId: 'zone' }],
  );
  assert.deepEqual(Array.from(blockers), ['1 child unit', '1 registered slot', '1 access path']);
  assert.deepEqual(Array.from(rules.unitRemovalBlockers('zone', units, [], [])), []);
});

// --- site selection ------------------------------------------------------

test('a real site requested by the parent wins', () => {
  assert.equal(rules.resolveActiveSiteId('site-b', 'site-a', ['site-a', 'site-b']), 'site-b');
});

test('the owner choice survives while the parent still says "all"', () => {
  // This is the regression that made the site dropdown snap back: the parent default
  // is the "all" sentinel, so the local pick must not be reset to the first site.
  assert.equal(rules.resolveActiveSiteId('all', 'site-b', ['site-a', 'site-b']), 'site-b');
  assert.equal(rules.resolveActiveSiteId(null, 'site-b', ['site-a', 'site-b']), 'site-b');
  assert.equal(rules.resolveActiveSiteId(undefined, 'site-b', ['site-a', 'site-b']), 'site-b');
});

test('an unknown or stale requested id falls back to the current site', () => {
  assert.equal(rules.resolveActiveSiteId('deleted-site', 'site-b', ['site-a', 'site-b']), 'site-b');
  assert.equal(rules.resolveActiveSiteId('', 'site-b', ['site-a', 'site-b']), 'site-b');
});

test('an invalid current site falls back to the first available site', () => {
  assert.equal(rules.resolveActiveSiteId(null, 'gone', ['site-a', 'site-b']), 'site-a');
  assert.equal(rules.resolveActiveSiteId('all', '', ['site-a', 'site-b']), 'site-a');
});

test('no available sites yields the empty selection', () => {
  assert.equal(rules.resolveActiveSiteId('all', 'site-a', []), '');
  assert.equal(rules.resolveActiveSiteId('site-b', 'site-a', []), '');
});

// --- compatibility -------------------------------------------------------

test('one category resolves without a declared primary, several never infer one', () => {
  const single = rules.resolveSlotPrimary(['CAR']);
  assert.equal(single.primary, 'CAR');
  assert.equal(single.error, null);
  assert.equal(rules.resolveSlotPrimary(['CAR', 'MOTORCYCLE']).primary, null);
  assert.match(rules.resolveSlotPrimary(['CAR', 'MOTORCYCLE']).error, /primary/i);
  const declared = rules.resolveSlotPrimary(['CAR', 'MOTORCYCLE'], 'MOTORCYCLE');
  assert.equal(declared.primary, 'MOTORCYCLE');
  assert.equal(declared.error, null);
  assert.match(rules.resolveSlotPrimary(['CAR'], 'MOTORCYCLE').error, /must be one of/);
  assert.match(rules.resolveSlotPrimary([]).error, /at least one/);
});

test('categories are capped at three and can be removed again', () => {
  const first = rules.toggleSlotCategory(['CAR'], 'MOTORCYCLE');
  assert.deepEqual(Array.from(first.categories), ['CAR', 'MOTORCYCLE']);
  const second = rules.toggleSlotCategory(first.categories, 'OVERSIZED');
  assert.equal(second.error, null);
  const refused = rules.toggleSlotCategory(['CAR', 'MOTORCYCLE', 'OVERSIZED'], 'TRUCK');
  assert.equal(refused.categories.length, 3);
  assert.match(refused.error, /at most 3/);
  const removed = rules.toggleSlotCategory(['CAR', 'MOTORCYCLE'], 'CAR');
  assert.deepEqual(Array.from(removed.categories), ['MOTORCYCLE']);
});

// --- registration outcomes ----------------------------------------------

test('per-item codes are unique case-insensitively before any request is sent', () => {
  const issues = rules.registrationCodeIssues([
    { key: 'a', code: ' A-001 ' }, { key: 'b', code: 'a-001' }, { key: 'c', code: '' },
  ]);
  assert.match(issues.a, /another selected mesh/);
  assert.match(issues.b, /another selected mesh/);
  assert.equal(issues.c, 'Enter a slot code.');
});

test('retry excludes released identifiers and keeps them as decisions', () => {
  const summary = rules.summarizeRegistration([
    { key: 'a', status: 'succeeded' },
    { key: 'b', status: 'failed' },
    { key: 'c', status: 'released' },
    { key: 'd', status: 'resolved' },
    { key: 'e', status: 'failed' },
  ]);
  assert.equal(summary.total, 5);
  assert.equal(summary.succeeded.length, 1);
  assert.deepEqual(Array.from(summary.retryable, item => item.key), ['b', 'e']);
  assert.deepEqual(Array.from(summary.decisions, item => item.key), ['c']);
  assert.deepEqual(Array.from(summary.resolved, item => item.key), ['d']);
});

// --- maintenance windows -------------------------------------------------

const iso = value => new Date(value).toISOString();

test('a maintenance window is half-open and cannot start in the past', () => {
  const now = new Date('2026-10-29T10:00:00Z');
  assert.equal(rules.validateMaintenanceWindow(iso('2026-10-29T12:00:00Z'), iso('2026-10-29T13:00:00Z'), now), null);
  assert.match(rules.validateMaintenanceWindow(iso('2026-10-29T13:00:00Z'), iso('2026-10-29T13:00:00Z'), now), /half-open \[start, end\)/);
  assert.match(rules.validateMaintenanceWindow(iso('2026-10-29T09:00:00Z'), iso('2026-10-29T13:00:00Z'), now), /cannot start in the past/);
  assert.match(rules.validateMaintenanceWindow(iso('2026-10-29T13:00:00Z'), iso('2026-10-29T12:00:00Z'), now), /end must be after the start/);
});

test('window coverage excludes the end instant', () => {
  const window = { start: iso('2026-10-29T12:00:00Z'), end: iso('2026-10-29T13:00:00Z') };
  assert.equal(rules.windowCovers(window, new Date('2026-10-29T11:59:59Z')), false);
  assert.equal(rules.windowCovers(window, new Date('2026-10-29T12:00:00Z')), true);
  assert.equal(rules.windowCovers(window, new Date('2026-10-29T12:59:59Z')), true);
  assert.equal(rules.windowCovers(window, new Date('2026-10-29T13:00:00Z')), false);
  assert.equal(rules.windowCovers({ ...window, cancelledAt: iso('2026-10-29T11:00:00Z') }, new Date('2026-10-29T12:30:00Z')), false);
});

test('windows only grow while they have not ended', () => {
  const window = { start: iso('2026-10-29T12:00:00Z'), end: iso('2026-10-29T13:00:00Z') };
  const now = new Date('2026-10-29T12:30:00Z');
  assert.equal(rules.validateWindowExtension(window, iso('2026-10-29T14:00:00Z'), now), null);
  assert.match(rules.validateWindowExtension(window, iso('2026-10-29T13:00:00Z'), now), /only move later/);
  assert.match(rules.validateWindowExtension(window, iso('2026-10-29T12:00:00Z'), now), /only move later/);
  assert.match(rules.validateWindowExtension(window, iso('2026-10-29T14:00:00Z'), new Date('2026-10-29T13:00:00Z')), /has not ended/);
});

test('local datetime input round-trips to a UTC instant and back', () => {
  const local = '2026-10-30T09:15';
  const isoValue = rules.localInputToIso(local);
  assert.ok(isoValue);
  assert.equal(rules.isoToLocalInput(isoValue), local);
  assert.equal(rules.localInputToIso(''), null);
  assert.equal(rules.localInputToIso('not-a-date'), null);
});

