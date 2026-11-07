# SPARK-330 backend test cleanup — completion report

## 1. Summary

Completed the agreed test cleanup on `chore/SPARK-330-cleanup-backend-tests`, based on
`dc70e524da0368099dde8b36abeae7c455cacc55` (also fetched `origin/develop`). Initial tree was clean.
The handoff reviewed `3d3def763870f66f8da71386357a66f776fa61e5`; the newer checkout contains
additional UserService tests and Payment tests, including three live broker cases.

Service suites now contain product tests and support; the 12-case repository guard has its own
solution project. Explicit categories separate offline, PostgreSQL, PostgreSQL/RabbitMQ,
Payment live broker and architecture execution. Setup now supplies one isolated broker's
actual configuration to every consumer. The registration migration path and deprecated test
host APIs are repaired. Database-only HTTP fixtures retain transactional capture without
starting the production broker connection or the workflow delivery timer.

**Scope decision:** The user explicitly chose “Keep the plan’s scope and document failures”
after the additional database failures were reported, then requested completion and movement
of the task record to completed. This completes the cleanup scope, **not certification of a
green product suite**. No schema, production source, frontend fixture, authentication policy,
package version, CI workflow, commit, push or PR was changed.

## 2. Audit Results

| Asset | Decision and evidence |
|---|---|
| `tests/Services/SmartParking.UserService.Tests/WorkflowTestSupport.cs` | Kept unchanged: real workflow construction, synthetic test helpers and schema lookup. |
| `tests/Services/SmartParking.UserService.Tests/SessionOwnershipPersistenceTests.cs` | Kept all three tests and assertions; added method traits only. Focused execution: one passes, two fail while inserting test sessions missing `last_refreshed_at`. |
| `tests/Services/SmartParking.UserService.Tests/LayerBoundaryTests.cs` | Moved to `scripts/architecture/SmartParking.Architecture.Tests/LayerBoundaryTests.cs`; namespace/category changed, all six Facts and six Theory rows retained. |
| `IdentityArchitectureTests.cs`, `UnusedIdentityAdapters.cs`, `UnsupportedVehicleRepository.cs`, `WorkflowSmtpTests.cs` in UserService tests | Kept product behavior and support unchanged. SMTP uses loopback, not an external email account. |
| `PostgresStructureWorkflowTests.cs`, `DirectRolePersistenceTests.cs`, `ProfileOutboxTests.cs` | Applied traits by method; offline metadata/wiring checks remain offline. |
| `IdentityMessagingTests.cs`, `ProfileOutboxBrokerTests.cs` | Kept real delivery guarantees; both pass against the same disposable broker. Malformed profile port no longer causes a conditional skip. |
| `RabbitMqLiveIntegrationTests.cs` in Payment tests | Retained its existing `LiveBroker` category; removed the application-port TCP availability probe. Explicit test configuration selects the shared isolated broker; unreachable configured servers fail. |
| Facility operational-layout contract/redaction tests and linked FE fixtures | Kept unchanged; all four baseline assertion failures remain visible. |

No product test was deleted. The architecture project reuses test SDK 17.14.1, xUnit 2.9.3,
runner 3.1.4 and the UserService Application reference. Its root lookup still walks to
`SmartParking.slnx`; existing solution-wide CI discovers it once without workflow redesign.

Discovery: **596 before → 597 after**. The one addition verifies HTTP-host outbox commit,
rollback and absence of a broker connection. Executed category results exactly match the final
discovery multiset, with no overlap between categories. One existing Theory has two identical
display names; both rows remain in the offline group and both pass.

| Project | Before | After | Offline | Postgres | PostgresRabbitMQ | LiveBroker | Architecture |
|---|---:|---:|---:|---:|---:|---:|---:|
| UserService | 300 | 289 | 233 | 54 | 2 | 0 | 0 |
| FacilityService | 125 | 125 | 112 | 13 | 0 | 0 | 0 |
| PaymentService | 170 | 170 | 167 | 0 | 0 | 3 | 0 |
| IoTService | 1 | 1 | 1 | 0 | 0 | 0 | 0 |
| Architecture | 0 | 12 | 0 | 0 | 0 | 0 | 12 |
| **Total** | **596** | **597** | **513** | **67** | **2** | **3** | **12** |

## 3. Files Changed

Main changes: solution/project relocation; test method traits; migration lookup; test-only
host lifecycle and broker selection; private settings generator and isolated Compose values;
checked runner; setup regression harness; Vietnamese local guide and this evidence record.
The complete changed-path inventory follows (all paths relative to repository root):

