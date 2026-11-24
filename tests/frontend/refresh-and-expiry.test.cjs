'use strict';

// SPARK-238/244 Phase 4.5 hotfix regression tests.
//
// Two E2E bugs are pinned here with source contracts:
//  1. Every owner/operator Refresh button must actually re-read from the server. Owner -> Operators
//     passes a live list, so the roster effect must fetch when `reload > 0` even though
//     `liveOperators` is supplied, and the Refresh button must also bump the shared owner list.
//  2. A timed emergency closure (`effective_until`) must auto-expire and the UI must pick that up
//     with a single-shot timer (never a polling `setInterval`), while a manual Refresh or that timer
//     must NOT wipe an in-progress restriction draft (only a real site change may).

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const FE_SRC = path.join(__dirname, '../../FE/src');

function read(relativePath) {
  return fs.readFileSync(path.join(FE_SRC, relativePath), 'utf8');
}

// Collapses runs of whitespace so multi-line JSX assertions are not brittle.
function squash(source) {
  return source.replace(/\s+/g, ' ');
}

const operatorManagement = read('roles/owner/operators/OperatorManagement.tsx');
const useLotStatus = read('roles/operator/parking-lot/useLotStatus.ts');
const operatorAssignments = read('roles/operator/dashboard/OperatorAssignments.tsx');
const operatorDashboard = read('roles/operator/dashboard/OperatorDashboard.tsx');
const emergencyPanel = read('roles/operator/emergency/EmergencyPanel.tsx');

// ---------------------------------------------------------------------------
// Hotfix 2.1 — Owner -> Operators Refresh
// ---------------------------------------------------------------------------

test('OperatorManagement re-reads the roster on Refresh even when a live list is supplied', () => {
  assert.ok(
    squash(operatorManagement).includes(
      'liveOperators === undefined || reload > 0 ? operatorsApi.list() : Promise.resolve(null)',
    ),
    'the roster effect must fetch when reload > 0 despite liveOperators',
  );
});

test('OperatorManagement Refresh button bumps reload and notifies the owner list', () => {
  assert.ok(
    squash(operatorManagement).includes('setReload((v) => v + 1) onOperatorsChanged()'),
    'Refresh must bump reload AND call onOperatorsChanged',
  );
});

// ---------------------------------------------------------------------------
// Hotfix 2.2 — Operator -> Lot Status "Retry now"
// ---------------------------------------------------------------------------

test('useLotStatus forces a reconcile when the user retries the layout', () => {
  assert.ok(
    useLotStatus.includes('reconcileOperatorLayout(site.id, "mutation", { force: true })'),
    'refreshLayout must force the reconcile so an explicit retry is never coalesced away',
  );
});

// ---------------------------------------------------------------------------
// Hotfix 2.3 — Operator assignments + selected-site refresh
// ---------------------------------------------------------------------------

test('OperatorAssignments accepts and calls onRefreshSites', () => {
  assert.ok(operatorAssignments.includes('onRefreshSites?: () => void'), 'prop must be declared');
  assert.ok(operatorAssignments.includes('onRefreshSites?.()'), 'Refresh Roster must call it');
  assert.ok(
    squash(operatorAssignments).includes('setReload((v) => v + 1) onRefreshSites?.()'),
    'Refresh Roster must bump reload AND refresh sites',
  );
});

test('OperatorDashboard refreshes sites and slots from the status toolbar', () => {
  assert.ok(
    operatorDashboard.includes('onRefreshSites={() => setRefreshKey((value) => value + 1)}'),
    'OperatorAssignments must be wired to the site refresh key',
  );
  assert.ok(
    squash(operatorDashboard).includes(
      'setRefreshKey((value) => value + 1) if (selectedLot) void reconcileOperatorLayout(selectedLot.id, "mutation", { force: true })',
    ),
    'Refresh sites and slots must both re-read sites and reconcile the selected lot',
  );
});

// ---------------------------------------------------------------------------
// Hotfix 2.4 — Emergency panel refresh + single-shot expiry timer
// ---------------------------------------------------------------------------

test('EmergencyPanel exposes a manual Refresh button', () => {
  assert.ok(
    squash(emergencyPanel).includes('Refresh </button>'),
    'a manual Refresh button must exist in the restrictions header',
  );
  assert.ok(
    emergencyPanel.includes('setReload((value) => value + 1)'),
    'Refresh must bump the reload counter',
  );
});

test('EmergencyPanel re-fetches on reload and never polls', () => {
  assert.ok(emergencyPanel.includes('}, [siteId, reload])'), 'the fetch effect must depend on reload');
  assert.equal(emergencyPanel.includes('setInterval'), false, 'no polling interval is allowed');
});

test('EmergencyPanel schedules exactly one single-shot timer at the earliest effective_until', () => {
  assert.ok(emergencyPanel.includes('window.setTimeout('), 'a single-shot timeout must be used');
  assert.ok(emergencyPanel.includes('2_147_483_647'), 'the delay must be capped for setTimeout');
  assert.ok(emergencyPanel.includes('effectiveUntil'), 'the timer must consider effectiveUntil');
});

test('EmergencyPanel resets the draft only on a real site change', () => {
  assert.ok(emergencyPanel.includes('prevSiteIdRef'), 'a previous-site ref must gate the reset');
  assert.ok(
    emergencyPanel.includes('if (prevSiteIdRef.current !== siteId)'),
    'the reset must run only when siteId actually changes',
  );
});
