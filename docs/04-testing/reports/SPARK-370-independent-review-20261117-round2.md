# SPARK-340 / SPARK-368 / SPARK-370 — Independent Review & Verification (Round 2)

- **Date:** 2026-11-17 (UTC timestamps inside `evidence/SPARK-370-independent-review-20261117-round2.json`)
- **Reviewer role:** independent reviewer / verification agent (read-only on source; no commit, no push, no reset/clean, no DB or volume deletion, no keypair change, no real provider credentials used)
- **Repo:** `Mock-Project-Smart-Parking`
- **Branch:** `feat/SPARK-340-admin-platform-policies`
- **HEAD:** `055da4591ee9a1822eb9870dc261c9685ca3258b`
- **Working tree:** **115 porcelain entries** (31 modified + 84 untracked). Every SPARK-340/370 artefact is uncommitted, so the review covers the **entire working tree**, not the tracked diff.
- **Supersedes:** conclusions in `SPARK-370-reviewer-after-followup-20261117.md` that were broader than their evidence (see §12). Earlier reports are preserved as history.
- **Inputs read:** `AGENTS.md` → `ai/AGENTS.md` → `docs/07-skills/SmartPark-Coding-Skill-v1.0.md` → session `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md` → `SPARK-340-review-followup-20261117.md` → 17/11 report + evidence → `SPARK-340-localhost-UAT-checklist.md` → ticket/API-design/decision records.
- **Rule applied:** the reports were treated as an index of what to check, never as a substitute for requirements or source.

---

## 1. Commands and exact counts (all re-run in this cycle — no previous totals reused)

| # | Command | Exit | Exact result |
|---|---|---:|---|
| 1 | `dotnet build SmartParking.slnx --nologo -m:1` | 0 | `0 Error(s)`, **56 Warning(s)** (package advisories only), 20.41 s |
| 2 | `dotnet test SmartParking.slnx --no-build --nologo -m:1` | 0 | **Passed 1161 / Failed 0 / Skipped 109 / Total 1270** |
| 3 | `dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests` | 0 | **Total 4 / Passed 4 / Failed 0 / Skipped 0** (34.4 s, real PostgreSQL) |
| 4 | `dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --filter FullyQualifiedName~PlatformPolicyPostgresConcurrencyTests` | 0 | **Total 3 / Passed 3 / Failed 0 / Skipped 0** (26.75 s, real PostgreSQL) |
| 5 | `node --test tests/postman/spark340-collection.test.cjs` | 0 | **tests 2 / pass 2 / fail 0 / skipped 0** |
| 6 | `npm --prefix FE run build` | 0 | `✓ built in 10.01s` |
| 7 | `node --test tests/frontend/*.test.cjs` | 0 | **tests 261 / pass 261 / fail 0 / skipped 0** |

Per project (command 2): Architecture 12/0/0 · Facility 199/0/29 · IoT 1/0/0 · Notification 220/0/0 · Operation 66/0/3 · Payment 311/0/7 · User 352/0/70.

**Failed tests: 0.** The 109 skips are all env-gated fixtures (`SMARTPARK_STRUCTURE_TEST_CONNECTION` 29, `SMARTPARK_OPERATION_POLICY_TEST_CONNECTION` 3, `SMARTPARK_PAYMENT_TEST_CONNECTION` 4, `SMARTPARK_RABBIT_TEST_PORT` 3, `SMARTPARK_AUTH_TEST_CONNECTION` 68, plus 2 single-purpose broker facts). Commands 3–4 explicitly supplied the two requested connection strings and those suites executed instead of skipping.

Build warnings are dependency advisories only: `NU1903` AutoMapper 16.2.0 ×2, `NU1902` MessagePack 2.5.302, `NU1903` SignalR Redis 10.0.12. FE build warnings: chunk >500 kB (`index`, 3.8 MB) and `INEFFECTIVE_DYNAMIC_IMPORT src/lib/operationalLayout.ts`.

PostgreSQL safety: the fixtures create and drop only `smartpark_payment_test_<32 hex>` / operation-policy temp databases. No application database was used as scratch. `.env` was parsed in-shell with one matching quote pair stripped; passwords and connection strings were never printed.

---

## 2. Newman — full collection, real UserService tokens, agreed test window

Run inside the window the user granted. Real `admin` / `owner` / `driver` tokens were issued by `POST /api/auth/login` on the live UserService immediately before the run (~15 min lifetime). Fixture ids came from API responses, not from hardcoded values:

