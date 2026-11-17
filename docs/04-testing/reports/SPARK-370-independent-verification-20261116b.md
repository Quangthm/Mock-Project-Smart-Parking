# SPARK-370 Independent Verification (SPARK-340/370, post-16/11 fixes)

- Date: 2026-11-16
- Branch: `feat/SPARK-340-admin-platform-policies`
- HEAD: `055da4591ee9a1822eb9870dc261c9685ca3258b` (unchanged, no commit)
- Working tree: 100 porcelain entries (28 M, 1 D, 71 ??) — all SPARK-340/370 artefacts uncommitted
- Evidence: `docs/04-testing/reports/evidence/SPARK-370-independent-Newman-20261116-tokenfresh.json` (sanitized; no tokens/headers/bodies)
- This report is additive; prior reports (2026-11-14, 2026-11-16 handoff) are retained as history.

## 1. Commands and exact results

| # | Command | Result |
|---|---------|--------|
| 1 | `dotnet build SmartParking.slnx --nologo -m:1` | exit 0 — 0 Error(s), 56 Warning(s) |
| 2 | `dotnet test SmartParking.slnx --no-build --nologo -m:1` | exit 0 — Passed 1156, Failed 0, Skipped 108, Total 1264 |
| 3 | `npm --prefix FE run build` | exit 0 — built ok |
| 4 | `node --test tests/frontend/*.test.cjs` | exit 0 — tests 261, pass 261, fail 0, skip 0 |
| 5 | Newman collection (54 req) via `npx --no-install newman run ...` | 48 executed, 75 assertions passed, 0 failed, 3 skipped, 6 requests NOT RUN |
| 6 | `dotnet test ...OperationService.Tests --filter ...PlatformPolicyPostgresConcurrencyTests` (real Postgres, `SMARTPARK_OPERATION_POLICY_TEST_CONNECTION`) | exit 0 — Passed 3, Failed 0, Skipped 0, Total 3 (25 s) |
| 7 | `dotnet test ...PaymentService.Tests --filter ...PaymentIntegrationPostgresConcurrencyTests` (real Postgres, `SMARTPARK_PAYMENT_TEST_CONNECTION`) | exit 0 — Passed 3, Failed 0, Skipped 0, Total 3 (21 s) |

Per-project .NET breakdown (Passed/Failed/Skipped/Total): Architecture 12/0/0/12 · IoT 1/0/0/1 · Facility 199/0/29/228 · User 352/0/70/422 · NotificationAudit 220/0/0/220 · Operation 65/0/3/68 · Payment 307/0/6/313. Aggregate 1156/0/108/1264. No failed tests, no environment-caused failures.

Note: `skipped` are xUnit skips (env-gated integration suites), **not** passes. The 6 Newman `skipRequest()`ed requests are counted separately as NOT RUN, not as passes.

## 2. Newman run — PASS/FAIL/SKIP/NOT RUN (no hardcoded PASS count)

Collection defines **54** requests. Executed **48**; **6** were `pm.execution.skipRequest()`ed.

- Assertions: **75 passed, 0 failed, 3 skipped**.
- Test scripts: 48 total / 0 failed. Pre-request scripts: 9 total / 0 failed.

The 6 NOT RUN requests (all folder `04 - Immutable Operational Policy Snapshot`, blocked by Blocker B):
Submit exactly the reviewed terms for admission · Read admission receipt until this booking is confirmed · Capture raw historical platform settings before activation · Read accepted booking before activation · Read accepted booking after activation · Compare historical raw platform settings after activation.

The 3 skipped assertions: Effective SITE provenance (`SITE_OVERRIDE` unobservable — site key absent) · Configure SANDBOX credentials (no `payment_test_*`) · Accept reservation terms (booking fixture not configured).

HTTP-level proofs obtained:
- RBAC matrix 401 (anon) / 403 (driver|owner) / 200 (admin) on Operations, Payment, Notification — GET **and** writes (folder 01/05).
- Catalogue 200 with 14 canonical keys; `ARRIVAL_LATE_TOLERANCE` bound 15..30, others null.
- Draft create 201; list DRAFT/pagination/read-by-id 200; unknown id 404.
- Update draft `If-Match` success 200; **stale `If-Match` → 409 `RESOURCE_VERSION_CONFLICT`**.
- Activate immediate 200; CURRENT/SCHEDULED/SUPERSEDED listings 200.
- Effective PLATFORM 200; Effective SITE 200.
- Payment GET masked 200; FIXTURE switch 200; disallowed BaseUrl 400 `INVALID_PAYMENT_BASE_URL`; invalid mode 400 `VALIDATION_FAILED`; blank credential 400 `VALIDATION_FAILED`.
- Notification 4 fixed channels ACTIVE 200.
- **Scheduling: future draft 201 → activate 200 → listed SCHEDULED; overlapping activate → 409 `OVERLAPPING_POLICY_INTERVAL`.**
- Owner/Driver create/update/activate platform draft → 403.

These settle the previously PARTIAL oracle criteria 3 (scheduling), 4 (overlap 409), 5 (stale If-Match 409), and part of 6 at the HTTP level.

## 3. Payment restart / force-recreate + key volume

- `docker compose -f compose.app.yml restart payment-service` → healthy; then `docker compose -f compose.app.yml up -d --no-deps --force-recreate payment-service` → healthy. Same image; DBs and volumes untouched.
- Volume `smartpark-app_payment-data-protection-keys` → `/app/.keys/payment`; `DataProtection__KeysDirectory=/app/.keys/payment`.
- Key file `key-5c70e0cf-827a-44af-9d35-03e6b6414638.xml` persisted across restart **and** recreate (name/hash unchanged). **No key XML printed.**
- Config retained: GET integration before / after restart / after recreate identical (PAYOS, mode FIXTURE, `isConfigured=true`, status HEALTHY).

