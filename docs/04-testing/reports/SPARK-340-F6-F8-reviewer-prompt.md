# Prompt reviewer — F6–F8 repair

Review independently on branch feat/SPARK-340-admin-platform-policies. Read AGENTS.md → ai/AGENTS.md → shared coding skill → latest SPARK-340 session → F6/F7 approved proposal → F6–F8 implementation report and rollout guide. Inspect committed and uncommitted delta relative to c38d6f8a. Report actual HEAD.

User explicitly approved dedicated checkout_signing_bindings and offline repair, including additive Schema Freeze exception. This is **not approval to run DDL/import on the shared/user DB**. Use generated temporary databases for automation. Do not restart shared Docker services or mutate platform policies outside a granted window. Preserve SPARK-238 uncommitted collection; no reset/clean, real provider calls, raw secrets in artifacts, commits or pushes unless explicitly requested.

Focus:

1. F6 import/inventory must resolve existing order and exact PAYOS/PAYMENT transaction/tenant/provider link. Unknown/foreign/CASH/invalid input and conflicting key cannot create/replace binding. Trusted operator mapping is not automatic proof of historical key provenance. Verify signed callback after import and restart, tamper rejection, no timestamp/current-key fallback and no invented provider success.
2. CLI: dry-run default, explicit apply, whole-manifest preflight, immutable/idempotent replay, safe partial-batch retry after a concurrent conflict, bounded pagination/manifest size, private input/key paths outside repo, no automatic key generation, generic error output with no credential echo. Exercise malformed input containing a synthetic secret. It must not alter amounts/status or invoke a provider. Application orchestrates, Persistence owns SQL, Infrastructure owns protection.
3. F7: dedicated table FK/unique/retention, no new checkout binding writes in pricing history; exact legacy protected-row bridge and explicit migration; different existing legacy key cannot be overwritten through the new table. Check new trusted PaymentOrderId flow for provider-code fallback and orphan rejection. Retain old policy rows and key material; no automatic cleanup.
4. PG tests must genuinely use two distinct backend PIDs at outbound boundary, survive new context/key provider, and fail under ordering/concurrency mutations. Verify positive external flags. Run concurrent bindings and idempotent DDL on temp DB, not app DB. Confirm financial rows unchanged by import.
5. F8 create AND update true/false must400 VALIDATION_FAILED with ArrivalLateToleranceEnabled field, no persisted candidate/version mutation; null/omitted succeeds. Verify MediatR production-like DI and existing Owner flow. Current collection58 requests has4 negatives; don't reuse historical54-request counts. Newly added HTTP cases remain NOT RUN until a prepared test stack is available.

Commands:

```powershell
dotnet build SmartParking.slnx --nologo -m:1
dotnet test SmartParking.slnx --no-build --nologo -m:1
dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests
node --test tests/frontend/admin-platform-policies.test.cjs tests/postman/spark340-collection.test.cjs
```

Set SMARTPARK_PAYMENT_TEST_CONNECTION and the correct local password privately; do not print connection strings. Current source tests are1176passed/113skipped, PG8/8 and scoped Node33/33, but rerun and report your own counts.

Deliver report/evidence sanitized, concrete file/line findings with triggers, PASS/FAIL/SKIP/NOT RUN separated, and a verdict for implementation versus live rollout readiness. Do not mark U19 or tester-group UAT passed. Record actual approved schema change; do not apply the old unconditional Schema Freeze rule to reject the approved design, and do not infer permission to execute it against user data. Keep historical reports and update session with the new result.
