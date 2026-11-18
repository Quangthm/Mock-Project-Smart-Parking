# SPARK-340 — Independent review of the F-A/F-B/F-C follow-up (18 November 2026)

Independent reviewer (not the implementer). Read-only review of the committed round-2 delta. This report supersedes nothing: it is an addendum to `SPARK-340-F6-F8-independent-review-20261117.md`, which stays in place as history.

| Field | Value |
|---|---|
| Date / time | 2026-11-18T01:59:17Z (08:59 +07:00) |
| Branch | `feat/SPARK-340-admin-platform-policies` |
| Actual HEAD | `fb4d2ce41683ff727efd8b48bd6ebaff8ee9d098` |
| Round-2 delta | `git diff --stat 8ca61a60..HEAD` = 18 files, +1067/−43 |
| Whole-story baseline | `c38d6f8a` (`c38d6f8a..fb4d2ce4` = 48 files, +2485/−64) |
| New commits | `3306bb14 fix(payment): require signing authority for checkout callbacks [SPARK-340]` (F-A), `596ac375 fix(payment): validate provider order bindings and inventory rows [SPARK-340]` (F-B + F-C), `fb4d2ce4 docs(policy): record independent review and binding hardening [SPARK-340]` |
| Working tree | `git status --porcelain` = **1 entry**: `M docs/04-testing/postman/SmartParking-FacilityStructure-SPARK-238.postman_collection.json` (unrelated, preserved untouched) |
| Untracked files | none |
| Inputs read | `AGENTS.md` → `ai/AGENTS.md` → `docs/07-skills/SmartPark-Coding-Skill-v1.0.md`; session §31–§32; `docs/03-design/api/SPARK-340-F6-F7-binding-repair-proposal.md` (APPROVED); `SPARK-340-FA-FC-implementation-20261117.md`; `SPARK-340-F6-F8-rollout-guide.md`; `evidence/SPARK-340-FA-FC-verification-20261117.json`; all changed source and test files |

No source, test, or configuration file was edited. No commit, push, reset, clean, or checkout. No Docker restart/recreate. **No DDL and no import was executed against any shared/user database** — every SQL-dependent check used the suite's generated temporary databases.

---

## 1. Commands and exact counts (this run)

| # | Command | Exit | Exact result |
|---|---|---:|---|
| 1 | `node --test tests/frontend/admin-platform-policies.test.cjs tests/postman/spark340-collection.test.cjs` | 0 | **tests 33 / pass 33 / fail 0 / skipped 0 / cancelled 0 / todo 0** (3412.5 ms) |
| 2 | `dotnet build SmartParking.slnx --nologo -m:1` | 0 | `Build succeeded.` **0 Error(s), 60 Warning(s)** (00:01:37.12) |
| 3 | `dotnet test SmartParking.slnx --no-build --nologo -m:1` | 0 | **Passed 1190 / Failed 0 / Skipped 114 / Total 1304** |
| 4 | `dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter "FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests"` | 0 | **Passed 9 / Failed 0 / Skipped 0 / Total 9** (1 m 26 s) |

`SMARTPARK_PAYMENT_TEST_CONNECTION` was built in-shell from `.env` (one surrounding quote pair stripped) against the local PostgreSQL service account with `CREATE DATABASE`; the connection string and password were never printed and captured output was filtered through `Password=***`.

### 1.1 Per-project breakdown of gate 3

| Project | Failed | Passed | Skipped | Total |
|---|---:|---:|---:|---:|
| `SmartParking.Architecture.Tests` | 0 | 12 | 0 | 12 |
| `SmartParking.FacilityService.Tests` | 0 | 199 | 29 | 228 |
| `SmartParking.IoTService.Tests` | 0 | 1 | 0 | 1 |
| `SmartParking.NotificationAuditService.Tests` | 0 | 220 | 0 | 220 |
| `SmartParking.OperationService.Tests` | 0 | 71 | 3 | 74 |
| `SmartParking.PaymentService.Tests` | 0 | 335 | 12 | 347 |
| `SmartParking.UserService.Tests` | 0 | 352 | 70 | 422 |
| **Aggregate** | **0** | **1190** | **114** | **1304** |

Payment moved 321→335 passed and 11→12 skipped relative to round 1 (the four new authority/inventory tests plus the new PG test run inside gate 4; the twelfth skip is the same env-gated PG class in gate 3).

