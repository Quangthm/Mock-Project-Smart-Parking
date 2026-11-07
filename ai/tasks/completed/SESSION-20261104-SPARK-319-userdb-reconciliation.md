# Session Review — 2026-11-04

## 1. Session Information
- **Session ID:** SESSION-20261104-SPARK-319-001
- **Date:** 2026-11-04
- **Agent:** Main Agent
- **Branch:** `fix/SPARK-319-user-service-legacy-cleanup`
- **Related Task:** SPARK-319 — UserDb schema, access-event delivery and obsolete onboarding reconciliation
- **Status:** Completed — user accepted completion after reporting a successful Docker database setup smoke test; the prior PostgreSQL-backed test failures remain documented as a follow-up validation gap.
- **Initial HEAD:** `a4434fd82315719890429baf0448d5705099b231`
- **Report location:** Moved to `ai/tasks/completed/` at the user's request after the user reported a successful Docker database setup smoke test. This completion records user acceptance and does not imply the previously failing PostgreSQL-backed suite passed.

## 2. Objectives
- [x] Consolidate canonical UserDb fresh-install schema and remove historical User migration layers from fresh setup.
- [x] Retain versioned access events while replacing custom event persistence with the existing MassTransit EF Bus Outbox/inbox.
- [x] Remove redundant generic `OnboardingComplete` state and document the authoritative Owner workflow state.
- [x] Retire the obsolete numbered workflow SQL files; retain required Facility behavior in its canonical microservice base SQL.
- [x] Keep OTP/provider ownership and delivery unchanged; do not add the rejected NotificationService API proposal.
- [x] Record user-reported successful Docker database setup smoke test and close the task with the known PostgreSQL-backed test failures documented as a follow-up gap.

## 3. Work Completed
- Added runtime-required UserService fields and constraints to canonical base01: `users.access_version`, `PENDING_VERIFICATION` account status, refresh activity timestamp, rotated-session proofs, and Owner lot-setup state. Preserved the pre-existing MFA schema edits in the same SQL file.
- Consolidated the former User MFA, identity-hardening and Owner-onboarding schema into the canonical UserDb base. Facility owner-onboarding persistence is defined in `microservices/02-facility-service-db.sql`.
- Deleted four retired numbered SQL patches. The Facility onboarding table is part of the Facility base; Compose and the migration runner no longer reference a dedicated owner-onboarding overlay. Existing Facility databases missing the relation require a separately reviewed migration.
- Replaced the User migration test that executed a retired patch with a PostgreSQL-backed test for active and pending Operator persistence against the canonical schema.
- Extended the read-only UserDb verifier and isolated schema-upgrade assertions to cover required schema and reject obsolete onboarding/custom access persistence.
- Removed `OnboardingComplete` from the User model, EF mapping, approval write, and session/current-user response shapes.
- Kept `OperatorAccessChanged` versioned and routed it through `IEventPublisher` and the scoped MassTransit EF Bus Outbox. Configured the existing access receive endpoint with the owning `AppDbContext` EF outbox/inbox middleware. The consumer sends only a SignalR invalidation hint; authorization continues to reload authoritative User, Account, Role and session state.
- Removed the unused custom access outbox store and state entity, and updated affected tests and fixtures to use event publishers and framework outbox/inbox entities.
- Corrected test-fixture compile issues: variable shadowing, missing `IEventPublisher` imports, MassTransit EF entity namespaces, and a persisted test session's missing `LastRefreshedAt`.
- Updated UserDb setup, architecture, data-dictionary and SPARK-248 deployment documentation to describe the consolidated base, retained access event, consumer behavior, and current validation limits.
- No production/shared database was used. Temporary isolated Docker containers and temporary output files were removed. No commit, staging, branch switch, reset, or push was performed by this task.

## 4. Technical Decisions
- **Decision:** Keep `AccessVersion` and event aggregate/schema versions; replace only the custom outbox/inbox/projection persistence.
  - **Reason:** The versioned event is still an approved integration contract, while its projection is not an authorization source. MassTransit’s existing EF Bus Outbox and inbox provide transactional capture and consumer deduplication using the owning context.
  - **Alternatives:** Remove access events or retain a second custom relay/projection. Both would either remove existing invalidation behavior or duplicate framework delivery state.
  - **Impact:** UserService Application event producers, API messaging registration, Infrastructure consumer, Persistence context/outbox, tests and UserDb schema.
