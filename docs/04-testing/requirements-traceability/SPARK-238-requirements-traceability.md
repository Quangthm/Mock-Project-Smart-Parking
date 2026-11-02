# SPARK-238 — Requirements traceability (FR-LOT-02 / UC-LOT-01 / US-OW01)

This file traces the twelve acceptance criteria **A1–A12** of SPARK-238 (owner-configured facility
structure) to their requirement anchors, implementation and the evidence that was actually run.
The global booking/payment acceptance scenarios stay in
[acceptance-scenarios.md](acceptance-scenarios.md) and remain **NOT RUN**.

| Field | Value |
|---|---|
| Jira (design) | SPARK-238 |
| Jira (implementation) | NOT IMPLEMENTED on `develop` yet — on branch `feat/SPARK-238-facility-structure-port` |
| Jira (verification) | SPARK-238 branch verification, 2026-11-01 |
| Requirements baseline | FR-LOT-02, FR-LOT-04 (`docs/01-requirements/srs/modules/02-facility-capacity.md`), UC-LOT-01 (`docs/01-requirements/use-cases/uc-lot-01.md`), US-OW01 |
| Contract | `docs/03-design/api/parking-structure-api-design.md` — **v1.3 APPROVED 2026-10-29** (DE-1…DE-17, maintenance-window table) |
| Owning service | `FacilityService` (structure configuration; evolved from ParkingService) + `OperationService` (commitments, edit holds, guard triggers) |
| Branch / commits | `feat/SPARK-238-facility-structure-port` on base `origin/develop` `ea0d212c`; `76630c39` (contract docs) · `ac849ea7` (tombstone drop) · `cd99422a` (P2 backend) · `ce76fd05` (P3 owner UI) |
| Test guide | [SPARK-238-branch-verification-test-guide.md](../test-plans/SPARK-238-branch-verification-test-guide.md) (C1–C10 checklist, D1–D8 packaging) |
| API collection | `docs/04-testing/postman/SmartParking-FacilityStructure-SPARK-238.postman_collection.json` (11 folders, 83 requests) |

**Status legend**

| Status | Meaning |
|---|---|
| Implemented & Verified | Executed by the agent on the branch with retained command output. |
| Implemented; user-executed | Exercised on the running Docker stack by the task owner (Postman / manual UI); not independently reproduced byte-for-byte by the agent. |
| Implemented; automated only | Covered by unit/HTTP tests, no live end-to-end run. |
| Partially deferred | Implemented for the covered part; a named sub-case is deferred (see the debt table). |

## A1–A12