| Fixture | Value | Source |
|---|---|---|
| `tenant_id` | `a0000000-0000-4000-8000-000000000001` | `tenantId` in `GET /api/parking-lots` response |
| `site_id` | `b0000000-0000-4000-8000-000000000001` | lot `DEMO-HQ` in the same response |
| `slot_id` | `b12892d2-2118-7542-3eb8-ebf992e6cb46` | first CAR / `OPERATIONAL` slot in `GET /api/parking-lots/{id}/structure` |
| `vehicle_id` | `19b6220a-7d42-448b-ab9b-b302534dd6bd` | `GET /api/vehicles` as the driver account |

`payment_test_*` were intentionally left blank: supplying fabricated SANDBOX credentials is forbidden, so that request stays SKIP.

### 2.1 Headline accounting — do not read this as "all passed"

| Metric | Value |
|---|---:|
| Requests **defined** in the collection | **54** |
| Requests **executed** | **48** |
| Requests **NOT RUN** (`skipRequest()`) | **6** |
| Assertions **passed** | **76** |
| Assertions **failed** | **1** |
| Assertions **skipped** | **1** |
| Test-script failures | **0** (48 scripts executed) |
| Pre-request-script failures | **0** (9 scripts executed) |
| Newman exit code | 1 — caused solely by the single expected failed assertion |

The one failure is the known U19 blocker, not a regression:

```
Accept reservation terms - capture accepted snapshot   HTTP 503
  FAILED  terms review succeeds
          expected response to have status code 200 but got 503
```

The single skip is `Configure SANDBOX credentials - masked (optional)` (no credentials supplied — honest SKIP, not a pass).

### 2.2 The 6 NOT RUN requests — all inside `04 - Immutable Operational Policy Snapshot`

1. Submit exactly the reviewed terms for admission
2. Read admission receipt until this booking is confirmed
3. Capture raw historical platform settings before activation
4. Read accepted booking before activation
5. Read accepted booking after activation
6. Compare historical raw platform settings after activation

**Reason (concrete, reproducible):** `POST /api/reservation-terms` with a real driver token and a real CAR slot returns

```json
{ "success": false, "code": "OWNER_UNAVAILABLE", "message": "Reservation commercial producer is not integrated." }
```

`OperationService.Infrastructure/InfrastructureDependencyInjection.cs:49` registers `UnavailableReservationCommercialOwner` as the only `IReservationCommercialOwner`. The collection's own pre-request script sets `snapshot_fixture_ready=true` (all three fixture ids present) and the terms request therefore fails rather than skips — which is correct behaviour. Every downstream folder-04 request then `pm.execution.skipRequest()`s because its captured variable (`snapshot_request_id`, `accepted_platform_revision_id`, …) is missing.

### 2.3 Per-request result (sanitized: name, method, path, status, assertion counts)

