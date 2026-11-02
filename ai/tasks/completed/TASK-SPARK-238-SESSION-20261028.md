# TASK-SPARK-238 — Session 2026-10-28

## 1. Session Information

| Field | Value |
|---|---|
| Session ID | TASK-SPARK-238-SESSION-20261028 |
| Date | 2026-10-28 (continued 2026-10-29, 2026-10-30, 2026-10-31, 2026-11-01) |
| Agent | Orchestrator (multi-session; subagent delegation used from 2026-10-30, see §6) |
| Branch | `feat/SPARK-238-facility-structure-port` (base `origin/develop` = `ea0d212c`, after the SPARK-228 merge and the `ParkingService`→`FacilityService` / `ReservationService`→`OperationService` rename). The earlier `feat/SPARK-238-facility-structure` could not be rebased and was re-delivered commit-by-commit — see §3.8 |
| Related task | SPARK-238 (US-OW01 / FR-LOT-02 / UC-LOT-01, Sprint 2) |
| Status | **Phase 1 APPROVED 2026-10-29**; **Phase 2 implemented and DB-verified** (P2a–P2d, P2f, P2g; P2e/P2h blocked, see §6/§3.8); **Phase 3 UI ported and green**; **Phase 4 acceptance tests green** (22 DB-backed). **Phase 5 packaging CLOSED 2026-11-01**: owner-UI defects fixed (§3.9), the maintenance overlay wired into the migration path, A1-A12 traceability added, branch pushed and a PR opened against `develop` (§8). Ported onto `develop` and committed as `76630c39` (a), `ac849ea7` (b), `cd99422a` (c), `ce76fd05` (d) — see §3.8 |

## 2. Objectives

1. P0.1 Audit the existing structure endpoints against FR-LOT-02 and produce a gap table.
2. P0.2 Read the `ParkingStructure` domain aggregate and the repository to confirm current limits.
3. P0.3 Record the accepted decisions Q1/Q2/Q3 (planning file §13) as binding inputs for the contract.
4. P1.1 Author the missing contract parts (ownership, multi-value compatibility, soft-delete/resurrect, capacity projection, maintenance window) in `docs/03-design/api/parking-structure-api-design.md`.
5. P1.2 Reconcile the OpenAPI surface with real routes.
6. P1.3 Produce PM review notes.

No production code is changed in Phase 0/1.

## 3. Work Completed

### 3.1 P0.2 — Current implementation inventory (verified by reading source)

| Area | Fact | Evidence |
|---|---|---|
| Aggregate | `ParkingStructure` holds site/units/slots/paths/backup policies + `protectedSlots` + `hasLiveCommitments` + capacity claims | `Domain/ParkingStructure.cs` |
| Hierarchy | `UnitType { FLOOR, ZONE, BLOCK }`; `AddUnit` requires `parent is null \|\| (int)type > (int)parent.Type` → max 3 levels, cycles impossible by ordering | `ParkingStructure.cs:125` region, `AddUnit` |
| Slot vehicle model | `ParkingSlot` carries **one** `VehicleType`; no compatibility collection | `ParkingStructure.cs:14` |
| Duplicate identifiers | `AddSlot` checks case-insensitive duplicates **over loaded (live) rows only**; the database enforces a full `UNIQUE (tenant_id, site_id, slot_code)` | `ParkingStructure.AddSlot`; `02-facility-service-db.sql:84` |
| Persistence of compat | `CommitAsync` writes `supported_vehicle_type` only; `slot_compatibilities` is never read or written by the service | `Persistence/PostgresParkingStructureRepository.cs:144-155` |
| Soft delete | `CommitAsync` soft-deletes paths → slots → units (`deleted_at`), and `Load` filters `deleted_at IS NULL` | `PostgresParkingStructureRepository.cs:129-134, 202-211` |
| Capacity invariant | Every edit requires `site.total_physical_capacity == slots.Count` else `STRUCTURE_CONFLICT`; `CommitAsync` writes `total_physical_capacity = slots.Count` | `Application/ParkingStructureService.cs:149-153`; repository `:127` |
| Mutation safety | `SafeChange(affected)` throws `STRUCTURE_IN_USE` whenever `hasLiveCommitments` is true (site-wide, blanket) and when an affected slot is occupied/UNKNOWN/protected/reserved | `ParkingStructure.cs:64` |
| Deactivation | `SetActive(false)` calls `SafeChange(all slots)` and sets `status='INACTIVE'`, `IsActive=false` | `ParkingStructure.cs:125-126` |
| Listing | `ListAsync` filters `deleted_at IS NULL` only; **no status/active filter** | `PostgresParkingStructureRepository.cs:39` |
| Idempotency | Only `POST /operations` wraps actions `removeSlot, removeUnit, capacity, active, moveSlot, configureSlot`; `POST /units`, `POST /slots`, `POST /paths` and `profile/renameUnit/add*/removePath/backup/markBackup` are not idempotent | `Application/StructureOperations.cs` |
| Owner-supplied coordinates | `configureSlot` accepts and persists `coordinates3D` and `features` | `Application/StructureEditHandler.cs`; repository `:148-154` |
| Operator layout route | `GET /api/parking-lots/{id}/operator-layout` is authorized through `OperatorSiteClient.ScopeAsync` → UserService `api/operators/site-scope?siteId=` (assigned-operator scope). Correcting an earlier note in this session: it is **not** unauthenticated. It does return the full aggregate including `capacityViews`. | `API/Program.cs:165`; `Infrastructure/OperatorSiteClient.cs`; `Application/OperatorLayoutService.cs` |
| Read-side arithmetic | `CapacityViews` is computed during serialization (`Views()`), and `View()` throws `CAPACITY_CONFLICT` when `available < 0` → an inconsistent site makes `GET /structure` fail instead of rendering | `ParkingStructure.cs:37, 88-89` |
| Legacy NULL compatibility | `supported_vehicle_type` is nullable in the schema, but `Load` calls `Enum.Parse<VehicleType>` on it → any NULL row breaks the whole structure read | `PostgresParkingStructureRepository.cs:207`; `02-facility-service-db.sql:88` |
| Seed path format | `sprint2-seed.sql` writes unit paths `'ZoneA'`, `'B1'`, `'B1/VIP'` while the domain generates `/{siteId}/…/{unitId}/`; prefix matching still works but the formats disagree | `sprint2-seed.sql:56-59`; `ParkingStructure.cs:136` |
| Maintenance windows | No table, column, route or constant exists anywhere in the repository | grep of `*.sql`, `src/**` |
| Reservation impact projection | `POST internal/structure/{siteId}/impact` returns reservations with `expectedStart`/`confirmedAt` but **no end time** | `ReservationService StructureSafetyStore.ImpactAsync` |

### 3.2 P0.1 — Gap table (existing routes × FR-LOT-02)

FR-LOT-02 requires: lot/floor/zone/row/space identifiers; access paths; compatible vehicle categories; category-specific pools; map associations; three templates (outdoor, multi-floor, multi-floor basement) with no artificial floor outdoors; car and motorcycle capacity not interchangeable; fixed 3D templates as deployment assets; no automatic mixed-category conversion; no EV charging in MVP.