| Case | Criterion (short) | Requirement | WBS | Implementation | Verification evidence | Status |
|---|---|---|---|---|---|---|
| A1 | Owner creates lot + floors + zones + slots + access paths in one session, no manual DB work | FR-LOT-02; UC-LOT-01 | 2.1, 3.1 / 4.2 | `POST /api/parking-lots`, `POST /{id}/units`, `/{id}/slots`, `/{id}/paths`, `PATCH /{id}/structure` (`API/Program.cs`) | `FacilityStructureEndpointTests.CreateSiteIs201WithStructureLocationAndProfile`, `AddSlotIs200WithNewSlotId`, `PatchActiveStructureActionIs200` (18/18); Postman 01–04; manual C6 | Implemented; user-executed |
| A2 | Units nest ≥3 levels (FLOOR→ZONE→BLOCK); `parentId` preserved on re-read | FR-LOT-02 | 2.1, 3.1 / 4.2 | `ParkingStructure.AddUnit` (parent type must precede child, max 3 levels), `Load` returns `parent_id`; `StructureTree` (FE) | `StructureTests` (unit ordering / duplicate sibling); `PostgresStructureWorkflowTests`; Postman 02.1–02.3 (FLOOR→ZONE→BLOCK); manual C6 | Implemented & Verified |
| A3 | Every route rejects a foreign owner without leaking lot existence | FR-LOT-02 (ownership) | 2.1 / 4.1 | `Scope()` tenant check → `FORBIDDEN` 403; middleware maps `SITE_NOT_FOUND`/`UNIT_NOT_FOUND`/`SLOT_NOT_FOUND` → 404 | `FacilityStructureEndpointTests.ListWithoutBearerIs401Unauthenticated`; Postman 10.1–10.10 (401 no bearer; 403 foreign tenant; 404 foreign lot); `GateAuthenticationTests` via gated Postgres run | Implemented; user-executed |
| A4 | Deactivating a lot deletes nothing; the lot stays visible to the Owner with its status | FR-LOT-02 | 2.3, 3.5 / 4.4 | `SetActive` writes `status`, never deletes; `ListAsync` returns rows regardless of status; Q4/DE-2 decision: **inactive lots remain visible** with `status` + optional filter | `FacilityStructureEndpointTests.ListIncludesInactiveLotInDataArray`; Postman 01.4–01.5; DB check `parking_sites.status='INACTIVE'` with data intact | Implemented & Verified |
| A5 | A released slot code is never silently re-created; it can be explicitly restored | FR-LOT-02; F06 (review) | 2.3 / 4.4 | `ReleasedIdentifierException` → 409 `IDENTIFIER_RELEASED`; `restoreSlot`/`restoreUnit`/`restorePath` actions; `CommitAsync` UPDATE-in-place | `PostgresStructureWorkflowTests.ReleasedSlotCodesRequireAnExplicitRestore`; `StructureTests` (6 restore tests); `FacilityStructureEndpointTests.AddSlotWithReleasedCodeIs409IdentifierReleased` + `PatchRestoreSlotStructureActionIs200`; Postman 03.10/03.11, 05.07/05.08, 05.13/05.14 | Implemented & Verified |
| A6 | Multi-value compatibility is stored and read back; the single-value column is never contradictory | FR-LOT-02 | 2.2, 3.3 / 4.3 | `slot_compatibilities` is the source of truth; `supported_vehicle_type` is the primary-category projection; both written in the same transaction; claim query reads the table with a projection fallback | `PostgresStructureWorkflowTests.MultiValueCompatibilityRoundTripsThroughTheCompatibilityTable`, `CompatibilityIsAPredicateNotStockForGateClaims`, `LegacyProjectionIsTheClaimFallbackAndANullProjectionNeverThrows`; `StructureTests.CompatibilitySetIsAPredicateAndThePrimaryCategoryIsNeverInferred`; Postman 03.2 | Implemented & Verified |
| A7 | Displayed capacity counts registered slots only; unregistered meshes are excluded | FR-LOT-02 | 2.4 / 4.5 | `total_physical_capacity` is reconciled to the live slot count on every edit (the old hard failure was removed); read side excludes soft-deleted rows | `PostgresStructureWorkflowTests.StaleDeclaredCapacityIsReconciledInsteadOfBlockingTheEdit`; DB check `total_physical_capacity = count(slots)`; FE `CapacitySummary` + `mesh-slot-mapping.test.cjs` | Implemented & Verified (see DE-16 for the typed `capacity` block) |
| A8 | Replaying the same `idempotencyKey` creates no second record | FR-LOT-02 | 2.5 / 4.6 | `structure_operations` `UNIQUE (site_id, idempotency_key)`; `POST /{id}/operations` replays the stored row; a different payload with the same key → 409 | `PostgresStructureWorkflowTests.OperationIdempotencyReplaysOneRecordAndRejectsADifferentPayload`; `StructureOperationAndEditHandlerTests`; Postman 08.3, 09.3 | Implemented & Verified |
| A9 | No route lets the Owner supply 2D/3D coordinates | FR-LOT-02 (fixed 3D templates) | — / route list check | `configureSlot` rejects non-null `coordinates3D`/`features` with `INVALID_STRUCTURE`; loaded geometry passes through unchanged; DTO exposes them read-only | `PostgresStructureWorkflowTests.OwnerGeometryAndFeaturesWritesAreRejectedAndStoredValuesSurvive`; `StructureOperationAndEditHandlerTests`; Postman 05.11/05.12 (400) | Implemented & Verified |
| A10 | A window overlapping a live commitment is refused with the overlapping commitments; nothing is auto-cancelled | Q3, approved 2026-10-29 | 2.6 | Overlapping maintenance windows → 409 `STRUCTURE_IN_USE` (DB EXCLUDE constraint `ex_maintenance_windows_site_overlap`); the service never cancels or moves a commitment | `PostgresStructureWorkflowTests.FutureMaintenanceWindowIsScheduledAndHalfOpenOverlapIsEnforced`; `FacilityStructureEndpointTests.OverlappingMaintenanceWindowIs409StructureInUse`; Postman 07.4 | Partially deferred (the `commitments[]` list needs DE-4) |
| A11 | A window starting after the last commitment is accepted; service returns after it ends; grow-only extension; other closures still guard cancellation | Q3 | 2.6 | `MaintenanceWindowRules.ValidateCreate`/`Extend`; `MaintenanceWindowState.Derive` (`SCHEDULED` → `MAINTENANCE_UNCERTAIN`/`MAINTENANCE` → `ACTIVE`); expiry is derived at read time | `MaintenanceWindowTests` (6 tests); `PostgresStructureWorkflowTests.MaintenanceWindowIsGrowOnlyAndCancellationCannotBypassAnotherClosure`; `FacilityStructureEndpointTests.{CreateMaintenanceWindowIs201, ExtendMaintenanceWindowWithShorterEndIs409, CancelMaintenanceWindowGuardedByAnotherLiveWindowIs409}`; Postman 07.1–07.11; live probes 7.1/7.2/7.10 | Implemented & Verified |
| A12 | Unregistered template meshes stay inert (no hover, no click, no capacity) and are never auto-claimed | Q1; FR-LOT-02 | 3.2 / C7 | `meshSlotMapping` accepts only name/code matches; `registeredMeshMapping.mapRegisteredMeshes` separates `allSlotMeshes` from `unregisteredSlotMeshes`; the viewer tints unregistered meshes only while registering, otherwise restores the original material | `tests/frontend/mesh-slot-mapping.test.cjs` (19 tests, incl. the "never auto-bind" case); `SlotRegistrationPanel` empty state; manual C7 | Implemented & Verified (with a documented positional-fallback caveat) |