- **Decision:** Retain the broker endpoint name and event versioning, and make the consumer an invalidation notifier only.
  - **Reason:** Preserves the existing event delivery route and user-visible realtime hint without treating an eventually consistent projection as permission authority.
  - **Impact:** Operator activation/provisioning event path and SignalR client invalidation behavior.
- **Decision:** Use canonical base01 as the single fresh-install UserDb definition; do not replay legacy User scripts.
  - **Reason:** Runtime mappings and current workflow require fields absent from the previous canonical base. Fresh setup must not depend on stacked historical patches.
  - **Alternatives:** Keep migration stacking or silently repair existing databases. Stacking preserves drift; an automatic in-place repair would violate reviewed-migration constraints.
  - **Impact:** Compose, migration runner, verifier, schema tests, setup documentation. Populated/partial databases still require a separately reviewed migration.
- **Decision:** Remove generic `OnboardingComplete`; preserve Owner workflow/application/account state.
  - **Reason:** A generic user flag duplicates Owner-specific state and was not needed in ordinary session/current-user responses.
  - **Impact:** Domain, persistence mapping, approval workflow and response DTOs.
- **Decision:** Leave OTP/provider delivery ownership unchanged and do not implement NotificationService API work.
  - **Reason:** The user explicitly narrowed scope after declining that architectural proposal.
  - **Impact:** No NotificationService files or OTP delivery ownership changes.

## 5. Files Changed
| File(s) | Change | Reason |
|---|---|---|
| `compose.schema-integration.yml`; `scripts/apply-workflow-migrations.ps1`; `scripts/database/README.md` | Removed User legacy migration layers and corrected the setup/legacy guard description. | Ensure fresh UserDb setup uses canonical base only and documentation matches runner behavior. |
| `scripts/database/microservices/02-facility-service-db.sql`; Compose/migration runner; deleted retired numbered patches; Facility schema integration test; User identity schema test | Kept Facility owner-onboarding DDL in its base SQL and removed its overlay wiring; tested canonical User state persistence instead of applying an obsolete patch. | Keep clean setup aligned to the service-owned base schema without dropping required Facility behavior or regression coverage. |
| `scripts/database/microservices/01-user-access-service-db.sql`; `scripts/database/test/verify-user-access-schema.sql`; `scripts/test-workflow-upgrade.ps1` | Added runtime schema requirements and verifier/upgrade assertions. | Align fresh install and compatibility checks with UserService runtime; assert obsolete persistence is absent. |
| `src/Services/UserService/SmartParking.UserService.Domain/Entities/User.cs`; `src/Services/UserService/SmartParking.UserService.Persistence/AppDbContext.cs`; `src/Services/UserService/SmartParking.UserService.Application/Services/OwnerRegistrationService.cs`; `src/Services/UserService/SmartParking.UserService.Application/Services/AuthSessionService.cs`; `src/Services/UserService/SmartParking.UserService.Application/DTOs/UserSessionDto.cs`; `src/Services/UserService/SmartParking.UserService.Application/Usecase/Session/GetCurrentUserQuery.cs` | Removed `OnboardingComplete` model, mapping, write and response use. | Remove redundant generic onboarding flag. |
| `src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Services/AccessEvents.cs`; `src/Services/UserService/SmartParking.UserService.Application/Usecase/IdentityAccess/CreateOperator.cs`; `src/Services/UserService/SmartParking.UserService.Application/Usecase/IdentityAccess/ActivateOperator.cs`; `src/Services/UserService/SmartParking.UserService.Infrastructure/Messaging/MassTransitEventPublisher.cs`; `src/Services/UserService/SmartParking.UserService.Infrastructure/Messaging/ProfileMessagingRegistration.cs`; `src/Services/UserService/SmartParking.UserService.Infrastructure/Services/AccessMessaging.cs`; `src/Services/UserService/SmartParking.UserService.API/Program.cs`; `src/Services/UserService/SmartParking.UserService.Persistence/ProfileOutboxRegistration.cs`; `src/Services/UserService/SmartParking.UserService.Persistence/PersistenceDependencyInjection.cs` | Rewired versioned event publication, EF outbox/inbox endpoint configuration and invalidation-only consumer. | Preserve transactional event capture and the existing realtime hint without the custom projection store. |
| `src/Services/UserService/SmartParking.UserService.Domain/Entities/AccessEventState.cs`; `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/AccessEventStore.cs` | Deleted obsolete custom access event persistence. | Avoid overlapping custom delivery/inbox/projection storage. |
| `tests/Services/SmartParking.UserService.Tests/AuthSessionTests.cs`; `AuthorizationPersistenceTests.cs`; `AuthorizationTestSupport.cs`; `DriverRegistrationHttpTests.cs`; `IdentityAccessHardeningTests.cs`; `IdentityMessagingTests.cs`; `IdentityMigrationTests.cs`; `OperatorProvisioningHttpTests.cs`; `OwnerRegistrationHttpTests.cs`; `OwnerRegistrationTests.cs`; `OwnerTenantApprovalTests.cs`; `PostgresAuthTests.cs`; `ProfileOutboxTests.cs`; `SessionOwnershipPersistenceTests.cs`; `UnusedIdentityAdapters.cs`; `UserManagementTests.cs`; `WorkflowTestSupport.cs` | Updated event/outbox/inbox assertions, schema fixture and test compile references; initialized test refresh time. | Exercise the canonical schema and new event path while keeping test session rows valid. |
| `docs/02-architecture/database/data-dictionary.md`; `docs/02-architecture/database/microservices/01-user-access-service-db-docs.md`; `docs/02-architecture/database/schema-integration.md`; `docs/04-testing/reports/SPARK-248/follow-up.md`; `docs/04-testing/reports/SPARK-248/own-profile-outbox.md` | Reconciled deployment, schema, event and historical setup descriptions. | Explain canonical setup and distinguish access invalidation from profile Audit delivery. |
| `ai/tasks/active/SESSION-20261104-SPARK-319-userdb-reconciliation.md` | Created this session report. | Preserve task decisions, file provenance, validation evidence and next steps in the repository’s required session location. |

