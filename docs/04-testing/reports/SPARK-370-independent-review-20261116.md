# Independent SPARK-340 / SPARK-370 review — 16 November 2026

**Decision — P4 acceptance remains open.** Working tree on `feat/SPARK-340-admin-platform-policies`, HEAD `055da459`, including uncommitted files. The historical claim that every blocking defect is resolved is not supported by the remaining implementation defects and skipped acceptance cases below.

## Independent verification

| Check | Result |
| --- | --- |
| Solution restore/build outside sandbox | PASS, zero errors, 56 NuGet advisory warnings |
| Full solution tests without PostgreSQL opt-in | PASS, 1,136 passed, 107 skipped, zero failed |
| Real PostgreSQL Operation concurrency suite | PASS, 3/3, zero skipped |
| Real PostgreSQL Payment allocation suite | PASS, 2/2, zero skipped |
| FE build | PASS |
| Node frontend suite | PASS, 261/261 |
| Newman current 51-request collection | PASS_WITH_SKIPS, 75 assertions passed, 6 skipped, zero failed |

The initial sandboxed no-restore build failed because its package paths were unavailable. Restore/build outside sandbox succeeded. Initial Operation PostgreSQL attempts omitted the password from the connection string; rerunning with the correctly configured secret passed all three cases. No source correction was needed for these environment failures. Real database suites create/drop generated temporary databases and preserve application databases.

All five backend containers and frontend were started from existing volumes. The three SPARK-340 images were rebuilt from current source. Sanitized current evidence is `evidence/SPARK-370-independent-Newman-20261116.json`. Historical evidence was preserved. No token, populated environment, authentication header or credential body is exported.

## Confirmed corrections

- Real UserService lowercase Admin tokens now succeed on all three Admin endpoints. Owner/Driver/anonymous denial checks pass.
- Owner publication and Admin activation acquire the same PostgreSQL transaction advisory lock before validating facts. The two-connection tests genuinely execute competing operations and pass.
- Payment revision allocation is serialized inside PostgreSQL and its two real-database tests pass.
- Full multi-mode store/keyring serialization, hydration retry, cancellation snapshot redaction and canonical error code improvements exist.
- Newman now validates filter query parameters, platform provenance, scheduled creation/exact overlap and unauthorized writes.

## Remaining defects

### P1 — Payment Save publishes uncommitted state and can roll back another accepted write

`PaymentIntegrationSettingsStore.cs:229–300` mutates the singleton under `_gate`, releases the lock, then persists. If A is persisting while B saves successfully, A's eventual failure unconditionally restores A's old state, discarding B's accepted runtime configuration. Successful appends can also occur in a different order from in-memory mutation. The repository's atomic revision allocator does not protect the store-level transaction.

Build a candidate state, serialize the complete save protocol and publish it only after durable commit. Test two interleaved saves with A failing after B succeeds, and verify runtime, latest revision and restarted store agree. Concurrent payment requests must never see credentials from a failed save.

### P1 — Failed hydration permits a destructive full-state save

`PaymentIntegrationSettingsStore.cs:229,625–655` catches read/decryption failures, leaves hydration incomplete, then permits Save to continue from in-memory defaults. A transient read failure followed by a successful append can replace existing durable mode credentials with defaults even when Admin omitted credential fields. A lost DataProtection key can trigger the same path.

Require successful authoritative hydration before accepting mutations, validate/decrypt into temporary state before publishing it, and test a failed read followed by successful write availability. Retrying reads does not make the intervening write safe.

### P1 — Checkout mode/key binding remains process-local

`PaymentIntegrationSettingsStore.cs:354–389` stores the order binding only in a dictionary. `PayOsAdapter.cs:536–544` falls back to creation-time lookup after restart or on another replica. `PaymentIntegrationSettingsStore.cs:335–350` prefers the current mode when its key interval overlaps. Two preconfigured modes therefore still permit selecting the wrong key for a valid old callback after a mode switch/restart.

Persist the issuance mode/key-version binding and retrieve that binding for callback verification. Keeping the current keyring in durable history does not reconstruct which snapshot issued each checkout. The existing report explicitly lists this feature as unapplied.

### P1 — Encrypted configuration lacks durable decryption keys in Compose

`InfrastructureDependencyInjection.cs:50–54` supports an optional DataProtection directory, but the Payment service in `compose.app.yml` supplies no stable key path/volume. Recreating the container can make previously accepted encrypted credentials undecryptable. Wire a durable key store and test container recreation with all modes and keyring restored. This remains an open acceptance dependency in the implementation report.

### P2 — Legacy tolerance grants new Owner policy values without Admin bounds

`LotPolicyRepository.cs:43–47` exempts every supplied DRIVER_REALLOCATION_REQUEST_LIMIT value in 0..5, including a new site and changes from 2 to 5. It does not compare a prior accepted site value. The policy catalogue requires Admin-defined bounds, and the Owner catalogue declares this field non-overridable. Preserve existing records without silently granting new overrides, or align an explicitly approved authoring contract across both services and the catalogue.

### P2 — Integer overflow in bound representability checks

`PolicyBoundsValidator.cs:76` and the Owner display conversion use `(value + divisor - 1) / divisor` in int arithmetic. A bound such as `[int.MaxValue,int.MaxValue]` can overflow the ceiling and pass the whole-hour representability check despite having no representable integer-hour value. Use overflow-safe arithmetic and verify boundary values.

## Test/evidence gaps

The six skipped Newman assertions cover actual SITE_OVERRIDE provenance, optional SANDBOX credentials, accepted reservation terms and booking before/after snapshot checks. Exporting them as `passed:true` in the historical evidence overstates acceptance. The current evidence explicitly records skipped assertions separately.

Snapshot logic still compares site-effective values from reviewed terms against raw historical PLATFORM values (`postman_collection.json:2273–2294`). Valid site overrides can fail this assertion. The supplied reservation_id is independent from the terms captured in the folder, so the test does not prove that the compared booking accepted those terms. Link the booking to accepted terms and compare the resolved/persisted snapshot.

Payment and Notification auth still need actor-contract parity with OperationService: sub/account_id GUID checks and uniqueness are not established merely by the passing real-token RBAC matrix. Verify malformed trusted claims and key rotation/outage in addition to ordinary login.

## Acceptance disposition

User FE+BE UAT can proceed on the running stack. Closing P4 requires correction and regression evidence for the remaining P1 defects, completed booking snapshot proof, and explicit accounting for every skipped criterion. Stable keys and durable order binding remain required behavior even though the implementation report calls them dependencies for P5. A clean Newman summary of available fixtures does not satisfy the skipped parent-story acceptance cases.

Only review/evidence/session artifacts were changed in this review. Application source was preserved.
