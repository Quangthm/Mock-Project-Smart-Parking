# SPARK-373 — User media foundation verification and handoff

**Date:** 7 November 2026 (Asia/Saigon). **Branch:** `feature/SPARK-373-cloudinary-media-foundation`.
**Starting HEAD:** `e925520318f5ca7cfb15283a965d68de463ef4de`. Implementation is uncommitted; this is not a deployed or live-provider certification. Plan baseline `dc70e524da0368099dde8b36abeae7c455cacc55` differs by the existing SPARK-330 test restructuring; current test categories/fixtures were retained.

## Delivered scope

- Backend-only optional Cloudinary configuration for four .NET hosts and both Python processes; UserService media explicitly defaults off. Synthetic checks cover setup modes, rotation/removal, host aliases, process-environment restoration, secret exclusion and JWT signing isolation.
- UserService image upload/status/attach/protected content routes through real DI, MediatR, local authorization and scoped persistence. CloudinaryDotNet 1.29.3 and SkiaSharp 4.154.0 (including matching Linux native assets) are Infrastructure dependencies. SkiaSharp remains independent of the storage port.
- Actual JPEG/PNG/WebP decoding, 5 MiB inclusive, maximum 8,192 pixels per side and 25 megapixels, no animated/multiframe input. A real two-frame APNG exposed native decoding as a still image; explicit PNG/WebP animation-marker checks now close that gap.
- Durable pending admission before remote I/O, deterministic opaque locator, scoped idempotency/fingerprint, authenticated storage without overwrite, verified provider identity/bytes/hash, bounded fenced recovery and protected proxy reads. No remote call holds a PostgreSQL transaction.
- Avatar User scope and exact Driver Account vehicle scope, atomic business attachment, identical replay and stale replacement 409. Timestamps and expired reads/attachments now enforce 24-hour unattached drafts and admission-anchored 30-day attached images.
- Additive fresh/upgrade SQL, assets/independent hold metadata and local FKs; existing vehicle identities, bindings, terms and legacy columns remain. Both frontend vehicle forms now upload files and send asset IDs; the HTTPS-string writer is removed as approved.
- Partner PDF formats/count/lifecycle/retention policy published in canonical requirements, contracts and acceptance specifications. Partner runtime remains deferred. Automatic deletion remains **USER-MEDIA-CLEANUP-01**, not silently completed.

## Verification

Private/synthetic logs and TRX files are retained under ignored `.cache/media-verification/`; do not commit its env/runsettings. Commands below use that private fixture only.

| Check | Command / fixture | Actual result |
|---|---|---|
| Final solution build | `dotnet build SmartParking.slnx --nologo --verbosity quiet` | PASS, 0 warnings, 0 errors |
| Focused final backend | `dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj -c Release --settings .cache/media-verification/media.runsettings --filter 'FullyQualifiedName~MediaAsset\|FullyQualifiedName~VehicleRegistrationTests\|FullyQualifiedName~OwnProfile\|FullyQualifiedName~ApiErrorHandlingTests'` | PASS: 94 passed, 0 failed/skipped; `final-results/media-final.trx` |
| Setup | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-setup.ps1` | PASS on Windows PowerShell 5.1; synthetic Fresh/Keep/Team, rotation/removal and secret-isolation checks |
| Compose | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-media-compose.ps1` | PASS; effective app/AI/schema mappings rendered privately, no stack started |
| Frontend build | `npm run build` in `FE` | PASS; existing chunk/dynamic-import warnings remain |
| Frontend suite | `node --test tests/frontend/*.test.cjs` | 141 passed, 8 failed, 0 skipped (149 total). Baseline: 140 passed, identical 8 failed (148 total); new multipart transport test passes |
| Broader backend | `dotnet test SmartParking.slnx -c Release --settings .cache/media-verification/media.runsettings --filter 'Category!=PostgresRabbitMQ&Category!=LiveBroker'` | 581 passed, 23 failed, 0 skipped (604 total), including 12/12 architecture checks; NOT a green repository-wide gate |
| Baseline comparison | Export `git archive HEAD` into ignored `.cache`, run unchanged sources against the same disposable fixture; exclude Architecture because the archive has no Git checkout metadata | 553 passed, 27 failed (580 total). 22 current failures reproduce on baseline; remaining initial failure was a database read timeout, isolated rerun PASS: 1/1 (`final-results/isolated-regression.trx`) |
| Live Cloudinary | Synthetic upload/proxy/unsigned-original/derived denial, wrong credentials, deletion/recovery/backups | **NOT RUN**. Root local env has none of the three media settings. No credential values read into report; account permissions/capabilities not established |
| Linux native runtime / browser visual walkthrough | Linux container execution / interactive UI | **NOT RUN**. Windows native decoding and frontend build are the evidence available |
| Broker categories | PostgreSQL + RabbitMQ / live broker | **NOT RUN**; no broker changes or broker startup in this task |
| Real deployment upgrade / inventory | Shared application databases | **NOT RUN**; only authorized disposable PostgreSQL fixtures used |
| Partner acceptance AC-078/079/111 | PDF/review/version/retention runtime | **NOT RUN**, specifications published for later implementation |
| Automatic deletion / hold-delete serialization | USER-MEDIA-CLEANUP-01 | **DEFERRED**, domain hold metadata tests are not operational deletion proof |