| # | Method | Path | Status | P | F | S |
|---|---|---|---|---:|---:|---:|
| 1 | GET | `/api/v1/admin/policies/operations/catalogue` | 401 | 1 | 0 | 0 |
| 2 | GET | `/api/v1/admin/policies/operations/catalogue` | 403 | 1 | 0 | 0 |
| 3 | GET | `/api/v1/admin/policies/operations/catalogue` | 403 | 1 | 0 | 0 |
| 4 | GET | `/api/v1/admin/policies/operations/catalogue` | 200 | 1 | 0 | 0 |
| 5 | GET | `/api/v1/admin/integrations/payment` | 401 | 1 | 0 | 0 |
| 6 | GET | `/api/v1/admin/integrations/payment` | 403 | 1 | 0 | 0 |
| 7 | GET | `/api/v1/admin/integrations/payment` | 403 | 1 | 0 | 0 |
| 8 | GET | `/api/v1/admin/integrations/payment` | 200 | 1 | 0 | 0 |
| 9 | GET | `/api/v1/admin/integrations/notifications` | 401 | 1 | 0 | 0 |
| 10 | GET | `/api/v1/admin/integrations/notifications` | 403 | 1 | 0 | 0 |
| 11 | GET | `/api/v1/admin/integrations/notifications` | 403 | 1 | 0 | 0 |
| 12 | GET | `/api/v1/admin/integrations/notifications` | 200 | 1 | 0 | 0 |
| 13 | GET | `/api/v1/admin/policies/operations/catalogue` | 200 | 6 | 0 | 0 |
| 14 | POST | `/api/v1/admin/policies/operations/draft` | 201 | 2 | 0 | 0 |
| 15 | GET | `/api/v1/admin/policies/operations/revisions?status=DRAFT` | 200 | 3 | 0 | 0 |
| 16 | GET | `/api/v1/admin/policies/operations/revisions?status=ALL&page=1&pageSize=5` | 200 | 1 | 0 | 0 |
| 17 | GET | `/api/v1/admin/policies/operations/revisions/{id}` | 200 | 1 | 0 | 0 |
| 18 | GET | `/api/v1/admin/policies/operations/revisions/1111…1111` | 404 | 1 | 0 | 0 |
| 19 | PUT | `/api/v1/admin/policies/operations/draft/{id}` | 200 | 3 | 0 | 0 |
| 20 | PUT | `/api/v1/admin/policies/operations/draft/{id}` (stale) | **409** | 1 | 0 | 0 |
| 21 | POST | `/api/v1/admin/policies/operations/revisions/{id}/activate` | 200 | 1 | 0 | 0 |
| 22 | GET | `…/revisions?status=CURRENT` | 200 | 2 | 0 | 0 |
| 23 | GET | `…/revisions?status=SCHEDULED` | 200 | 2 | 0 | 0 |
| 24 | GET | `…/revisions?status=SUPERSEDED` | 200 | 2 | 0 | 0 |
| 25 | GET | `/effective` (PLATFORM) | 200 | 4 | 0 | 0 |
| 26 | GET | `/effective?tenantId=&siteId=` (SITE) | 200 | **4** | 0 | 0 |
| 27 | GET | `/api/v1/admin/integrations/payment` | 200 | 5 | 0 | 0 |
| 28 | PUT | `/api/v1/admin/integrations/payment` (FIXTURE) | 200 | 1 | 0 | 0 |
| 29 | PUT | `/api/v1/admin/integrations/payment` (bad BaseUrl) | 400 | 1 | 0 | 0 |
| 30 | PUT | `/api/v1/admin/integrations/payment` (bad mode) | 400 | 1 | 0 | 0 |
| 31 | PUT | `/api/v1/admin/integrations/payment` (blank credential) | 400 | 1 | 0 | 0 |
| 32 | PUT | `/api/v1/admin/integrations/payment` (SANDBOX, optional) | 400 | 0 | 0 | **1** |
| 33 | GET | `/api/v1/admin/integrations/notifications` | 200 | 4 | 0 | 0 |
| 34 | POST | `/api/reservation-terms` | **503** | 0 | **1** | 0 |
| 35 | POST | `/api/v1/admin/policies/operations/draft` (replacement) | 201 | 1 | 0 | 0 |
| 36 | POST | `…/revisions/{id}/activate` (replacement) | 200 | 1 | 0 | 0 |
| 37 | GET | `/effective` (platform reflects change) | 200 | 3 | 0 | 0 |
| 38 | POST | `/api/v1/admin/policies/operations/draft` (future `effectiveFrom`) | 201 | 1 | 0 | 0 |
| 39 | POST | `…/revisions/{id}/activate` (scheduled) | 200 | 1 | 0 | 0 |
| 40 | GET | `…/revisions?status=SCHEDULED&scope=PLATFORM` | 200 | 3 | 0 | 0 |
| 41 | POST | `/api/v1/admin/policies/operations/draft` (overlapping) | 201 | 1 | 0 | 0 |
| 42 | POST | `…/revisions/{id}/activate` (overlap) | **409** | 1 | 0 | 0 |
| 43 | POST | `/api/v1/admin/policies/operations/draft` (owner) | 403 | 1 | 0 | 0 |
| 44 | POST | `/api/v1/admin/policies/operations/draft` (driver) | 403 | 1 | 0 | 0 |
| 45 | PUT | `/api/v1/admin/policies/operations/draft/{id}` (owner) | 403 | 1 | 0 | 0 |
| 46 | PUT | `/api/v1/admin/policies/operations/draft/{id}` (driver) | 403 | 1 | 0 | 0 |
| 47 | POST | `…/revisions/{id}/activate` (owner) | 403 | 1 | 0 | 0 |
| 48 | POST | `…/revisions/{id}/activate` (driver) | 403 | 1 | 0 | 0 |

Fully proven at HTTP level by this run: RBAC matrix (401 anon / 403 driver / 403 owner / 200 admin on **both** reads and writes, three surfaces), 14-key catalogue with null-vs-configured bounds, draft create/update/activate, stale-`If-Match` → **409 `RESOURCE_VERSION_CONFLICT`**, overlap → **409 `OVERLAPPING_POLICY_INTERVAL`**, CURRENT/SCHEDULED/SUPERSEDED classification, payment URL/mode/blank-credential validation and masking, notification 4-channel contract.

**Side effect of the run (disclosed):** the collection creates and activates PLATFORM revisions. After the run the platform pointer is revision `8ec7d40b-231c-4796-82d3-1221acce7e5d`, version **45**, `effectiveFrom 2026-11-17T03:13:37.905235+00:00`. This was inside the agreed window.

