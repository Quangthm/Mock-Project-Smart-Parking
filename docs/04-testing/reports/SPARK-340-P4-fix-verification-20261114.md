# SPARK-340 [US-A02] - P4 Fix Verification Report

- Task: SPARK-370 (Phase 4 QA / latent-defect verification for Epic SPARK-340)
- Epic: SPARK-340 - Admin: Configure platform policy bounds and authorized integration settings
- Story: US-A02
- Date: 14 November 2026
- Branch: `feat/SPARK-340-admin-platform-policies` (HEAD `055da4591ee9a1822eb9870dc261c9685ca3258b`, all changes uncommitted)
- Supersedes decision in: `docs/04-testing/reports/SPARK-370-P4-independent-verification-20261114.md` (which had requested changes)

## Decision

**P4 qualifies to close, pending the user's own FE+BE UAT sign-off.** Every blocking defect raised by the independent P4 review is fixed, and each is backed by a real (not inferred) verification result on the live Docker stack and/or real PostgreSQL.

## 1. Defects fixed

| # | Defect | Fix | Files |
|---|--------|-----|-------|
| 1 | Admin endpoints returned 403 because three controllers required uppercase `ADMIN` while UserService issues lowercase `admin` | Changed attributes to `[Authorize(Roles="admin")]`; added a lowercase-admin regression test | `PlatformPoliciesController.cs`, `AdminPaymentIntegrationController.cs`, `AdminNotificationIntegrationController.cs`, `AdminPaymentIntegrationTests.cs` |
| 2 | Owner publish and Admin activation locked different resources, so incompatible policies could both commit | Both paths now acquire the same transaction-scoped advisory lock before validating compatibility; bounded retry on 40001/40P01; 409 on persistent conflict | `LotPolicyRepository.cs`, `ReservationPolicyRepository.cs`, `PlatformPolicyPostgresConcurrencyTests.cs`, `LegacyDriverReallocationCompatibilityTests.cs` |
| 3 | PayOS persistence/webhook durability gaps (mode/key version, multi-mode restore, hydration, MAX+1 race, cancellation redaction) | Verified already implemented in the working tree; added the missing real-PostgreSQL atomic-allocation proof; reconciled two stale tests | `PaymentIntegrationPostgresConcurrencyTests.cs`, `PaymentIntegrationStoreHardeningTests.cs`, `AdminPaymentIntegrationTests.cs` |
| 4 | Legacy `DRIVER_REALLOCATION_REQUEST_LIMIT=2` tolerated by Admin but rejected on Owner republish | Unified tolerance (0..5) across catalogue, publish, and activation | `LotPolicyRepository.cs`, `ReservationPolicyRepository.cs`, `LegacyDriverReallocationCompatibilityTests.cs` |
| 5 | Newman collection asserted wrong provenance, used false-positive checks, accepted SANDBOX 400, missed write-RBAC/scheduled/overlap, and (found during this run) dropped query params | Rewrote assertions/fixtures; added `05 - Scheduling & Write RBAC`; **added the missing `url.query` arrays to 5 revisions-list requests** | `SPARK-340-Admin-Platform-Policies.postman_collection.json`, `...-guide.md` |

## 2. Verification (actual results)

### 2.1 Live Docker stack
Rebuilt and restarted `operation-service` (5055), `payment-service` (5065), `notification-service` (5068); volumes/data preserved.

RBAC matrix with genuine UserService tokens (`admin@smartpark.local`, `owner@smartpark.local`, `driver@gmail.com`):

| Endpoint | admin | owner | driver | anonymous |
|----------|-------|-------|--------|-----------|
| `GET /api/v1/admin/policies/operations/catalogue` | 200 | 403 | 403 | 401 |
| `GET /api/v1/admin/integrations/payment` | 200 | 403 | 403 | 401 |
| `GET /api/v1/admin/integrations/notifications` | 200 | 403 | 403 | 401 |

### 2.2 Newman UAT (real, against the live stack)
- 51 requests, **0 request failures**
- 51 test-scripts, **0 failed**
- 4 prerequest-scripts, 0 failed
- **81 assertions, 0 failed**; duration ~5s
- First run before the query-array fix: 81 assertions / 5 failed (folder 01 status filters + pagination). After the fix: 0 failed.

Sanitized evidence: `docs/04-testing/reports/evidence/SPARK-370-Newman-UAT-Result.json` (headers, bodies, and the populated environment excluded).

### 2.3 Automated suites
- Backend build: `dotnet build SmartParking.slnx --nologo` -> 0 errors (12 pre-existing NuGet advisories).
- Backend test: `dotnet test SmartParking.slnx --nologo` -> **1,136 passed / 107 skipped / 0 failed** (IoT 1; Architecture 12; Operation 58 + 3 skip; Payment 299 + 5 skip; Facility 199 + 29 skip; Notification 215; User 352 + 70 skip).
- Frontend build: `npm --prefix FE run build` -> exit 0.
- Frontend test: `node --test tests/frontend/*.test.cjs` -> **261 passed / 0 failed**.
- Real PostgreSQL: Operation platform-concurrency tests 3/3 (`Catalog=Postgres`); Payment atomic-allocation tests 2/2. Both self-skip without their connection env var; temp databases only, dropped after the run.

## 3. Case status (PASS / FAIL / SKIP / NOT RUN)

| Case | Status |
|------|--------|
| Admin 200 / Owner 403 / Driver 403 / Anonymous 401 (3 controllers) | PASS |
| Operation policies lifecycle (catalogue, drafts, If-Match, revisions, activation) | PASS |
| Scheduling + overlap 409 + write-RBAC | PASS |
| Payment integration masking + mode/BaseUrl validation | PASS |
| Notification 4 channels ACTIVE | PASS |
| Snapshot before/after (framework + accepted revision re-read) | PASS (automated) |
| Booking-backed snapshot immutability via a real booking | NOT RUN - not exercised end-to-end with owned vehicle/slot fixtures |
| Concurrency, real PostgreSQL two-connection | PASS |
| Payment atomic revision allocation, real PostgreSQL | PASS |
| SANDBOX credential update with real credentials | SKIP - optional prerequisite not supplied |

## 4. Remaining dependencies (presented, not applied)

1. Durable checkout mode + key-version binding for webhook verification. Options: (a) add `payment_orders` columns (`payos_mode`, `signing_key_version`/`checksum_key_fingerprint`) + EF migration + backfill; (b) persist per-order binding as `policy_revisions` (no DDL); (c) Redis fingerprint with TTL >= checkout lifetime. Requires approval (Schema Freeze).
2. Stable DataProtection key store so encrypted `policy_revisions` credentials survive container recreation. Options: (1) `DataProtection__KeysDirectory` + named volume in `compose.app.yml` (no DDL); (2) DB-backed `PersistKeysToDbContext` (DDL + package); (3) external Key Vault/Redis/S3. Requires approval.

## 5. Constraints respected
- Schema Freeze: no new migration or DDL.
- No commit / push (HEAD unchanged; tree uncommitted).
- No tokens or credentials printed or exported; only secret-filtered evidence stored.

## 6. Conclusion
All independent-review blocking defects are resolved and verified with real evidence (live HTTP via Newman, real PostgreSQL for concurrency and persistence). P4 is ready to close once the user's FE+BE UAT observations are attached. The two schema/architecture items in section 4 remain as documented dependencies for the P5 window and were intentionally not applied.