## How to reproduce

Automated (from the repo root, branch `feat/SPARK-238-facility-structure-port`):

```powershell
dotnet build SmartParking.slnx --nologo -m:1
dotnet test  SmartParking.slnx --nologo -m:1 --no-build

# HTTP contract for the structure surface (no database needed)
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj `
  --nologo -m:1 --no-build --filter "FullyQualifiedName~FacilityStructureEndpointTests"

# Database-backed cases (needs the shared facility database on 5442; the connection only
# serves as a CREATE DATABASE anchor - each test builds and drops its own throw-away database)
$pw = ((Select-String -Path .env -Pattern '^SMARTPARK_DEV_PASSWORD=').Line -split '=',2)[1].Trim()
$env:SMARTPARK_STRUCTURE_TEST_CONNECTION = "Host=127.0.0.1;Port=5442;Database=smartpark_facility;Username=smartpark_facility;Password=$pw"
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj `
  --nologo -m:1 --no-build --filter "FullyQualifiedName~Postgres"

# Frontend
cd FE; npm run build; npx tsc --noEmit; cd ..
node --test --test-reporter=tap tests/frontend/parking-structure-rules.test.cjs
node --test --test-reporter=tap tests/frontend/mesh-slot-mapping.test.cjs
node --test --test-reporter=tap tests/frontend/address-composition.test.cjs
node --test --test-reporter=tap tests/frontend/operational-layout-adapter.test.cjs   # SPARK-228 guard
```

Last recorded results on 2026-11-01: build **0 warnings / 0 errors**; ungated solution tests
FacilityService **148 passed / 22 skipped**, Payment **88 passed**, User **103 passed / 34 skipped**, IoT **1 passed**
(0 failures); the 22 skipped Parking tests are the `SMARTPARK_STRUCTURE_TEST_CONNECTION`-gated
Postgres cases, all green when the variable is set; `FacilityStructureEndpointTests` **18/18**;
FE build **exit 0**; `tsc --noEmit` reports exactly **4 pre-existing** errors in files this work did not
touch (`GroupedNumberInput.tsx`, `OperatorDashboard.tsx`, `SiteManagement.tsx`, `useSiteManagement.ts`);
FE suites **24/24, 19/19, 16/16, 9/9**.

Manual (see the test guide C1–C10): the Postman collection (83 requests) and the Owner/Operator
browser flows were executed by the task owner on the running Docker stack and reported passing.

## Deferred / out-of-scope debt (D8)

| Item | Gap | Affected surface | Owner / Jira | Status |
|---|---|---|---|---|
| DE-4 | The Reservation impact projection returns no commitment end time, so a 409 for a window overlapping live commitments cannot list `commitments[]` (`id`, `type`, `start`, `end`) | `POST/PATCH /{id}/maintenance-windows`, `PATCH /{id}/structure` | OperationService / Reservation lane | Open — deferred |
| DE-13 | No resource-scoped commitment query exists (`HoldSnapshot.HasLiveCommitments` is a single site-wide flag), so the site-wide guard in `SafeChange` could not be narrowed to the affected resources and no enforcement/acknowledgement state beyond `pending` is written | `PATCH /{id}/structure` guard scope; maintenance-window `enforcement_state` | OperationService | Open — deferred |
| DE-16 | `GET /{id}/structure` serializes the domain aggregate, not a typed read DTO: there is no `capacity` block (`registeredPhysicalCount`/`configuredLimit`/`operationalAvailable`), no `primaryVehicleType`/`compatibilitySource`, and a read may still throw `CAPACITY_CONFLICT`; `expectedUpdatedAt` is not exposed by any read | `GET /{id}/structure`, FE `CapacitySummary`, batch `expectedUpdatedAt` | FacilityService read path (SPARK-228 no-touch rule) | Open — deferred |
| DE-3 | Unit/slot-scoped maintenance windows are rejected at the boundary (`400 INVALID_STRUCTURE`); only site-wide windows are accepted | `POST /{id}/maintenance-windows` | FacilityService | Accepted limitation |
| A12 caveat | When the remaining mesh count exactly equals the remaining slot count, `mapMeshCandidates` binds even unnamed meshes (positional fallback); this is intentional (keeps a fully registered template interactive) and is asserted as-is in `mesh-slot-mapping.test.cjs` | `meshSlotMapping.ts` | FacilityService FE | Accepted, documented |
| Pre-existing test issues (not SPARK-238) | `tests/frontend/operator-provisioning.test.cjs` = 7 pass / 10 fail (stale stub import `../dashboard/BackupCapacityPanel`); 4 `tsc --noEmit` errors in untouched files | frontend harness | Existing backlog | Open — not caused by this work |