- `.env.example`
- `ai/tasks/completed/TASK-SPARK-330-SESSION-20261106.md`
- `compose.tests.yml`
- `docs/04-testing/reports/SPARK-330-backend-test-cleanup-2026-11-06.md`
- `scripts/architecture/SmartParking.Architecture.Tests/LayerBoundaryTests.cs`
- `scripts/architecture/SmartParking.Architecture.Tests/SmartParking.Architecture.Tests.csproj`
- `scripts/configure-test-settings.ps1`
- `scripts/test-backend-setup.ps1`
- `scripts/test-backend.ps1`
- `SmartParking.slnx`
- `tests/README.md`
- `tests/Services/SmartParking.FacilityService.Tests/GateAuthenticationTests.cs`
- `tests/Services/SmartParking.FacilityService.Tests/PostgresStructureWorkflowTests.cs`
- `tests/Services/SmartParking.PaymentService.Tests/Integration/AuthenticationEmpiricalChallengeTests.cs`
- `tests/Services/SmartParking.PaymentService.Tests/Integration/RabbitMqLiveIntegrationTests.cs`
- `tests/Services/SmartParking.UserService.Tests/AuthorizationPersistenceTests.cs`
- `tests/Services/SmartParking.UserService.Tests/DirectRolePersistenceTests.cs`
- `tests/Services/SmartParking.UserService.Tests/DriverRegistrationTests.cs`
- `tests/Services/SmartParking.UserService.Tests/DriverWorkflowTests.cs`
- `tests/Services/SmartParking.UserService.Tests/IdentityAccessHardeningTests.cs`
- `tests/Services/SmartParking.UserService.Tests/IdentityMessagingTests.cs`
- `tests/Services/SmartParking.UserService.Tests/IdentityRegressionTests.cs`
- `tests/Services/SmartParking.UserService.Tests/IdentitySchemaTests.cs`
- `tests/Services/SmartParking.UserService.Tests/LayerBoundaryTests.cs`
- `tests/Services/SmartParking.UserService.Tests/MfaLifecycleTests.cs`
- `tests/Services/SmartParking.UserService.Tests/OwnerRegistrationTests.cs`
- `tests/Services/SmartParking.UserService.Tests/OwnerSetupRecoveryTests.cs`
- `tests/Services/SmartParking.UserService.Tests/OwnerTenantApprovalTests.cs`
- `tests/Services/SmartParking.UserService.Tests/OwnProfilePersistenceTests.cs`
- `tests/Services/SmartParking.UserService.Tests/PostgresAuthTests.cs`
- `tests/Services/SmartParking.UserService.Tests/ProfileAuthorizationRaceTests.cs`
- `tests/Services/SmartParking.UserService.Tests/ProfileOutboxBrokerTests.cs`
- `tests/Services/SmartParking.UserService.Tests/ProfileOutboxTests.cs`
- `tests/Services/SmartParking.UserService.Tests/SessionOwnershipPersistenceTests.cs`
- `tests/Services/SmartParking.UserService.Tests/SessionSchemaReconciliationTests.cs`
- `tests/Services/SmartParking.UserService.Tests/UserManagementTests.cs`
- `tests/Services/SmartParking.UserService.Tests/UserServiceDatabaseFixture.cs`
- `tests/Services/SmartParking.UserService.Tests/VehicleRegistrationTests.cs`

## 4. Tests Executed

Environment: .NET SDK **10.0.401**, Docker engine **29.6.1**, disposable password-authenticated
PostgreSQL **16-alpine**, disposable RabbitMQ **4-alpine**, loopback ports allocated for this
task. No existing application database or broker was used. Generated credentials were stored
only in ignored local files, never printed. Secret-value scan found zero matches in generated
logs/reports. Disposable resources and private credential files were cleaned up after verification.

Baseline commands (Release, no integration settings):

```powershell
git status --short --branch --untracked-files=all
git fetch origin develop
git rev-parse HEAD origin/develop
git diff --stat 3d3def763870f66f8da71386357a66f776fa61e5 HEAD -- tests scripts .github SmartParking.slnx
dotnet build SmartParking.slnx -c Release --nologo
dotnet test SmartParking.slnx -c Release --no-build --list-tests
dotnet test SmartParking.slnx -c Release --no-build --logger trx --results-directory TestResults/spark330/baseline
```

| Baseline project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| UserService | 245 | 0 | 55 |
| FacilityService | 108 | 4 | 13 |
| PaymentService | 167 | 0 | 3 |
| IoTService | 1 | 0 | 0 |
| **Total** | **521** | **4** | **71** |

