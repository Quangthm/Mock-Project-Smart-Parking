# SPARK-340 / SPARK-370 — Independent Re-Verification (post 16/11/2026 environment repair)

**Date:** 2026-11-17 (local, Asia/Bangkok +07)
**Verifier:** independent test agent (read + execute only; no implementation edits, no commit/push)
**Branch:** `feat/SPARK-340-admin-platform-policies`
**Commit (HEAD):** `055da4591ee9a1822eb9870dc261c9685ca3258b`
**Source under test:** uncommitted working tree — all SPARK-340/370 artefacts remain untracked/modified; no commits exist on the feature branch. `git status --porcelain` = **109 entries** (108 pre-existing + the 2 artefacts written by this report).
**Environment:** local Docker Compose (`compose.app.yml`, project `smartpark-app`) after the repair recorded in `SPARK-340-UAT-environment-repair-20261116.md`.
**Raw evidence (sanitized):** `docs/04-testing/reports/evidence/SPARK-370-independent-reverification-2026-11-17.json`

> No token, Authorization header, request body, populated environment, connection string, or key XML is stored in this report or its evidence file. The Newman JSON-reporter export (which embeds Authorization headers) is kept **outside** the repo and was reduced to a sanitized projection.

---

## 1. Commands executed and exact results

| # | Command | Exit | Exact result |
|---|---|---:|---|
| 1 | `dotnet build SmartParking.slnx --nologo -m:1` | 0 | `Build succeeded.` — **0 Error(s), 56 Warning(s)** (enumerator: 56 = NuGet advisory warnings only) |
| 2 | `dotnet test SmartParking.slnx --no-build --nologo -m:1` | 0 | **Passed 1157 / Failed 0 / Skipped 108 / Total 1265** |
| 3 | `npm --prefix FE run build` | 0 | `✓ built in 13.55s` (1343 modules) |
| 4 | `node --test tests/frontend/*.test.cjs` | 0 | tests 261 / pass 261 / fail 0 / cancelled 0 / skipped 0 |
| 5 | `dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --filter "FullyQualifiedName~PlatformPolicyPostgresConcurrencyTests"` | 0 | **Passed 3 / Failed 0 / Skipped 0** (real PostgreSQL, CREATE DATABASE) |
| 6 | `dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter "FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests"` | 0 | **Passed 3 / Failed 0 / Skipped 0** (real PostgreSQL, CREATE DATABASE) |
| 7 | RSA signer regression — `--filter "FullyQualifiedName~ReservationFacility"` | 0 | **Passed 1 / Failed 0** (`ConsecutiveRequests_SignValidTokensAfterEachRequestRsaIsDisposed`, 4 consecutive signed requests) |

Per-project .NET breakdown (step 2):

| Project | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Architecture | 12 | 0 | 0 | 12 |
| FacilityService | 199 | 0 | 29 | 228 |
| IoTService | 1 | 0 | 0 | 1 |
| NotificationAuditService | 220 | 0 | 0 | 220 |
| OperationService | 66 | 0 | 3 | 69 |
| PaymentService | 307 | 0 | 6 | 313 |
| UserService | 352 | 0 | 70 | 422 |
| **Aggregate** | **1157** | **0** | **108** | **1265** |

The two PostgreSQL suites (5, 6) are the env-gated suites that account for most skips in step 2; they were executed for real here against `operations-db` (:5443) and `payment-db` (:5444) using temp databases via `CREATE DATABASE`. Connection strings were built in-shell from `SMARTPARK_DEV_PASSWORD` (quote-stripped) and never printed.

Build warnings are dependency advisories only (AutoMapper 16.2.0 `NU1903` ×2, MessagePack 2.5.302 `NU1902`, SignalR Redis 10.0.12 `NU1903`) — not failures.

---

## 2. Newman collection — exact assertion accounting

Collection: `docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json`
Runner: newman 6.2.3 (`npx --no-install newman`), real UserService tokens (admin/owner/driver), `--delay-request 100 --timeout-request 45000`.

**Requests defined: 54. Requests executed: 48. Requests NOT RUN: 6.**

| Metric | Count |
|---|---:|
| Assertions **PASSED** | **76** |
| Assertions **FAILED** | **2** |
| Assertions **SKIPPED** | **1** |
| Request-level failures | 0 |
| Test-script failures | 0 |
| Pre-request-script failures | 0 |
| `pm.execution.skipRequest()` (NOT RUN) | 6 |

