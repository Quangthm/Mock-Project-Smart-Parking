# SPARK-340 — Independent review of F6/F7/F8 repair (17 November 2026)

Reviewer role: independent reviewer (not the implementer). Read-only review of the committed delta plus the one uncommitted working-tree entry.

| Field | Value |
|---|---|
| Date / time | 2026-11-17T09:09:24Z (16:09 +07:00) |
| Branch | `feat/SPARK-340-admin-platform-policies` |
| Actual HEAD | `8ca61a609f07fb1acec8a9654d9944fbd50309db` |
| Review baseline | `c38d6f8a` (`c38d6f8a644e778e99c4d6d7077653aa471c5614`) |
| Delta reviewed | `git diff --stat c38d6f8a..HEAD` = 40 files, +1437/−40 |
| Working tree | `git status --porcelain` = **1 entry**: `M docs/04-testing/postman/SmartParking-FacilityStructure-SPARK-238.postman_collection.json` (unrelated SPARK-238 work, preserved untouched) |
| Untracked files | none |
| Inputs read | `AGENTS.md` → `ai/AGENTS.md` → `docs/07-skills/SmartPark-Coding-Skill-v1.0.md`; latest SPARK-340 session; `docs/03-design/api/SPARK-340-F6-F7-binding-repair-proposal.md` (APPROVED); `SPARK-340-F6-F8-implementation-20261117.md`; `SPARK-340-F6-F8-rollout-guide.md`; `SPARK-340-F6-F8-reviewer-prompt.md`; `evidence/SPARK-340-F6-F8-verification-20261117.json` |

No source, test, or configuration file was edited. No commit, push, reset, clean, or checkout. No Docker restart/recreate. **No DDL and no import was executed against the running application database**; every SQL-dependent check used the suite's generated temporary databases.

---

## 1. Commands and exact counts

All four gates were executed by this reviewer on the state above. These are this run's numbers; the implementation report's numbers are listed separately in §9.

| # | Command | Exit | Exact result |
|---|---|---:|---|
| 1 | `node --test tests/frontend/admin-platform-policies.test.cjs tests/postman/spark340-collection.test.cjs` | 0 | **tests 33 / pass 33 / fail 0 / skipped 0 / cancelled 0 / todo 0** (1204 ms) |
| 2 | `dotnet build SmartParking.slnx --nologo -m:1` | 0 | `Build succeeded.` **0 Error(s), 60 Warning(s)** (00:00:58.21) |
| 3 | `dotnet test SmartParking.slnx --no-build --nologo -m:1` | 0 | **Passed 1176 / Failed 0 / Skipped 113 / Total 1289** |
| 4 | `dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter "FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests"` | 0 | **Passed 8 / Failed 0 / Skipped 0 / Total 8** (58 s) |

`SMARTPARK_PAYMENT_TEST_CONNECTION` was built in-shell from `.env` (one surrounding quote pair stripped) pointing at the local PostgreSQL service account with `CREATE DATABASE`; the connection string and password were never printed and all captured output was filtered through `Password=***`.

### 1.1 Per-project breakdown of gate 3

| Project | Failed | Passed | Skipped | Total |
|---|---:|---:|---:|---:|
| `SmartParking.Architecture.Tests` | 0 | 12 | 0 | 12 |
| `SmartParking.FacilityService.Tests` | 0 | 199 | 29 | 228 |
| `SmartParking.IoTService.Tests` | 0 | 1 | 0 | 1 |
| `SmartParking.NotificationAuditService.Tests` | 0 | 220 | 0 | 220 |
| `SmartParking.OperationService.Tests` | 0 | 71 | 3 | 74 |
| `SmartParking.PaymentService.Tests` | 0 | 321 | 11 | 332 |
| `SmartParking.UserService.Tests` | 0 | 352 | 70 | 422 |
| **Aggregate** | **0** | **1176** | **113** | **1289** |

No failed test names, assertion messages, or stack traces were emitted by any command.

### 1.2 PASS / FAIL / SKIP separation