Baseline discovery/build exited 0; tests exited 1. Build had **four warnings**, zero errors:
`AuthenticationEmpiricalChallengeTests.cs` in Payment tests: `ASPDEPR004` at `(39,27)` and
`(363,27)`; `ASPDEPR008` at `(108,16)` and `(408,28)`. Replaced obsolete WebHostBuilder/TestServer
construction with HostBuilder, UseTestServer and asynchronous startup; all Payment offline tests
still pass. Final Release build: **exit 0, zero warnings, zero errors**. An initial restricted
build exited 1 without compiler diagnostics; it was rerun successfully with installed package access.

Final verification commands (the private settings path existed only during this run):

```powershell
dotnet build SmartParking.slnx -c Release --nologo
dotnet test SmartParking.slnx -c Release --no-build --list-tests
.\scripts\test-backend.ps1 -Mode Offline -NoBuild
.\scripts\test-backend.ps1 -Mode Architecture -NoBuild
.\scripts\test-backend.ps1 -Mode Postgres -Settings TestResults/spark330/isolated.runsettings -NoBuild
.\scripts\test-backend.ps1 -Mode PostgresRabbitMQ -Settings TestResults/spark330/isolated.runsettings -NoBuild
.\scripts\test-backend.ps1 -Mode LiveBroker -Settings TestResults/spark330/isolated.runsettings -NoBuild
powershell.exe -NoProfile -File scripts/test-backend-setup.ps1
git diff --check
```

| Final group/project | Passed | Failed | Skipped | Group exit |
|---|---:|---:|---:|---:|
| Offline / UserService | 233 | 0 | 0 | 1 |
| Offline / FacilityService | 108 | 4 | 0 | 1 |
| Offline / PaymentService | 167 | 0 | 0 | 1 |
| Offline / IoTService | 1 | 0 | 0 | 1 |
| Postgres / UserService | 33 | 21 | 0 | 1 |
| Postgres / FacilityService | 11 | 2 | 0 | 1 |
| PostgresRabbitMQ / UserService | 2 | 0 | 0 | 0 |
| LiveBroker / PaymentService | 3 | 0 | 0 | 0 |
| Architecture | 12 | 0 | 0 | 0 |
| **Combined disjoint groups** | **570** | **27** | **0** | — |

The first concurrent verification attempt produced PostgreSQL connection/read timeouts:
database group 31 passed/36 failed and UserService broker group 0 passed/2 failed. The table
above uses the subsequent independent group runs; it does not hide the initial failures.
Do not launch several full solution runs concurrently when measuring disposable DB behavior.

Focused commands:

```powershell
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj -c Release --no-build --settings TestResults/spark330/isolated.runsettings --filter 'FullyQualifiedName~SessionOwnershipPersistenceTests|FullyQualifiedName~RegistrationLifecycleConcurrencyAndDeliveryRollback|FullyQualifiedName~DatabaseOnlyHttpHostCapturesOutboxWithoutConnectingToBroker' --logger trx --results-directory TestResults/spark330/focused-user
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj -c Release --no-build --settings TestResults/spark330/isolated.runsettings --filter 'FullyQualifiedName~GateAuthenticationTests' --logger trx --results-directory TestResults/spark330/focused-gate
```

Focused UserService: 3 passed/2 failed/0 skipped, exit 1. Registration's two migration executions
and lifecycle/rollback assertions pass. New HTTP-host capture/rollback/no-broker check passes.
All three ownership tests execute: current-account refresh passes; two fail on missing fixture
timestamp before reaching their intended database guard assertions. Focused gate-password test:
1 passed/0 failed/0 skipped, exit 0; its full-group cleanup failure is retained below.

Setup regression harness: exit 0 under Windows PowerShell 5.1. Verified database-only settings,
both broker ports, credential/vhost overrides, XML round trip with special characters, Compose
shell-port precedence, rejection of conflicting ports and missing settings, and rejection of
skipped TRX rows even when the adapter's `notExecuted` counter is zero and process exit is zero.
The runner reads result rows as well as counters; an optional raw `dotnet test` run retains
conditional skips. Configured-server failures were observed as failures, not skips.

Local detailed evidence remains under ignored `TestResults/spark330/` and `TestResults/backend-*`.
Baseline logs: `baseline-build.log`, `baseline-discovery.log`, `baseline-tests.log`; final logs:
`final-build.log`, `after-discovery.log`, `offline.log`, `architecture.log`, `postgres-retry.log`,
`broker-retry.log`, `payment-broker.log`, `focused-user.log`, `focused-gate.log`,
`settings-checks-ps51.log`. Raw TRX is local only; the sanitized failure inventory follows.