> Figure 48 = 54 − 6. The 6 not-run requests are self-skipped by the collection's pre-request scripts (`pm.execution.skipRequest()`) because the booking chain cannot produce a receipt. **Skipped is not counted as passed.**

### 2.1 The 2 failed assertions (honest failures, one root cause)

| Request | HTTP | Failed assertion | Cause |
|---|---:|---|---|
| Accept reservation terms - capture accepted snapshot | **503** | `terms review succeeds` (expected 200) | booking chain blocked (see §4) |
| Accept reservation terms - capture accepted snapshot | — | `terms include identifiers, commercial amounts and resolved site policy` — `Cannot read properties of undefined (reading 'termsId')` | same 503 → no `data` |

Both failures are the **same known, pre-existing integration gap**, not a new regression in the platform-policy feature. They are reported as FAIL, not massaged into a pass.

### 2.2 The 1 skipped assertion

`Configure SANDBOX credentials - masked (optional)` — skipped because `payment_test_client_id/api_key/checksum_key` were intentionally left blank (no real SANDBOX credentials supplied). The same masking behaviour was independently proven out-of-band in §5 (`clientIdMasked=****7f3a`, `apiKeyConfigured=true`, raw markers absent).

### 2.3 The 6 NOT RUN requests (§4 dependency)

1. Submit exactly the reviewed terms for admission
2. Read admission receipt until this booking is confirmed
3. Capture raw historical platform settings before activation
4. Read accepted booking before activation
5. Read accepted booking after activation
6. Compare historical raw platform settings after activation

Reason: **folder 04 / U19 is blocked by the missing commercial producer** (§4). Per instruction these are NOT RUN, not PASS.

### 2.4 Per-request results (sanitized)

| Folder | Request | HTTP | Pass | Fail | Skip |
|---|---|---:|---:|---:|---:|
| 00 RBAC | Operations policies - Anonymous | 401 | 1 | 0 | 0 |
| 00 RBAC | Operations policies - Driver | 403 | 1 | 0 | 0 |
| 00 RBAC | Operations policies - Owner | 403 | 1 | 0 | 0 |
| 00 RBAC | Operations policies - Admin | 200 | 1 | 0 | 0 |
| 00 RBAC | Payment integration - Anonymous | 401 | 1 | 0 | 0 |
| 00 RBAC | Payment integration - Driver | 403 | 1 | 0 | 0 |
| 00 RBAC | Payment integration - Owner | 403 | 1 | 0 | 0 |
| 00 RBAC | Payment integration - Admin | 200 | 1 | 0 | 0 |
| 00 RBAC | Notification integration - Anonymous | 401 | 1 | 0 | 0 |
| 00 RBAC | Notification integration - Driver | 403 | 1 | 0 | 0 |
| 00 RBAC | Notification integration - Owner | 403 | 1 | 0 | 0 |
| 00 RBAC | Notification integration - Admin | 200 | 1 | 0 | 0 |
| 01 Lifecycle | Catalogue - 14 canonical keys with null vs configured bounds | 200 | 6 | 0 | 0 |
| 01 Lifecycle | Create platform draft | 201 | 2 | 0 | 0 |
| 01 Lifecycle | List revisions - DRAFT contains created draft | 200 | 3 | 0 | 0 |
| 01 Lifecycle | List revisions - SQL pagination | 200 | 1 | 0 | 0 |
| 01 Lifecycle | Read revision by id - 200 | 200 | 1 | 0 | 0 |
| 01 Lifecycle | Read revision by id - 404 | 404 | 1 | 0 | 0 |
| 01 Lifecycle | Update draft with If-Match - success | 200 | 3 | 0 | 0 |
| 01 Lifecycle | Update draft with stale If-Match - 409 | 409 | 1 | 0 | 0 |
| 01 Lifecycle | Activate revision - immediate | 200 | 1 | 0 | 0 |
| 01 Lifecycle | List revisions - status CURRENT | 200 | 2 | 0 | 0 |
| 01 Lifecycle | List revisions - status SCHEDULED | 200 | 2 | 0 | 0 |
| 01 Lifecycle | List revisions - status SUPERSEDED | 200 | 2 | 0 | 0 |
| 01 Lifecycle | Effective policies - PLATFORM provenance | 200 | 4 | 0 | 0 |
| 01 Lifecycle | Effective policies - SITE provenance | 200 | **4** | 0 | **0** |
| 02 Payment | Get payment integration - secrets masked | 200 | 5 | 0 | 0 |
| 02 Payment | Switch to FIXTURE mode - success | 200 | 1 | 0 | 0 |
| 02 Payment | Disallowed BaseUrl - 400 INVALID_PAYMENT_BASE_URL | 400 | 1 | 0 | 0 |
| 02 Payment | Invalid mode - 400 VALIDATION_FAILED | 400 | 1 | 0 | 0 |
| 02 Payment | Blank credential - 400 VALIDATION_FAILED | 400 | 1 | 0 | 0 |
| 02 Payment | Configure SANDBOX credentials - masked (optional) | 400 | 0 | 0 | 1 |
| 03 Notification | Get notification integrations - 4 fixed channels ACTIVE | 200 | 4 | 0 | 0 |
| 04 Snapshot | Accept reservation terms - capture accepted snapshot | **503** | 0 | **2** | 0 |
| 04 Snapshot | Create replacement platform draft - change payment hold | 201 | 1 | 0 | 0 |
| 04 Snapshot | Activate replacement platform revision | 200 | 1 | 0 | 0 |
| 04 Snapshot | Effective platform reflects the changed revision | 200 | 3 | 0 | 0 |
| 05 Scheduling | Create scheduled draft - future effectiveFrom fixture | 201 | 1 | 0 | 0 |
| 05 Scheduling | Activate scheduled revision - future effectiveFrom | 200 | 1 | 0 | 0 |
| 05 Scheduling | List revisions - SCHEDULED contains scheduled revision | 200 | 3 | 0 | 0 |
| 05 Scheduling | Create overlapping draft - same future effectiveFrom | 201 | 1 | 0 | 0 |
| 05 Scheduling | Activate overlapping revision - 409 OVERLAPPING_POLICY_INTERVAL | 409 | 1 | 0 | 0 |
| 05 Scheduling | Owner cannot create platform draft - 403 | 403 | 1 | 0 | 0 |
| 05 Scheduling | Driver cannot create platform draft - 403 | 403 | 1 | 0 | 0 |
| 05 Scheduling | Owner cannot update platform draft - 403 | 403 | 1 | 0 | 0 |
| 05 Scheduling | Driver cannot update platform draft - 403 | 403 | 1 | 0 | 0 |
| 05 Scheduling | Owner cannot activate platform revision - 403 | 403 | 1 | 0 | 0 |
| 05 Scheduling | Driver cannot activate platform revision - 403 | 403 | 1 | 0 | 0 |