The focused tests exercise fresh schema, populated pre-media upgrade twice, read-only inventory SQL, real HTTP/DI, same-key replay/conflict, foreign User and same-User other Account denial, purpose/expiry/disabled paths, exact byte boundaries and content validation. Concurrent replacement has one winner. Lost response/restart and provider-success/database-failure tests preserve one logical asset with a **fake provider**. Existing HTTP error tests verify started-response failures do not append JSON. No fake-provider result establishes Cloudinary delivery security or deletion behavior.

The initial frontend run exposed an unguarded `FormData` global in isolated JavaScript environments. The fix preserves JSON requests when that browser type is absent and includes a real FormData transport assertion. Final authentication tests pass. The eight unrelated owner/operator failures reproduce exactly using unchanged baseline sources with only a module-loader redirect to the existing installed TypeScript dependency.

## Existing broad-suite failures

These test failures reproduce on the unchanged starting commit. They were not silently repaired as part of this media task. Categories include stale operational-layout fixtures, missing refresh timestamps in historical test seeds, existing MFA error expectations, profile outbox assertions and a duplicate phone fixture.

- `FacilityService.Tests.OperationalLayoutContractFixtureTests.FixtureBindsToProductionProjectionDtoAndKeepsContractInvariants(fileName: "slots-operational-fixture.json")`
- `FacilityService.Tests.OperationalLayoutContractFixtureTests.FixtureBindsToProductionProjectionDtoAndKeepsContractInvariants(fileName: "slots-operational-stale-fixture.json")`
- `FacilityService.Tests.OperationalLayoutContractFixtureTests.FixturesDifferOnlyInGeneratedAtAndIsFreshAndFreshnessUsesFixedClock`
- `FacilityService.Tests.OperationalLayoutRedactionTests.SanitizedRealFixtureMasksBackupAndUsesMaskedReservedMetric`
- `FacilityService.Tests.PostgresStructureWorkflowTests.ReactivatingSitePublishesStateBeforeCapacityWithBackup`
- `UserService.Tests.IdentityRegressionTests.RefreshWinningAfterLogoutAuthenticationCannotKeepLogicalSessionAlive`
- `UserService.Tests.IdentitySchemaTests.CanonicalSchemaPersistsActiveAndPendingOperatorStates`
- `UserService.Tests.MfaLifecycleTests.ConcurrentRecoveryUseSucceedsOnceAndOtpSignInCannotBypassMfa`
- `UserService.Tests.MfaLifecycleTests.InvalidCodesPersistLockoutAndExpiredChallengesNeverCreateSessions`
- `UserService.Tests.MfaLifecycleTests.PasswordChallengeRequiresProofAndSingleUseRecoveryCodeBeforeIssuingSession`
- `UserService.Tests.MfaLifecycleTests.ReplacementKeepsOldAuthenticatorUntilConfirmedThenRevokesSessionsAndOldCodes`
- `UserService.Tests.MfaLifecycleTests.TotpLoginRejectsMalformedReplayAndExpiredChallengesWithoutServerErrors`
- `UserService.Tests.ProfileAuthorizationRaceTests.InvalidatorsCommitFirstAndActualHandlerRefusesTheWrite`
- `UserService.Tests.ProfileAuthorizationRaceTests.PostCheckInvalidatorsMayCommitBeforeAuthorizedNameSave`
- `UserService.Tests.ProfileAuthorizationRaceTests.SessionAccountBindingIsImmutableInDatabaseAndRotationPreservesIt`
- `UserService.Tests.ProfileAuthorizationRaceTests.SharedUserAcrossAccountsUsesOrdinaryLastWriteWins`
- `UserService.Tests.ProfileAuthorizationRaceTests.StaleTrackedAuthorityIsRefreshedAfterCommittedRevocation`
- `UserService.Tests.ProfileOutboxTests.ActualProfileHandlerCapturesChangedOnlyAndRejectedWritesCaptureNothing`
- `UserService.Tests.ProfileOutboxTests.VersionedAccessEventUsesTransactionalMassTransitBusOutbox`
- `UserService.Tests.SessionOwnershipPersistenceTests.DatabaseRejectsMismatchedOwnershipAndRetargeting`
- `UserService.Tests.SessionOwnershipPersistenceTests.VerificationPreservesLegacyNullBindingAndRefreshRejectsIt`
- `UserService.Tests.UserManagementTests.RealDatabaseSearchTransitionsAndSessionRevocation`