- **PASS**: all 4 commands exited 0; 1176 unit/integration tests passed; 8/8 real-PostgreSQL tests passed; 33/33 scoped Node tests passed.
- **FAIL**: none, in any gate.
- **SKIP (113 in gate 3, all environment-gated, unchanged from prior cycles)**: 29 Facility `SMARTPARK_STRUCTURE_TEST_CONNECTION`; 3 Operation `SMARTPARK_OPERATION_POLICY_TEST_CONNECTION`; 4 Payment `SMARTPARK_PAYMENT_TEST_CONNECTION` (the same four tests that gate 4 executed); 3 Payment `RabbitMqLiveFact` (`SMARTPARK_RABBIT_TEST_PORT`); 68 User `SMARTPARK_AUTH_TEST_CONNECTION`; 1 User `ProfileBrokerFact`; 1 User `RabbitPostgresFact`. Skips are **not** counted as passes anywhere in this report.
- **NOT RUN**: all HTTP-level verification of the F8 negatives and the whole 58-request Newman collection — see §6.

Build warnings (60) are NuGet package advisories only (`NU1903` AutoMapper 16.2.0 ×2, `NU1902` MessagePack 2.5.302, `NU1903` Microsoft.AspNetCore.SignalR.StackExchangeRedis 10.0.12) plus the `NU1900` advisory-feed warning introduced by restoring the new offline tool. No compile errors, no analyzer errors.

---

## 2. F6 — offline repair / import (verdict: PASS with one operational finding)

### 2.1 Evidence gate resolves the real order and the exact provider link

`Application/Integrations/ImportCheckoutBinding.cs`:

- `OrderCode <= 0`, a source outside `{LEGACY_IMPORT, LEGACY_POLICY}`, blank/overlong (>255) `ProviderLinkId`, blank/overlong (>256) or control-character `EvidenceReference` → `INVALID_INPUT` (lines 32-39).
- For `LEGACY_IMPORT` additionally: `Mode` outside `{LIVE, SANDBOX}`, `Version <= 0`, blank/`>1024`/control-character `ChecksumKey` → `INVALID_INPUT`.
- `orders.GetByOrderCodeAsync(...)` null → `ORDER_NOT_FOUND`.
- The mapping is accepted only when a transaction exists with **all** of: the same `PaymentOrderId`, `TenantId == order.TenantId`, `PaymentMethod.PAYOS`, `TransactionType.PAYMENT`, `GatewayTransactionId == ProviderLinkId`, and a non-blank `CheckoutUrl`; otherwise `CHECKOUT_EVIDENCE_MISMATCH`. This is what makes CASH, foreign-tenant, wrong-order and invented links unpresentable.
- `LEGACY_POLICY` requires an authentic previously protected row (`LEGACY_BINDING_NOT_FOUND` when absent) and never invents values; `LEGACY_IMPORT` conflicts (`CONFLICT`) when a different binding already exists.
- The handler depends only on read-only order/transaction repositories and the binding port → **no provider call, no order/transaction write, no status or amount change** anywhere in the import path. Verified by direct read of the whole file and by `ImportCheckoutBindingTests.cs`.

### 2.2 CLI behaviour

`scripts/payment/SmartParking.CheckoutBindingRepair/Program.cs` (124 lines) and `CheckoutBindingRepairPostgresTests.cs` (real subprocess execution):

| Criterion | Verdict | Evidence |
|---|---|---|
| Dry-run is the default | PASS | `apply` flag is opt-in; apply branch only runs after a whole-manifest `Apply=false` preflight returns all `READY` |
| Whole-manifest preflight, zero writes on any bad entry | PASS | PG test: batch `[valid, wrong-link]` → exit 2, `CHECKOUT_EVIDENCE_MISMATCH`, binding count still 1 |
| Immutable + idempotent replay | PASS | replay `--apply` returns identical `ProtectedBinding`; `ON CONFLICT (order_code) DO NOTHING` + read-back compare |
| Safe partial-batch retry after conflict | PASS | apply loop breaks on first non-`APPLIED`; earlier committed rows are replayed idempotently (documented in the guide) |
| Bounded pagination / manifest size | PASS | inventory `Limit 1..1000`, `Offset >= 0`; manifest `<= 1 MiB`, `1..1000` entries, duplicate `OrderCode` rejected |
| Private input/key paths outside the repo | PASS (with F-F) | `RequireOutsideRepository` walks to the first `.git`; both manifest and key directory are checked |
| No automatic key generation | PASS | keys directory must already exist and contain `key-*.xml`; `DisableAutomaticKeyGeneration()` |
| Generic error output, no credential echo | PASS | bare `catch` prints a fixed sentence (exit 1); PG test asserts a malformed JSON manifest embedding a synthetic checksum key produces output that does **not** contain the key |
| Application orchestrates / Persistence owns SQL / Infrastructure owns protection | PASS | MediatR command + handler in Application; raw SQL confined to `CheckoutBindingRepository`; DataProtection protector in `CheckoutSigningBindings` |