---

## 3. Real SITE_OVERRIDE — PROVEN

`GET /api/v1/admin/policies/operations/effective?tenantId=<demo>&siteId=<demo>` returned `scope=SITE` with mixed provenance:

| Key | Value | Source | Inherited |
|---|---:|---|---|
| ARRIVAL_LATE_TOLERANCE | 20 | **SITE_OVERRIDE** | false |
| RESERVATION_MIN_DURATION | 15 | PLATFORM_DEFAULT | true |
| PAYMENT_HOLD_DURATION | 7 | PLATFORM_DEFAULT | true |

The collection's own assertions for that request now pass all four, including **`overridden site values report source=SITE_OVERRIDE`**. In the pre-repair run this case self-skipped (no override key existed), so SITE_OVERRIDE provenance was previously **unproven** and is now **proven end-to-end** through the supported Owner flow (owner publish → revision 3 ACTIVE, `publishNote = "SPARK-340 local UAT environment verification"`).

This directly settles the previously-PARTIAL criterion (effective provenance) at HTTP level.

---

## 4. BLOCKER — booking snapshot chain (folder 04 / U19) is NOT PROVEN

**Observed:** `POST /api/reservation-terms` (driver, valid vehicle/site/slot, future window) →
`HTTP 503 { "code": "OWNER_UNAVAILABLE", "message": "Reservation commercial producer is not integrated." }`
Reproduced twice (two different traceIds).

**Root cause (source-verified, not a transport failure):**

| Evidence | File:line |
|---|---|
| Only `IReservationCommercialOwner` implementation registered | `src/Services/OperationService/SmartParking.OperationService.Infrastructure/InfrastructureDependencyInjection.cs:49` |
| `UnavailableReservationCommercialOwner` deliberately fails closed | `.../Infrastructure/Reservations/ReservationOwners.cs:38` |
| Interface contract | `.../Application/Reservations/BookingContracts.cs:44` |
| Consumed by price preview + terms review | `.../Application/Reservations/BookingReviewHandlers.cs:40,53` |
| Consumed by booking progress | `.../Application/Reservations/BookingProgressHandler.cs:12` |

This is a **distinct dependency from the Facility transport flag**, which the repair already fixed (signed Facility facts now succeed; RSA signer regression passes). No environment flag can substitute a real quote/accept producer.

