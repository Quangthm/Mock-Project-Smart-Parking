# SPARK-243 implementation and verification — 8 November 2026

## Provenance and disposition

- Branch: `feature/SPARK-243-driver-vehicle-registration-`.
- Baseline SHA: `917975c56b5008d472bd3372cf2555aee14be87d`; implementation is uncommitted. No staging, push, PR or shared database migration.
- Approved decisions: claim photo association now; reviewer access with future adjudication; explicit preservation backlog; temporary existing `Services:Key` protection; OAuth replacement deferred.
- **Implementation delivered; full deployment acceptance pending.** User explicitly left live storage/browser checks pending, then requested deployment instructions instead of further costly local testing.
- [Deployment checklist](SPARK-243-deployment-checklist.md), [API contract](../../03-design/api/vehicle-registration-api-design.md), [database rollout](../../02-architecture/database/microservices/01-user-access-service-db-docs.md).

## Architecture correction — current state

The user rejected the earlier service/store/SQL-overlay structure. The current rewrite removes the registration service/interface and binding store; dedicated MediatR handlers coordinate aggregate repositories and IUnitOfWork. Domain mutations enforce identity/metadata rules; API uses narrow AutoMapper DTOs and the shared response factory. Media persistence is an aggregate repository, and its User/vehicle lookups use their repositories. Binding revision is maintained by UoW and mapped as an optimistic concurrency token.

Only the original microservice SQL declares the final schema. All table/index declarations use IF NOT EXISTS. ALTER is limited to circular media FKs; the original authentication immutability trigger and reference role seed are explicitly retained by user clarification. No vehicle function/trigger, overlay or task inventory script remains.

**Current verification:** affected backend/test graph build passed with zero warnings/errors. **30 focused offline checks passed, 0 failed/skipped**, including Domain rules, AutoMapper configuration/safe media projection, FluentValidation, production handler composition and framework DDL parity. Current PostgreSQL/HTTP, SQL replay and live acceptance are NOT RUN after this rewrite; follow the deployment checklist. The earlier results below are historical and must not be carried forward as current passes.

