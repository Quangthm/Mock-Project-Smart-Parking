# Operations and facility review — 15 October 2026

Review of the actual dirty `Fix2FA` checkout at base `6165024c634316d15525fe48bdc5a3ddd8193c1a`. Existing local edits were preserved. Review only; no source edits. The Downloads AGENTS guide and review-package README/02/03 informed the review method; they are direction and historical evidence, not proof that the current implementation passes. The user's requested demo on 16 October determines priority.

## Demo readiness

| Demo step | Backend evidence | Assessment |
| --- | --- | --- |
| Owner creates lot, units and slots | `src/Services/FacilityService/SmartParking.FacilityService.API/Program.cs:93`, `:108`, `:109` | Implemented routes; requires active approved Owner tenant, User API, Parking API/DB and Reservation API/DB. HTTP acceptance was not executed in this review. |
| Owner views saved structure/3D data | Parking API `Program.cs:98`; repository `PostgresParkingStructureRepository.cs:205` | Authoritative structure and optional coordinate JSON are returned. This does not prove that the frontend renders the saved layout. |
| Operator check-in/check-out | Reservation API `Program.cs:37–51`; Parking API `Program.cs:92–121` | Missing public admission/session/checkout/departure routes. The Reservation host only serves three internal structure-safety operations. |
| Slot changes immediately | Parking repository `PostgresParkingStructureRepository.cs:150–160`, `:205` | Physical occupancy is read but no entry/departure workflow writes it. No SignalR host/hub/subscription implementation found in `src`. |
| Online payment | `src/Services` contains User, Parking and Reservation service source; `compose.schema-integration.yml` defines a payment database | Payment database schema is present; no executable Payment service, order/collection/callback/tariff use case or provider adapter was found. SQL schema is not a working payment flow. |

## Findings by priority

### OPS-01 — P1: The requested operator entry/exit demo has no authoritative backend workflow

**Evidence:** `src/Services/OperationService/SmartParking.OperationService.API/Program.cs:37–51` registers prepare/impact/release for structure edits only. The complete Parking public route set at `src/Services/FacilityService/SmartParking.FacilityService.API/Program.cs:92–121` exposes structure CRUD, impact operations and backup marking. `SlotOperatorClient.cs:18` authorizes `SLOT_OVERRIDE`, which is a backup operation permission, not admission/checkout authority.

**Impact:** The current frontend check-in/out action cannot create an authoritative backend parking session, enforce backend capacity or establish consistency across separate browsers/devices. LocalStorage can survive a page reload, so reload alone does not prove backend persistence. Tables and protection triggers in SQL do not implement those commands. This is a missing slice, not a small wiring fix.

**Required minimal slice:** Operations-owned entry/session creation, explicit current assigned-lot entry permission, stable operation ID, transactional capacity/slot claim and session write, duplicate replay protection and a guarded actual-departure command that releases occupancy once. Preserve separate checkout cutoff/payment/departure state. Test two concurrent entries to the same slot, duplicate entry/exit and a revoked/wrong-lot Operator using independent database contexts.

**Requirements:** `docs/01-requirements/srs/modules/05-sessions-operations.md:49` (FR-GATE-03 actual entry/session), `:65` (FR-GATE-05 payment clearance versus physical departure), `docs/01-requirements/srs/permissions.md:3` and `:22` (current scope; checkout/cash authority).

### OPS-02 — P1: Owner permission from tenant A authorizes structure changes in tenant B

**Evidence:** `src/Services/UserService/SmartParking.UserService.API/Controllers/IdentityScopeController.cs:19` calls `RequirePermissionAsync`, whose query at `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/AccountWorkflowService.cs:35–47` only proves that some active Owner account grants the action. The subsequent query in `IdentityScopeController.cs:20–23` returns every active Owner tenant without filtering each membership's permission array. Parking's tenant selection accepts any tenant in that response (`Parking API Program.cs:85–90`); `OwnerIdentityClient.IsOwnerAsync` checks the same response (`Infrastructure/ServiceClients.cs:30–34`).