---

## 3. Real SITE_OVERRIDE — proven, not inferred

`GET /api/v1/admin/policies/operations/effective?tenantId=…&siteId=…` returns `scope: "SITE"` with:

- **`ARRIVAL_LATE_TOLERANCE` = 20, `source = SITE_OVERRIDE`, `inherited = false`**
- the other **13** keys `source = PLATFORM_DEFAULT`

The override was produced through the supported Owner flow (`/api/v1/lots/{siteId}/policies` draft → publish) during the 16/11 environment repair, **not** through the admin API — admin site-scope draft creation is rejected `403 FORBIDDEN_SCOPE_WRITE` by design, which is itself a correct authorization result.

Collection assertion `Effective policies - SITE provenance`: **4 passed / 0 failed / 0 skipped** (previously 3 passed + 1 skipped when no override existed). The skipped assertion from earlier cycles is now a real pass, so SITE_OVERRIDE is **PASS**, not "partially proven".

---

## 4. Payment persistence across restart and force-recreate

Executed with the user's window. Application database and volume were **not** deleted; no key material was printed.

| Step | Command | Result |
|---|---|---|
| Baseline | `GET /api/v1/admin/integrations/payment` | `mode=FIXTURE`, `baseUrl=http://localhost:5173`, `isConfigured=true`, `status=HEALTHY` |
| Restart | `docker compose -f compose.app.yml restart payment-service` | `running/healthy` in 6 s; state unchanged |
| Force-recreate | `docker compose -f compose.app.yml up -d --no-deps --force-recreate payment-service` | `Recreated`, `running/healthy` in 6 s; `mode=FIXTURE`, `baseUrl` unchanged, `isConfigured=true`, `status=HEALTHY` |
| Mount | `docker inspect … {{json .Mounts}}` | `smartpark-app_payment-data-protection-keys -> /app/.keys/payment`, unchanged across recreate |
| Key ring | `ls -1 /app/.keys/payment \| wc -l` | **1** key file; **contents never printed, no key XML anywhere in this report or the evidence file** |
| Logs | `docker logs --tail 200` filtered for `Exception\|ERROR\|fail` | no matches |

**What this does and does not prove.** The returned integration state is decrypted from the persisted payment history revision using the volume-backed DataProtection key ring — a missing or mismatched key would have failed hydration (fail-closed) instead of returning the stored `mode`/`baseUrl`. So key-ring retention and decrypt-after-restart are demonstrated.

It does **not** re-demonstrate *credential* decryption: FIXTURE mode deliberately stores no credentials (`PaymentIntegrationSettingsStore.cs:229`, `:365`), so `clientIdMasked` is `null` and both `*Configured` flags are `false`. That is design, not a defect. The SANDBOX credential-marker round trip was proven in the previous cycle and is not repeated here, because writing marker credentials into the integration config during the user's UAT would be an unnecessary mutation.

---

## 5. Code review findings

Severity: **P1** security/data-loss · **P2** high · **P3** medium · **P4** low. Bugs and style are separated; style nits are not listed.