### 2.3 Signed callback after import and after restart

PG test `OfflineRepair_DryRunApplyReplayConflictAndBatchPreflight_ThenWebhookSurvivesRestart` proves, in order: pre-import webhook signed with the historical key is **invalid** (no binding → fail closed); after `--apply`, the same callback verifies `true`; a `"tampered-signature"` payload verifies `false`; a **fresh key provider + fresh DbContext** still decrypts and verifies. No timestamp inference, no current-key substitution, no invented provider success is exercised or possible on this path.

### 2.4 Finding

- **F-C (P3, operational)**: `GetCheckoutBindingInventoryHandler` throws `InvalidOperationException("Inventory contains an unsupported provider order code.")` on the first non-numeric `payment_orders.order_code` instead of reporting that row. One unsupported historical code aborts the entire `--inventory` run (exit 1, generic message) — precisely the step the rollout guide mandates before deploying. Mitigated in practice because every current writer generates numeric order codes (Unix-millisecond based: `CreatePaymentOrderCommand.cs:70`, `EnsureReservationDepositOrderCommandHandler.cs:71/129`, `PrepareParkingFeeOrderCommandHandler.cs:175`).

---

## 3. F7 — dedicated binding table (verdict: PASS, with one documented deviation)

| Criterion | Verdict | Evidence |
|---|---|---|
| Dedicated Payment-owned table, not pricing history | PASS | `checkout_signing_bindings` in `scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql` + fresh-init declaration in `04-payment-pricing-service-db.sql`; EF mapping `CheckoutSigningBindingConfiguration.cs` |
| FK + unique order identity | PASS | `payment_order_id UUID NOT NULL REFERENCES payment_orders(id) ON DELETE RESTRICT`; `ux_checkout_signing_binding_order` unique on `payment_order_id`; PK `order_code`; `CHECK (order_code > 0)` |
| No new checkout-binding writes in `policy_revisions` | PASS | grep for `PolicyRevisions.Add/Update/Remove` under `src/Services/PaymentService` returns only the pre-existing `PaymentIntegrationHistoryRepository.cs:132` integration-settings audit row |
| Exact legacy protected-row bridge | PASS | `ReadLegacyAsync` matches `Id == PolicyId(...) && PolicyId == Id && RevisionNumber == 1 && PolicyType == "TARIFF" && ScopeType == "GLOBAL"` and deserializes only the protected payload; same DataProtection purpose/discriminator (`...CheckoutSigningBinding.v1` + order code) as the previous deployment |
| Explicit, idempotent migration | PASS | `MigrateLegacyAsync` is dry-run by default, requires the legacy row, refuses on conflict, writes `source='LEGACY_POLICY'`; PG test runs the production upgrade script twice and keeps one row, `policy_revisions` count unchanged |
| Different existing legacy key cannot be overwritten | PASS | `BindAsync` reads existing (new table **then** legacy) and returns false when `existing is not null && existing != binding`, before reserving the new row |
| Retention / no auto-cleanup | PASS | table comment states retention with financial history, no TTL/prune; no DELETE/DROP/TRUNCATE in the upgrade script; `ON DELETE RESTRICT` prevents silent parent deletion |
| Trusted `PaymentOrderId` flow | PASS (with F-B) | `InitiatePaymentCheckoutCommandHandler.cs:191` passes `PaymentOrderId: order.Id`; `PayOsAdapter.cs:202` forwards `CheckoutBindingProvenance("CHECKOUT", PaymentOrderId: ...)`; `StoreIfAbsentAsync` inserts via `SELECT ... FROM payment_orders WHERE id = {PaymentOrderId}` and throws when no matching parent exists |
| Orphan rejection | PASS | unused code + random Guid → `InvalidOperationException("An existing payment order is required for a checkout binding.")` |
| Binding before outbound HTTP | PASS | `PayOsAdapter.cs:202` bind, `:243` send; storage failure → `CHECKOUT_BINDING_UNAVAILABLE` with zero HTTP (proven by `CheckoutBindingOutboundOrderingTests` and by PG `handler.Calls == 1` / external visibility flag) |