### 1.2 PASS / FAIL / SKIP separation

- **PASS**: four commands exit 0; 1190 unit/integration tests; 9/9 real-PostgreSQL tests; 33/33 scoped Node tests.
- **FAIL**: none in any gate.
- **SKIP (114, all environment-gated, unchanged set)**: 29 Facility `SMARTPARK_STRUCTURE_TEST_CONNECTION`; 3 Operation `SMARTPARK_OPERATION_POLICY_TEST_CONNECTION`; 4 Payment `SMARTPARK_PAYMENT_TEST_CONNECTION` (the same class gate 4 executed); 3 Payment `RabbitMqLiveFact`; 68 User `SMARTPARK_AUTH_TEST_CONNECTION`; 1 User `ProfileBrokerFact`; 1 User `RabbitPostgresFact`. Skips are **not** counted as passes.
- **NOT RUN**: the live HTTP layer — see §6.

Warnings (60) are NuGet advisories only (`NU1903` AutoMapper 16.2.0 ×2, `NU1902` MessagePack 2.5.302, `NU1903` SignalR Redis 10.0.12, plus `NU1900` for the offline tool's feed). The implementation report's "29 warnings" is a `--no-restore` measurement; a restored build of the same tree yields 60. Both are advisory-only.

---

## 2. F-A — signing authority for callbacks (verdict: **CLOSED**)

| Claim | Verdict | Evidence |
|---|---|---|
| Options `ChecksumKey` can never authenticate an unbound callback | PASS | `PayOsAdapter` webhook path: `var trustedKey = _checkoutBindings is null ? null : await _checkoutBindings.ReadChecksumKeyAsync(orderCode, ct);` — the `_settings is null ? _options.ChecksumKey :` fallback is removed; a null/blank key rejects the callback before any signature acceptance. `PayOsAuthorityHardeningTests.OptionsCurrentKey_CannotValidateAnUnboundWebhook` theory over with/without a binding service asserts `IsValidSignature == false`. |
| Direct construction without settings cannot bypass the verification rule | PASS | Same test constructs the adapter from options only and asserts rejection in both configurations. |
| Real checkout fails closed without settings authority **or** without binding persistence | PASS | `PayOsAdapter.InitiatePaymentAsync` binding block now `if (_settings is null || _checkoutBindings is null) throw new InvalidOperationException("Missing checkout signing authority.");` inside the `try`, mapped to `BindingFailure("CHECKOUT_BINDING_UNAVAILABLE", ...)`. `OptionsOnly_RealCheckoutCannotBypassSettingsAndBinding` theory (no binding service / binding service present but no settings) asserts `IsSuccess false`, `ErrorCode == CHECKOUT_BINDING_UNAVAILABLE`, `handler.Calls == 0`. |
| An exact older bound key is trusted while the current options key is rejected | PASS | `OptionsOnlyVerifier_TrustsExactBindingAndRejectsCurrentKey`: binds `synthetic-issued-before-rotation`, payload signed with it verifies true; payload signed with `synthetic-current-option-key` verifies false. |
| Production DI resolves a working binding authority, and removal is detected | PASS | `ProductionInfrastructureDi_RequiresBindingRegistrationForRealOutbound` builds `AddInfrastructureServices(...)` + `PaymentIntegrationSettingsStore` + `EphemeralDataProtectionProvider` + `CheckoutBindingFixture`, forces every named client's `PrimaryHandler` via `PostConfigureAll<HttpClientFactoryOptions>`, and asserts `handler.Calls == 1` with a resolvable binding (`ReadChecksumKeyAsync(Code) == CurrentKey`) when registered, `Calls == 0` + `CHECKOUT_BINDING_UNAVAILABLE` when `ICheckoutSigningBindings` is removed. |
| Zero-outbound local emulation preserved | PASS | Ordering verified in source: FIXTURE returns `EmulatedResult` at lines 145-149 **before** the authority check; LIVE without credentials returns `PAYOS_CONFIGURATION_MISSING` (151-169); credential-less SANDBOX emulates (171-175); the authority check runs at 197-210, signature from `snapshot.ChecksumKey` at 220, `SendAsync` at 243. |

Honest scope limits confirmed in the implementation report and by my read: `PayOsOptions.ChecksumKey` is still used for the outbound checkout signature and for cancel/status configuration; `GetTrustedWebhookChecksumKey` still exists in `PaymentIntegrationSettingsStore` with no production caller.

---

## 3. F-B — provider code validated from the persisted order (verdict: **CLOSED** for supported writers)

| Claim | Verdict | Evidence |
|---|---|---|
| Single issuance mapping shared by checkout and persistence | PASS | New `Application/Integrations/PayOsOrderCodes.cs`: `Resolve(localOrderCode, paymentOrderId)` — parses `NumberStyles.Integer`; rejects non-positive numeric codes with `PaymentDomainException("INVALID_PAYMENT_ORDER_CODE", ...)`; falls back to `BinaryPrimitives.ReadInt64LittleEndian(guid) → magnitude % 900000000000UL + 100000000000L` with an explicit `Int64.MinValue` safe magnitude. `InitiatePaymentCheckoutCommandHandler` now calls it instead of the inline `Math.Abs(...)` expression. |
| Existing issuance mapping preserved (no silent code change) | PASS | `CheckoutBindingInventoryTests.CanonicalProviderCode_PreservesExistingIssuanceMapping` compares `Resolve` to the legacy formula for `"123"`, `"legacy-code"`, and an `Int64.MaxValue`-shaped Guid; `MinimumSignedGuidFragment_HasStablePositiveProviderCode` proves `Int64.MinValue` yields a code in `[100000000000, 999999999999]` (the previous `Math.Abs` would have thrown `OverflowException`). |
| Repository rejects a provider code that does not belong to the referenced order **before** inserting | PASS | `CheckoutBindingRepository.StoreIfAbsentAsync` loads the authoritative order (by trusted `PaymentOrderId`, else by `order_code`), then `if (order is null || PayOsOrderCodes.Resolve(order.OrderCode, order.Id) != orderCode) throw new InvalidOperationException("The provider order code does not match the payment order.")`. |
| Conditional INSERT cannot race a concurrent code change | PASS | `... SELECT {orderCode}, id, ... FROM payment_orders WHERE id = {orderId} AND order_code = {observedCode} ON CONFLICT (order_code) DO NOTHING`; the post-check requires the row plus a parent with `Id == orderId && OrderCode == observedCode`. Local `order_code` is `UNIQUE` (`04-payment-pricing-service-db.sql:170`), so a single-row lookup is well defined. |
| Same bound code under a different trusted `PaymentOrderId` is rejected with zero rows | PASS | New PG test `FirstBinding_RejectsAnotherOrdersProviderCodeBeforeInsert`: seeds orders 741852991/741852992, attempts 741852992 with the first order's id → `InvalidOperationException` and **0** binding rows; the correct pair then succeeds. |
| Non-positive codes rejected | PASS | `NonpositiveProviderCodeIsRejected` theory `"0"`/`"-1"` → `PaymentDomainException`; `PaymentExceptionHandler` maps it to 400 with the domain code (not 500). |

Stated limitation, which I confirm and accept: this protects supported application writers, not privileged out-of-band DML; **no new DDL or trigger** was added in this follow-up, consistent with the approval boundary.

Minor inconsistency (P4, new): `Resolve` accepts `NumberStyles.Integer` (so `" 123"` and `"+123"` parse), while the inventory canonical check uses `NumberStyles.None` plus `candidate.OrderCode == code.ToString(Invariant)` and classifies those same strings as `UNSUPPORTED_ORDER_CODE`. Issuance and inventory therefore disagree about malformed-but-parseable local codes. Fail-closed in both directions, but worth aligning.

---

## 4. F-C — mixed inventory (verdict: **CLOSED**)

| Claim | Verdict | Evidence |
|---|---|---|
| Unsupported rows are reported, not fatal | PASS | Handler validates with `NumberStyles.None`, `code > 0`, and canonical round-trip; failing rows are appended as `UNSUPPORTED_ORDER_CODE` with `OrderCode = null` and the loop `continue`s. The old `InvalidOperationException` throw is gone. |
| Local identity retained for unsupported rows | PASS | Row shape `CheckoutBindingInventoryRow(PaymentOrderId, OriginalOrderCode, OrderCode?, ProviderLinkId, State)`; unit test asserts the preserved `PaymentOrderId` and `OriginalOrderCode == "historic/not-numeric"`. |
| Valid rows after an unsupported row are still inspected | PASS | Unit test with 4 candidates returns `UNSUPPORTED`, `MISSING_BINDING`(456), `UNSUPPORTED`("000123" non-canonical), `BOUND_LEGACY_POLICY`(123). |
| Real CLI path proves the same | PASS | PG `OfflineRepair_...` seeds a 4th checkout with `OrderCode = "legacy-inventory-unsupported"`; `--inventory` exits 0, output parses to 4 rows, exactly one `UNSUPPORTED_ORDER_CODE` with the real `PaymentOrderId` and JSON `null` order code, and the numeric row (`code + 2`) is still reported. |
| Exit 0 semantics documented | PASS | Rollout guide now states exit 0 means the report completed, not that every row is ready; `MISSING_BINDING` and `UNSUPPORTED_ORDER_CODE` remain rollout actions. |

---

## 5. F6/F7/F8 criteria re-checked in this delta

| Criterion | Verdict | Note |
|---|---|---|
| Import resolves order + exact PAYOS/PAYMENT transaction/tenant/provider link | PASS | Unchanged logic; earlier evidence stands, re-exercised by the PG CLI test in this run. |
| Unknown/foreign/CASH/invalid input and conflicting key cannot create or replace a binding | PASS | `ImportCheckoutBindingTests` + PG conflict/replay assertions; new mapping check adds a further guard. |
| Trusted operator mapping is not proof of provenance | PASS | Report and guide both state it explicitly; signed-callback verification after import remains the authority. |
| Signed callback after import and restart; tamper rejection | PASS | PG CLI test: invalid before import → valid after apply → tampered false → valid after fresh key provider + context. |
| No timestamp/current-key fallback; no invented provider success | PASS | Now literally true on the verification path (F-A). Only the dead `GetTrustedWebhookChecksumKey` remains. |
| CLI: dry-run default, explicit apply, whole-manifest preflight, immutable idempotent replay, safe partial retry, bounded pagination/manifest, private paths, no key generation, generic errors, malformed input with synthetic secret | PASS | Unchanged code paths; re-proven end-to-end in this run's PG test (exit codes 0/1/2, 4-row inventory, batch preflight with zero writes, malformed JSON not echoing the key). |
| No amount/status mutation and no provider call in import | PASS | Handler has only read repositories + binding port; PG asserts binding/pricing counts and order state untouched. |
| Application orchestrates / Persistence owns SQL / Infrastructure protects | PASS | `PayOsOrderCodes` is an Application-level contract; SQL stays in Persistence; protection stays in `CheckoutSigningBindings`. |
| F7 dedicated table, FK/unique/retention, no pricing-history writes, legacy bridge + migration, no overwrite, orphan rejection, trusted order flow | PASS | Unchanged in this delta; earlier verification stands, plus the new pre-insert identity check. |
| Retention: old policy rows and key material preserved, no automatic cleanup | PASS | No new DDL; no DELETE/prune; legacy row survives migration in the PG test. |
| PG tests use two distinct backend PIDs, survive new context/key provider, assert positive external flags, temp DBs only, financial rows unchanged | PASS | `BackendPidAsync(writer)` vs reader with `Assert.NotEqual`; fresh provider/context test; `handler.Calls == 1` + post-verification visibility flag; temp DB name asserted and dropped `WITH (FORCE)`; counts asserted before/after. |
| F8 create+update true/false → 400 `VALIDATION_FAILED` with `ArrivalLateToleranceEnabled`, no candidate/version mutation, null/omitted valid, MediatR production DI, Owner flow unchanged | PASS at source/test level | Unchanged in this delta; the 4 HTTP negatives remain NOT RUN. |
| Collection is 58 requests with 4 negatives; no reuse of historical 54-request counts | PASS | Re-verified earlier this cycle; the harness test (33/33) still passes unchanged. |

---

## 6. NOT RUN register (must not be read as PASS)

| Item | Reason |
|---|---|
| Live Newman run of the 58-request collection | Policy-mutating; no agreed test window on a prepared stack; the user is running localhost UAT. Historical 54-request results stay historical. |
| 4 F8 HTTP negatives | Require a live draft id and admin token from the same window. Only the script harness is proven. |
| Live owner publish / SITE_OVERRIDE | Would mutate the UAT site policy; no window granted. |
| DDL / inventory / import against the target Payment database | Outside the approval: dedicated table and offline tool are approved as design + temporary-database testing, not as execution on user data. |
| Payment restart / force-recreate durability re-check | Would interrupt user UAT; round-1 evidence remains historical. |
| U19 booking-snapshot chain; U06; U11; U20; tester-group UAT | Unchanged blocked/unrun status. |

---

## 7. Findings

Severity: **P1** security/data-loss · **P2** high · **P3** medium · **P4** low. No P1 or P2 was introduced by this follow-up. The pre-existing P1 (committed HMAC JWT key in `PaymentService.API/appsettings.json:23`) and the P2 machine-auth/`EnsureOrder` gaps remain unchanged and are recorded in the round-1 report and §3 of the earlier U19 analysis.

### 7.1 Closed from round 1

| ID | Round-1 issue | Status now |
|---|---|---|
| F-A | current-key fallback reachable when `IPaymentIntegrationSettings` is absent | **CLOSED** |
| F-B | insert did not enforce provider code ↔ referenced order | **CLOSED** for supported writers |
| F-C | inventory aborted on one non-numeric order code | **CLOSED** |

### 7.2 New findings in this delta

| ID | Sev | File / line | Trigger | Expected | Actual | Impact | Acceptance criterion |
|---|---|---|---|---|---|---|---|
| G-A | P4 | `Application/Integrations/PayOsOrderCodes.cs` vs inventory canonical check in `ImportCheckoutBinding.cs` | Stored local code like `" 123"` or `"+123"` | One consistent canonical rule | Issuance parses it (`NumberStyles.Integer`), inventory labels it `UNSUPPORTED_ORDER_CODE` (`NumberStyles.None` + round-trip) | Operator sees a code classified unsupported although issuance would accept it; fail-closed either way | F-B single mapping contract / F-C inventory accuracy |
| G-B | P4 (process) | Implementer's first DI regression attempt | Named-client configuration overrode the defaults-level fake handler | Test harness must isolate outbound HTTP | The failed attempt "may have attempted the configured origin with synthetic credentials"; corrected with `PostConfigureAll<HttpClientFactoryOptions>` over every client's `PrimaryHandler` | No real credentials or user data involved; residual risk is that a similar mis-wired test could emit outbound traffic unnoticed | Verification hygiene for zero-outbound claims |
| G-C | P4 | `PaymentIntegrationSettingsStore.GetTrustedWebhookChecksumKey` (`...:263`) | Future re-wire | Removed or unreachable | Still implemented, still referenced by the interface and tests, no production caller | Latent re-introduction of a timestamp/current-mode fallback | F7 no-fallback |

### 7.3 Earlier findings still open (unchanged, not introduced here)

| ID | Sev | Summary |
|---|---|---|
| F-D | P4 | No immutability trigger on `checkout_signing_bindings`; immutability is insert-only convention + unique index. |
| F-E | P4 | CLI usage errors (`--offset`/`--limit` parse failures, missing value after `--manifest`, conflicting switches) exit 1 rather than 2. The guide now documents this ("known low finding"), so this is documentation-consistent but still a contract deviation. |
| F-F | P4 | `RequireOutsideRepository` silently passes when no `.git` ancestor exists. |
| F-G | P4 | PG helper `.env` fallback strips `"` only (`PaymentIntegrationPostgresConcurrencyTests.cs:327`). |
| F-H | P4 | Pre-existing: a stored non-numeric `order_code` is not resolvable at webhook time; explicitly not repaired by this follow-up and stated as such in the report. |
| F-I | P4 | Platform create/update handlers hardcode `ArrivalLateToleranceEnabled = null`; validator is the only guard against a silent drop. |

---

## 8. Approved schema change (recorded; nothing new, nothing applied)

- Approval stands as given on 17 November: *"Duyệt bảng riêng và công cụ offline"* — additive Schema Freeze exception for the Payment-owned `checkout_signing_bindings` table, its unique index on `payment_order_id`, and the retention comment.
- **This follow-up adds no schema change.** `git diff --stat 8ca61a60..HEAD` contains no `scripts/database/**` entry; F-B's guarantee is application-level only, as the report states.
- The only schema artefact remains `scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql` (transaction-scoped, idempotent, no DROP/TRUNCATE/DELETE/UPDATE), mirrored in the fresh-init DDL. It has **not** been applied to the running user database by the implementer or by this reviewer.
- Applying the old unconditional "no schema change" rule to reject this design would be wrong; the approval is equally not permission to execute it against user data.

---

## 9. Claim reconciliation

| Claim | Verdict |
|---|---|
| "1190 passed / 114 skipped" | **Confirmed exactly.** |
| "PG 9/9" | **Confirmed exactly** (1 m 26 s). |
| "scoped Node 33/33 remains historical because no FE/collection source changed" | Accurate reasoning; I re-ran it anyway and it still passes 33/33. |
| "build 0 errors / 29 warnings" | Accurate for `--no-restore`; a restored build yields 60 warnings, advisory only. |
| "Options.ChecksumKey is never a verification fallback" | **Confirmed** by source read and by two tests. |
| "Real checkout requires both settings authority and binding" | **Confirmed**, with the FIXTURE / credential-less SANDBOX emulation paths deliberately ahead of the check and still zero-HTTP. |
| "Provider code validated from persisted order using the same issuance mapping before a conditional INSERT" | **Confirmed**, including 0 rows after rejection. |
| "Mixed inventory continues to valid rows" | **Confirmed** in unit test and in the real CLI JSON. |
| "No additional schema change or shared DB execution authorized/occurred" | **Confirmed** — no DDL in the delta; nothing applied. |
| "One initial DI regression failed … may have attempted the configured origin with synthetic credentials" | Disclosed honestly and consistent with the corrected harness; recorded as G-B. |
| "Newman 58 / live DDL / import / restart / user UAT remain NOT RUN" | Accurate; kept NOT RUN here. |

---

## 10. UAT / U19 status (unchanged — not passed)

- **U19 remains BLOCKED and belongs to SPARK-273**: `UnavailableReservationCommercialOwner` is still the only `IReservationCommercialOwner`; acceptance still runs inside the guarded Prepare transaction before the hold commit; `EnsureOrder` still runs inside the guarded transaction under site/vehicle locks; the creation receipt is not terminal at hold creation; the collection-event confirmation path has no implementation.
- **U06** effective-value boundaries remain unproven by automated tests; **U11** and **U20** are not exercised.
- The user's statement that most localhost checks went well is context only. **No per-case UAT PASS is inferred and tester-group sign-off remains outstanding.**

---

## 11. Verdict

| Question | Verdict |
|---|---|
| Are F-A, F-B, F-C repaired as claimed? | **YES — all three closed.** Every promoted claim is backed by a test I ran or read verbatim, and the earlier "no current-key fallback" overstatement is now literally true. |
| Is the implementation acceptable overall? | **YES**, with the P4 observations G-A/G-B/G-C and the previously recorded F-D…F-I unchanged. No new P1/P2. |
| Is it ready for live rollout on the user's database? | **NO.** The approved upgrade script has not been applied, the inventory has not been run against real data, historical checkouts have no disposition, the new binary requires the table before it can serve checkouts, and the live 58-request Newman run plus the 4 F8 HTTP negatives are NOT RUN. |
| May P4 be closed? | **NO.** U19 is blocked under SPARK-273, U06/U11/U20 are unproven, and tester-group sign-off is pending. |
| Proposal status | Approved: dedicated `checkout_signing_bindings` + offline repair tool. Everything else in the five-step orchestration proposal remains **PROPOSED**, not delivered. |

---

## 12. Hygiene

- No secret in this report or the accompanying evidence: no tokens, Authorization headers, credentials, connection strings, key XML, or checksum values.
- Working tree unchanged at 1 entry before and after; the SPARK-238 collection was not touched.
- No application database or volume deleted, no keypair changed, no Docker service restarted.
- All historical reports and evidence (including the round-1 review this addendum builds on) are preserved unmodified.

## 13. Artefacts

- This report: `docs/04-testing/reports/SPARK-340-FA-FC-independent-review-20261117.md`
- Sanitized evidence: `docs/04-testing/reports/evidence/SPARK-340-FA-FC-independent-review-20261117.json`
- Session update: `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md` §33
- Prior artefacts retained: `SPARK-340-F6-F8-independent-review-20261117.md`, `evidence/SPARK-340-F6-F8-independent-review-20261117.json`, `SPARK-340-F6-F8-implementation-20261117.md`, `SPARK-340-FA-FC-implementation-20261117.md`, `SPARK-340-F6-F8-rollout-guide.md`