| # | Sev | Finding | Location | Trigger → expected vs actual |
|---|---|---|---|---|
| F1 | **P1** | Production Payment API trusts a **repo-committed symmetric JWT key** — `"SecretKey": "smartpark-default-…"` is added unconditionally as a signing key in every non-Development environment; compose overrides `Jwt__Issuer` but not `Jwt__SecretKey` | `PaymentService.API/appsettings.json:23`; `Program.cs:56-72` | Non-Development start → expected RS256/JWKS-only trust; actual: anyone who can read the repo mints HS256 tokens with `iss=SmartParking.UserService`, `aud=SmartParking`, `role=admin\|INTERNAL_SERVICE`. **Pre-existing** — that file is unmodified and the literal is tracked content, not introduced by this branch. Still a real production exposure. |
| F2 | **P2** | Deposit-order endpoints stay on the **loose default scheme** while the strict RS256/JWKS scheme protects only the admin configuration controller | `InternalReservationOrdersController.cs:11`; `Payment Program.cs:96-107`; `DevAuthenticationHandler.cs:28-37,52-68` | Any `role=SERVICE\|OPERATIONS_WORKER\|INTERNAL_SERVICE` or `scope=s2s` claim is accepted with no audience/per-capability check; in Development **any** nonblank `X-Internal-Service-Key` and **any** Bearer string authenticate, and `X-Actor-UserId/TenantId/SiteId` are trusted. Expected: dedicated Operations identity + Payment-specific audience (11/11 decision §3/§5). |
| F3 | **P2** | `EnsureOrder` does not bind the full immutable quote | `EnsureReservationDepositOrderCommandHandler.cs:94-127` | Validates only status, expiry, `AccountId`, `DepositAmount`. Never compares tenant/site/currency/booked amount/prepayment mode/terms version/selection, and never requires `H ≤ quote.ValidUntil`. Replay compares `ReferenceId+AccountId+Amount` only → a same-`operationId` call with a different site silently returns the original order. |
| F4 | **P2** | Concurrent same-`operationId` `EnsureOrder` returns **500** | same handler `:40,:149-150`; `PaymentExceptionHandler` has no `DbUpdateException`/`PostgresException` case | `payment_orders.idempotency_key` is UNIQUE (DDL:171) but the handler does read-then-insert with no transaction/advisory lock → loser hits the unique violation and surfaces `500 INTERNAL_SERVER_ERROR` instead of a replay or 409. |
| F5 | **P2** | Quote acceptance is **not serialized** | same handler `:94,:107,:126,:150` | `GetByIdAsync` without `FOR UPDATE`, no unique constraint on `payment_orders.quoteId` → two different `operationId`s for one quote both pass `Status == ISSUED` and both create deposit orders. Double-charge risk. |
| F6 | **P2** | Deploying this branch **rejects webhooks for pre-existing checkouts** | `PayOsAdapter.cs:388-399`; dead fallback `PaymentIntegrationSettingsStore.cs:263-307` | Callback for an order created before the binding feature has no binding row → `InvalidCallback` (401 `INVALID_WEBHOOK_SIGNATURE`) on every payOS retry until manual reconciliation. A rolling deploy with in-flight links loses confirmations. |
| F7 | **P3** | Checkout signing bindings stored in the **pricing** `policy_revisions` table | `CheckoutBindingRepository.cs:10-21` | One permanent `TARIFF`/`GLOBAL` row per order code. Unbounded growth in a pricing-owned table; a future prune/archive deletes live signing bindings. Contradicts the follow-up's own proposal not to overload unrelated policy history. |
| F8 | **P3** | `arrivalLateToleranceEnabled` accepted then **silently discarded** | `AdminPolicyContracts.cs:75,96`; `CreatePolicyDraft.cs:50`; `UpdatePolicyDraft.cs:54` | Client/Postman/FE all send it; both handlers hardcode `null` yet return 201/200. Either reject non-null for platform scope or remove it from the contract. |
| F9 | **P4** | Platform draft version allocation is check-then-insert | `ReservationPolicyRepository.cs:137-149` | Concurrent admins compute the same version; the loser gets a generic `409 RESOURCE_CONFLICT` with no retry hint (`ReservationExceptionHandler.cs:103-105`). No corruption. |
| F10 | **P4** | Mixed clocks at policy time boundaries | `ReservationPolicyRepository.cs:41,94,134,177,247` | Reads use the app clock, writes use DB `CURRENT_TIMESTAMP`; an "effective now" boundary can differ by skew. |
| F11 | **P4** | `SchedulePolicyRevision` can commit an already-past `effectiveFrom` and return a stale SCHEDULED/CURRENT token | `SchedulePolicyRevision.cs:16-22` | Benign (contiguous intervals) but the returned status can disagree with committed state. |
| F12 | **P4** | Admin endpoints return **500** instead of 401/503 when authentication is unconfigured | `Operation Program.cs:32,103`; `PlatformPoliciesController.cs:12` | Misconfiguration-only, but the uniform error envelope is not honoured. |
| F13 | **P4** | Fixture notification providers report `IsReady=true / Status=ACTIVE` | `GetAdminNotificationIntegrationsQuery.cs:30-68` | `LOCAL_*_FIXTURE` providers read as "integration ready" during UAT. |

Verified sound (no action): uniform `IExceptionHandler` with the legacy middleware removed; raw conditional optimistic `UPDATE` with explicit columns and correct zero-row classification; shared advisory lock between owner publish and admin activation with `FOR UPDATE` and exclusion-constraint handling; server-side scope enforcement; clamped paging; masked secrets; provider-text redaction; `JwksConfigurationManager` bounds (no startup fetch, 2 s timeout, 300 s freshness, shared in-flight refresh, 30 s throttle, 64 KiB cap, `kid`/shape validation).

Note for the record: `PaymentService`'s default JWT scheme uses `ClockSkew = 1 minute` (`Program.cs:82`), which contradicts the repair document's "three services use 30 s" statement **for that scheme**.

---

## 6. Do the new ordering tests actually catch the mutations? (duty 2)

