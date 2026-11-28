'use strict';

// SPARK-238/244 Phase 3 Slice 3C regression tests.
//
// Operator side: the Emergency panel must only ever send REAL backend UUIDs as
// `unitId`/`slotId` (never a synthesized `zone:<floorId>:<zoneCode>` id, which the
// Facility model binder rejects as 400), requires an explicit second confirmation
// before a whole-site closure, warns that OCCUPIED vehicles are never displaced, and
// asks the dashboard to reconcile the layout after a create/release without ever
// starting a poll. The pure draft/scope rules back all of that, so they are tested
// directly; the two components are guarded with source contracts.

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

// All `operatorApi` imports in the rules module are type-only and erased, so the
// only runtime dependency the transpiled helper needs is `operationalLayout`.
const operationalLayout = loadModule('lib/operationalLayout.ts');
const rules = loadModule('lib/emergencyRestrictionRules.ts', {
  './operationalLayout': operationalLayout,
});

// ---------------------------------------------------------------------------
// Fixtures — real backend UUID shapes
// ---------------------------------------------------------------------------

const FLOOR_A = '11111111-1111-4111-8111-111111111111';
const FLOOR_B = '22222222-2222-4222-8222-222222222222';
const ZONE_A = '33333333-3333-4333-8333-333333333333';
const SLOT_1 = '44444444-4444-4444-8444-444444444444';
const SLOT_2 = '55555555-5555-4555-8555-555555555555';
const SYNTHETIC_ZONE = `zone:${FLOOR_A}:ZA`;

function projectionSlot(slotId, slotCode, floorId, floorCode, zoneCode, physicalState) {
  return {
    slotId,
    slotCode,
    floorId,
    floorCode,
    zoneCode,
    supportedVehicleType: 'CAR',
    slotType: 'STANDARD',
    physicalState,
    reservationState: 'UNRESERVED',
    isEvCharging: false,
    isBookable: physicalState === 'AVAILABLE',
  };
}

function projectedSite(overrides = {}) {
  return {
    id: 'site-1',
    tenantId: 'tenant-1',
    code: 'SITE-1',
    name: 'Site 1',
    address: '1 Demo Road',
    latitude: null,
    longitude: null,
    isActive: true,
    totalSlots: 2,
    floors: 2,
    configuredBackupCount: 0,
    assignmentPermissions: ['SLOT_OVERRIDE'],
    operationalLayout: {
      siteId: 'site-1',
      siteCode: 'SITE-1',
      siteName: 'Site 1',
      contractVersion: '1.3.0',
      generatedAt: '2026-11-24T10:00:00Z',
      isFresh: true,
      stalenessThresholdSeconds: 30,
      metrics: {
        totalPhysicalCapacity: 2,
        occupiedCount: 1,
        reservedCount: 0,
        availableCarSlots: 1,
        motorcycleCapacity: { maxCapacity: 0, occupiedCount: 0, availableCount: 0 },
        zoneCapacities: [
          {
            zoneId: ZONE_A,
            zoneCode: 'ZA',
            floorCode: 'G',
            name: 'Zone A',
            maxCapacity: 10,
            occupiedCount: 1,
            allocatedSlotCount: 1,
            zoneOnlyReservedCount: 0,
          },
        ],
      },
      floors: [
        { floorId: FLOOR_A, floorCode: 'G', name: 'Ground', level: 1, maxCapacity: 10 },
        { floorId: FLOOR_B, floorCode: '1', name: 'Level 1', level: 2, maxCapacity: 10 },
      ],
      slots: [
        projectionSlot(SLOT_1, 'CarParkingSpace_001', FLOOR_A, 'G', 'ZA', 'OCCUPIED'),
        projectionSlot(SLOT_2, 'CarParkingSpace_002', FLOOR_B, '1', null, 'AVAILABLE'),
        // A non-UUID slot id must never become a target.
        projectionSlot('not-a-uuid', 'CarParkingSpace_003', FLOOR_B, '1', null, 'AVAILABLE'),
      ],
    },
    layout: null,
    slots: [],
    units: [],
    ...overrides,
  };
}