### 3.1 Findings

- **F-B (P3, documented deviation)**: the approved proposal says "Validate that numeric order code belongs to the referenced order before insert". `StoreIfAbsentAsync` does **not** enforce `checkout_signing_bindings.order_code == payment_orders.order_code`. With a non-null trusted id that points at a *different existing* order and no prior binding, the insert succeeds and the post-check passes because it only compares `PaymentOrderId`. There is no DB constraint tying the two columns. The new PG test deliberately codifies this (it rewrites an order's `order_code` to a non-numeric string and then binds numeric code `741852969` through the trusted id). This is justified by the hash-derived provider-code fallback (`InitiatePaymentCheckoutCommandHandler.cs:87/129`), so the requirement cannot be satisfied literally — but the guarantee is caller-trust based rather than FK-enforced. No live exploit path today: the only production caller passes the order's own id.
- **F-D (P4)**: no trigger or rule prevents `UPDATE` of `protected_binding`/`source`; immutability rests on insert-only code + `ON CONFLICT DO NOTHING` + the unique index.
- **F-H (P4, pre-existing, not introduced here)**: for an order whose `payment_orders.order_code` is non-numeric, the hash-derived provider code cannot be resolved by `GetByOrderCodeAsync` at webhook time → `order == null` → the webhook is marked FAILED and the collection is never applied. This is why the F6/F7 report's statement that the fallback is "preserved" should be read narrowly.

---

## 4. F8 — platform enablement rejection (verdict: PASS at source/test level; HTTP NOT RUN)

| Criterion | Verdict | Evidence |
|---|---|---|
| Create rejects `true` **and** `false` | PASS | `CreatePolicyDraft.cs` validator `RuleFor(x => x.ArrivalLateToleranceEnabled).Null().WithMessage("Arrival late tolerance enablement belongs to the site owner and must be null for platform drafts.")`; `PlatformArrivalEnablementValidationTests.Create_ExplicitSiteEnablement_IsRejectedBeforePersistence` theory over `true`/`false` |
| Update rejects `true` **and** `false` | PASS | same `.Null()` rule in `UpdatePolicyDraft.cs`; `Update_ExplicitSiteEnablement_IsRejectedWithoutChangingDraft` theory over both |
| Canonical 400 + `VALIDATION_FAILED` + field identity | PASS at source level | `ReservationExceptionHandler.cs:53-58` maps `ValidationException` on `/api/v1/admin/policies/operations` to `(400, "VALIDATION_FAILED", "Request validation failed.")`; `:77` emits `{ property = e.PropertyName, ... }` → `ArrivalLateToleranceEnabled` |
| No persisted candidate / no version mutation | PASS | create test asserts the platform policy set is empty after rejection; update test asserts the row keeps `PaymentHoldDurationMinutes == 15`, `ArrivalLateToleranceEnabled == null`, and `UpdatedAt == draft.UpdatedAt` |
| Null / omitted still succeeds | PASS | `NullEnablement_RemainsValidForCreateAndUpdate` validates both commands |
| Production-like DI (MediatR + `ValidationBehavior`) | PASS | test resolves `IMediator` from `AddApplicationServices()`, which registers `AddValidatorsFromAssembly` and `cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))` |
| Owner/site flow unchanged | PASS | `SaveLotPolicyDraftCommandValidator` contains **no** `ArrivalLateToleranceEnabled` null rule; site enablement is still persisted and asserted non-null by `LotPoliciesSecurityAndIntegrityTests.cs:220-231`, and site drafts are created with `ArrivalLateToleranceEnabled = true` in `LotPoliciesLifecycleTests.cs:42` |

**NOT RUN**: the actual HTTP exchange for the 4 new collection negatives. They are asserted only by the Postman-script harness (`spark340-collection.test.cjs`), which proves the assertions would fail on a 200 and would reject a wrong field name — it does not prove the live endpoint returns 400.

---

## 5. Real-PostgreSQL evidence (8/8)

Generated temporary databases only; `PaymentTempDatabase` asserts its own name against `^smartpark_payment_test_[0-9a-f]{32}$`, clears the pool, and drops with `WITH (FORCE)` on the admin connection. Application databases were never used as scratch space.

| Test | What it actually proves |
|---|---|
| `CheckoutBinding_IsCommittedAndReadableByIndependentConnectionBeforeOutbound` | Writer connection is pinned and `pg_backend_pid()` captured; at the outbound boundary the reader opens a **different** backend (`Assert.NotEqual(writerPid, ...)`), decrypts the checksum through a fresh `CheckoutSigningBindings`, and only then sets `BindingWasVisible = true`, asserted after the handler returns with `Calls == 1`. Resolves the earlier "same pooled backend" limitation. |
| `CheckoutBinding_SurvivesFreshContextAndKeyProvider_AndCannotBeReplaced` | Two independent `ServiceProvider`s over a temp file key ring; same binding readable/decryptable after "restart"; a rotated LIVE/version-8 binding returns false and does not replace; unknown code → null. |
| `BindingUsesTrustedOrderIdentity_RejectsOrphans_AndPreventsOrderDeletion` | Trusted-id insert works; different existing order → `InvalidOperationException`; unused code → `InvalidOperationException`; deleting the parent order raises a foreign-key violation. |
| `LegacyPolicyBinding_RemainsVerifiable_MigratesWithoutTariffWrites_AndSurvivesLegacyRemoval` | Legacy reserved-namespace row reads through the new repository; dry-run migration writes 0 rows; different key cannot overwrite; apply writes `LEGACY_POLICY` with evidence and unchanged `policy_revisions` count; after deleting the legacy row a fresh context still decrypts; production upgrade script is idempotent. |
| `OfflineRepair_DryRunApplyReplayConflictAndBatchPreflight_ThenWebhookSurvivesRestart` | Full CLI subprocess: inventory, dry-run, apply, replay, conflict, batch preflight, malformed-JSON-with-secret, restart signature verification, tamper rejection. |
| `ConcurrentDifferentKeys_OnlyOneImmutableBindingWins_AndPricingHistoryDoesNotGrow` | Two concurrent binds, different keys → exactly one winner, one row, pricing history count unchanged. |
| `ConcurrentAppends_AllocateUniqueContiguousRevisionNumbers` / `..._AcrossTwoWaves_ContinueFromPreviousMaximum` | Concurrent history allocation stays unique and contiguous (1..12, then 1..16). |

**Financial rows unchanged by import**: asserted directly — binding count and `policy_revisions` count are checked before/after in three tests, and the import handler has no write path to orders or transactions.

---

## 6. NOT RUN register (must not be read as PASS)

| Item | Reason |
|---|---|
| Live Newman run of the 58-request collection (`docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json`) | The collection mutates platform and site policies; no agreed test window on a prepared stack. The user is running localhost UAT. Historical 54-request results (76 passed / 2 failed / 1 skipped / 6 not run) remain historical and are **not** evidence for this revision. |
| 4 F8 HTTP negatives (`F8 - create|update rejects site enablement true|false`) | Depend on a live draft id and a live admin token from the same window; no prepared stack. Only the script logic is verified. |
| Live `SITE_OVERRIDE` / owner publish re-check | Would mutate the UAT site policy; not in scope of this review and no window granted. |
| Rollout on the target Payment database (DDL, inventory, import) | Explicitly outside the approval: "Duyệt bảng riêng và công cụ offline" authorises the design and temporary-database testing, not execution against user data. |
| Payment restart / force-recreate durability re-check | Would interrupt the user's UAT; not re-run this cycle. Prior cycle's SANDBOX durability evidence stands as history only. |
| U19 booking-snapshot chain; U06 boundary; U11; U20; tester-group sign-off | Still blocked/unrun as recorded in the 17/11 review and follow-up. |

---

## 7. Findings (ordered by severity)

Severity: **P1** security/data-loss · **P2** high · **P3** medium · **P4** low.
No P1 or P2 defect was introduced by this delta. The pre-existing P1 (committed HMAC JWT key in `PaymentService.API/appsettings.json:23`) and the P2 machine-auth/`EnsureOrder` gaps were already reported in the 17/11 review and are **unchanged** by F6–F8.

| ID | Sev | File / line | Trigger | Expected | Actual | Impact | Acceptance criterion |
|---|---|---|---|---|---|---|---|
| F-A | P3 | `PayOsAdapter.cs:195`, `:220`, `:389-393`; `PaymentIntegrationSettingsStore.cs:263`; `IPaymentIntegrationSettings.cs:65` | `IPaymentIntegrationSettings` absent from DI (wiring regression) | Binding always recorded; callback verified only against a persisted per-order key | Initiate skips binding entirely and signs with `_options.ChecksumKey`; webhook falls back to `_options.ChecksumKey`; `GetTrustedWebhookChecksumKey` still exists with no production caller | Latent fail-open; the report's absolute "no current-key fallback" claim is inaccurate | F7 "no timestamp/current-key fallback" |
| F-B | P3 | `Persistence/Repositories/CheckoutBindingRepository.cs:16-33`; upgrade DDL | Non-null trusted `PaymentOrderId` pointing at a different existing order, no prior binding | Numeric provider code must belong to the referenced order before insert (proposal) | Row inserted linking code X to order Y; post-check passes (compares only `PaymentOrderId`); no DB constraint | Guarantee is caller-trust based; not exploitable by the current single caller | F7 proposal line "Validate that numeric order code belongs to the referenced order" |
| F-C | P3 | `Application/Integrations/ImportCheckoutBinding.cs` inventory handler | Any `payment_orders.order_code` that is not numeric | Flag that row and continue | `InvalidOperationException` aborts the whole `--inventory` run (exit 1) | Pre-deployment inventory step can be blocked entirely | F6 "bounded pagination / inventory identifies unresolved checkouts" |
| F-D | P4 | `checkout_signing_bindings` (DDL + EF) | Direct DB writer | Immutable rows | Insert-only convention; no immutability trigger | Tamper requires DB access; detectable by comparison | F7 "immutable" |
| F-E | P4 | `scripts/payment/.../Program.cs` arg loop | `--offset abc`, missing value after `--manifest`, conflicting switches | Exit 2 (usage validation) per the guide's table | Exit 1 (generic failure) | Operator UX / guide inaccuracy | F6 exit-code contract |
| F-F | P4 | `Program.cs` `RequireOutsideRepository` | Tool published/run with no `.git` ancestor | Path must be proven outside the repo | Silently passes | Low; source builds are inside the repo | F6 "input paths outside repo" |
| F-G | P4 | `PaymentIntegrationPostgresConcurrencyTests.cs:327` | Single-quoted `SMARTPARK_DEV_PASSWORD` used via the `.env` fallback | Strip one matching pair | `Trim('"')` only | Test-only; bypassed when the connection string is supplied | Test-infra correctness |
| F-H | P4 (pre-existing) | `ProcessPayOsWebhookCommandHandler.cs:49-53` + hash-derived code at `InitiatePaymentCheckoutCommandHandler.cs:129` | Order created with non-numeric `order_code` | Callback resolvable | Order not found → webhook FAILED, collection never applied | Pre-existing; affects the claim that the fallback is "preserved" | F7 provider-code fallback |
| F-I | P4 | `CreatePolicyDraft.cs:50`, `UpdatePolicyDraft.cs:54` | Any future bypass of the validator | Reject rather than silently drop | Handlers hardcode `null` | Defense-in-depth only | F8 "no silent drop" |

---

## 8. Claim reconciliation against the implementation report and session §31

| Claim | Verdict |
|---|---|
| "0 errors / 29 warnings" | Accurate for `--no-restore`; a restored build on the same tree yields **60 warnings** (adds `NU1900` for the new tool). Both are advisory-only. |
| "1176 passed / 113 skipped" | **Confirmed exactly** by my own run. |
| "PG 8 passed / 0 skipped" | **Confirmed exactly** (58 s). |
| "scoped Node 33 passed" | **Confirmed exactly** (30 FE + 3 collection). |
| "The outbound ordering proof … asserts a different `pg_backend_pid`" | **Confirmed** in source and by the passing test. |
| "No new checkout binding writes in pricing history" | **Confirmed** by repository grep and by two PG assertions on the row count. |
| "Different existing key cannot be overwritten" | **Confirmed** for both the legacy bridge and the new table. |
| "No timestamp/current-key fallback" | **Overstated** — see F-A. Fail-closed in the normal wiring, but the fallback code path and `GetTrustedWebhookChecksumKey` still exist. |
| "Validate that numeric order code belongs to the referenced order" | **Not implemented literally** — see F-B; the new test codifies the permissive behaviour. |
| "Live Newman 58-request revision NOT RUN" | Accurate and correctly disclaimed; kept as NOT RUN here. |
| "No running application database or Docker service was changed" | Consistent with what I observed; nothing was applied by this review either. |

---

## 9. Approved schema change (recorded, not applied)

The user's 17 November approval — *"Duyệt bảng riêng và công cụ offline"* — sanctions exactly one additive Schema Freeze exception:

- New Payment-owned table `checkout_signing_bindings` plus `ux_checkout_signing_binding_order`, one `COMMENT ON TABLE`.
- Additive only: no `DROP`, `TRUNCATE`, `DELETE`, or `UPDATE`; no financial-state or policy-history mutation.
- Prepared as a reviewable upgrade script (`scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql`, transaction-scoped, idempotent) and mirrored in the fresh-init DDL. **No automatic startup migration.**
- **Not applied** to the running user database by the implementer or by this reviewer. All execution evidence comes from generated temporary databases.

Applying the old unconditional "no schema change" rule to reject this design would be incorrect. Conversely, the approval is not permission to execute the script against user data — that remains an owner-run rollout step with the inventory gate described in the rollout guide.

---

## 10. UAT / U19 status (unchanged — not passed)

- **U19 (immutable accepted booking snapshot) remains BLOCKED.** The blocker is not a missing adapter alone: the live path calls `AcceptAsync` inside the guarded transaction *before* the hold is committed, calls `EnsureOrder` inside the same transaction under site/vehicle locks, the creation receipt is not terminal at hold creation, and the approved collection-event confirmation path has no implementation. This belongs to **SPARK-273** (Operation–Payment commercial integration), not to this branch.
- **U06** effective-value boundaries are still not proven by automated tests (only SCHEDULED/CURRENT/SUPERSEDED classification).
- **U11** (Admin raising inherited min above a site max) and **U20** (responsive UI) are not exercised.
- The user's general statement that "most localhost FE+BE checks went well" is recorded as user-reported context only. **No per-case UAT PASS is inferred, and tester-group sign-off is still outstanding.**

---

## 11. Verdict

| Question | Verdict |
|---|---|
| Are the F6/F7/F8 **implementations** acceptable? | **YES, with three medium findings (F-A, F-B, F-C) and three low findings (F-D/E/F/G plus pre-existing F-H).** The dedicated table, legacy bridge, immutable binding, fail-closed callback verification, offline repair CLI, and platform enablement rejection all behave as specified, and the strongest claims are backed by real-PostgreSQL evidence this reviewer ran. |
| Is the change **ready for live rollout** on the user's database? | **NO.** The upgrade script has not been applied, the inventory has not been run, no historical checkout has been reconciled, the live 58-request collection and the 4 F8 HTTP negatives remain NOT RUN, and the new binary requires the table to exist before it can serve checkouts. |
| May P4 be closed? | **NO.** U19 is still blocked under SPARK-273, U06/U11/U20 are unproven, and tester-group sign-off is pending. F6–F8 close their own code-level scope only. |
| Are the F6/F7/F8 contract proposals approved? | The dedicated-table/offline-tool proposal is **APPROVED** (design + temporary-DB testing). Everything else in the follow-up's five-step proposal remains **PROPOSED**, not delivered. |

---

## 12. Hygiene statement

- No secret material appears in this report or in the accompanying evidence: no tokens, no Authorization headers, no credentials, no connection strings, no key XML, no checksum values.
- `git status --porcelain` was unchanged at 1 entry before and after the review; the SPARK-238 collection was not touched.
- No application database or volume was deleted, no keypair was changed, no Docker service was restarted or recreated.
- Historical reports (`SPARK-340-P4-fix-verification-20261114.md`, `SPARK-340-fixes-and-handoff-20261116.md`, `SPARK-370-independent-reverification-2026-11-17.md`, `SPARK-340-review-followup-20261117.md`, `SPARK-370-reviewer-after-followup-20261117.md`, `SPARK-340-F6-F8-implementation-20261117.md`, `SPARK-340-F6-F8-rollout-guide.md`) are preserved unmodified.

## 13. Artefacts

- This report: `docs/04-testing/reports/SPARK-340-F6-F8-independent-review-20261117.md`
- Sanitized evidence: `docs/04-testing/reports/evidence/SPARK-340-F6-F8-independent-review-20261117.json`
- Session update: `ai/tasks/active/TASK-SPARK-340-SESSION-20261112.md` §32