| Mutation | Verdict | Mechanism |
|---|---|---|
| Move the binding **after** `SendAsync` | **Caught** (all three tests) | Blocked-store timeout / the handler's read returning `null` faulting `pending` / `Assert.Equal(0, handler.Calls)` |
| **Not awaiting** `BindAsync` | **Caught**, deterministically | `StoreEntered` is signalled synchronously, then the adapter sends while storage is still blocked → `Calls == 0` and `IsCompleted == false` both fail |
| Storage failure still sends HTTP | **Caught** | `Assert.Equal(0, handler.Calls)` in both theory cases and in the conflict case |
| Adapter swallows an assertion → false PASS | **Cannot happen** | `XunitException` is neither `HttpRequestException` nor `OperationCanceledException`, so the `PayOsAdapter.cs:306-307` filter does not catch it and it escapes `SendAsync`; additionally the external flags (`bindingWasReadable`, `handler.BindingWasVisible`) are assigned **after** the assertion and then asserted, so a widened catch would still fail |
| Binding committed **and independently visible** at the boundary | **Only partially caught** | See below |

**`CheckoutBinding_IsCommittedAndReadableByIndependentConnectionBeforeOutbound` — PARTIALLY PROVEN.** Ordering and decryption at the outbound boundary are real: a fresh `PaymentDbContext` plus a fresh `CheckoutSigningBindings` re-reads and unprotects the row inside `HttpMessageHandler.SendAsync`, and `handler.BindingWasVisible` is set only after that succeeds. But the name over-claims:

- the reader calls `fixture.Open()`, which rents from the **same Npgsql pool** the writer just returned its connection to — very likely the same backend;
- there is no `pg_backend_pid()` inequality, no `Pooling=false`, no pre-opened independent connection.

So a mutation wrapping the insert in a never-committed transaction would still pass. Recommended: rename to `…ReadableByFreshContext…`, or force a distinct connection (separate pool/`Pooling=false` + PID assertion).

Residual gaps neither file catches: the outbound signature is built from `snapshot.ChecksumKey` (`PayOsAdapter.cs:220`) rather than the read-back binding, so mutating the signature source stays green; no assertion on the returned `PaymentInitiateResult`/URI/headers; no concurrent `BindAsync` race for one order code; `_settings is null` skips binding entirely with no DI regression test; no `WebApplicationFactory` test for the 503 envelope; no SANDBOX-with-live-credentials run.

---

## 7. Postman terms-guard test (duty 4)

`node --test tests/postman/spark340-collection.test.cjs` → **2 passed / 0 failed**. Reading the test and then running the real collection confirms all three requirements:

1. A 503 on the terms review still produces **exactly one failed assertion** (`terms review succeeds`), not a silent pass.
2. The script returns before dereferencing `data.termsId`, so the previous cascading `Cannot read properties of undefined` regression is gone — the live run shows **0 test-script failures**.
3. `environment.set` is not reached on the error path, so nothing can be captured as accepted terms from an error response (the fake `pm` makes `environment.set` an `assert.fail`).

Live recheck through the real collection: HTTP 503, 1 failed assertion, 0 script failures. Booking snapshot therefore stays **BLOCKED** — it is not upgraded by this test, and it must not be inferred from any policy-revision read.

---

## 8. U04 / U06 reconciliation (duty 5)

| Case | Correct evidence | Status |
|---|---|---|
| **U04** — concurrent/stale second save | The applicable 409 is `Update draft with stale If-Match` → **`RESOURCE_VERSION_CONFLICT`** (verified live, 1 assertion passed). The overlap `409 OVERLAPPING_POLICY_INTERVAL` proves **U07**, not U04 — using it as a substitute would be a wrong-evidence claim. | **PASS (HTTP, If-Match path)** |
| **U06** — scheduled activation before / at / after `effectiveFrom` | **No automated test asserts effective values at those three points.** Source search over `tests/Services/SmartParking.OperationService.Tests` finds no test for `GetEffectivePolicies`/`ResolveReservationPolicies`. What exists is revision **classification** from a fixed clock (`PlatformPoliciesTests.cs:266`) and owner-publish scheduling (`:441`, `:475`). The live collection proves create → activate → `SCHEDULED` listing only. | **PARTIAL — boundary values NOT PROVEN** |

SCHEDULED listing alone does not prove the boundary behaviour, and this report does not treat it as such.

---

## 9. U19 against the 11 November decision (duty 6)

**Verdict: BLOCKED. Owner: SPARK-273 (reservation producer / commercial integration).** It is not merely a missing HTTP adapter.

Verified in source:

