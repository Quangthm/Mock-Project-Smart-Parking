# SPARK-228 Branch Verification — Test Guide for PM Approval to Merge into `develop`

**Branch:** `feat/SPARK-228-3D_Model_BE`  
**PR:** [#36](https://github.com/Quangthm/Mock-Project-Smart-Parking/pull/36) (base: `develop`)  
**Jira:** SPARK-228 — 3D Digital Twin slot-state read path  
**Prepared for:** Tester / QA → PM sign-off before merge  
**Date:** 2026-10-30

---

## 1. Scope of this branch

This branch delivers the **3D Digital Twin slot-state read path** for SPARK-228:

- **Phase 1a — backend projection:** new `GET /api/parking-lots/{id}/operational-layout` endpoint (dual-mode auth + redaction: `Sanitized` vs `Telemetry`), lock-free read path, `Cache-Control: no-store, no-cache, must-revalidate` + `Vary: Authorization`.
- **Phase 1b — redaction:** visibility masking of `BACKUP` → `RESERVED` in Sanitized mode; `metrics.reservedCount` masked accordingly.
- **Phase 2 — frontend binding:** 2-axis (`physicalState` × `reservationState`) vocabulary on operator + owner screens; 3D viewer with 6-state legend, 30% dim for unavailable, stale-data banner, fail-closed mesh binding.
- **Phase 3 — hardening:** contract fixture tests (20/20 matrix), A2/A7 unit tests, F1–F4 fixes, merge `origin/develop` twice (service rename + Fix2FA), smoke HTTP with real tokens.
- **Phase 3 close (commit `e62553f5`):** owner BACKUP now renders cyan `#0891b2` in both 3D and 2D (was amber/yellow); demo identity repair script; Postman 33/123/0.
- **F3 (commit `48547dd0`):** owner read path switched from legacy aggregate to `/operational-layout` projection (parity with operator).

**Commits on branch (latest first):** `48547dd0`, `e62553f5`, `26c2f0e4`, `27f11f31`, `048c1ad3`, `a2176840` (plus merge commits).

---

## 2. Prerequisites

1. Docker Desktop running; schema containers healthy (`./setup.ps1` or `docker compose -f compose.app.yml up -d --build`).
2. Services started:
   - User: `http://localhost:5035`
   - Parking/Facility: `http://localhost:5045`
   - Reservation/Operation: `http://localhost:5055`
3. Seed demo site (idempotent): `./scripts/seed-demo-site.ps1` → creates `DEMO-HQ` (`b0000000-…0001`), 48 slots (36 `CarParkingSpace_*` + 12 `PARKING_EV_CAR_*`), assigns operator with `SLOT_OVERRIDE`.
4. Root `.env` present with real `SMARTPARK_*_PASSWORD` values.
5. If passwords were changed, run `./scripts/repair-demo-identity.ps1 -Apply` to rehash demo accounts.
6. FE deps installed (`FE/node_modules`); `motion` present.

---

## 3. Automated verification (must all be green)

Run from repo root unless noted.

| # | Command | Expected |
|---|---|---|
| A1 | `dotnet build SmartParking.slnx --nologo -v q` | 0 Warnings, 0 Errors |
| A2 | `dotnet test tests/Services/SmartParking.FacilityService.Tests --nologo` | 112 passed, 0 failed, 13 skipped (125 total) |
| A3 | `cd FE; npm run build` | ✓ built, exit 0 (pre-existing warnings only) |
| A4 | `cd FE; npx tsc --noEmit -p tsconfig.json` | Only 4 pre-existing errors (GroupedNumberInput, OperatorDashboard/SlotStatusFilter, SiteManagement/setMesage, useSiteManagement/reassignSite); **no new errors** |
| A5 | `node --test tests/frontend/operational-layout-adapter.test.cjs tests/frontend/operational-layout-visual.test.cjs` | 26 pass, 0 fail |
| A6 | Newman run (see §5) | 33 requests, 123 assertions, 0 failed |

---

## 4. Manual verification — screens (tester opens browser)

### 4.1 Stack + login
- [ ] `http://localhost:5173` loads; Vite transforms `main.tsx` and `operationalLayout.ts` → 200.
- [ ] Login as `driver@gmail.com` / `Password@123` → role `driver`.
- [ ] Login as `owner@smartpark.local` (password from `.env`) → role `owner`.
- [ ] Login as `operator@smartpark.local` → role `operator`.
- [ ] Login as `admin@smartpark.local` → role `admin`.

### 4.2 Operator screen (3D + 2D)
- [ ] Open a parking lot as operator with `SLOT_OVERRIDE` → 3D viewer renders 48 slots, no all-grey.
- [ ] Legend shows 6 states (available/occupied/reserved/protected/backup/maintenance/unknown) with correct colours.
- [ ] Unavailable slots dimmed 30%; stale banner appears when threshold exceeded.
- [ ] Filter by status (available/occupied/reserved/backup) works; selection clears when filtered out.
- [ ] Slot inspection modal: `UNKNOWN` no longer shows "VACANT".
- [ ] Search in LotStatus: typing raw code (`CAR-008`) — known limitation F8, acceptable.

### 4.3 Owner screen — 3D + 2D (Phase 3 close verification)
- [ ] Open **Parking Structure** as owner.
- [ ] Mark a slot BACKUP via operator endpoint (or run SQL demo in §6 of session file).
- [ ] Switch to owner account → **3D model**: BACKUP slot is **cyan** `#0891b2`, NOT amber `reserved`.
- [ ] **2D slot view**: BACKUP slot border/text **cyan** `#0891b2`, NOT yellow border.
- [ ] Card label reads "Backup".
- [ ] Other statuses unchanged (occupied amber-red, maintenance, etc.).

### 4.4 Owner path now uses projection (F3)
- [ ] Owner "Parking Structure" loads slots with correct axes (physicalState/reservationState) — same as operator.
- [ ] Freshness banner/stale logic now active on owner screen (previously always null).
- [ ] Address/lat/lng/isActive still display correctly (merged from list).
- [ ] No `POST`/`PUT`/`PATCH`/`DELETE` issued while panning/selecting in 3D (read-only, A6).

### 4.5 No write path from 3D view (A6)
- [ ] DevTools Network: only `GET /api/parking-lots/{id}/operational-layout` + identity/scope calls while interacting with 3D. No mutations.

---

## 5. Postman / Newman verification (B1 evidence)

Follow `docs/04-testing/postman/SPARK-228-manual-test-guide.md`.

### 5.1 Newman command
```powershell
$s = & ./scripts/load-env.ps1 -EnvPath ./.env -NoExport
npx.cmd --yes --cache .cache/npm --package newman newman run `
  "docs/04-testing/postman/SmartParking-OperationalLayout-SPARK-228.postman_collection.json" `
  -e "docs/04-testing/postman/SmartParking-OperationalLayout-SPARK-228.postman_environment.json" `
  --env-var "driverEmail=driver@gmail.com" --env-var "driverPassword=Password@123" `
  --env-var "ownerEmail=$($s['SMARTPARK_OWNER_EMAIL'])" --env-var "ownerPassword=$($s['SMARTPARK_OWNER_PASSWORD'])" `
  --env-var "operatorEmail=$($s['SMARTPARK_OPERATOR_EMAIL'])" --env-var "operatorPassword=$($s['SMARTPARK_OPERATOR_PASSWORD'])" `
  --env-var "adminEmail=$($s['SMARTPARK_ADMIN_EMAIL'])" --env-var "adminPassword=$($s['SMARTPARK_ADMIN_PASSWORD'])"
```

**Expected:** 33 requests, 123 assertions, 0 failed.

### 5.2 Optional strengthens (not blocking)
- Create a BACKUP slot via operator endpoint → rerun 3.1/4.1/5.1/7.1-7.2 to see telemetry vs sanitized masking.
- Activate an operator without `SLOT_OVERRIDE` (invitation flow) → unblocks 4.3 and 8.3.
- Create a distinct second-tenant owner → unblocks 5.2.
- Provide `expiredToken` → unblocks 2.2.

These are **Blocked** (not failed) if prerequisites unavailable — acceptable for merge.

---

## 6. Acceptance criteria checklist (PM sign-off)

| AC | Description | Status |
|---|---|---|
| A1 | Schema/envelope/version/threshold/cache headers | ✅ Postman 1.1 + unit |
| A2 | Two-axis states (20/20) | ✅ fixture tests + live smoke |
| A3 | Bookable rule | ✅ unit tests |
| A4 | UNKNOWN fail-closed | ✅ unit + live |
| A5 | Freshness/staleness (server-decided) | ✅ unit + live fields |
| A6 | Read-only / no mutation from 3D | ✅ manual (§4.5) |
| A7 | Redaction (sanitized vs telemetry) | ✅ Postman 7.1/7.2 (two-token proof) |
| A8 | slotCode ↔ mesh identity | ✅ unit; demo uses human codes (partial) |
| B1 | Runtime end-to-end | ✅ smoke + Newman 33/123/0 |
| F3 | Owner reads projection | ✅ commit `48547dd0` + tests 26/26 |

---

## 7. Known limitations (accepted, not blockers)

- `siteFromStructure` retained but unused after F3 (no public contract removed).
- FE has 4 pre-existing TS errors (not introduced by this branch).
- Frontend harness tests: `sign-in` 17/21, `operator-provisioning` 1/22 still red due to UI drift (documented in session §6 F1; not SPARK-228 regressions).
- `isFresh` is prototype `true` (no heartbeat table) — R3 documented.
- Demo seed has no BACKUP slots (shape-only evidence unless §5.2 strengthen).
- `operator-provisioning` test import map missing `../dashboard/BackupCapacityPanel` (develop-side, not SPARK-228).
- Session file `ai/tasks/active/SESSION-20261028-spark-228-planning.md` intentionally untracked per `ai/tasks/README.md`.

---

## 8. Sign-off

- [ ] Tester confirms all automated checks (§3) green.
- [ ] Tester confirms manual screens (§4) pass.
- [ ] Tester confirms Newman (§5) 33/123/0 (or documents Blocked items).
- [ ] PM approves merge of `feat/SPARK-228-3D_Model_BE` → `develop`.

**PR:** https://github.com/Quangthm/Mock-Project-Smart-Parking/pull/36