Current command (no database needed):

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-build --no-restore --nologo -m:1 --filter 'FullyQualifiedName~VehicleArchitectureTests|FullyQualifiedName~AuthorizationScopeTests|FullyQualifiedName~ProfileOutboxTests.ReviewedSqlMatchesExactFrameworkModel|FullyQualifiedName~VehicleRegistrationTests.EquivalentFormats|FullyQualifiedName~VehicleRegistrationTests.InvalidApplicationFormats|FullyQualifiedName~VehicleRegistrationTests.DomainNormalizer' --logger 'trx;LogFileName=spark243-architecture-verified.trx'
```

The test assembly was rebuilt after correcting a test-source overload ambiguity. The restricted runner aborted because vstest could not connect to testhost within 90 seconds; the focused compiled run outside that runner completed in 18 seconds. No new containers/databases were started. Static audit confirms exactly three circular-FK ALTER statements, only the explicitly retained auth function/trigger, idempotent table/index declarations and no removed service/store symbols in source/tests.

## Historical checks — before architecture correction

| Check | Actual result | Limits |
|---|---|---|
| `dotnet build SmartParking.slnx --no-restore --nologo -m:1` | PASS, 0 warnings/errors | Build before final test-only additions; final focused run compiled the changed backend/test graph successfully |
| `npm --prefix FE run build` | PASS | Existing chunk-size/ineffective dynamic import warnings. Final tiny toast wording and stale-error guards were edited afterward; no subsequent frontend build claimed |
| Final focused UserService run | **70 passed, 0 failed, 0 skipped** | Real PostgreSQL, production TestServer host/mediator/decoder; synthetic identities and fake external storage |
| Full UserService rerun | 288 passed, 19 failed, 2 skipped (309 total) | Before final additional two-Driver-Accounts HTTP test; that case passed in final focused run |
| Unchanged UserService baseline export | 284 passed, 17 failed, 2 skipped (303 total) | Same SHA and PostgreSQL environment; 17 failing test names overlap current run |
| Initial full solution run: Facility | 120 passed, 5 failed | Same 5 failures reproduced on unchanged baseline |
| Full solution: Payment / IoT | 167 passed, 3 skipped / 1 passed | Broker-dependent skipped coverage remains unverified |
| `node --test tests/frontend/*.test.cjs` | 141 passed, 8 failed | Same 149 tests and 8 failures reproduced in unchanged baseline using existing FE dependencies |
| Diff whitespace | PASS at handoff | No deployment or real browser acceptance implied |

The first broad run exposed missing registrations for `IVehicleBindingStore` in nonvehicle HTTP test hosts. Those fixture registrations were corrected; affected HTTP fixtures pass in the final focused run. A later attempt ran against a vanished temporary database and failed connection setup; it is excluded from product acceptance. Only the task-owned disposable PostgreSQL fixture was recreated. No shared application database was used.

Final focused command (requires a disposable test-server connection supplied securely as `SMARTPARK_AUTH_TEST_CONNECTION`):

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-restore --nologo -m:1 --filter 'FullyQualifiedName~VehicleRegistrationTests|FullyQualifiedName~MediaAsset|FullyQualifiedName~AuthorizationScopeTests|FullyQualifiedName~OwnProfileTests|FullyQualifiedName~DriverRegistrationHttpTests|FullyQualifiedName~OperatorProvisioningHttpTests|FullyQualifiedName~OwnerRegistrationHttpTests' --logger 'trx;LogFileName=spark243-focused-final.trx'
```

Machine reports are under the ignored `tests/Services/*/TestResults/` directories. Baseline source was exported to a temporary directory, leaving the working branch untouched. No credentials are included in this report.

## Historical feature evidence — rerun required

Tests: [registration](../../../tests/Services/SmartParking.UserService.Tests/VehicleRegistrationTests.cs), [claims/concurrency/resolver](../../../tests/Services/SmartParking.UserService.Tests/VehicleRegistrationClaimTests.cs), [HTTP ownership/photo](../../../tests/Services/SmartParking.UserService.Tests/VehicleClaimHttpTests.cs).

| Executed test | Observable evidence |
|---|---|
| `PersistenceOwnershipUpdatesAndDuplicatePlateConflict` | Stable IDs, both physical categories, original input, foreign ownership rejection, identity-changing edit unavailable |
| `RealHttpValidationCrossAccountAndCurrentAccountState` | Real auth host; GET/POST/PATCH; validation; missing token; locked/inactive current authority; safe own metadata edit |
| `UploadedClaimIsPrivateAndForgedFieldsCannotChangeOwnership` | Real multipart upload/decoder, raw string preserved; forged Account/status ignored; claimant-only pending/media; third Account denied; incumbent remains; unrelated uniqueness failure gives 500 and no claim |
| `TwoDriverAccountsForOneUserStayIsolatedThroughHttp` | Bound token cannot list/edit sibling Driver Account vehicle; conflict belongs to exact claimant Account; fresh write and list remain bound |
| `ConcurrentClaimantsPreserveWinnerAndReplayNeverPromotesAfterRemoval` | Independent PostgreSQL connections yield one active and one pending; original photo/time preserved on retry after incumbent removal; original binding resolves |
| `SameAccountConcurrentReplayCreatesNoClaimAndNullOwnerStaysProtected` | Same-Account double submit yields one active, zero claims; null-owner live binding remains protected |
| `FailureAfterClaimInsertRollsBackClaimAndPhotoAttachment` | Failure after claim insert but before attachment commit leaves neither claim nor attachment |
| `SharedKeyResolverRejectsAnonymousAndPreservesRealCategoryAndRevision` | Missing/wrong key rejected; MOTORCYCLE category preserved; foreign/removed result INELIGIBLE; stale revision 409; removal advances revision |
| `UploadedClaimIsPrivateAndForgedFieldsCannotChangeOwnership` resolver assertions | Active CAR still resolves after rival claim; claim ID resolves INELIGIBLE |
| `CanonicalSchemaIsRerunnableAndKeepsVehicleIdentity` / `MediaAssetMigrationTests` (renamed/reworked after these historical results) | Earlier upgrade proof is superseded. Current tests now verify canonical creation-script replay; no existing-schema migration is claimed |
| Existing `MediaAsset*` suites | JPEG/PNG/WebP decoding; 5 MiB boundary and over-limit; disguised content; failed/uncertain upload; purpose/ownership; account isolation; expired drafts and recovery |

## Broad-suite failures and remaining uncertainty

**Reproduced baseline failures:** 17 UserService tests in MFA, refresh/session identity, profile authorization/outbox, User management and identity schema. Common failures include missing `last_refreshed_at` in older fixture writes, MFA HTTP expectations and outbox metadata. No unrelated fixes were introduced. Facility has four operational-layout fixture/redaction failures and one reactivation constraint failure. Frontend has four operational-layout adapter failures, three Operator UI fixture failures and one owner-contract fixture import failure. Their test names also fail in the unchanged baseline; this does not make those suites passing.

**Two current-run-only timeouts remain unresolved:**

- `SessionSchemaReconciliationTests.ConsolidatedBaseIsCompleteAndLegacyNullSessionsRemainImmutable` — PostgreSQL read timeout at test database cleanup (`DROP DATABASE`, line 92).
- `ProfileOutboxTests.ScopedPublicationCommitsWithNameAndRollsBackWithName` — PostgreSQL read timeout during the test/fixture teardown path (reported line 135).

These passed in the unchanged baseline run but timed out in the current broad run. They are not classified as confirmed regressions or automatically dismissed as infrastructure. User-requested follow-up is a targeted run in an isolated deployment-like test environment; see checklist. No new broad local run will be started solely to chase these failures.

## Not verified / deferred

- Live provider upload/restricted delivery, actual browser forms/responsive view, browser offline/account switch: **pending by user decision**.
- Existing real booking unchanged, actual Operations caller failure handling and race-safe reservation revalidation: **pending cross-service acceptance**.
- Both forced winning orders in the simultaneous-create race, forced missing-config resolver startup, live-service outage behavior and explicit wrong-index/duplicate-data inventory verification were not separately executed. The random two-connection race, both sequential holder/claimant paths, missing/wrong keys and the previous prototype upgrade were executed; that upgrade is now removed; do not conflate them.
- Adjudication, transfer/removal (AC-098), claim preservation holds, automated media deletion and service-token rollout are **deferred scope**, not failed registration tests.
- Production rollout requires a new or independently confirmed aligned schema before exposing this code. CREATE IF NOT EXISTS is not an existing-schema upgrade. Shared-key access is temporary and shared across its holders; it does not identify Operations uniquely.

## Follow-up audit: removed database logic and acceptance-document ownership

The removed vehicle trigger advanced a binding revision for every SQL writer. Its supported application replacement was added during the architecture correction: UnitOfWork compares original/current Account, canonical plate, category and removal state, advances the mapped revision, and EF includes the original revision in its concurrency predicate. Lookup rejects a mismatched expected revision. Raw SQL, ExecuteUpdate, direct DbContext saves and database cascades bypass this maintenance; future binding writers must follow repository/UoW coordination. The current feature withholds identity-changing edits. This is not a claim of identical protection for arbitrary database writes, nor post-rewrite PostgreSQL acceptance.

The removed overlay's table/index/column declarations are in the original schema. Active-plate uniqueness, pending-claim uniqueness, checks and foreign keys remain database enforced. Its explicit preflight checks for duplicate plates/wrong existing index definitions are not replaced by a backend startup migration or verifier. Existing-schema compatibility is owner-reviewed deployment work; CREATE IF NOT EXISTS does not validate or repair it. The pre-existing authentication immutability trigger was retained by explicit user instruction.

Task provenance and execution status were removed from acceptance-scenarios.md. That document now contains generic user-media and vehicle-registration scenarios. Media foundation history remains in the [media evidence report](SPARK-373/media-foundation-verification.md); registration execution history remains here. No runtime changes or new tests were made for this documentation/audit follow-up.