- `InfrastructureDependencyInjection.cs:49` registers `UnavailableReservationCommercialOwner` as the only `IReservationCommercialOwner` (`ReservationOwners.cs:38-44` throws from `AcceptAsync`/`EnsureOrderAsync`/`CollectionsAsync`/`ReconcileAsync`).
- `BookingProgressHandler.PrepareAsync` calls `commercial.AcceptAsync` (`:117`) **inside** the guarded transaction and **before** the hold commit (`:49`) — this violates the decision's "external financial calls must not occur under the guarded transaction" twice over.
- `ProgressBookingAsync` calls `EnsureOrderAsync` (`:154`) inside the same transaction and under site/vehicle locks (`:91-93`).
- The creation receipt is **not terminal at the hold**: `AttachBooking` sets `BOOKING` (`ReservationRequest.cs:43-48`) and only confirmation sets `COMPLETED` (`:241`). No `HOLD_CREATED` outcome exists, so U19's existing receipt contract would be reinterpreted.
- Payment *does* combine acceptance with order creation (`EnsureReservationDepositOrderCommandHandler.cs:125-150`), but Operations never calls it; `DevAuthenticationHandler` is not a machine-identity contract.

Still open, and must be stated as open rather than as delivered:

| Item | State |
|---|---|
| Contract route/transport | `PUT /internal/reservation-deposit-orders/{operationId}` exists in Payment only; the versioned authenticated transport is not agreed |
| Machine authentication | OPEN and worse than described — dev header plus the symmetric-key-capable default scheme |
| Coverage evidence | OPEN — `CoverageVersion` is only validated non-blank; Payment never receives coverage |
| Event-driven confirmation | NOT IMPLEMENTED — no `ReservationDepositCollectedV1`; Payment emits generic `PaymentSucceededV1`; Operations has no reservation-collection consumer |
| Recovery | PARTIAL — ~1 s `CollectionsAsync` polling, no inbox/dedup, no durable `operationId → outcome` record |
| Persistence | No Operations-side durable order intent before the financial call; no `operation_id` column |

Forbidden shortcuts, explicitly not taken and not proposed here: fake quote/accept producer, a separate acceptance endpoint, or dropping the Facility confirmation dependency without a writer-invariant proof.

All five orchestration/contract proposals in `SPARK-340-review-followup-20261117.md` remain **PROPOSED, not implemented**. They must not be cited as delivered.

---

## 10. Criteria coverage summary

| # | Criterion | Evidence in this cycle | Verdict |
|---|---|---|---|
| 1 | Build + full backend regression | command 1, 2 | **PASS** |
| 2 | FE build + frontend unit tests | commands 6, 7 | **PASS** |
| 3 | Real-PostgreSQL Operation concurrency | command 4 (3/3) | **PASS** |
| 4 | Real-PostgreSQL Payment concurrency + durable binding | command 3 (4/4) | **PASS** (with the isolation caveat in §6) |
| 5 | Full collection accounting, honest SKIP/NOT RUN | §2 | **PASS** (76/1/1, 48/54) |
| 6 | Real SITE_OVERRIDE | §3 | **PASS** |
| 7 | Scheduling before/at/after boundary values | §8 | **NOT PROVEN** |
| 8 | overlap 409 / stale If-Match 409 / RBAC 200-403-401 | §2.3 rows 12, 19, 20, 42, 43-48 | **PASS** |
| 9 | Payment config + key retention across restart/recreate | §4 | **PASS** (config + key ring; credentials not applicable in FIXTURE) |
| 10 | Reviewed terms → receipt → booking → immutable snapshot | §2.2 | **BLOCKED** |
| 11 | Legacy reallocation preserve-only + overflow-safe bounds | source review (2 PASS, earlier cycle) + green suite | **PASS** at code level |
| 12 | U19 conformance to the 11/11 decision | §9 | **BLOCKED / OPEN** |

---

## 11. UAT checklist mapping (U01–U20) — only what was actually exercised