function legacySite(overrides = {}) {
  return {
    id: 'site-legacy',
    tenantId: 'tenant-1',
    code: 'SITE-L',
    name: 'Legacy Site',
    address: '2 Demo Road',
    latitude: null,
    longitude: null,
    isActive: true,
    totalSlots: 3,
    floors: 1,
    configuredBackupCount: 0,
    operationalLayout: null,
    layout: null,
    assignmentPermissions: ['SLOT_OVERRIDE'],
    units: [
      { id: FLOOR_A, type: 'FLOOR', name: 'Ground', parentId: null, maxCapacity: 10 },
      { id: ZONE_A, type: 'ZONE', name: 'Zone A', parentId: FLOOR_A, maxCapacity: 5 },
      // Synthesized id, but it points at a real parent floor.
      { id: SYNTHETIC_ZONE, type: 'ZONE', name: 'ZA', parentId: FLOOR_A, maxCapacity: 1 },
    ],
    slots: [
      { id: SLOT_1, number: 'CarParkingSpace_001', unitId: ZONE_A, physicalState: 'AVAILABLE' },
      // Synthesized unit id that resolves to its real parent floor.
      { id: SLOT_2, number: 'CarParkingSpace_002', unitId: SYNTHETIC_ZONE, physicalState: 'OCCUPIED' },
      // Synthesized unit id with no real parent — must be dropped entirely.
      { id: '66666666-6666-4666-8666-666666666666', number: 'CarParkingSpace_003', unitId: 'zone:ghost:Z', physicalState: 'AVAILABLE' },
    ],
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// P3.6B — extractEmergencyScopeTargets
// ---------------------------------------------------------------------------

test('extractEmergencyScopeTargets returns no targets for a missing site', () => {
  for (const value of [null, undefined]) {
    const targets = rules.extractEmergencyScopeTargets(value);
    assert.equal(targets.units.length, 0);
    assert.equal(targets.slots.length, 0);
  }
});

test('extractEmergencyScopeTargets reads real floor/zone/slot ids from the projection', () => {
  const targets = rules.extractEmergencyScopeTargets(projectedSite());

  const unitIds = targets.units.map((unit) => unit.unitId);
  assert.deepEqual([...unitIds], [FLOOR_A, FLOOR_B, ZONE_A]);
  assert.equal(targets.units.find((unit) => unit.unitId === ZONE_A).kind, 'ZONE');
  assert.equal(targets.units.find((unit) => unit.unitId === FLOOR_A).kind, 'FLOOR');
  assert.equal(targets.units.find((unit) => unit.unitId === FLOOR_A).label, 'Floor Ground (G)');

  // Non-UUID slot ids are excluded from the catalogue.
  assert.deepEqual(
    [...targets.slots.map((slot) => slot.slotId)],
    [SLOT_1, SLOT_2],
  );
});

test('a zoned slot resolves its unitId to the real zone UUID, an unzoned one to the floor', () => {
  const targets = rules.extractEmergencyScopeTargets(projectedSite());
  const zoned = targets.slots.find((slot) => slot.slotId === SLOT_1);
  const unzoned = targets.slots.find((slot) => slot.slotId === SLOT_2);

  assert.equal(zoned.unitId, ZONE_A);
  assert.equal(zoned.unitId, targets.units.find((unit) => unit.unitId === ZONE_A).unitId);
  assert.equal(unzoned.unitId, FLOOR_B);
  assert.equal(zoned.physicalState, 'OCCUPIED');
  assert.ok(zoned.label.includes('CAR-001'));
});

test('extractEmergencyScopeTargets never emits a synthesized zone id', () => {
  const targets = rules.extractEmergencyScopeTargets(projectedSite());
  for (const unit of targets.units) assert.equal(unit.unitId.startsWith('zone:'), false);
  for (const slot of targets.slots) assert.equal(slot.unitId.startsWith('zone:'), false);
});

test('the legacy fallback drops synthesized ids but follows a synthesized id to its real parent', () => {
  const targets = rules.extractEmergencyScopeTargets(legacySite());

  // Only the two real-UUID units survive; the synthesized one is filtered out.
  assert.deepEqual(
    [...targets.units.map((unit) => unit.unitId)],
    [FLOOR_A, ZONE_A],
  );
  assert.equal(targets.units.some((unit) => unit.unitId === SYNTHETIC_ZONE), false);

  const byId = new Map(targets.slots.map((slot) => [slot.slotId, slot]));
  assert.equal(byId.get(SLOT_1).unitId, ZONE_A);
  // Synthesized slot unit id resolved to the real parent floor.
  assert.equal(byId.get(SLOT_2).unitId, FLOOR_A);
  // Orphan synthesized unit id: the slot is dropped instead of sending a bad id.
  assert.equal(byId.has('66666666-6666-4666-8666-666666666666'), false);
  for (const slot of targets.slots) assert.equal(slot.unitId.startsWith('zone:'), false);
});

// ---------------------------------------------------------------------------
// P3.6B — canManageEmergencyRestrictions
// ---------------------------------------------------------------------------

test('canManageEmergencyRestrictions follows the SLOT_OVERRIDE permission', () => {
  assert.equal(rules.canManageEmergencyRestrictions(projectedSite()), true);
  assert.equal(
    rules.canManageEmergencyRestrictions(projectedSite({ assignmentPermissions: ['GATE_OPERATOR'] })),
    false,
  );
  // No permission list means the backend stays authoritative, so controls stay enabled.
  assert.equal(
    rules.canManageEmergencyRestrictions(projectedSite({ assignmentPermissions: undefined })),
    true,
  );
  assert.equal(rules.canManageEmergencyRestrictions(null), false);
});

// ---------------------------------------------------------------------------
// P3.6B — validateEmergencyRestrictionDraft
// ---------------------------------------------------------------------------

const NOW = Date.parse('2026-11-24T10:00:00Z');

function draft(overrides = {}) {
  const targets = rules.extractEmergencyScopeTargets(projectedSite());
  return {
    mode: 'EMERGENCY',
    scope: 'SITE',
    unitId: '',
    slotId: '',
    reason: 'Burst pipe',
    effectiveUntilLocal: '',
    targets,
    ...overrides,
  };
}

test('a whole-site draft sends no unit or slot id', () => {
  const result = rules.validateEmergencyRestrictionDraft(draft(), NOW);
  assert.equal(result.ok, true);
  assert.deepEqual({ ...result.payload }, {
    mode: 'EMERGENCY',
    scope: 'SITE',
    unitId: null,
    slotId: null,
    reason: 'Burst pipe',
    effectiveUntil: null,
  });
});

test('a unit draft accepts a real floor/zone UUID and trims the reason', () => {
  const result = rules.validateEmergencyRestrictionDraft(
    draft({ scope: 'UNIT', unitId: ZONE_A, reason: '  Zone A flooded  ', mode: 'FORCED_MAINTENANCE' }),
    NOW,
  );
  assert.equal(result.ok, true);
  assert.equal(result.payload.unitId, ZONE_A);
  assert.equal(result.payload.slotId, null);
  assert.equal(result.payload.reason, 'Zone A flooded');
  assert.equal(result.payload.mode, 'FORCED_MAINTENANCE');
});

test('a slot draft resolves both ids from the selected target, never the raw synthetic id', () => {
  const result = rules.validateEmergencyRestrictionDraft(
    draft({ scope: 'SLOT', slotId: SLOT_1, unitId: '' }),
    NOW,
  );
  assert.equal(result.ok, true);
  assert.equal(result.payload.unitId, ZONE_A);
  assert.equal(result.payload.slotId, SLOT_1);
});

test('an optional future effective-until is emitted as UTC ISO', () => {
  const input = '2026-11-25T10:00';
  const result = rules.validateEmergencyRestrictionDraft(
    draft({ effectiveUntilLocal: input }),
    NOW,
  );
  assert.equal(result.ok, true);
  assert.equal(result.payload.effectiveUntil, new Date(input).toISOString());
  assert.ok(Date.parse(result.payload.effectiveUntil) > NOW);
});

test('a blank reason is rejected', () => {
  const result = rules.validateEmergencyRestrictionDraft(draft({ reason: '   ' }), NOW);
  assert.equal(result.ok, false);
  assert.equal(result.error, 'A reason is required.');
});

test('a unit draft without a real unit id is rejected', () => {
  for (const unitId of ['', '   ', SYNTHETIC_ZONE]) {
    const result = rules.validateEmergencyRestrictionDraft(draft({ scope: 'UNIT', unitId }), NOW);
    assert.equal(result.ok, false, `unitId=${JSON.stringify(unitId)} should be rejected`);
    assert.equal(result.error, 'Select a floor or zone to restrict.');
  }
});

test('a slot draft without a real slot id is rejected', () => {
  const missing = rules.validateEmergencyRestrictionDraft(draft({ scope: 'SLOT', slotId: '' }), NOW);
  assert.equal(missing.ok, false);
  assert.equal(missing.error, 'Select a specific bay to restrict.');

  const synthetic = rules.validateEmergencyRestrictionDraft(
    draft({ scope: 'SLOT', slotId: SYNTHETIC_ZONE, unitId: ZONE_A }),
    NOW,
  );
  assert.equal(synthetic.ok, false);
  assert.equal(synthetic.error, 'Select a specific bay to restrict.');
});

test('an effective-until in the past or unparsable is rejected', () => {
  const past = rules.validateEmergencyRestrictionDraft(
    draft({ effectiveUntilLocal: '2020-01-01T00:00' }),
    NOW,
  );
  assert.equal(past.ok, false);
  assert.equal(past.error, 'Effective until must be in the future.');

  const invalid = rules.validateEmergencyRestrictionDraft(
    draft({ effectiveUntilLocal: 'definitely-not-a-date' }),
    NOW,
  );
  assert.equal(invalid.ok, false);
  assert.equal(invalid.error, 'Effective until must be a valid date and time.');
});

// ---------------------------------------------------------------------------
// P3.6C — formatEmergencyRestrictionScope
// ---------------------------------------------------------------------------

test('formatEmergencyRestrictionScope labels site, unit and slot restrictions', () => {
  const targets = rules.extractEmergencyScopeTargets(projectedSite());

  assert.equal(
    rules.formatEmergencyRestrictionScope({ scope: 'SITE', unitId: null, slotId: null }, targets),
    'Whole site',
  );
  assert.equal(
    rules.formatEmergencyRestrictionScope({ scope: 'UNIT', unitId: ZONE_A, slotId: null }, targets),
    targets.units.find((unit) => unit.unitId === ZONE_A).label,
  );
  assert.equal(
    rules.formatEmergencyRestrictionScope({ scope: 'SLOT', unitId: ZONE_A, slotId: SLOT_1 }, targets),
    targets.slots.find((slot) => slot.slotId === SLOT_1).label,
  );
  // An unknown target falls back to a short id rather than throwing.
  assert.equal(
    rules.formatEmergencyRestrictionScope({ scope: 'UNIT', unitId: FLOOR_A, slotId: null }, { units: [], slots: [] }),
    `Unit ${FLOOR_A.slice(0, 8)}`,
  );
});

// ---------------------------------------------------------------------------
// P3.6B/3C — source contracts
// ---------------------------------------------------------------------------

const FE_SRC = path.join(__dirname, '../../FE/src');
const readFe = (relativePath) => fs.readFileSync(path.join(FE_SRC, relativePath), 'utf8');

test('operatorApi.ts exposes the three emergency-restriction endpoints', () => {
  const source = readFe('lib/operatorApi.ts');
  assert.ok(source.includes('listEmergencyRestrictions'));
  assert.ok(source.includes('createEmergencyRestriction'));
  assert.ok(source.includes('releaseEmergencyRestriction'));
  assert.ok(source.includes('"/emergency-restrictions"'));
  assert.ok(source.includes('/emergency-restrictions/${encodeURIComponent'));
});

test('EmergencyPanel keeps the incident logbook and adds the restriction controls', () => {
  const source = readFe('roles/operator/emergency/EmergencyPanel.tsx');

  // Incident Dispatch UI is preserved.
  assert.ok(source.includes('Open ('));
  assert.ok(source.includes('Resolved ('));
  assert.ok(source.includes('File Incident Report'));
  assert.ok(source.includes('Submit Incident Dispatch'));

  // Emergency closure controls.
  assert.ok(source.includes('Emergency Closure & Slot Restrictions'));
  assert.ok(source.includes('extractEmergencyScopeTargets'));
  assert.ok(source.includes('validateEmergencyRestrictionDraft'));
  assert.ok(source.includes('formatEmergencyRestrictionScope'));
  assert.ok(source.includes('canManageEmergencyRestrictions'));

  // OCCUPIED guidance is always present.
  assert.ok(source.includes('OCCUPIED'));
  assert.ok(source.includes('never displaced'));

  // A whole-site closure always takes a second confirmation click.
  assert.ok(source.includes('siteClosurePending'));
  assert.ok(source.includes('Confirm whole-site closure'));

  // Create/release must notify the dashboard, and the panel must never poll.
  assert.ok(source.includes('onRestrictionsChanged'));
  assert.equal(source.includes('setInterval'), false);
});

test('OperatorDashboard passes the selected lot and reconciles on restriction changes', () => {
  const source = readFe('roles/operator/dashboard/OperatorDashboard.tsx');
  assert.ok(source.includes('<EmergencyPanel'));
  assert.ok(source.includes('lot={selectedLot}'));
  assert.ok(source.includes('onRestrictionsChanged'));
  assert.ok(source.includes('reconcileOperatorLayout(selectedLot.id, "mutation", { force: true })'));
});

test('EmergencyPanel clears stale restriction state on site change and guards submit with permissions', () => {
  const source = readFe('roles/operator/emergency/EmergencyPanel.tsx');

  const effectStart = source.indexOf('useEffect(() => {');
  const effectEnd = source.indexOf('}, [siteId', effectStart);
  assert.ok(effectStart >= 0 && effectEnd > effectStart, 'expected a [siteId] effect');
  const effectBlock = source.slice(effectStart, effectEnd);

  // Switching sites must reset the draft, drop a pending whole-site confirm and clear feedback,
  // so a stale unitId/slotId or confirmation never leaks across site switches.
  assert.ok(effectBlock.includes('resetRestrictionDraft()'));
  assert.ok(effectBlock.includes('setRestrictionFeedback("")'));

  // The draft reset must also clear the pending whole-site confirmation.
  const resetStart = source.indexOf('function resetRestrictionDraft()');
  assert.ok(resetStart >= 0, 'expected resetRestrictionDraft');
  const resetEnd = source.indexOf('setSiteClosurePending(false)', resetStart);
  assert.ok(resetEnd > resetStart);
  const resetBlock = source.slice(resetStart, resetEnd);
  assert.ok(resetBlock.includes('setRestrictionDraft({'));

  // Pressing Enter must not bypass the disabled submit button for an unprivileged operator.
  assert.ok(
    source.includes('if (!siteId || !canManage || busy || restrictionBusyId) return'),
    'submitRestriction must early-return when the operator cannot manage restrictions',
  );
});
