# SPARK-340/370 — Follow-up repairs and independent review handoff

Date: 17 November 2026. Branch: `feat/SPARK-340-admin-platform-policies`. Baseline HEAD: `055da4591ee9a1822eb9870dc261c9685ca3258b`. Existing uncommitted work is preserved. No commit/push, application database edits, schema changes or service restarts were performed in this follow-up.

## Completed changes

- Added `CheckoutBindingOutboundOrderingTests`: pending storage blocks outbound; a separate binding reader recovers the checksum at the send boundary; storage failure and readback failure each produce zero HTTP calls; an existing conflicting binding is preserved and produces zero HTTP calls. The synthetic HTTP handler never reaches PayOS.
- Added `CheckoutBinding_IsCommittedAndReadableByIndependentConnectionBeforeOutbound` to the real PostgreSQL suite. The HTTP handler opens another database context/connection and decrypts the committed binding before accepting the outbound attempt. This complements the existing fresh-context/fresh-key-provider durability proof. Assertions inside the adapter's caught HTTP path have an external completion flag so a swallowed exception cannot make the test pass.
- Fixed the terms-review Postman script to retain the HTTP failure assertion and return on a non-200 response before dereferencing `data.termsId`. An outage remains a failure. No status was relaxed into success.
- Added `tests/postman/spark340-collection.test.cjs`: regression for one failed assertion on terms503 without a cascading dereference, plus compilation of every collection event script.
- Created the reviewer prompt `SPARK-370-reviewer-after-followup-20261117.md`.

## Evidence interpretation corrections

The independent report dated 17 November is preserved as historical evidence. Apply these corrections when deciding acceptance:

| Case | What the existing evidence proves | Remaining evidence |
|---|---|---|
| U04 stale draft update | The stale If-Match/expectedUpdatedAt request returning `409 RESOURCE_VERSION_CONFLICT` is relevant. Overlap409 proves U07, not U04. | Tester should reproduce the two-window FE scenario and attach that result separately. |
| U06 temporal boundary | SCHEDULED listing proves future scheduling/status filtering. It does not by itself prove effective values before, exactly at, and after effectiveFrom. | Cite the exact automated boundary tests and their timestamps/versions, or run HTTP effective reads with explicit supported as-of times. Record live clock transition separately. |
| U11 composed invariant | Existing unit/concurrency coverage is useful automated evidence. | The exact localhost Admin inherited-min/site-max activation scenario remains NOT EXERCISED in the independent run. |
| U19 linked snapshot | Policy-revision immutability and binding durability are separate proofs. | Real terms → durable receipt → same booking and persisted accepted snapshot before/after policy activation remains BLOCKED. |
| U20 responsive UI | No automated backend evidence establishes responsive behavior. | Group tester must verify FE widths and attach actual UAT evidence. |

The user reports that most localhost FE+BE checks went well, with a further group-tester pass pending. No per-case PASS is inferred from that general statement.

## U19 dependency — source reconciled against the newer approved decision

This is broader than a missing HTTP adapter. The [approved 11 November decision](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md), [implementation alignment](../../05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md) and [producer handoff](../../05-project-management/handoffs/2026-11-09-reservation-producer-handoff.md) supersede older workflow assumptions:

- Operations commits the authoritative PENDING_PAYMENT hold and durable order intent before the financial call.
- Quote acceptance is combined with idempotent EnsureOrder. There must be no additional acceptance HTTP endpoint.
- Order provisioning runs outside Operations database locks/transactions. Payment collection and Operations confirmation are separate event-driven work, with targeted recovery.
- Transport wire schemas, machine-token format, quote/coverage concurrency and concrete ownership guarantees still require owner agreement. The documents explicitly identify these as unresolved details.

Current source still registers `UnavailableReservationCommercialOwner`. In addition, `ProgressReservationHandler.PrepareAsync` calls `AcceptAsync` while the guarded transaction is held. `ProgressBookingAsync` still uses the prior continuing workflow. Payment's EnsureOrder already accepts the quote while creating an order, but the existence of that endpoint does not deliver the full approved orchestration, quote/coverage protocol or authenticated transport. Development `DevAuthenticationHandler` accepts any nonblank internal service header; it is not a production machine-identity contract.