Pre-existing changes preserved, not attributed to this implementation: staged `ai/tasks/active/SPARK-248-USER-SERVICE-LEGACY-AUDIT.md`; deleted `ai/tasks/active/SESSION-20261027-account-role-authorization.md`; edits to `docs/07-skills/SmartPark-Coding-Skill-v1.0.md`; and MFA schema additions that predated this task in `01-user-access-service-db.sql`.

## 6. Issues and Blockers
- **Issue:** PostgreSQL-backed UserService suite is not green.
  - **Root Cause:** Not established. The final run reports MFA lifecycle assertions receiving HTTP 500 where tests expect 401, including invalid-proof/recovery-code cases. No causal link to the schema/event/onboarding changes has been established.
  - **Resolution:** Fixed one test-fixture persistence defect (`LastRefreshedAt` was null for a synthesized session). Did not change MFA/OTP behavior, consistent with the scope boundary.
  - **Remaining Risk / Follow-up:** 21 failures remain in the final database-enabled suite. User accepted task completion based on the successful Docker database setup smoke test; investigate against an isolated test database before treating the full PostgreSQL-backed suite as green. Do not assume the failures are pre-existing without baseline evidence.
- **Issue:** Initial combined broker test invocation had a transient RabbitMQ startup/readiness problem and one broker outage/recovery test failed to connect.
  - **Resolution:** Retried the access-event consumer test and profile outbox broker test separately with fresh isolated PostgreSQL/RabbitMQ containers; both passed. Temporary containers were removed.
  - **Remaining Risk:** Combined invocation was not a passing run; individual real-broker checks passed.
- **Issue:** Existing or partial deployed UserDb instances are not migrated by this change.
  - **Resolution:** No production/shared database was modified. Migration runner/verifier fail safely; any populated database requires a separately reviewed migration.