## 5. Risks and Remaining Work

**DEFERRED by user:** repair of the additional database/fixture failures; no expectations were
weakened, tests deleted, blanket warnings suppressed or product behavior changed to make a run green.
The four Facility offline failures were reproduced before and after cleanup. Additional database
failures were first exercised with isolated infrastructure during this task; whether each also
fails on the unchanged baseline is **[UNVERIFIED]**. Do not call all of them proven pre-existing.

Follow-up owners should address missing required refresh timestamps in synthetic sessions,
duplicate seeded phone data, MFA HTTP/recovery outcomes, a refresh/logout assertion, event JSON
shape, Facility reactivation and gate-role cleanup under the full group. Gate authentication itself
passes in isolation but full-group cleanup reported PostgreSQL `2BP01`; treat that as unresolved.
Facility/FE owners must resolve fixture freshness/occupancy/redaction intent before editing JSON
or expectations. Keep redaction inputs with sensitive fields so masking remains exercised.

| Final failing case | Sanitized result |
|---|---|
| `FacilityService.Tests.OperationalLayoutContractFixtureTests.FixtureBindsToProductionProjectionDtoAndKeepsContractInvariants(fileName: "slots-operational-stale-fixture.json")` | Assert.Equal() Failure: Values differ  / Expected: 8  / Actual:   0 |
| `FacilityService.Tests.OperationalLayoutRedactionTests.SanitizedRealFixtureMasksBackupAndUsesMaskedReservedMetric` | Assert.Equal() Failure: Values differ  / Expected: 24  / Actual:   0 |
| `FacilityService.Tests.OperationalLayoutContractFixtureTests.FixturesDifferOnlyInGeneratedAtAndIsFreshAndFreshnessUsesFixedClock` | Assert.Equal() Failure: Collections differ  / Expected: string[]                        ["data.generatedAt", "data.isFresh"]  / Actual:   OrderedIterator<string, string> ["data.generatedAt", "data.isFresh", "data.slots[0].updatedAt", "data.slots[10].updatedAt", "data.slots[11].updatedAt", ···]  |
| `FacilityService.Tests.OperationalLayoutContractFixtureTests.FixtureBindsToProductionProjectionDtoAndKeepsContractInvariants(fileName: "slots-operational-fixture.json")` | Assert.Equal() Failure: Values differ  / Expected: 8  / Actual:   0 |
| `FacilityService.Tests.PostgresStructureWorkflowTests.ReactivatingSitePublishesStateBeforeCapacityWithBackup` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23514: Resource is removed or inactive |
| `FacilityService.Tests.GateAuthenticationTests.GatePoolPreservesConfiguredPasswordDuringAuthentication` | Npgsql.PostgresException : 2BP01: role "gate_auth_role_<generated-id>" cannot be dropped because some objects depend on it  /   / DETAIL: Detail redacted as it may contain sensitive data. Specify 'Include Error Detail' in the connection string to include this information. |
| `UserService.Tests.ProfileAuthorizationRaceTests.SharedUserAcrossAccountsUsesOrdinaryLastWriteWins` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.MfaLifecycleTests.TotpLoginRejectsMalformedReplayAndExpiredChallengesWithoutServerErrors` | Assert.Equal() Failure: Values differ  / Expected: Unauthorized  / Actual:   InternalServerError |
| `UserService.Tests.MfaLifecycleTests.InvalidCodesPersistLockoutAndExpiredChallengesNeverCreateSessions` | Assert.Equal() Failure: Values differ  / Expected: Unauthorized  / Actual:   InternalServerError |
| `UserService.Tests.OwnProfilePersistenceTests.TrackedLoaderRefreshesStaleStateAndPreservesPendingWork` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.OwnProfilePersistenceTests.SharedUserNameAndLegacyAvatarRoundTripWithoutChangingOtherFields` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.IdentitySchemaTests.CanonicalSchemaPersistsActiveAndPendingOperatorStates` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23505: duplicate key value violates unique constraint "uq_active_user_phone"  /   |
| `UserService.Tests.SessionOwnershipPersistenceTests.DatabaseRejectsMismatchedOwnershipAndRetargeting` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.SessionOwnershipPersistenceTests.VerificationPreservesLegacyNullBindingAndRefreshRejectsIt` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.UserManagementTests.RealDatabaseSearchTransitionsAndSessionRevocation` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.MfaLifecycleTests.ReplacementKeepsOldAuthenticatorUntilConfirmedThenRevokesSessionsAndOldCodes` | Assert.Equal() Failure: Values differ  / Expected: Unauthorized  / Actual:   InternalServerError |
| `UserService.Tests.ProfileAuthorizationRaceTests.SessionAccountBindingIsImmutableInDatabaseAndRotationPreservesIt` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.ProfileAuthorizationRaceTests.InvalidatorsCommitFirstAndActualHandlerRefusesTheWrite` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.MfaLifecycleTests.ConcurrentRecoveryUseSucceedsOnceAndOtpSignInCannotBypassMfa` | Assert.Single() Failure: The collection did not contain any matching items  / Expected:   (predicate expression)  / Collection: [StatusCode: 200, ReasonPhrase: 'OK', Version: 1.1, Content: System.Net.Http.StreamContent, Headers:  |
| `UserService.Tests.ProfileOutboxTests.VersionedAccessEventUsesTransactionalMassTransitBusOutbox` | System.Collections.Generic.KeyNotFoundException : The given key was not present in the dictionary. |
| `UserService.Tests.OwnProfilePersistenceTests.ApprovedScalarBoundaryMatchesPostgresCapacityAndRejectsOverflowBeforeMutation` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.MfaLifecycleTests.PasswordChallengeRequiresProofAndSingleUseRecoveryCodeBeforeIssuingSession` | Assert.Equal() Failure: Values differ  / Expected: Unauthorized  / Actual:   InternalServerError |
| `UserService.Tests.ProfileAuthorizationRaceTests.PostCheckInvalidatorsMayCommitBeforeAuthorizedNameSave` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.ProfileOutboxTests.ActualProfileHandlerCapturesChangedOnlyAndRejectedWritesCaptureNothing` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.OwnProfilePersistenceTests.ACurrentLockedAccountIsDeniedDespiteAnotherEligibleAccount` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |
| `UserService.Tests.IdentityRegressionTests.RefreshWinningAfterLogoutAuthenticationCannotKeepLogicalSessionAlive` | Assert.True() Failure  / Expected: True  / Actual:   False |
| `UserService.Tests.ProfileAuthorizationRaceTests.StaleTrackedAuthorityIsRefreshedAfterCommittedRevocation` | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.  / ---- Npgsql.PostgresException : 23502: null value in column "last_refreshed_at" of relation "user_refresh_tokens" violates not-null constraint  /   |

Visual Studio/Test Explorer instructions were updated but interactive VS execution is
**[UNVERIFIED]**. No CI run or hosted Node-action deprecation fix was performed; workflow
maintenance remains separate. The generated broker settings require Docker Compose CLI for
configuration rendering; `-DatabaseOnly` does not. Existing `.runsettings` must be regenerated
to acquire both port names and matching test credentials.

## 6. Acceptance Criteria

- [x] Preserve both named UserService files and all product test assertions.
- [x] Relocate the repository guard and execute all 12 cases once through the solution.
- [x] Partition current discovery with VSTest traits; everyday run requires no external services and has zero skips.
- [x] Repair migration path; execute both migrations and the registration lifecycle/rollback test successfully.
- [x] Align all broker consumers with one isolated Compose configuration; verify 2 UserService and 3 Payment delivery cases.
- [x] Prevent incidental broker startup in DB-only HTTP fixture; verify capture/rollback without a connection.
- [x] Remove the four reproduced test-host deprecation warnings; final build has zero warnings/errors.
- [x] Document safe setup, permissions, provider-specific guarantees, both broker ports, Test Explorer, commands and results.
- [x] Explicit integration runner rejects missing prerequisites, empty discovery and skipped test rows.
- [x] Keep additional failures visible and defer repairs under the user's explicit scope decision.
- [ ] Entire product suite green: **not achieved and outside the confirmed completion scope** (4 offline + 23 database failures).
- [ ] All three ownership assertions reach/pass their target: two blocked by test-session insertion errors; documented for follow-up.

## 7. Suggested Commit Message

```text
chore(tests): clean up backend test structure and scope [SPARK-330]

Move repository architecture guards into a dedicated solution test project
and separate offline, PostgreSQL and broker test runs with VSTest traits.
Repair registration migration lookup, share isolated broker settings, and
prevent broker startup in database-only HTTP fixtures while preserving outbox capture.

Replace deprecated Payment test hosts and add checked runners, setup regression
checks and local documentation. Record the remaining offline and database
failures as deferred follow-up under the approved SPARK-330 scope.
```

Changes are uncommitted. No staging, commit, push or PR action was requested or performed.