| # | FR-LOT-02 clause | Current coverage | Verdict |
|---|---|---|---|
| G1 | Identifiers lot→floor→zone→**row**→space | `POST /{id}/units` + `spatial_units` support FLOOR→ZONE→BLOCK (3 levels) | **PARTIAL** — no `ROW`; DB `CHECK (unit_type IN ('ZONE','FLOOR','BLOCK'))` also blocks it |
| G2 | Three lot templates; outdoor has no artificial floor | No template/archetype field on `parking_sites`; a floorless lot is only implied by allowing a root-level `ZONE` | **MISSING** — nothing records which template a lot uses |
| G3 | Compatible vehicle categories (plural) | Single `parking_slots.supported_vehicle_type`; `slot_compatibilities` unused | **MISSING** — see Q2 |
| G4 | Category-specific pools (`spatial_unit_capacities`) | Table exists but no route or code reads/writes it; `spatial_units.max_capacity` is a single cross-category number; pool totals are derived from slot counts | **MISSING** for configured pools (derived capacity works) |
| G5 | Map associations | `POST /{id}/paths` accepts `mapData`; slots carry `coordinates_3d`/`features` writable by the Owner | **PARTIAL** — A9 violation, coordinates must not be Owner input |
| G6 | Access paths | `POST /{id}/paths`, `PATCH removePath`, both same-lot validated | **SUFFICIENT** |
| G7 | Car/motorcycle capacity not interchangeable | Capacity views and outcome pools are computed per `VehicleType` | **SUFFICIENT** |
| G8 | Fixed 3D templates are deployment assets | Assets served statically (`FE/public/models/parking/*.glb`); no upload route | **SUFFICIENT** (3D binding belongs to SPARK-228) |
| G9 | No mixed-category conversion / no EV in MVP | No conversion logic exists; `SlotType.EV` exists as a slot attribute only | **SUFFICIENT** (out of MVP scope, no behaviour required) |
| G10 | Ownership scope on every action (A3) | Owner routes call `Scope(...)`; Operator routes call `OperatorSiteClient.ScopeAsync`; the repository always filters `tenant_id`, and a foreign site returns `SITE_NOT_FOUND` | **SUFFICIENT** (implementation) — still needs A3 tests for every route |
| G11 | Soft delete then re-register same code (A5) | `AddSlot` sees only live rows, so re-adding a removed code reaches the full table-level unique → `23505` → `DUPLICATE_IDENTIFIER` | **BROKEN** — must resurrect |
| G12 | Deactivate with live commitments (A10/A11) | Blanket `STRUCTURE_IN_USE` whenever any live commitment exists at the site; no window, no commitment list | **MISSING** — see Q3 |
| G13 | Replay of `idempotencyKey` (A8) | Only 6 actions are wrapped by `/operations`; per-item creation routes accept no key | **PARTIAL** |
| G14 | Capacity displayed = registered slots (A7) | Read filters soft-deleted rows; `total_physical_capacity` is rewritten to `slots.Count` on each edit | **SUFFICIENT** at read level; see R-1 below |

### 3.3 Additional defects found during the audit

| ID | Defect | Impact |
|---|---|---|
| R-1 | `sprint2-seed.sql:48-51` inserts sites with `total_physical_capacity` 100/300 and **zero** slots, which violates the edit precondition in `ParkingStructureService.EditAsync` | Every edit on the seeded lots fails `STRUCTURE_CONFLICT`; any legacy row whose declared capacity ≠ slot count is un-editable |
| R-2 | **Corrected 2026-10-28:** the earlier note claiming the design doc cited a non-existent index was wrong. `scripts/database/overlay/parking-slot-constraints.sql:11` does create `uq_site_slot_code_ci ON parking_slots(tenant_id, site_id, upper(slot_code))` (non-partial); the base schema separately has `uq_active_slot_code_ci ON parking_slots(spatial_unit_id, upper(slot_code)) WHERE deleted_at IS NULL` | Both indexes exist. Because the site-wide one is not partial, a removed code can never be re-INSERTed under any case variant → A5 can only be satisfied by resurrecting the soft-deleted row |
| R-3 | `ListAsync` has no status filter, so a deactivated lot still appears in `GET /api/parking-lots` | A4 wording ("lot disappears from Owner's list") conflicts with the need to reactivate it |
| R-4 | `RemoveUnit` requires an empty unit (no children/slots/paths) | Aligned with FR-LOT-04 (no silent cascade), but means deleting a subtree is N explicit calls — must be documented, not "fixed" |
| R-5 | Reservation `ImpactAsync` projection exposes no commitment end time | A10's `(id, type, start, end)` list cannot be produced without extending the Reservation projection |
| R-6 | `3d-digital-twin-openapi.yaml` declares `GET /operational-layout` with anonymous/Driver access and a SignalR hub, while the service requires an assigned Operator and has no hub | Spec drift on the 3D read path; owned by SPARK-228/SPARK-244. Not changed here beyond the rejected batch route |
| R-7 | PostgreSQL structure tests never run. `DatabaseFixture.CreateAsync` reads `scripts/database/microservices/02-parking-service-db.sql` and `03-reservation-session-service-db.sql`, both removed by the Facility/Reservation split, so the 9 `[StructurePostgresFact]` tests stay skipped unless `SMARTPARK_STRUCTURE_TEST_CONNECTION` is set. Repointed to `02-facility-service-db.sql` + `overlay/parking-tenants.sql` + `03b-reservation-operations-service-db.sql` + `overlay/reservation-site-capacity-pools.sql`, 7 of 10 tests pass | Real regression safety for structure work is unverified. The 3 remaining failures expose a genuine ordering defect: `StructureSafetyStore.ReleaseAsync` updates `structure_site_state` / `site_capacity_pools` before deleting the hold, so `guard_structure_edit` raises `55P03`; EF wraps it in `DbUpdateException`/`InvalidOperationException` while those tests expect a raw `PostgresException`. Harness paths were restored to their committed state rather than half-fixed |
| R-8 | **Found during PM-review remediation (2026-10-29).** The gate claim query in `Persistence/OperatorGateSlotStore.cs:28` filters on `supported_vehicle_type = $3` and never consults `slot_compatibilities`. It is the **only** consumer of the projection column that authorizes an allocation | A dual-compatible slot is invisible to a claim for its secondary category, so the approved Q2 model ("table is the source of truth") is contradicted at the exact point where a vehicle is assigned a slot. Makes F05 a P2 code change, not a documentation change |
| R-9 | **Found during PM-review remediation.** With `vehicleType = "ANY"` the claim predicate short-circuits (`$3 = 'ANY' OR supported_vehicle_type = $3`), so a slot with a NULL `supported_vehicle_type` **is claimable**; the returned row is then read with `GetString(2)`, which throws `ArgumentNullException` and surfaces as a 500 | A legacy NULL row can break walk-in claiming as well as the structure read. The review did not catch this; it reinforces DE-6 (legacy NULL must be reported `unclassified`, never defaulted) |
| R-10 | **Found during PM-review remediation.** `3d-digital-twin-api-design.md:291`, `3d-digital-twin-spark-228-api-design.md:256` and `3d-digital-twin-spark-244-api-design.md:243` state the legacy route is `/operator-layout` at `Program.cs:142` and the canonical route is `/operational-layout` at `Program.cs:165`; the source has both line references **inverted** | Documentation defect in SPARK-228/SPARK-244 scope, not fixed here. Recorded in the SPARK-238 contract as a route-reconciliation note so the drift is not lost |

### 3.4 Change applied during this session — `structure_removed_resources` dropped

Pre-existing table in `smartpark_reservation` (`overlay/reservation-structure-safety.sql:28`). It was written only by `StructureSafetyStore.ReleaseAsync`, read only by `guard_structure_edit()`, never cleaned, and referenced by no other service or frontend code. It could never hold a live-referenced resource, because `SafeChange()` in the Parking aggregate refuses any structure edit while a live commitment exists — so the trigger branches it fed were inert except for one harmful effect: a slot that was removed and later resurrected stayed blocked, which is exactly the A5 path.

Decision: drop the table and the tombstone write; keep the hold (`55P03`), commitment-scope immutability (`23514`) and inactive-site (`23514`) rules.

| File | Change |
|---|---|
| `ReservationService.Domain/Entities/ReservationEntities.cs` | `StructureRemovedResourceRow` deleted |
| `ReservationService.Persistence/ReservationDbContext.cs` | DbSet + `Map<StructureRemovedResourceRow>` removed |
| `ReservationService.Persistence/StructureSafetyStore.cs` | tombstone insert loop removed from `ReleaseAsync`; `EditOutcome.RemovedSlots/RemovedUnits` kept because the release receipt compares the outcome JSON |
| `scripts/database/overlay/reservation-structure-safety.sql` | table DDL removed; `CREATE OR REPLACE FUNCTION` now precedes `DROP TABLE IF EXISTS` (plpgsql resolves table names at runtime, so dropping first would raise `42P01` for every write to the four guarded tables); dead `slot`/`unit` variables removed; message now `Resource is inactive` |
| `scripts/database/integration/03-reservation-structure-safety.sql` | same change in the reference mirror |
| `tests/Services/SmartParking.ParkingService.Tests/PostgresStructureWorkflowTests.cs` | two tombstone asserts replaced: table absence and absence of any `structure_removed_resources` reference in the function body |