## 4. Secret-marker leak check (config path)

Saved marker credentials (`Z9CLIENTMARK` / `Z9APIMARK370` / `Z9CHECKMARK370`) via PUT, then GET and container logs:
- GET returns only `clientIdMasked="****MARK"` + booleans; raw markers absent.
- `docker logs payment-service` (65 lines since save): markers absent.
- FIXTURE mode restored.
- **initiate / cancel / mid-flight rotation marker checks: NOT RUN (safe-constrained)** — SANDBOX BaseUrl is pinned to the real `api-merchant.payos.vn`, so driving initiate with fake credentials would send fake credentials to the real provider, which is explicitly forbidden. Source review shows FIXTURE initiate/cancel early-return without outbound HTTP and provider text is redacted.

## 5. Real-PostgreSQL suites (settle env-gated gaps)

Both suites ran against the local PostgreSQL servers (`operations-db` :5443 / `payment-db` :5444, `POSTGRES_USER` superuser) using temp databases only. Connection strings were built in-shell from `SMARTPARK_DEV_PASSWORD` and **never printed**.
- Operation `PlatformPolicyPostgresConcurrencyTests`: 3/3.
- Payment `PaymentIntegrationPostgresConcurrencyTests`: 3/3 (includes immutability of the encrypted per-order checkout binding over a fresh DbContext + fresh DataProtection provider).

This settles the oracle's "unproven (e)/(5)": the strongest PG proofs are no longer skipped.

## 6. Findings

| ID | Severity | Area | Finding | Evidence |
|----|----------|------|---------|----------|
| F1 | P1 (environment) | OperationService DB | `operations-db-1` lacks `operational_policy_drafts` (and related columns) → owner lot-policy draft/publish return HTTP 500 (`42P01`). Blocks the only supported path to create a real `SITE_OVERRIDE`. | operation-service logs; DDL `scripts/database/microservices/03b-reservation-operations-service-db.sql:236-258` (tracked, committed, purely additive; volume initialised before commit `e25354b2`) |
| F2 | P1 (environment) | Booking chain | `POST /api/reservation-terms` → 503 `OWNER_UNAVAILABLE` ("Facility dependency authority is unavailable."). `ReservationIntegration:Enabled=false` on operation-service **and** facility-service. Blocks folder 04 booking-snapshot chain and UAT U19. | `ReservationFacilityClient.cs:29,61`; live container env |
| F3 | P2 | Test evidence | Previously unproven schedule/overlap/If-Match now proven at HTTP level; still unproven: real `SITE_OVERRIDE` provenance, Admin bound `min>max`/`min≤0` backend tests, storage-failure→probe-no-outbound (store-level), end-to-end 503 through real pipeline. | this run vs `SPARK-340-fixes-and-handoff-20261116.md:40`; oracle reviews |
| F4 | P3 (observation) | Auth | JWT clock-skew inconsistency: operations/payment reject at expiry (skew≈0) while notification accepts ~3–4 min past `exp` (default 5-min skew). Harness note — can make RBAC runs non-deterministic with near-expiry tokens. | run-1 vs run-2 with same token |
| F5 | P2 (positive) | Authorization | Admin site-scope draft write correctly rejected → 403 `FORBIDDEN_SCOPE_WRITE` ("Platform administrators may only create platform-scope policy drafts."). | direct API call |
| F6 | P2 (source PASS) | Operation domain | Legacy driver-reallocation preserves only previously-accepted value (never new grant) and bound arithmetic is overflow-safe at `Int32.MaxValue`. | oracle-2 criteria 1/2 (source + tests PASS) |

## 7. UAT checklist mapping (U01–U20)

PASS: U01 (catalogue 14 keys), U05 (immediate activate), U07 (overlap 409), U12 (masked), U13 (invalid URL/blank → 400), U15 (restart/force-recreate keeps config+keys), U17 (notification 4 channels), U18 (RBAC 401/403/200).
PARTIAL: U06 (SCHEDULED proven; before/at/after market boundary not directly), U08 (PLATFORM PASS; **SITE_OVERRIDE NOT RUN**), U14 (mode isolation source-verified; no live sandbox test), U16 (503 unit-tested; no end-to-end).
NOT RUN: U04/U09/U10/U11 (owner publish path — F1), U19 (booking snapshot — F2), U20 (responsive — FE manual, not executed).

## 8. P4 closure verdict

**Not eligible to close unconditionally.** Core evidence is green (build 0 err; BE 1156/0/108; FE build; Node 261/261; PG Operation 3/3 + Payment 3/3; Newman 75/0/3 with 6 NOT RUN). However, two environment blockers (F1 owner-publish schema drift, F2 booking chain disabled) leave the immutable booking-snapshot chain and real `SITE_OVERRIDE` provenance unproven, and several UAT items are NOT RUN.

Recommended to unblock and re-verify: (1) apply the additive DDL from the tracked migration to `operations-db` (keeps data/volume), and (2) enable `ReservationIntegration` for operation-service + facility-service (recreate), then re-run folder 04 + owner publish + user UAT U19. No implementation defect was found in the 16/11 fixes; the blockers are operational/environmental.

## 9. Working-tree / safety

No commit, no push. No source/test/config edits by verification. No database or volume deleted. Only `payment-service` was restarted/recreated (as authorised); config and decryption keys retained. Tokens/connection strings kept outside the repo and never printed.