## 7. Validation Results
- **Build:** Production projects compiled during test runs. No standalone solution build was run.
- **Tests:**
  - `dotnet test tests\Services\SmartParking.UserService.Tests\SmartParking.UserService.Tests.csproj --no-restore --nologo`: **PASS**, 217 passed, 54 skipped, 0 failed. Skips are environment-gated.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\test-workflow-upgrade.ps1`: **PASS**. Isolated schema test covered canonical apply, second-run skip, explicit environment-file use from another directory, and persisted data across existing-volume restart.
  - With an isolated PostgreSQL and RabbitMQ, `dotnet test ... --filter 'FullyQualifiedName~IdentityMessagingTests'`: **PASS**, 1 passed. Verifies real broker delivery, EF inbox, authorized SignalR invalidation and revalidation behavior.
  - With isolated PostgreSQL and RabbitMQ, `dotnet test ... --filter 'FullyQualifiedName~ProfileOutboxBrokerTests'`: **PASS**, 1 passed. Verifies broker outage/recovery routing without an Audit consumer.
  - PostgreSQL-enabled full UserService run after the fixture fix: **FAIL**, 248 passed, 21 failed, 2 skipped (271 total). The unresolved failures are concentrated in MFA lifecycle behavior returning HTTP 500 for cases expected to be 401; refer to the captured test output in session history, not as a committed artifact.
  - An earlier database-enabled run had 31 failures; one was the null `last_refreshed_at` test fixture, which was fixed and its focused persistence test passed. The final full run reduced failures to 21.
- **Earlier cleanup-phase verification:** UserService and FacilityService test project builds passed with 0 warnings/errors; `git diff --check` passed. No PostgreSQL tests, setup script, or Docker commands were run during that cleanup.
- **Owner-overlay follow-up:** Facility base retains the owner-onboarding table. Compose and migration runner no longer mount/apply its dedicated overlay; other deployed overlays remain. Facility test project build **PASS** (0 warnings/errors), Compose config validation **PASS**, migration-runner PowerShell parse **PASS**, 15 SQL mounts confirmed, and no owner-onboarding overlay reference remains. `git diff --check` **PASS**. No PostgreSQL test or database setup was run. Existing Facility databases missing this table require a separately reviewed migration; the runner fails explicitly rather than replaying base SQL.
- **Earlier validation:** The original implementation's migration guard and isolated schema-upgrade test passed before the cleanup. The replacement-overlay behavior has not been exercised against PostgreSQL in this continuation.
- **Manual Verification:** Confirmed isolated test containers were removed and ports 55439/55679 were clear. No authoritative/shared database was accessed.
- **User smoke test:** Owner signup, operator creation and verification, and 2FA setup/login with an authenticator security code were manually exercised by the user after a fresh setup.
- **User-reported final smoke test:** Docker database setup smoke test passed after the owner-onboarding DDL was moved into the Facility base. The user reported the result; no additional database setup or PostgreSQL test was run by the agent for this confirmation.

## 8. Git Status
- **Branch:** `fix/SPARK-319-user-service-legacy-cleanup`
- **Initial HEAD:** `a4434fd82315719890429baf0448d5705099b231`
- **Current HEAD at cleanup continuation:** `f74b0ad569ee45dd5d4587157694795dad1a1723`
- **Commit:** Not committed.
- **Working Tree:** Modified with uncommitted SPARK-319 changes and other pre-existing/unrelated changes, including user-owned deletions. The report is moved to `ai/tasks/completed/`.
- **Uncommitted Changes:** Changes remain unstaged and uncommitted. No files were staged or committed during this continuation.

## 9. Handoff / Next Steps
- [x] User-reported Docker database setup smoke test passed after the canonical Facility base update.
- [ ] Follow-up, not a blocker to this user-accepted closure: classify the unresolved PostgreSQL-backed UserService test failures; do not report the full DB-enabled suite as passing until rerun and green.
- [ ] Follow-up regression checks, if desired: complete the manual API/function checklist below against the fresh setup.
- [ ] User fresh setup (repository root): after deliberately removing old schema volumes, use the Quick Start happy path `.\setup.ps1`, then `.\scripts\run-app-service.ps1 -Service All`. This creates six schema databases and starts the four API containers plus frontend; setup alone does not start the app containers. If old schema volumes remain, only use `.\setup.ps1 -RecreateDb` when their data may be permanently discarded; this deletes all six DB volumes.
- [ ] Check all six schema DB containers are healthy and the four API `/health` URLs respond, as described in `QUICK_START.md` §4.
- [ ] Run the read-only UserDb verifier after setup: `Get-Content .\scripts\database\test\verify-user-access-schema.sql -Raw | docker compose --project-name smartpark-schema --env-file .env -f compose.schema-integration.yml exec -T user-db psql -U smartpark_user -d smartpark_user -v ON_ERROR_STOP=1`. Expect the canonical schema/outbox prerequisite PASS notice. It makes no schema or data changes.
- [ ] Manual functional regression checklist after app startup:
  - [ ] Authenticate with seeded Admin, Owner, Operator and Driver accounts; check current-user/session responses still return the correct roles and do not contain the obsolete `onboardingComplete` field.
  - [ ] Exercise the Owner application/review path: submit a new application, verify it is not usable as an active Owner before approval, approve it as Admin, then confirm the Owner can sign in and existing tenant/site provisioning behavior remains correct. Also try rejection and confirm it does not provision an active tenant/site.
  - [ ] Exercise Owner operator provisioning: create an operator for an allowed site; verify the new operator is pending and cannot sign in/use protected operations before activation; complete the bootstrap-password activation and verify access works only for the assigned site/permissions. Try an out-of-scope site and confirm it is denied.
  - [ ] Revoke or change that operator's access while a session is active. Confirm the client is prompted to reload/invalidate (when messaging is enabled) and, independently, a subsequent protected API request is denied using current authoritative access state. A SignalR event must not itself grant access.
  - [ ] Exercise routine login, refresh-token rotation and logout: the new refresh token works, the rotated token is rejected, logout revokes that session, and a separate session is not inadvertently revoked. Run the normal profile read/update and vehicle list/create flows as regression smoke checks.
  - [ ] OTP/MFA smoke tests are unchanged in scope; record their outcomes separately. Do not treat the known database-enabled MFA failures as caused by this change or as baseline until classified.
- [ ] For live access-event delivery in the app stack, note `QUICK_START.md` defaults messaging and outbox delivery to disabled. In `.env`, set `SMARTPARK_MESSAGING_ENABLED=true` and `Messaging__OutboxDeliveryEnabled=true` (with the configured RabbitMQ credentials), then recreate/restart UserService so it receives the configuration. The schema setup already starts RabbitMQ. Without these flags, test access/session authorization and state transitions, but do not claim a broker-delivery check.
- [ ] Optional automated regression: run `dotnet test tests\Services\SmartParking.UserService.Tests\SmartParking.UserService.Tests.csproj --no-restore --nologo` against a disposable PostgreSQL server. The last observed DB-enabled run had 248 passed, 21 failed, 2 skipped, mostly MFA lifecycle responses returning 500 instead of expected 401; capture and classify any recurrence rather than assuming it is pre-existing.
- [ ] Optional real-broker regression: with isolated PostgreSQL and RabbitMQ configured, run the `IdentityMessagingTests` and `ProfileOutboxBrokerTests` filters separately. Do not point test fixtures at shared services.
- [ ] Treat the retired numbered patches as historical setup artifacts, not a migration path. The current runner intentionally rejects legacy UserDb layouts; do not run `.\setup.ps1 -RecreateDb` against a database with data to simulate migration. Existing/partial databases need a separately reviewed migration and backup/restore plan.
- [ ] Recheck final diff and worktree provenance; preserve unrelated changes and do not stage or commit without authorization.
- [ ] After reviewing this cleanup, personally delete the generated helper `scripts/test-workflow-upgrade.ps1` as requested; it was intentionally left in place.
- [ ] Rotate the credential previously exposed in the IDE. If it is a PostgreSQL role password, changing `.env` alone does not update the role password in an initialized volume.
- [ ] Move this session record to `ai/tasks/completed/` only after all required validation passes or the task is explicitly accepted with the gap documented.
- [ ] Do not stage, commit, push, apply schema to a shared database, or implement the declined NotificationService API without explicit authorization.

## 10. Context for Next Session
- Scope is SPARK-319 schema/setup, versioned access-event delivery and generic onboarding-state cleanup only. OTP/provider delivery ownership is unchanged.
- Read `ai/AGENTS.md`, `ai/rule.md` Rule 3, this session report, and `docs/07-skills/SmartPark-Coding-Skill-v1.0.md` before continuing.
- Canonical UserDb fresh setup applies `scripts/database/microservices/01-user-access-service-db.sql`; the read-only verifier is `scripts/database/test/verify-user-access-schema.sql`. Do not replay retired numbered patches on a fresh install or auto-migrate populated databases.
- Access authorization continues to use current User/Account/Role/session facts. `OperatorAccessChanged` only prompts client reload through SignalR and is persisted/deduplicated using MassTransit EF Bus Outbox/inbox.
- User requested a manual post-clean-setup functional checklist. The repository Quick Start file is `QUICK_START.md` (uppercase); its normal flow is `.\setup.ps1` then `.\scripts\run-app-service.ps1 -Service All`. Access-event delivery is disabled by default in `compose.app.yml`; enable `SMARTPARK_MESSAGING_ENABLED` and `Messaging__OutboxDeliveryEnabled` in `.env` and restart UserService before claiming live delivery coverage.
- Never point automated test fixtures at development/shared services.
- No temporary Docker containers or test output artifacts from this work remain.