### Concrete implementation proposal — PROPOSED, not implemented

1. **Contract delivery owned jointly by Payment and Operations.** Retain Payment's existing preview/quote/order/collection/reconciliation use cases. Define versioned machine-only preview/quote transport, authenticated caller and trusted Account/site/vehicle/slot/interval binding. Never turn default FULL/STANDARD values into evidence of real coverage. Supply a configured tariff and its issuing authority.
2. **Machine authentication.** Prefer a dedicated Operations RS256 identity with a Payment-specific audience and narrow reservation permissions, following the existing dedicated Facility integration pattern. Payment validates issuer, signature, audience, lifetime and permission. No reuse of a nonblank development header as authority. Concrete issuer/audience/key ownership/rotation and DTO/route names require agreement.
3. **Combined acceptance and order.** Keep `PUT /internal/reservation-deposit-orders/{operationId}` as the logical idempotent boundary, subject to versioned contract agreement. In one Payment-owned transaction, validate all immutable quote terms and scope, validity/coverage evidence, amount/currency, selected inputs, exact H and full replay payload. Serialize competing consumers of the same quote. Same identity/same payload replays the original outcome, different payload conflicts. Quote expiry after durable acceptance must not turn replay into a second order.
4. **Operations orchestration.** Remove the remote acceptance step from guarded Prepare only as part of the coordinated change. Atomically persist the hold, immutable reviewed terms, stable order intent and outbox, then finish creation with a documented/versioned receipt outcome. A separate durable provisioning worker invokes Payment outside the transaction and attaches the authoritative order with replay fencing. Add collection consumer, deduplication, guarded confirmation and targeted expiry/reconciliation recovery. Preserve the Facility confirmation dependency until writer-invariant proof permits removal.
5. **Persistence and rollout.** Owners must map stable operation/quote/terms/coverage bindings, outcome lookup and event identity to durable service-owned records. If current columns cannot represent the approved protocol, provide a concrete schema delta and request a Schema Freeze exception before editing DDL. Do not overload unrelated policy history or keep acceptance only in RAM. Deploy both sides behind explicit configuration, then provide a valid tariff/coverage fixture for U19.

The cheaper alternative is to keep this dependency explicitly OPEN under SPARK-273 and continue independent SPARK-340 validation. That permits verification progress, but does not establish U19 or close P4. A fake commercial implementation or separate accept endpoint is not an acceptable alternative.

### Why implementation stops at this boundary

The shared [coding skill](../../07-skills/SmartPark-Coding-Skill-v1.0.md), section "Architecture/direction approval gate", requires agents to "stop before implementing the unapproved architectural part" and "require explicit user/project-lead approval before implementing that new direction." The general repair request authorizes fixes, but does not resolve the owner-designated open machine-authentication, wire, coverage-race and persistence choices. The approved architecture is retained; the proposed concrete contracts above need approval and owner alignment before the cross-service implementation proceeds. All independent repairs above are complete without waiting for that decision.

## Validation

- Payment ordering regression: **4 passed / 0 failed / 0 skipped**.
- Real PostgreSQL Payment suite, including new independent-connection proof: **4 passed / 0 failed / 0 skipped**. Only generated temporary databases were created/dropped. First attempt failed authentication before database creation because the inherited password differed from local configuration; rerun with `.env` parsed through `util.parseEnv` passed. No password or connection string was printed.
- Postman regression/syntax tests: **2 passed / 0 failed**.
- Whole Payment suite: **311 passed / 0 failed / 7 skipped**. The four PostgreSQL cases above were separately enabled and passed; the remaining three skips require live-broker configuration. No new whole-solution/FE run is claimed for this test-only follow-up.
- Focused Newman request with real Driver JWT and configured vehicle/slot: **HTTP503 OWNER_UNAVAILABLE, 1 assertion failed, 0 script errors**. Evidence: `evidence/SPARK-370-terms-guard-recheck-20261117.json`. This proves the guard correction and confirms that commercial integration is still unavailable. It is not a passing booking test. No platform/site mutations were run.

No FE implementation changed. Tester-group UAT, full Newman rerun, real sandbox credentials and linked booking snapshot remain separate gates. P4 is still OPEN while U19 and final tester acceptance are unresolved.