**Reproduction:** Give the same active user an Owner membership in A granting `PARKING_MANAGE`, and another in B with `permissions = {}`. Request owner scope, then create/edit a lot with `tenantId=B`. A satisfies the global permission check, and B is returned as authorized scope. `BACKUP_CONFIGURE`/`BACKUP_MARK` checks use the same pattern.

**Impact:** Explicit denied permissions on B are bypassed; this violates the attached review guide's warning against proving permission through one membership and using all memberships as scope.

**Remedy:** Filter the returned membership/tenant IDs by the requested action, or accept a target tenant and require action and resource scope on the same current membership. Include a real SQL/HTTP regression covering allow A / deny B, null defaults and explicit empty permissions. No fix made here.

### OPS-03 — P1: Immediate slot color updates need an occupancy write and authorized projection

**Evidence:** `PostgresParkingStructureRepository.cs:205–208` derives `IsPhysicallyOccupied` exclusively from Parking's `physical_state='OCCUPIED'`. Existing slot updates at `:150–154` write structure fields and `reservation_state`, not `physical_state`. Reservation's `StructureSafetyStore.cs:99–110` reads protected slots/open sessions/capacity; it does not publish accepted occupancy into that Parking projection. No `AddSignalR`, `MapHub` or `HubConnection` implementation was found under `src` in this review.

**Impact:** Even after a future Reservation session insertion, the structure response cannot automatically turn the correct slot red/green. Polling a projection that is never updated cannot fix this.

**Remedy:** Keep accepted occupancy authoritative in Operations; expose it through a scoped Operations read contract or a durable versioned projection. Commit entry/departure and outbox intent atomically; deduplicate and reject stale delivery at the projection. UI should refresh authoritative data after its command and reconnect; authorized push may distribute subsequent changes. For the demo, refresh after confirmed actual entry/departure is sufficient to show immediate updates without claiming live push is implemented. Never free occupancy merely on payment success or gate-open.

### OPS-04 — P1 for optional payment: Payment implementation is absent

**Evidence:** Source inventory of `src/Services` has no Payment service or provider adapter; Reservation API has no payment endpoints. `scripts/database/microservices/04-payment-service-db.sql` and compose's payment-db define storage only.

**Impact:** No real payOS order, trusted callback verification, financial clearance or replay-safe collection can be demonstrated from current backend source.

**Remedy:** Prioritize entry/departure before this optional demo item. A separately labelled deterministic fixture can demonstrate UI intent, but cannot be reported as implemented real payments. Do not substitute cash as if it bypasses missing audited collection. Approved FR-PAY-03/04 require payOS priority and trusted verification; FR-GATE-05 separates settlement from actual exit.

### OPS-05 — P2: Facility implementation still diverges from the supplied coding direction

**Evidence:** Parking routes dispatch directly to `ParkingStructureService` (`Parking API Program.cs:93–121`); no MediatR command/validator pipeline is composed. Internal endpoints also execute `NpgsqlCommand` directly (`:64–83`). The Domain parses JSON with `System.Text.Json.JsonDocument.Parse` (`Domain/ParkingStructure.cs:181–186`).

**Impact:** These are concrete reasons a leader can call the implementation structurally inconsistent with the attached Clean Architecture/CQRS and JSON-outside-Domain direction. They are not proof that every structure operation is broken.

**Remedy:** Migrate one touched vertical slice at a time into explicit application commands/queries, domain typed rules, repository/UOW ports and external serialization; do not perform a service-wide rewrite just before the demo. Keep the existing useful short local transaction, site lock and durable synchronization mechanisms.

## Existing useful safety mechanisms

`ParkingStructureService.cs:135–174` serializes structure work with a site gate, recovers pending synchronization, acquires Reservation protection before a local edit and stores the physical result plus exact replay outcome atomically. `PostgresParkingStructureRepository.cs:20–27` checks the active tenant; `:198–205` constrains reads/writes by tenant/site. These mechanisms cover structure edits, not missing admission/payment workflows. Cross-service revocation is rechecked but explicitly not atomic (`ParkingStructureService.cs:143–149`); do not claim stronger guarantees from inspection.

