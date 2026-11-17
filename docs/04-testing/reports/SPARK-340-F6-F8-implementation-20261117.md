# SPARK-340 — F6–F8 implementation and verification

17/11/2026. Baseline `c38d6f8a644e778e99c4d6d7077653aa471c5614`, feature branch `feat/SPARK-340-admin-platform-policies`. User approved the dedicated Payment table and offline repair tool, including the additive Schema Freeze exception. No running application database or Docker service was changed.

## Implemented

**F6 — trusted legacy repair.** Added Application-owned import/inventory use cases and an offline .NET tool. It validates exact existing Payment order, PAYOS/PAYMENT transaction, tenant and provider link, and requires an owner evidence reference. LEGACY_IMPORT requires explicit issuing mode/version/checksum from trusted records; LEGACY_POLICY recovers the exact previously protected record. Default dry-run writes no binding, --apply preflights the whole manifest, writes immutable mappings, verifies read-back and supports replay. Conflicts fail closed. Invalid/malformed input does not echo secrets. No current-key/timestamp inference, fake provider or new Admin HTTP permission.

An import cannot cryptographically establish the provenance of an arbitrary key entered by an operator: the approved owner/provider evidence and subsequent signed-callback verification remain mandatory. Unknown historical key/link evidence stays a reconciliation case. Production/shared-stack legacy data has not been imported by this turn.

**F7 — correct persistence boundary.** New Payment-owned checkout_signing_bindings table and EF mapping, one immutable protected binding per order/provider code, FK RESTRICT to Payment orders. Repository no longer writes checksum bindings into pricing policy_revisions. It retains read compatibility with exact legacy reserved rows, rejects attempts to replace a legacy key through the new store, and migrates them idempotently on explicit repair. No policy/history rows are automatically deleted. No TTL/prune is introduced; bindings and key ring stay with financial history until owner-approved archival.

Normal checkout passes trusted PaymentOrderId through the Application gateway request, preserving the existing nonnumeric-local-code/provider-code fallback when storing the order reference. Offline inventory/import by historical numeric orderCode does not guess mappings for unsupported nonnumeric historical references; those require owner investigation. The repository rejects orphan references. Missing DDL or failed persistence blocks outbound creation.

**F8 — reject ignored platform input.** Create/Update draft validators now reject both true and false for ArrivalLateToleranceEnabled; null/omitted remain the platform contract. MediatR/DI tests demonstrate rejection before persistence and unchanged draft/version. The Owner/site flow remains unchanged. Updated contract and collection; four new Postman negative requests require400 VALIDATION_FAILED with the exact field error.

## Actual verification

| Check | Result |
|---|---|
| Full solution build, --no-restore |0 errors/29 warnings. Includes existing package advisories and NU1900 advisory-feed access warning for the tool; restore was retried outside sandbox, no package was added/upgraded. |
| Full solution dotnet test --no-build |**1176 passed /0 failed /113 skipped /1289 total**. Operation71/3skip, Payment321/11skip; other project totals unchanged. |
| Real PostgreSQL Payment suite |**8 passed /0 failed /0 skipped** on generated temporary databases. No app DB writes. |
| Scoped FE Admin + Postman regression |**33 passed /0 failed** (30 Admin FE,3 collection tests). No FE implementation changed. |
| Focused F8 + policy suite |24 passed. |
| Focused import + ordering suite |14 passed. |

PG evidence covers protected legacy bridge/migration, unchanged pricing row count, fresh-context/key-provider decryption, concurrent first-writer winner, trusted order identity, orphan rejection, FK delete restriction, idempotent additive upgrade and offline CLI execution. CLI case proves pre-feature callback fails without binding, dry-run0writes, apply/replay, checksum conflict and wrong provider-link rejection, whole-batch preflight, sanitized malformed JSON, valid callback after restart and tampered signature rejection.

Final review also tightened rejection of an existing code presented with a different trusted PaymentOrderId. After that change, rebuilt the solution and reran the affected Payment suite (**321passed/11skipped/0failed**) and PG suite (**8/8passed**); other code was unchanged from the whole-solution run.

The outbound ordering proof now pins the writer connection and asserts a **different pg_backend_pid** on the reader at the HTTP boundary. This resolves the earlier partial-proof limitation. Positive completion flags prevent a swallowed callback assertion from producing a false pass.

Evidence: [verification JSON](evidence/SPARK-340-F6-F8-verification-20261117.json). The harness initially expected compact test-summary text for normal-verbosity PG output; its parser was corrected from the saved successful8-test log, without rerunning or relabeling a failing test.

## Deployment/review boundaries

- Reviewed additive script: `scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql`. Fresh init declaration also updated. User approval permits this specific Schema Freeze exception; prior "no SQL changed" statements describe older checkpoints only.
- The new binary requires the dedicated table before startup/use. Existing user DB has not been upgraded, no key material moved and no service rebuilt/restarted here. Follow [rollout guide](SPARK-340-F6-F8-rollout-guide.md).
- Current collection58 requests includes four F8 negatives. Syntax and assertion behavior verified locally; **live Newman against this revision NOT RUN**. The earlier54-request76/1/1 result is historical and remains unchanged. Tester/reviewer must rebuild against a prepared test DB to verify actual HTTP400.
- F6/F7/F8 code repairs are implemented and automated verification passed; independent review and owner rollout/inventory validation remain pending. Do not claim all historical checkouts were repaired or P5 fully approved solely from these tests.
- U19/SPARK-273, U06/U11/U20, group-tester sign-off and unrelated low findings retain their existing status. No new reservation orchestration was implemented.

Suggested commits: `fix(policy): reject site enablement in platform drafts [SPARK-368]`, `fix(payment): isolate signing bindings and support legacy repair [SPARK-340]`, `docs(policy): document binding rollout and verification [SPARK-340]`.