Applied to the running `operations-db-1` with the non-destructive delta (`docker cp` + `psql -f`, no `-RecreateDb`) because another session is working on SPARK-228. The table held 0 rows, so no export was taken, per instruction. Verified after apply: `to_regclass('structure_removed_resources') IS NULL` → true; 4 `guard_structure_edit` triggers plus the untouched `reservation_confirmation_time` present; 0 `pg_proc` rows whose definition still references the table. Pre-change rollback snapshot: `%TEMP%\opencode\spark238-tombstone\` (`rollback-guard_structure_edit.sql`, `triggers-before.txt`).

Not rebuilt: `smartpark-app-reservation-service-1` still runs the pre-change binary. Its release path would fail with `42P01` only when a structure edit actually commits (the write sat inside `if (outcome is not null)`), and a failed publish could leave that site's synchronisation needing recovery on the next read. Rebuilding/restarting the container is an app start/stop, which requires explicit permission.

### 3.5 PM review disposition (2026-10-29) — F01 to F16

The PM review at `D:\My file\FA26 - OJT FSoft\Report\SmartPark-238-API-Session-Review-2026-10-29 (1).md` was written against a **v1.1 export** that predates the SPARK-238 contract section: it reports the API header as v1.1, quotes the "Selective slot registration" section as open, and states that the SPARK-238 contract and DE-1…DE-11 are absent. It also states it did not inspect current source, deployed schema, planning §13 or the updated OpenAPI. Those are facts about the attachment, not about the working tree, so each finding is dispositioned below with the evidence that decided it.

| Finding | Verdict | Evidence and action |
|---|---|---|
| F01 — supplied file is not the claimed version | **Valid as an evidence complaint** | Attachment was stale; the working tree has the contract (contract §88 onward, header v1.2 then v1.3). Action: header now carries approval refs, a review-evidence row and this disposition table; §5 lists every changed file with its purpose |
| F02 — registration vs mandatory atomic batch deactivation | **Valid; real gap** | The contract deferred the D11 wire shape to V11-DES-09. Action: concrete `POST /{id}/structure/batch` contract added (see §4 D-9) |
| F03 — scheduling maintenance ≠ immediate deactivation | **Valid in part** | The two concepts were already separate rows, but lifecycle `status` and derived availability were conflated, boundaries were closed-interval, and reopening had no guard. Action: two-concepts framing, half-open `[start,end)`, future windows never write `status`, reopen blocked while another active closure covers now, late exits conflict at activation, guards not globally narrowed |
| F04 — overlap inspection ≠ concurrency protection | **Valid; real gap** | No enforcement acknowledgement existed. Action: `enforcement_state`/`enforcement_ref`/`acknowledged_at` + `MAINTENANCE_UNCERTAIN` + 6-point protocol; a saved row alone never means effective |
| F05 — compatibility must not multiply capacity | **Valid, and worse than the review knew** | Non-multiplication invariant added **and** a defect the review missed: `OperatorGateSlotStore.cs:28` is the only allocation consumer and ignores `slot_compatibilities` (R-8). Action: allocation-consumer row with the replacement predicate; explicit `primaryVehicleType` (no inference); DE-14 P2 work |
| F06 — stale-intent protection for per-item creates | **Valid; contract defect** | The earlier contract auto-resurrected on any `POST /slots`, so a delayed retry could undo a deliberate delete. Action: creates never restore; `409 IDENTIFIER_RELEASED`; explicit `restoreSlot`/`restoreUnit`/`restorePath`; per-item outcome table + retry rows |
| F07 — capacity repair must preserve meaning | **Valid** | Seed declared 100/300 with zero slots; the read threw `CAPACITY_CONFLICT` during serialization. Action: seed corrected to 0; typed read DTO with `projectionState`; three separate numbers; reconciliation replaces the hard edit precondition; DE-16 ownership split with SPARK-228 |
| F08 — batching does not inherently delete/recreate | **Valid wording criticism** | Action: rejected thing restated as destructive **replace-all** ingestion, not batching or the HTTP verb |
| F09 — uniqueness text vs R-2 | **Valid; contract contradicted the schema** | `overlay/parking-slot-constraints.sql:12` creates the non-partial site-wide CI index. Action: uniqueness row corrected, DE-7 resolved, normalization specified |
| F10 — `/operational-layout` vs `/operator-layout` | **Valid, and the review was told the wrong thing** | Both routes exist with different authorities (`Program.cs:142`/`:165`), and the three 3D design docs invert the line references (R-10). Action: two rows + reconciliation note; no route change in SPARK-238 |
| F11 — deprecated operation still in Swagger | **Valid** | Action: the batch path was deleted from `paths:` (OpenAPI 1.4.0); schemas retained as review history |
| F12 — Q4 inactive lots | **Valid** | Action: listing includes inactive/maintenance lots with status and an optional filter; Driver discovery stays a separate contract |
| F13 — coordinates/features over-restriction | **Valid point, different resolution** | Restricting both fields entirely is defensible because no canonical Owner-settable non-geometric attribute exists (FR-LOT-02 excludes EV charging), and an open JSON bag cannot be validated. Action: both fields server-owned, with a documented rationale rather than an invented allow-list; wire-change impact recorded; no client sends either field today |
| F14 — ROW / template deferral | **Accepted** | `ROW` stays deferred (DE-1, V11-DES-04); outdoor root zone is the supported floorless representation (DEC-LOT-01) |
| F15 — maintenance schema and routes before approval | **Satisfied** | DDL + four routes + state semantics now in the contract and approved 2026-10-29 |
| F16 — genuine validation reporting | **Accepted** | YAML could not be parser-validated (no PyYAML, no Node YAML library, no `node_modules`); §7 states that plainly instead of claiming a check that did not run |

Not remediated here, by design: the review's own `§2` items are decisions, not defects — they are preserved rather than re-litigated.

### 3.6 Phase 2 implementation and Phase 4 acceptance tests (2026-10-29)

Isolated lane: container `facility-db-1` (unchanged, shared), host port 5442, database **`smartpark_facility_238`** — the SPARK-228 lane keeps `smartpark_facility` untouched (8 slots before and after every step). No container, volume or port was added. Demo data seeded into the 238 database: tenant `1111…1111`, site `1000…0001`, floor `2000…0001`, zone `2100…0001`, slots `3000…0001…0008`, `total_physical_capacity = 8`.

New approved DDL: `scripts/database/overlay/parking-structure-maintenance.sql`. **Deviation found while applying it: `window` is a reserved PostgreSQL keyword** (`CREATE TEMP TABLE t (window tstzrange)` → `syntax error`). The column is therefore `service_window`; the wire field stays `window`/`start`/`end`. The overlay is deliberately **not** yet added to `apply-workflow-migrations.ps1` (that script is shared with the SPARK-228 lane) — add it at merge.

| Step | Change |
|---|---|
| P2a | `slot_compatibilities` is the source of truth: `CommitAsync` deletes and re-inserts the set for every live slot in the same transaction; `Load` rebuilds it with a `[column]` fallback; `OperatorGateSlotStore` claim predicate prefers the table and only falls back to the projection when a slot has no rows; the NULL projection no longer throws on claim. `SlotCompatibility.Primary()` is the single resolver shared by `POST /slots` and `PATCH configureSlot`. `POST /slots` accepts `vehicleTypes` + `primaryVehicleType` |
| P2b | `configureSlot` rejects a non-null `coordinates3D`/`features` with `INVALID_STRUCTURE` "Slot geometry comes from the lot's fixed 3D template."; loaded geometry passes through untouched |
| P2c | The hard `TotalPhysicalCapacity != Slots.Count` edit precondition is replaced by reconciliation; the write path still stores the live slot count |
| P2d | `restoreSlot` / `restoreUnit` / `restorePath` plus `ReleasedIdentifierException` → `409 IDENTIFIER_RELEASED` with `released{id, code, deletedAt}`. Creates never resurrect a released identifier; the INSERT-vs-UPDATE decision uses live ∪ released so a restore updates the original row and keeps its id |
| P2f | `structure_maintenance_windows` store + four routes (`GET`/`POST` collection, `PATCH` extend-only, `DELETE` cancel), half-open `[start, end)`, `23P01` → `409 STRUCTURE_IN_USE`, `serviceState` added additively inside `data` of `GET /structure` |
| P2g | `POST /api/parking-lots/{id}/structure/batch` — 1–50 deactivation-family items, `Idempotency-Key` required, per-item `NOT_FOUND` for foreign/missing ids, all-or-nothing through one gate/hold/transaction/audit/`structure_operations` row, `expectedUpdatedAt` recheck at commit |
| P4 | `StructureSafetyStore.ReleaseAsync` defect fixed: the `structure_edit_holds` row is now deleted **and flushed before** the `structure_site_state` / `site_capacity_pools` writes (the `guard_structure_edit()` trigger raised `55P03` otherwise); `SaveAsync` surfaces the raw `PostgresException` and clears the EF tracker so a retry starts from committed state. Fixture repointed from the pre-split script paths to the current `microservices/` + `overlay/` files, which is what un-skipped the 9 Postgres tests. 13 new acceptance tests added |

Blocked, deliberately not approximated: **P2e** (narrowing `SafeChange`'s site-wide `hasLiveCommitments` guard needs resource-scoped commitment data Reservation does not project — DE-13) and **P2h** (the typed read DTO that stops `CapacityViews` throwing during serialization; `ReadAsync`/`ReadOperationalAsync` are SPARK-228-owned per planning §11 — DE-16).

### 3.7 Phase 3 (P3) — Owner facility-structure UI (2026-10-29)

Implemented in `FE/` only; no backend file was touched. Pure logic was separated from the components so it is testable without a DOM: `FE/src/lib/structureRules.ts` (hierarchy/name rules, explicit compatibility-primary resolution, registration validation + per-item aggregation, half-open window rules) and `FE/src/lib/3d/meshSlotMapping.ts` (identifier normalisation, candidate classification, registered-slot matching). The Owner screen was rebuilt from `FE/src/roles/owner/structure/ParkingStructure.tsx` around panels for the spatial tree (`StructureTree.tsx`, `UnitDialog.tsx`), mesh-driven slot registration (`SlotRegistrationPanel.tsx`), registered slots + compatibility (`RegisteredSlotsPanel.tsx`, `SlotCompatibilityDialog.tsx`), access paths (`AccessPathsPanel.tsx`), service availability and maintenance windows (`ServiceAvailabilityPanel.tsx`), the three-number capacity summary (`CapacitySummary.tsx`), the reusable `409 IDENTIFIER_RELEASED` decision (`ReleasedIdentifierDialog.tsx`), shared feedback (`StructureFeedback.tsx`) and a one-in-flight mutation runner (`useStructureAction.ts`).

Behaviour worth recording:
- **A12 (inert unregistered meshes) is now real:** `FE/src/lib/3d/slotMatcher.ts` gives slot identity only to meshes mapped to a registered slot; unregistered template meshes are exposed separately and are inert in normal mode (no hover identity, no click-to-claim, not counted). They are selectable only inside explicit registration mode. Matching order is exact normalised code → hyphen-insensitive alias → positional pairing only when the remaining mesh and slot counts are exactly equal; a partial positional guess was rejected on purpose because it would make arbitrary meshes interactive.
- No coordinate or `features` input exists anywhere in the UI (A9 enforced by omission), and the compatibility dialog states that those fields are server-owned.
- The capacity panel never merges the three numbers and never fabricates one: when `capacity` is absent from the response it shows the live registered count as an explicitly labelled derived count and marks the other two as not reported.
- Multi-select deactivation uses the atomic batch route for slots; units and paths still use their single-resource actions.
- `expectedUpdatedAt` is never sent, because no read response exposes a token yet (DE-16).

Verification (orchestrator re-ran independently): `npm run build` in `FE/` → **built in 5.57 s, exit 0**; `node --test tests/frontend/parking-structure-rules.test.cjs` → **19 tests, 19 pass, 0 fail**. The full `tests/frontend/*.test.cjs` suite has **56 pre-existing failures** from the harness's module resolution (`Unexpected import: …/components/ui/RoundedSelect`, `PageHeader`, and an `auth-api` fixture without `require`); the count is unchanged by this work (73 tests / 17 pass before, 92 / 36 after). `npx tsc --noEmit` still reports three type errors in files this task did not touch (`OperatorDashboard.tsx:307`, `SiteManagement.tsx:61`, `useSiteManagement.ts:93`). No frontend dependency was added; `npm ci` only materialised the absent `node_modules` and neither manifest nor lockfile changed.

### 3.8 Port to FacilityService/OperationService and 228↔238 matcher reconciliation (2026-10-30)

While SPARK-238 was in Phase 2/3, `origin/develop` merged SPARK-228 (PR #36) and renamed `ParkingService` → `FacilityService` and `ReservationService` → `OperationService`. The Phase 1–3 commits could not be rebased (two waves of conflicts plus a rename/relocation rewrite), so on user direction the work was re-delivered commit-by-commit onto a fresh branch `feat/SPARK-238-facility-structure-port` (base `origin/develop` = `ea0d212c`):

| Commit | Contents |
|---|---|
| `76630c39` | (a) contract/docs — `parking-structure-api-design.md` v1.3 APPROVED, `3d-digital-twin-openapi.yaml` 1.4.0, `sprint2-seed.sql`, data-dictionary rows, session log |
| `ac849ea7` | (b) `structure_removed_resources` drop ported to `OperationService` (7 files) |
| `cd99422a` | (c) P2 backend ported to `FacilityService`/`OperationService` — 18 files, +1589/−202 |
| `ce76fd05` | (d) P3 owner facility-structure UI port — 20 files, +4126/−397 |

The stale `src/Services/ParkingService/` and `src/Services/ReservationService/` trees and the leftover `tests/Services/SmartParking.ParkingService.Tests/` (obj artifacts only, 0 tracked files) were removed. All 22 gated Postgres tests pass on the port branch (`SMARTPARK_STRUCTURE_TEST_CONNECTION` against `smartpark_facility_238` on `facility-db-1:5442`); the ungated solution is green (`dotnet build` 0/0).

#### 228↔238 conflict at `FE/src/lib/3d/slotMatcher.ts` (resolved — Option A)

The P3 FE port initially took `slotMatcher.ts` wholesale from SPARK-238, which **regressed SPARK-228's contract test**. Measured with a controlled file swap:

| `slotMatcher.ts` | `tests/frontend/operational-layout-adapter.test.cjs` |
|---|---|
| develop (`ea0d212c`, 228) | **16/16 pass** |
| P3 (238) | **14/16 pass, 2 fail** (`Error: Unexpected import: ./meshSlotMapping`; changed result fields; tolerant-vs-raw-exact semantics) |

Resolution (user Option A): `git checkout origin/develop -- FE/src/lib/3d/slotMatcher.ts` restores 228's raw-exact matcher unchanged; 238's tolerant scene-level binding moved into a **new, owner-clean module** `FE/src/lib/3d/registeredMeshMapping.ts` (it could not go into `meshSlotMapping.ts`, which must keep **zero imports** because `tests/frontend/parking-structure-rules.test.cjs` loads it with a `require` that throws). `Parking3DViewer.tsx` computes **both** mappings at each call site and never mixes sources for one decision. `matchSlotCodes`/`matchSlotCode` in `operationalLayout.ts` were **retained** because 228's contract test still consumes them.

**Ownership boundary (durable):**

| Concern | Owner module | Semantics |
|---|---|---|
| Operational-layout overlay colouring (`slotMatcher.ts` + `matchSlotCodes` in `operationalLayout.ts`) | **SPARK-228** | raw-exact binding; `unmatchedSlotCodes`/`unmatchedMeshNames`; must not be re-used or re-shaped by 238 |
| Slot registration / hover-click-pick / inert unregistered meshes (A12) / census / focus | **SPARK-238** (`registeredMeshMapping.ts` + `meshSlotMapping.ts`) | tolerant matching (normalised-exact → hyphen alias → all-or-nothing positional fallback) |

Gate results after the split: `operational-layout-adapter.test.cjs` **16/16**; `parking-structure-rules.test.cjs` **19/19**; `npm run build` in `FE/` exit 0; `npx tsc --noEmit` still only the 4 pre-existing errors in untouched files (`GroupedNumberInput.tsx`, `OperatorDashboard.tsx`, `SiteManagement.tsx`, `useSiteManagement.ts`); `slotMatcher.ts` byte-identical to `origin/develop` (`b0c30f87acc…`).

Deferred: merging the two normalisers into one shared module (not done in 238 — it would couple the raw-exact 228 contract to 238's tolerant rules).

Cross-service findings recorded at the same time (both **BLOCKED, deferred, do not implement in 238**):

- **P2h/DE-16** — `FacilityService` still returns the `ParkingStructure` aggregate from `GetAsync`/`ReadAsync`; there is no typed read DTO carrying `capacity`, `compatibilitySource` or `primaryVehicleType`, so the DE-8 read-safety fix must land with SPARK-228 (which owns the read path).
- **P2e/DE-13** — `OperationService.StructureController` exposes only site-scoped endpoints (`snapshot`/`impact`/`release`/`holds`) and `HoldSnapshot.HasLiveCommitments` is a single site-wide bool; there is no resource-scoped commitment projection, so `SafeChange` keeps its site-wide guard.

Merge checklist (recorded, not executed): during the merge to `develop`, `scripts/apply-workflow-migrations.ps1` must gain `overlay/parking-structure-maintenance.sql` (shared file — coordinate); merge order is SPARK-238 → `develop`, then SPARK-244 resumes the FacilityService consumer side.

### 3.9 Phase 5 — owner-UI defect fixes, packaging and closure (2026-10-31 / 2026-11-01)

Deployment diagnosis first: several reported failures were environment, not code. The running `parking-service` image (built 2026-10-29 17:23) predated the branch, so `/maintenance-windows` and `/structure/batch` answered 404; the frontend container was bind-mounted from a different checkout (`Mock-Project-Smart-Parking\FE`), so no Owner-UI fix of this branch was visible; and the deploy database lacked `structure_maintenance_windows` because the overlay had only been applied to the isolated 238 database. Rebuilding `parking-service`/`reservation-service` from the worktree, force-recreating `frontend` and applying the overlay to `smartpark_facility` removed the entire reported 404/405/500 family and the "Authentication request failed." message on the structure tab (that string is the generic fallback for a non-401 response whose body carries no `message`).

| Defect | Root cause | Fix (commit) |
|---|---|---|
| `PATCH action=backup` returned 500 | The backup-policy INSERT omitted `tenant_id`, which the deployed table declares NOT NULL, so Postgres raised 23502 | `PostgresParkingStructureRepository.CommitAsync` writes `tenant_id` (`3b218a53`) |
| Panels duplicated/repeated on every interaction; DEMO content persisted after switching site; 3D not interactive | Three sibling panels inside the gated fragment shared `key={siteId}` (React logged duplicate keys equal to the site id); separately, a structure edit notified the parent, whose site-list refetch churned the `sites` identity and re-ran the site-resolution effect | One `<Fragment key={siteId}>` for the whole block with the per-panel keys removed; `refresh()` no longer notifies the parent; the parent keeps the previous list identity on an equal refetch; the effect re-resolves only when the requested site or the available set changes (`8ed8b5d1`) |
| Unit capacity accepted above the model bay count | The dialog had only an integer/non-negative check, no upper bound | `UnitDialog` rejects a capacity above `useModelCarCapacity` for the selected template; `0` still means no layout limit (`8ed8b5d1`) |
| Unregistered meshes could not be selected | Every template mesh was already bound to a registered code, or the GLB had not loaded and the procedural fallback draws bays only for registered slots | `SlotRegistrationPanel` renders an explicit empty state naming both causes and the next actions (`8ed8b5d1`) |
| My Parking Lots rendered the empty state after a failed load | The protected list request ran before the session was restored, and the render fell through to the empty state even when the request had failed | The hook waits for `authReady`, maps 401/403 to actionable messages, and the page suppresses the empty state while an error is present (`2e404525`) |
| Edit site toasted success but the change reverted | The form sent only `action=profile`, so a status change was toasted and then reverted by the post-save refetch | The edit persists `active` as a second call and re-reads the list, verifying `code`/`name`/`address`/`isActive` before reporting success (`2e404525`) |
| A name-only edit rewrote the address | The form seeded city/district from the first catalogue entry and always recomposed the address | The stored address is parsed and sent back verbatim when untouched (`addressComposition.ts`, `2e404525`) |
| Indoor model reported 36 bays instead of 64 | The bay key stripped Blender suffixes before de-duplicating, collapsing distinct bays | `modelSlotCapacity` keys on the ancestor chain plus the preserved name; outdoor 48, indoor 64, underground 48 (`2e404525`) |

Packaging (D1-D8): the change set was split into six logical commits on top of the four port commits (see §8); `overlay/parking-structure-maintenance.sql` was added to `apply-workflow-migrations.ps1` (Parking script list, required relations and re-runnable feature set) and mounted in `compose.schema-integration.yml` (`7cd0f45f`); `docs/04-testing/requirements-traceability/SPARK-238-requirements-traceability.md` traces A1-A12 with the deferred debt table; the PM review findings F01-F16 remain dispositioned in §3.5.

Honest limits of this phase: `scripts/test-workflow-upgrade.ps1` was **not** run (it starts isolated containers, which needs explicit permission); the Postman collection and the browser flows were executed by the task owner, not reproduced by the agent; and the C5 fix was confirmed **structurally** (the Vite-served module contains a single `_jsxDEV(Fragment` wrapper and exactly one key binding) rather than observed in a browser.

## 4. Technical Decisions

| Decision | Reason | Alternatives | Impact |
|---|---|---|---|
| Q1 = NO batch `POST /structure`; per-item routes only | Avoids diff/reconcile against `deleted_at` and the full `UNIQUE (tenant_id, site_id, slot_code)`; model parsing is client-side and already per-lot-type | Single-call structural ingest; batch upsert endpoint | P3 registers slots by multi-select + N calls; A12 requires unregistered meshes to render inert |
| Q2 = `slot_compatibilities` is the source of truth; `supported_vehicle_type` remains a one-value projection written in the same transaction | Many-to-many is required by FR-LOT-02; keeping the column preserves fast reads and existing consumers | Drop the column (breaking, migration blocked); keep single value (fails FR) | P2 writes both; contract must define disagreement resolution = table wins |
| Q3 = deactivate allowed with a maintenance window `[start, end]`; block only if a commitment overlaps the window; return `409 STRUCTURE_IN_USE` with the overlapping commitments | Blanket blocking makes deactivation impossible in any active lot | Block whenever commitments exist (current); auto-cancel bookings (forbidden) | Requires `structure_maintenance_windows` (proposed) and an extended Reservation impact projection |
| Session log naming | `ai/rule.md` Rule 3 requires `TASK-<JiraKey>-SESSION-YYYYMMDD.md` | `SESSION-<date>-slug.md` used by the earlier unrelated file | This file follows Rule 3 |
| **D-5** — contract promoted to v1.3 APPROVED on the strength of DE-1…DE-11 + the maintenance-window resource | PM approved both on 2026-10-29 (planning §13 Q1–Q4 plus DE-1…DE-11) | Leaving it IN REVIEW until every F-finding closed (rejected: the findings were about the stale attachment, not the decisions) | Phase 1 gate is passed; Phase 2 may start. Review findings are dispositioned in §3.5 rather than re-approved |
| **D-6** — per-item creates never restore a released identifier (F06) | A delayed retry of an old create must not undo a deliberate delete; restore is a user decision | Automatic resurrection on `POST` (the previous contract); version-guarded upsert | New `409 IDENTIFIER_RELEASED` + explicit `restoreSlot`/`restoreUnit`/`restorePath`. FE "retry failed" must surface the released case as a choice |
| **D-7** — the read route returns a typed projection DTO, and capacity is three separate numbers (F07) | The domain aggregate's `CapacityViews` throws during JSON serialization, so a data fault becomes a 500; a read must never fabricate or fail on arithmetic | Keep serializing the aggregate, or return `available: 0` when inconsistent (both rejected) | P2 must build a DTO and must not touch `ReadAsync`/`ReadOperationalAsync` without agreeing ownership with SPARK-228 (DE-16) |
| **D-8** — compatibility is a predicate, never stock (F05) | One physical slot compatible with two categories still supplies one allocatable slot | Summing per-category eligibility (double counts) | Invariant `sum(category pool totals) == registeredPhysicalCount`; `primaryVehicleType` is explicit and never inferred |
| **D-9** — same-lot batch deactivation gets a concrete atomic route (F02) | N independent calls cannot satisfy D11's all-or-nothing rule; a third item failing must leave the first two untouched | Defer to V11-DES-09 (the previous deferral, which left D11 with no workable client contract) | `POST /api/parking-lots/{id}/structure/batch`, 1–50 items, `Idempotency-Key` required, `expectedUpdatedAt` per item, one gate/hold/transaction/audit; 409 per item with nothing applied |
| **D-10** — a maintenance window is only effective once the authoritative service acknowledges it (F04) | Facility cannot fence another service's booking transaction; interval overlap is an inspection, not a lock | Treating a saved window as effective (rejected: booking could still be accepted inside it); refusing all windows (rejected: Q3 approved them) | `enforcement_state ∈ {pending, acknowledged, failed}`; `pending` never reported as closed; unknown outcome reads as `MAINTENANCE_UNCERTAIN`; wire contract still OPEN (DE-13) |
| **D-11** — window boundaries are half-open `[start, end)` and a future window never writes lifecycle `status` (F03) | Lets "serving until start, closed from start, serving again at end" be stated without off-by-one ambiguity, and keeps "next Monday's maintenance" from deactivating the lot today | Closed intervals (overlap at a single instant is undefined); writing `status` for scheduled windows | Commitment ending exactly at `start` does not overlap; one starting at `end` does not. Reopening is blocked while another active closure still covers now |
| **D-12** — `coordinates3D` and `features` are both server-owned (F13) | Slot geometry comes from the fixed 3D template (A9) and no canonical Owner-settable non-geometric attribute exists; an open JSON bag cannot be validated | Inventing a feature allow-list (rejected: not in any requirement); allowing the fields with validation (rejected: nothing to validate against) | Rejecting non-null values is a wire change; no current client sends either field, and existing rows are preserved unchanged |

## 5. Files Changed

| File | Change | Reason |
|---|---|---|
| `ai/tasks/active/TASK-SPARK-238-SESSION-20261028.md` | Created (untracked) | Rule 3 checkpoint at Phase 0 completion; now also holds the F01–F16 disposition (§3.5) and decisions D-5…D-12 |
| `docs/03-design/api/parking-structure-api-design.md` | **+516/−20**: header → **v1.3 APPROVED** 2026-10-29 with approval/evidence rows; selective registration restated (F08/F09); new *SPARK-238 structure configuration contract*; typed read DTO (F07); two-concept deactivation + half-open windows + enforcement protocol (F03/F04); explicit restore actions and `409 IDENTIFIER_RELEASED` (F06); non-multiplying compatibility + allocation consumer (F05); concrete atomic batch route (F02); DE-1…DE-17 | D1/P1.1 — the contract the implementation must satisfy; the review's blocking findings are answered here |
| `docs/03-design/api/3d-digital-twin-openapi.yaml` | **+12/−69**: version 1.3.0 → **1.4.0**; the rejected `POST /{id}/structure` path block deleted from `paths:`; `info.description` explains the absence and keeps `BatchStructureIngest*` schemas as review history | P1.2/D2 + F11 — an active contract must not ship a rejected, never-implemented operation to generated clients |
| `scripts/database/seeds/sprint2-seed.sql` | **+8/−4**: `total_physical_capacity` 100/300 → `0` for the two slot-less sites, with an explanatory comment; path-format drift annotated | F07 — the old values made both seeded lots permanently un-editable |
| `scripts/database/overlay/reservation-structure-safety.sql` | **+20/−14**: `structure_removed_resources` DDL removed; `CREATE OR REPLACE FUNCTION` now precedes `DROP TABLE IF EXISTS`; dead variables removed; message → `Resource is inactive` | Drop the inert tombstone table (user-authorised, option A) |
| `scripts/database/integration/03-reservation-structure-safety.sql` | **+13/−11**: same change in the reference mirror plus a header note on statement order | Keep the non-mounted mirror consistent with the applied overlay |
| `src/Services/ReservationService/.../Entities/ReservationEntities.cs` | **−7**: `StructureRemovedResourceRow` deleted | No remaining reader or writer |
| `src/Services/ReservationService/.../Persistence/ReservationDbContext.cs` | **−2**: DbSet + `Map<StructureRemovedResourceRow>` removed | Same |
| `src/Services/ReservationService/.../Persistence/StructureSafetyStore.cs` | **−5**: tombstone insert loop removed from `ReleaseAsync`; `EditOutcome.RemovedSlots/RemovedUnits` kept | Same — the release receipt compares the outcome JSON, so those fields must stay |
| `tests/Services/SmartParking.ParkingService.Tests/PostgresStructureWorkflowTests.cs` | **+5/−2**: two tombstone assertions replaced with "table absent" and "function body does not reference the table" | Keep the regression signal without the table |
| `docs/02-architecture/database/data-dictionary.md` | **1 line**: `structure_removed_resources` removed from the Reservation overlay listing | Data dictionary must match the applied schema |
| `docs/02-architecture/database/microservices/03-parking-operations-service-db-docs.md` | **1 line**: same removal | Same |

Untouched on purpose: nothing under `FE/`, no ParkingService source, and no change to `ParkingStructureService.ReadAsync`/`ReadOperationalAsync` (SPARK-228 owns the read path; the DTO change is scheduled through DE-16).

This table records the **Phase 0/1** contract change set only. The port to `FacilityService`/`OperationService`, the owner-UI fixes and the packaging work are listed in §3.8 and §3.9 and in the commit list in §8.

## 6. Issues and Blockers

| Issue | Root cause | Resolution | Remaining risk |
|---|---|---|---|
| Subagent delegation unavailable | Root cause found 2026-10-29: `~/.config/opencode/opencode.json` overrode `explorer` with a typo'd model id `ckey/deepseek-v4.1-flash-ckey` (extra `v`), shadowing the valid preset. Corrected to `ckey/deepseek-v4.1-flash-ckey`; `fixer` and `observer` then probed successfully | Config is loaded once at startup, so this session kept running without delegation. No delegation was used for the remaining work | Slower Phase 2–4 execution in this session; a restart restores parallel lanes |
| No maintenance-window storage | Schema never modelled it | Approved `structure_maintenance_windows` table in P1 (PM 2026-10-29); P2 implements | If enforcement acknowledgement (DE-13) slips, windows stay `pending` and A10/A11 must be recorded as DEFERRED |
| Commitment end times absent from the impact projection | `ImpactAsync` selects `expectedStart` only | P1 documents the required additive projection fields (`expectedEnd`, `type`); the Reservation change must be scheduled with that owning service (DE-13) | Cross-service dependency may block A10 verification |
| A4 wording conflict | Planning file says the lot "disappears from Owner's list"; the list has no status filter and reactivation needs the row | **Resolved 2026-10-29 (Q4 / DE-2):** the lot stays listed with `status` + `serviceState` | Closed — `ListAsync` must not gain a blanket active filter |

## 7. Validation Results

| Check | Result |
|---|---|
| Build | `dotnet build SmartParking.slnx --nologo -m:1` after the P2 code changes → **succeeded, 0 warnings, 0 errors** (re-run by the orchestrator after P2f/P2g and after the test-only P4 change). The plain `--nologo` form hit a transient `MSB4166: Child node "11" exited prematurely` earlier and a `MSB3027`/`MSB3021` file lock while the API exe was running; `-m:1` was used throughout |
| Tests | **Green, with database-backed tests actually executing.** With `SMARTPARK_STRUCTURE_TEST_CONNECTION` set to the isolated server, `dotnet test SmartParking.slnx --nologo -m:1 --no-build` → `ParkingService` **88 passed / 0 skipped / 0 failed**, `Payment` 80 passed, `User` 74 passed / 14 pre-existing skips. The 9 formerly skipped `[StructurePostgresFact]` tests now run (R-7 fixed) and 13 new acceptance tests were added. The gated subset was re-run independently by the orchestrator: **22 passed / 0 failed** |
| DB-backed coverage (P4) | 13 new tests in `PostgresStructureWorkflowTests.cs` cover multi-value compatibility round trip, compatibility-as-a-predicate (a dual-compatible slot is not counted twice), legacy projection fallback + NULL projection claimable via `ANY` without throwing, rejected Owner geometry, explicit restore of a released code (including a claim by the secondary category afterwards), capacity reconciliation, maintenance windows (`pending` + `SCHEDULED`, `23P01` overlap → `409 STRUCTURE_IN_USE`, half-open adjacency, grow-only extension, cancel guard), atomic batch (per-item failure applies nothing, valid batch applies all, stale `expectedUpdatedAt` rejects, unsupported action/count rejected) and idempotency replay |
| Test isolation | Every DB test creates and drops only its own `structure_parking_test_*` / `structure_reservation_test_*` databases on the isolated container. `smartpark_facility` (SPARK-228 lane) was never modified; `smartpark_facility_238` served only as the `CREATE DATABASE` anchor. A mid-run container restart by another user aborted one suite run (`57P03`); it was retried after the container returned healthy and one orphan test-database pair was dropped |
| Test isolation (2026-11-02 update) | The separate `smartpark_facility_238` anchor was **dropped** and the gated tests now point at the shared `smartpark_facility` on the same `facility-db-1:5442`. It had been introduced only on the instruction of the task owner to keep the parallel SPARK-228 lane's database untouched; SPARK-228 is merged, and the database was never more than a `CREATE DATABASE` anchor (the fixture builds and drops its own `structure_parking_test_*` / `structure_reservation_test_*`), so the extra name added a failure mode for no benefit — recreating the container volume on 2026-11-02 deleted it and made 21 of 22 gated tests fail with `3D000`. Re-run against the shared anchor: **22 passed / 0 failed**. The test guide and the traceability file were updated accordingly |
| Lint | Not applicable |
| Final verification (2026-11-01, port branch HEAD) | `dotnet build SmartParking.slnx --nologo -m:1` → **Build succeeded, 0 warnings, 0 errors**; `dotnet test SmartParking.slnx --nologo -m:1 --no-build` → FacilityService **148 passed / 22 skipped**, IoT **1 passed**, Payment **88 passed**, User **103 passed / 34 skipped**, **0 failed**. The 22 skips are the `SMARTPARK_STRUCTURE_TEST_CONNECTION`-gated database tests, all green when the variable is set (22/22) |
| Frontend verification (2026-11-01) | `npm run build` exit 0; `npx tsc --noEmit` → exactly the 4 pre-existing errors in files this work did not touch; `parking-structure-rules` 24/24, `mesh-slot-mapping` 19/19, `address-composition` 9/9, `operational-layout-adapter` (SPARK-228 guard) 16/16 |
| Migration-script validation (2026-11-01) | `apply-workflow-migrations.ps1` parses cleanly (PowerShell AST parse) and `docker compose -f compose.schema-integration.yml --env-file .env config --quiet` exits 0. `scripts/test-workflow-upgrade.ps1` was **not** run: it starts isolated containers, which needs explicit permission |
| OpenAPI validation | **Structurally verified, NOT parser-validated (F16).** No YAML library is available: `python -c "import yaml"` → `ModuleNotFoundError`; `node -e require('yaml')` and `require('js-yaml')` → not installed; no `node_modules` at the repo root or under `FE/`. Verification was therefore by inspection and text search: the batch path block is absent from `paths:`, exactly two path entries remain, `components` sections are intact, and the file is 881 lines |
| Seed verification | `sprint2-seed.sql` reviewed by reading; both slot-less sites now declare `0`. Not executed against a database in this change set |
| HTTP-level verification | **Performed 2026-11-01.** In-process HTTP contract tests cover the structure routes (`FacilityStructureEndpointTests`, 18/18). On the running Docker stack, with a real Owner token: `PATCH action=backup` → 200; `GET /{id}/structure` → 200 with `serviceState.state=ACTIVE`; `GET /{id}/maintenance-windows` → 200 `[]`; `POST` window → 201 `pending`; `DELETE` window → 200; `PATCH markBackup` → 403 (correct: the demo Owner holds no `BACKUP_MARK`, DEC-LOT-04). The Postman collection (11 folders, 83 requests) was executed by the task owner and passes |
| Manual verification | Source reading only, anchored per row in §3.2–§3.5 and §3.3 (each claim cites file/line) |
| Session-start git status | `feat/SPARK-238-facility-structure`, working tree clean at the first message of the session |

## 8. Git Status

| Field | Value |
|---|---|
| Branch | `feat/SPARK-238-facility-structure-port` (base `origin/develop` = `ea0d212c`), pushed to `origin` |
| Last commit | `7cd0f45f chore(database): apply the maintenance-window overlay in the migration script [SPARK-238]` |
| Commits | **11 on the branch:** the four port commits (see §3.8) `76630c39` (a) contract/docs · `ac849ea7` (b) `structure_removed_resources` drop → OperationService · `cd99422a` (c) P2 backend → FacilityService/OperationService (18 files, +1589/−202) · `ce76fd05` (d) P3 owner facility-structure UI (20 files, +4126/−397); then `3b218a53` backup-policy `tenant_id` · `8ed8b5d1` owner structure screen fixes · `2e404525` My Parking Lots + site edit persistence · `c54301ae` HTTP/frontend/address tests · `8e1e47ae` testing docs + A1-A12 traceability · `7cd0f45f` maintenance overlay in the migration script · and the `chore(session)` commit that moves this log to `ai/tasks/completed/` |
| Working tree | **Clean** at the close of the session. The pre-port working tree (branch `feat/SPARK-238-facility-structure`, +1657/−207 across ParkingService/ReservationService) was re-delivered commit-by-commit and no longer exists as a live tree |
| Applied outside git | `operations-db-1` received the overlay/delta (no `-RecreateDb`). `smartpark_facility` received `parking-structure-maintenance.sql`, so the deployed database matches the overlay. `parking-service`, `reservation-service` and `frontend` were rebuilt/recreated from this worktree with explicit user permission |

## 9. Handoff / Next Steps

### P1.3 — PM review: **CLOSED 2026-10-29**

The seven items below were the P1 gate. PM approved DE-1…DE-11 and the `structure_maintenance_windows` resource on 2026-10-29; each item's outcome is recorded so P2 can proceed without re-litigating it.

| # | Question | Outcome (2026-10-29) |
|---|---|---|
| 1 | **Q4** — should a deactivated lot disappear from `GET /api/parking-lots`? | **Approved: keep it listed** with `status` + `serviceState`; optional `status` filter; no blanket active filter. "Disappears" is Driver discovery, a separate contract (DE-2 closed) |
| 2 | Maintenance windows: adopt the proposed table and the 4 new routes? | **Approved.** Site-scoped enforcement in this sprint; unit/slot scope rejected at the boundary rather than stored (DE-3), admission enforcement out of scope (DE-10) |
| 3 | Who enforces closure at admission? | **Approved: not Facility.** Requires the additive Operations/Reservation enforcement acknowledgement; windows stay `pending` until acknowledged and read as `MAINTENANCE_UNCERTAIN` otherwise (DE-13, wire contract still OPEN) |
| 4 | Commitment overlap list needs `end` per commitment | **Approved as additive Reservation projection work** (`expectedEnd`, `type`); scheduled with that owning service, not inside SPARK-238 |
| 5 | Does capacity block edits when the declared count disagrees? | **Approved: no — reconcile.** The derived count is rewritten on write; a configured limit is never overwritten by it (F07) |
| 6 | `ROW` unit type | **Approved as deferred** to V11-DES-04; Sprint 2 ships FLOOR→ZONE→BLOCK, which already satisfies A2's ≥3 levels (DE-1) |
| 7 | Owner-writable `coordinates3D`/`features` | **Approved: reject non-null** for both. No current client sends either field, so no client migration is needed today (D-12) |

### Next steps

**P5 packaging — CLOSED 2026-11-01**

| # | D-item | Outcome |
|---|---|---|
| D1 | Commit the FE capacity/empty-state work | Done — `8ed8b5d1` (structure screen) and `2e404525` (parking lots + capacity) |
| D2 | Commit the My Parking Lots / edit-site work | Done — `2e404525` |
| D3 | Commit the `tenant_id` fix and the new tests | Done — `3b218a53` and `c54301ae` |
| D4 | Add the maintenance overlay to the migration path | Done — `7cd0f45f` (`apply-workflow-migrations.ps1` script list + required relations + feature set, and the `compose.schema-integration.yml` mount) |
| D5 | A1-A12 traceability | Done — `docs/04-testing/requirements-traceability/SPARK-238-requirements-traceability.md` |
| D6 | Session log → `ai/tasks/completed/` | Done — this file |
| D7 | Push the branch and open a PR against `develop` | Done — branch pushed to `origin`; PR opened against `develop` |
| D8 | Record the out-of-scope debt | Done — DE-4, DE-13, DE-16 plus DE-3 and the A12 caveat, in the traceability debt table and §10 |

Forward-looking items:

1. **P2e — BLOCKED (do not approximate):** narrowing `SafeChange`'s site-wide `hasLiveCommitments` check to affected resources needs resource-scoped commitment data that Reservation does not project (DE-13). The site-wide guard stays until `expectedEnd`/`type` exist.
2. **P2h — BLOCKED on SPARK-228 (DE-16):** the typed read DTO that replaces serializing the domain aggregate (so `CapacityViews`/`CAPACITY_CONFLICT` can never throw mid-response) touches `ReadAsync`/`ReadOperationalAsync`, which planning §11 forbids this task from editing while SPARK-228 is in flight. Agree shared-file ownership and land it with or after SPARK-228.
3. **P3 — done** in the port (commit `ce76fd05`) and hardened in `8ed8b5d1`. Still open for a later task: honouring `expectedUpdatedAt` once DE-16 exposes a version token.
4. **P4 — done:** 22 database-backed tests (9 pre-existing + 13 new) plus 18 in-process HTTP contract tests and 24/19/9 frontend tests cover A5-A11, A1-A3 and A12. The review's maintenance-race scenario still cannot be written until DE-13 defines an enforcement acknowledgement.
5. Merge order: SPARK-238 → `develop`, then SPARK-244 resumes the FacilityService consumer side. The maintenance overlay is already wired into the migration path, so no merge-time script edit is required.
6. Do not touch `ParkingStructureService.ReadOperationalAsync` / `ReadAsync` while SPARK-228 is in flight.

## 10. Context for the Next Session

- Decisions Q1/Q2/Q3 are **accepted** (planning file §13) and are the basis of the P1 contract; they override any older text in the design doc.
- The current blocker for A10/A11 is not policy but data: no window table and no commitment end times.
- The current blocker for A5 is the full table-level unique on `(tenant_id, site_id, slot_code)`; re-registration must update the soft-deleted row back to `deleted_at = NULL`, never insert.
- Capacity figures are already "registered slots only" at read time; the remaining work is to keep `total_physical_capacity` consistent instead of asserting equality and failing.
- `slot_compatibilities` has a `slot_id` FK with `ON DELETE CASCADE`, so compat rows survive a soft delete and can be reused on resurrect.
- The contract is now **APPROVED v1.3 (2026-10-29)** in `docs/03-design/api/parking-structure-api-design.md` §*SPARK-238 structure configuration contract*, with DE-1…DE-17 and decisions D-5…D-12. It is authoritative; do not re-derive decisions from the code, and do not re-open what §3.5 already dispositioned.
- **Restore is never automatic.** `POST /slots` (and `/units`, `/paths`) on a released identifier returns `409 IDENTIFIER_RELEASED`; only `restoreSlot`/`restoreUnit`/`restorePath` may resurrect. This is deliberate: it is what stops a delayed retry from undoing a delete.
- **Capacity is three numbers, not one**: derived registered count, configured limit, operational availability. A mutation may recompute the first; it must never overwrite the second with it.
- **The read path must not throw**: `CapacityViews` throwing `CAPACITY_CONFLICT` during serialization is a known defect; the approved shape is a typed DTO with `projectionState`/`reason` and `available: null`.
- **Compatibility is a predicate, not stock**: `sum(category pool totals) == registeredPhysicalCount` must always hold, and the claim query (`OperatorGateSlotStore.cs:28`) must consult `slot_compatibilities` before the projection column (R-8, DE-14).
- A maintenance window is only effective after the authoritative service acknowledges it; `pending` is never reported as closed and an unknown outcome reads as `MAINTENANCE_UNCERTAIN`. Facility cannot fence another service's booking transaction, so overlap inspection is never authorization to cancel.
- Two read-side defects must be fixed with the same change set: a NULL `supported_vehicle_type` throws in `Load` (`Enum.Parse`) and is claimable via `ANY` before throwing (R-9), and an inconsistent projection throws during serialization of `GET /structure`.
- `GET /api/parking-lots/{id}/operator-layout` is correctly authorized (operator site-scope); the earlier note claiming otherwise was wrong and is corrected in §3.1. `GET /{id}/operational-layout` is a different route with lot-scoped `SLOT_OVERRIDE`.
- `POST /api/parking-lots/{id}/structure` (replace-all batch ingestion) never existed in code, is rejected under Q1, and is now absent from the OpenAPI `paths:`. Do not implement it. The **approved** batch route is the narrower `POST /{id}/structure/batch` for atomic same-lot deactivation only.
- **Work now lives on `feat/SPARK-238-facility-structure-port`** (base `origin/develop` = `ea0d212c`), re-delivered after the SPARK-228 merge and the service rename. Commits `76630c39` (a), `ac849ea7` (b), `cd99422a` (c), `ce76fd05` (d); working tree clean. The old `feat/SPARK-238-facility-structure` tree is abandoned (see §3.8).
- **Matcher ownership boundary is durable:** operational-overlay colouring (`slotMatcher.ts` + `matchSlotCodes` in `operationalLayout.ts`) belongs to SPARK-228 and is raw-exact; slot registration + the inert-unregistered-mesh rule (A12) belong to SPARK-238 (`registeredMeshMapping.ts` + `meshSlotMapping.ts`) and are tolerant. Do not let either side reuse the other's matcher. `meshSlotMapping.ts` must keep **zero imports** (a frontend test loads it with a throwing `require`). Merging the two normalisers is deferred.
- **P2h/DE-16 and P2e/DE-13 are BLOCKED cross-service** (FacilityService typed read DTO belongs with SPARK-228; resource-scoped commitments belong with OperationService). Recorded as deferred; do not implement inside SPARK-238.
- **P5 closed 2026-11-01.** The packaging commits are listed in §8; the A1-A12 traceability file is the canonical acceptance record; the deferred debt (DE-4, DE-13, DE-16, DE-3 and the A12 positional-fallback caveat) is recorded there and must **not** be implemented inside SPARK-238.
- The deployed stack was rebuilt from this worktree with explicit user permission, and `smartpark_facility` now carries `structure_maintenance_windows`. Do **not** recreate database volumes (`setup.ps1 -RecreateDb`, `docker compose down -v`).
- Known non-defects that must not be "fixed": 5.16/5.17 return 403 by design (DEC-LOT-04, `BACKUP_MARK` is never implicit); a new lot reports `totalPhysicalCapacity = 0` until slots are registered; the 4 `tsc --noEmit` errors and the 10 `operator-provisioning.test.cjs` failures are pre-existing and unrelated.
- Merge order: SPARK-238 → `develop`, then SPARK-244 resumes the FacilityService consumer side. The maintenance overlay is already wired into `apply-workflow-migrations.ps1` and `compose.schema-integration.yml`.