## Checks actually executed

```powershell
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-restore --verbosity minimal
```

Result: **24 passed, 0 failed, 9 skipped**, 33 total. Build of referenced Parking/Reservation Domain/Application/Persistence succeeded. All nine PostgreSQL-dependent checks skipped because `SMARTPARK_STRUCTURE_TEST_CONNECTION` was not configured. Therefore real database locking, trigger, recovery and concurrency acceptance remain unverified here. The suite does not reference the Parking API/Infrastructure projects, so its successful build does not certify their production composition.

Read-only source searches confirmed the route inventory, lack of occupancy/session/payment commands and lack of SignalR wiring. No live HTTP workflow, real provider call or real payment was executed. No new tests or source edits were made.

To run the skipped suite, set `SMARTPARK_STRUCTURE_TEST_CONNECTION` to an isolated PostgreSQL server account allowed to create/drop disposable databases, then rerun the command. `tests/Services/SmartParking.FacilityService.Tests/PostgresStructureWorkflowTests.cs:275–310` creates independent temporary Parking/Reservation databases and applies the actual scripts. Do not point it at a shared application database.

## Source recheck after Docker availability changed

The source was rechecked on 15 October after the user reported Docker running. The route inventory and missing occupancy/payment implementations above are unchanged. Docker availability alone does not implement these flows. No duplicate test run was performed by this reviewer during the recheck; the coordinating reviewer owns current Docker/runtime evidence.

Fixture prerequisites were inspected more closely:

- The fixture creates random `structure_parking_test_<guid>` and `structure_reservation_test_<guid>` databases and drops only those names in cleanup (`PostgresStructureWorkflowTests.cs:275–310`, `:357–368`). Normal test mutations run inside those separate databases, not the admin connection's database.
- The full suite also executes `GateAuthenticationTests.cs:25–35`, which creates a random login role and database, and cleans both in `finally`. Its connection account therefore needs **CREATE ROLE as well as CREATE DATABASE**, plus cleanup rights. The suite deliberately checks that a wrong password fails, so use a password-authenticated TCP connection rather than a trust/peer authentication path. A dedicated local PostgreSQL test container with an admin account is appropriate.
- Base Parking schema requires `uuid-ossp`; Reservation requires `uuid-ossp` and `btree_gist`. The isolated server must have those extension files available and allow installation. No remote provider, SMTP delivery or payOS call is involved in this structure suite.
- A failed second database creation occurs before the fixture's cleanup `try`, so inspect for this run's generated first database if setup fails at that point. This is a limited test-fixture cleanup gap, not an application data migration requirement. Never broadly drop similarly named databases without establishing ownership.

Safe additional checks: build API/Infrastructure projects; rerun the existing suite against a dedicated disposable server; inspect production route registration or issue read-only requests to already running local services. Avoid running general workflow migration scripts against existing demo databases merely to test availability. A read of `/structure` is not strictly side-effect free in this implementation: it prepares/releases a durable structure hold and recovers synchronization (`ParkingStructureService.cs:68–76`), so use an isolated test site when rehearsing it. Missing check-in/out routes should be reported directly rather than simulated with manual SQL against demo state.

## Demo rehearsal criteria

1. Approve a newly registered Owner; log in as that Owner; create a uniquely named lot and units/slots; reload and verify the same IDs and saved coordinates.
2. View that saved lot in 3D; compare number/codes of slots against the authoritative response. Coordinate persistence alone is not acceptance of visualization.
3. Provision and verify an assigned Operator. Wrong lot and revoked Operator must fail backend authorization.
4. After an admission workflow exists, actual entry must create one session and occupy exactly one slot; reload Owner/Operator views and verify occupancy. Replay must not increment twice; competing entry must lose safely.
5. Accept checkout, settle through an implemented/explicitly labelled financial path, then record actual departure. Only departure releases the occupied slot; duplicate departure is harmless. Reload both views to prove persistence.
6. Present payment as optional and currently missing unless a separate implemented adapter/fixture is explicitly introduced and verified.