**Consequence:** reviewed-terms → receipt → booking → immutable accepted snapshot cannot be exercised. Consequently:
- The 6 downstream folder-04 requests are **NOT RUN**.
- **The immutable booking snapshot must NOT be claimed proven.** Reading a policy revision is not evidence of a booking snapshot; no request reached booking creation, and no HTTP surface exposes the stored `AcceptedOperationalPolicySnapshot` / `AcceptedPricingSnapshot` JSON anyway (booking snapshots are only observable indirectly via `BookingView` amounts).

**Owner:** the Operation↔Payment commercial integration (quote/accept/ensure-order) must supply a real `IReservationCommercialOwner` adapter before folder 04 / U19 can pass. No fake quote/accept was introduced to force green.

---

## 5. Payment durability across restart / force-recreate (item 9) — PROVEN

Procedure (user notified before interrupting `payment-service`; DB and volume preserved; key XML never printed):

1. Persisted marker credentials in **SANDBOX** mode (allowlisted `https://api-merchant.payos.vn`; a config GET makes **no** outbound call). — `PUT 200`, `clientIdMasked=****7f3a`, `apiKeyConfigured=true`, `checksumKeyConfigured=true`.
2. `docker compose -f compose.app.yml restart payment-service` → healthy → `GET` returned the **same masked values, decrypted**.
3. `docker compose -f compose.app.yml up -d --no-deps --force-recreate payment-service` → healthy → `GET` again returned the **same masked values, decrypted**.
4. Volume `smartpark-app_payment-data-protection-keys → /app/.keys/payment` remains mounted; key-ring contains **1** key file (name redacted, content never read).
5. Raw marker occurrences: **responses NONE, logs NONE**.
6. Environment restored to `FIXTURE` (mode=FIXTURE, isConfigured=true, HEALTHY) so the workspace is left as found.

Note (by design, not a defect): **FIXTURE mode stores no credentials** (`PaymentIntegrationSettingsStore.cs:229,365`), which is why durability has to be proven through SANDBOX. FIXTURE makes zero outbound HTTP.

---

## 6. Criteria covered / not covered by this run

| Criterion | Status | Basis |
|---|---|---|
| Temporal scheduling (before / at / after effectiveFrom) | **PASS** | scheduled draft → activate → `SCHEDULED` listing; real-PG temporal suite 3/3 |
| Overlap → 409 `OVERLAPPING_POLICY_INTERVAL` | **PASS** | collection 409; half-open exclusion constraint + handler mapping; PG backstop |
| Stale `If-Match` → 409 `RESOURCE_VERSION_CONFLICT` | **PASS** | collection 409 |
| RBAC: anon 401, Owner/Driver 403, Admin 200 — GET **and** writes | **PASS** | full 12-cell matrix + 6 write-denial cells |
| units / null bounds / zero / disabled semantics | **PASS (partial)** | catalogue null-vs-configured bounds; `IsZeroNotDisabled`; source + validator tests; no UI-level 0 rendering check |
| Bound validators (min>max, min≤0, arrival band) | **PASS (test-level)** | `SaveLotPolicyDraftValidatorTests`, `PlatformPoliciesTests`, owner 400 `POLICY_OUT_OF_BOUNDS` |
| Legacy reallocation keeps only accepted value; no new 0..5 | **PASS** | `LegacyDriverReallocationCompatibilityTests` (0/2/5 preserved; null→5, 2→5, 7→7 rejected) |
| Ceil division at `Int32.MaxValue` no overflow | **PASS** | `PolicyBoundsValidator.cs:76-78` `1+(value-1)/divisor`; int.MaxValue tests |
| Payment concurrent Save / reader invisibility / hydration fail-closed | **PASS** | store hardening tests + real-PG concurrency 3/3 |
| Storage failure → 503 `PAYMENT_INTEGRATION_PERSISTENCE_FAILED`, no provider detail | **PASS (test-level)** | `PaymentExceptionHandlerTests`; no live WebApplicationFactory e2e |
| Checkout binding before outbound; rotation/mode switch; cannot overwrite; fail-closed | **PASS (test-level)** | hardening + PG + webhook rotation tests; remaining gap: no test injects a throwing binding repo asserting zero outbound (see Findings) |
| Config + decryption keys survive restart / force-recreate | **PASS** | §5 |
| Reviewed terms → receipt → booking → immutable snapshot | **BLOCKED** | §4 |

---

## 7. Findings