## Rollout, rollback and next executable steps

1. Review the uncommitted changes and apply the supported additive upgrade to an explicitly approved UserDb. The existing session/outbox preflight precedes the bounded canonical media section and post-check. No startup auto-migration or volume reset. See [database rollout/rollback](../../../../scripts/database/README.md#user-image-upgrade-and-rollback-spark-373).
2. Privately configure the three development provider values, synchronize configuration/restart through the established process, then opt User media in. The shared development key supplies provider permissions to all six consumers; it does not isolate services. SmartPark ownership checks still apply.
3. Run the live-provider acceptance matrix before claiming deployment readiness: upload synthetic images, authenticated application read and hash comparison, unsigned original/derived denial, safe wrong-credential behavior, unknown-outcome recovery, and account backup/deletion capability review. Automatic deletion is still deferred even if live reads pass.
4. Use [read-only inventory SQL](../../../../scripts/database/test/inventory-user-media.sql) only with authorization for the intended database. It emits aggregate categories without personal identifiers/URLs. Managed-ready metadata is not proof of remote-byte existence. No arbitrary URL fetch or automatic backfill; owner-led re-upload for unresolved legacy rows.
5. SPARK-243 uses the [implemented image contract](../../../03-design/api/user-images-api-design.md): upload under a stable per-attempt key/draft intent, observe ready state, register with `imageAssetId`; replacement includes `expectedImageAssetId`. Both forms are cut over. Full CAR/MOTORCYCLE registration/dispute end-to-end acceptance remains SPARK-243 work. Any future avatar/image display must use authorized fetch and clear object URLs/caches on logout/account switch; current frontend does not consume `avatarUrl`.
6. Implement [USER-MEDIA-CLEANUP-01](../../../05-project-management/decision-log/2026-11-06-user-media-cleanup-deferral.md) later. The worker can query stored expiry deadlines; it needs no timer that started at deployment. Hold acknowledgement/deletion decisions must serialize and exact originals/derived/cache/backup outcomes need verifiable evidence. Expired-read blocking does not remove stored bytes.

Rollback preserves additive metadata and legacy columns. Turning media off does not restore HTTPS-string registration; frontend and backend code must roll back together if that older contract is required. New managed images do not gain reusable provider URLs for old clients. No shared database or application process was changed during this task.

## Suggested Git message (not committed)

`feat(media)!: add owned Cloudinary image foundation [SPARK-373]`

Add optional backend Cloudinary settings, validated UserService image uploads and protected reads, durable recovery, owned attachment and additive metadata migrations. Replace vehicle HTTPS-string inputs in both forms with owned asset IDs. Record Partner document policy and defer automatic deletion under USER-MEDIA-CLEANUP-01 while enforcing expiry now.

`BREAKING CHANGE: vehicle registration requires imageAssetId; HTTPS imageReference input is no longer accepted as image evidence.`

## Follow-up — duplicate media SQL cleanup (7 November 2026)

At the user's request, removed the separate media overlay file. The canonical UserService database script is now the single SQL source. The migration runner and populated-database test select its explicitly bounded media section, preserving the existing additive upgrade path without replaying full bootstrap SQL. Updated verifier messages and rollout documentation.

Validation: extracted SQL exactly matches the previously verified overlay (normalized SHA-256 equality); PowerShell parser check and UserService test-project build passed (0 warnings/errors); no stale file references remain. Database execution was not repeated because the SQL is unchanged. No database/runtime mutation. Task remains Completed; changes uncommitted.