| Case | Status in this review |
|---|---|
| U01 catalogue 14 keys, no 403 | **PASS** (HTTP, 6 assertions) |
| U02 admin GET surfaces | **PASS** (HTTP, 200 for ops/payment/notification) |
| U03 validation (min ≤ 0, min > max, arrival tolerance, bound min > max) | **PARTIAL** — payment/integration 400s proven live; Admin bound min>max and min ≤ 0 have no backend test |
| U04 two-window save → 409 | **PASS** only via stale `If-Match` → `RESOURCE_VERSION_CONFLICT` (§8) |
| U05 immediate activation | **PASS** (HTTP 200) |
| U06 scheduled before/at/after `effectiveFrom` | **NOT PROVEN** for boundary values (§8) |
| U07 overlap → 409 | **PASS** (HTTP 409 `OVERLAPPING_POLICY_INTERVAL`) |
| U08 effective PLATFORM → SITE, 0 is a value | **PASS** for PLATFORM_DEFAULT vs SITE_OVERRIDE provenance; the explicit `0` case is code-reviewed only |
| U09 owner publish inside/outside Admin bounds | **PASS** for the supported flow (draft → publish → SITE_OVERRIDE); the outside-bounds rejection was observed in the 16/11 repair (`400 POLICY_OUT_OF_BOUNDS`), not re-run here |
| U10 legacy reallocation keeps 2, does not grant 5 | **PASS** at code + unit-test level |
| U11 site max 60 + Admin min 90 → activation rejected | **NOT EXERCISED** — no fixture was built, no test covers it |
| U12 masked PayOS config, no raw keys, no provider call | **PASS** (HTTP, 5 assertions; FIXTURE makes zero outbound calls) |
| U13 invalid URL / blank credential → 400 | **PASS** (HTTP, `INVALID_PAYMENT_BASE_URL`, `VALIDATION_FAILED`) |
| U14 mode isolation | **PASS** at code level (FIXTURE zero-HTTP early return); no live provider call was made |
| U15 restart / force-recreate keeps config + keys | **PASS** (§4) |
| U16 storage failure → 503 canonical code | **PASS** at unit-test level; no end-to-end HTTP test |
| U17 notification 4 channels | **PASS** (HTTP, 4 assertions) |
| U18 RBAC Owner/Driver 403, anon 401, Admin 200 | **PASS** (HTTP ×3 surfaces, reads and writes) |
| U19 confirmed booking snapshot | **BLOCKED** (§9) |
| U20 responsive | **NOT EXERCISED** — needs the tester group on a real UI |

The user reports that most localhost FE+BE checks went well and that a tester-group pass is still pending. **No per-case PASS is inferred from that statement.**

---

## 12. Corrections to earlier reports

1. **`SPARK-370-reviewer-after-followup-20261117.md`** presented the binding durability chain as largely settled. Correction: the `…IndependentConnection…` PG test proves *fresh-context* reads, not commit isolation or a distinct backend (§6). The ordering tests are genuine and mutation-sensitive; the durability claim needs the narrower wording.
2. **Earlier cycles** listed the `Effective policies - SITE provenance` assertion as skipped and SITE_OVERRIDE as unobservable. Correction: with the 16/11 repair it is **4 passed / 0 skipped** and a real `SITE_OVERRIDE` is observable (§3).
3. **Earlier cycles** used the overlap 409 as evidence for the two-window save case. Correction: overlap 409 is U07; U04's evidence is stale `If-Match` → `RESOURCE_VERSION_CONFLICT` (§8).
4. **`SPARK-340-review-followup-20261117.md`** claims Operations commits a durable order intent before the financial call. Correction: no such record exists, and quote acceptance happens *before* the hold commit (§9). Its five proposals are correctly labelled PROPOSED and were not implemented.

---

## 13. Conclusion

- The four local regression gates and both real-PostgreSQL suites pass with the exact counts in §1. No test fails anywhere.
- The full collection was executed inside the agreed window: **48/54 requests, 76 assertions passed, 1 failed, 1 skipped, 0 script failures**. The single failure is the known commercial-producer 503; the single skip is the optional SANDBOX credential case. **Six requests are NOT RUN with a stated reason and are not counted as passes.**
- Real **SITE_OVERRIDE** is proven. Payment config and the decryption key ring survive both restart and force-recreate without printing or exposing key material.
- Independent fixes achieved: SITE_OVERRIDE path, restart durability, mutation-sensitive ordering tests, terms-guard test, honest collection accounting.
- Not achieved: booking-snapshot chain (U19), U06 boundary values, U11, U20.

**P4 is NOT closeable.** U19 remains **BLOCKED** and belongs to **SPARK-273** (reservation producer / commercial integration) together with four concrete decision-conformance violations and a missing event-driven confirmation path. Tester-group UAT sign-off is also still outstanding.

---

## 14. Artefacts and scope discipline

- New evidence: `docs/04-testing/reports/evidence/SPARK-370-independent-review-20261117-round2.json` (sanitized: no `Authorization`, no tokens, no credentials, no connection strings, no key XML — verified by substring scan).
- New report: this file. Earlier reports and evidence were left untouched as history.
- Session log updated: `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md`.
- No commit, no push, no reset/clean, no database or volume deletion, no keypair change, no real provider credentials used, no assertion weakened.
- The only live mutations were the ones the user authorised in the test window: the collection's platform-policy revisions and the payment-service restart/force-recreate.