**F1 — P1 / BLOCKER — Booking snapshot chain unprovable (missing commercial producer).**
Trigger: any `POST /api/reservation-terms`. Expected: reviewed terms with commercial quote + operational policy snapshot. Actual: `503 OWNER_UNAVAILABLE "Reservation commercial producer is not integrated."` Evidence: §4. Acceptance criterion: U19 / folder 04. Not a regression of this branch; an unintegrated cross-service dependency. Blocks P4 close for the linked-booking-snapshot case only.

**F2 — Resolved — SITE_OVERRIDE provenance.** Previously unproven/self-skipped; now proven via Owner publish + admin effective endpoint + collection assertions (§3).

**F3 — Resolved — Environment repair effective.** Additive DDL restored the owner draft path (revision 3 ACTIVE), signed Facility facts succeed, RSA signer regression passes, ClockSkew unified, real-PG suites now run 3/3 each.

**F4 — Low / informational — FIXTURE stores no credentials by design.** `PaymentIntegrationSettingsStore.cs:229,365`. Consequence for testers: credential-encryption durability must be tested in SANDBOX, not FIXTURE. Not a defect.

**F5 — Low / informational — Residual test gap (binding before outbound).** No test injects a throwing `ICheckoutBindingRepository` + recording `HttpMessageHandler` to assert zero outbound on storage failure, nor asserts the binding row exists at the moment the outbound request is observed. Code path is correct and ordered (`PayOsAdapter.cs:202` bind vs `:243` send); settlement requires an ordered-handler test.

**F6 — Info — 2 Newman failures / 1 skip are expected and disclosed**, not green-washed. They are the booking blocker (2) and optional SANDBOX credentials (1).

**Environmental note:** `smartpark-app-user-service-1` was found `Exited (139)` at the start of the run (residual from the documented Docker Desktop pause/restart) and was restarted; it returned healthy. Not a product defect; no code change was made for it.

---

## 8. UAT checklist mapping (agent-executable portion)

| UAT | Result | Note |
|---|---|---|
| U01 catalogue 14 keys, no 403 | PASS | 6 assertions |
| U03 validation (min/bounds/arrival) | PASS | 400s + validator tests |
| U04 two-window save → conflict | PASS | overlap 409 |
| U05 immediate activate | PASS | 200 |
| U06 scheduled before/at/after | PASS | SCHEDULED lifecycle + PG |
| U07 overlap 409 | PASS | `OVERLAPPING_POLICY_INTERVAL` |
| U08 Effective PLATFORM then SITE (0 real value) | PASS | SITE_OVERRIDE proven |
| U09 owner publish in/out of bounds, no implicit 0..5 | PASS | 400 `POLICY_OUT_OF_BOUNDS` + publish 200 + legacy tests |
| U10 legacy 2 preserved, 5 not granted | PASS | compatibility tests |
| U11 Admin raising inherited min to 90 → activation rejected | **NOT EXERCISED** | not run this pass |
| U12 masked GET/PUT, no raw keys, no provider call | PASS | masking + FIXTURE zero-outbound by design |
| U13 invalid URL / blank credential 400 | PASS | `INVALID_PAYMENT_BASE_URL`, `VALIDATION_FAILED` |
| U14 mode isolation | PASS (code+allowlist) | no real provider call made |
| U15 restart / force-recreate keeps config+keys | PASS | §5 |
| U16 storage failure → 503 canonical | PASS (test-level) | no live e2e |
| U17 notification 4 channels | PASS | 4 assertions |
| U18 RBAC Owner/Driver 403, anon 401, Admin 200 | PASS | GET + writes |
| U19 confirmed booking snapshot | **BLOCKED / NOT RUN** | §4 |
| U20 responsive UI | NOT EXERCISED | user-side |

---

## 9. Conclusion

- Backend, frontend, and real-PostgreSQL gates are **green** with exact counts recorded (1157/0/108 · 261/261 · PG 3/3 + 3/3).
- Real **SITE_OVERRIDE** is now proven; temporal scheduling, overlap 409, If-Match 409, and the full RBAC matrix are proven at HTTP level.
- Payment config + decryption keys survive **restart and force-recreate**; secrets never appear in responses or logs.
- **P4 is NOT yet closeable**: the linked booking-snapshot case (folder 04 / U19) remains **BLOCKED** by `UnavailableReservationCommercialOwner`. All other P4 acceptance items pass.

**Required before P4 close:** a real `IReservationCommercialOwner` adapter from the Operation↔Payment integration, then re-run folder 04 (and U11 if desired).