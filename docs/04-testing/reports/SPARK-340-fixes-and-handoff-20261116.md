# SPARK-340 — Fixes and verification handoff, 16 November 2026

Implemented on the existing uncommitted working tree, base HEAD `055da459`. Source changes are ready for independent verification and manual localhost UAT. P4 acceptance still requires the fixture-dependent scenarios below.

## Fixes

- **Configuration Save atomicity inside the store.** The existing synchronization gate now covers candidate mutation, durable append and rollback. Competing saves and readers cannot observe an uncommitted candidate, and a failing save cannot roll back a later accepted save. The regression test pauses/fails one append while a second writer and reader wait, then compares live and restarted state.
- **Hydration fails closed.** Failed reads/decryption restore the previous in-memory state, leave hydration retryable and surface a safe Application exception. Mutations cannot append defaults while durable configuration is unknown. API maps that exception to 503 with the canonical code and excludes internal exception details.
- **Durable checkout signing binding.** A new Application port, Persistence adapter and Infrastructure service record the issued mode/config version/checksum under a per-order encryption purpose before outbound checkout HTTP. The immutable record uses the existing service-owned policy_revisions table and an isolated deterministic policy-id namespace, with existing allowed TARIFF/GLOBAL discriminators. There is no migration/DDL. Duplicate initiation can reuse the same binding; a different configuration cannot replace it. Store-backed webhook verification uses the saved binding exclusively and does not infer it from order creation time or current mode.
- **Stable encryption keys.** Payment Compose now mounts payment-data-protection-keys at /app/.keys/payment and supplies DataProtection:KeysDirectory. Existing local keys were transferred from the previous container to this named volume before recreation. A PostgreSQL test uses a fresh DbContext and a fresh DataProtection provider over the same key directory to prove encrypted immutable binding recovery.
- **Legacy Owner values.** Without Admin bounds, republish may retain an unchanged accepted driver reallocation limit. A fresh site or a change from 2 to 5 does not receive an implicit override grant. Explicit Admin bounds remain authoritative.
- **Overflow-safe bound arithmetic.** Both validation and Owner display ceiling conversions avoid int overflow at Int32.MaxValue.
- **Account-bound Admin identities.** Payment's dedicated Admin scheme and Notification's new Admin routes require one nonempty GUID sub/account_id and exactly one nonblank role. Notification's pre-existing non-Admin routes retain their established contract. Negative token tests cover missing/malformed account and duplicate roles.
- **Accurate snapshot tests.** Collection compares the same raw platform revision before/after and derives a booking only from the reviewed terms and completed request receipt. It no longer compares site-effective values to raw platform fields or accepts an unrelated externally supplied booking id as proof.
- **Stable audit test completion.** A pre-existing replay test's fixed 300 ms delay produced a transient full-suite failure. It now waits, with a deadline, for two consumed deliveries and retains the strict one-row dedup assertion.

## Verification

| Gate | Result |
| --- | --- |
| Solution build | PASS, zero errors, 28 existing NuGet advisory warnings on the final full build |
| Full backend regression | PASS, 1,156 passed, 108 skipped, zero failed |
| Real PostgreSQL Operation concurrency | PASS, 3/3 |
| Real PostgreSQL Payment | PASS, 3/3 including encrypted binding, fresh context/provider and immutable retry |
| FE build | PASS |
| Frontend Node tests | PASS, 261/261 |
| Newman after rebuild and Payment recreation | PASS_WITH_SKIPS, 48 executed requests, 75 passed assertions, 3 skipped, zero failed |

The collection defines 54 requests. Missing booking fixtures deliberately skip dependent requests, so executed count is lower. Evidence is `evidence/SPARK-370-fix-Newman-20261116.json`; raw tokens/headers/credential bodies/environments were excluded. Docker services remain running for the user's UAT.

An intermediate regression from applying account-bound validation to old Notification routes was corrected by scoping the new requirement to the Admin endpoint. The dedicated Notification suite passed 220/220. The new PostgreSQL binding test initially assumed an empty policy_revisions table; its assertions now account for the schema's seeded pricing rows while still requiring exactly one new immutable binding.

## Manual and independent test deliverables

- `SPARK-340-localhost-UAT-checklist.md` — twenty FE+BE scenarios, restart commands and required observations.
- `SPARK-370-independent-test-agent-prompt.md` — standalone task for a second agent, including PostgreSQL, callback/key rotation, log redaction and skip accounting.

## Remaining acceptance and rollout conditions

- Current runtime evidence skips actual SITE_OVERRIDE provenance, optional real SANDBOX credentials and linked booking snapshot scenarios because their fixtures/secrets were not supplied. Supply owned resources and complete those checks before claiming full P4 acceptance.
- Real PayOS checkouts issued before durable binding existed have no trusted persisted issuance reference. Recover known issuing configuration through an explicit reconciliation/backfill before handling their callbacks. Timestamp guessing remains disabled. The local fixture stack contains no demonstrated real-provider pending checkout.
- The local key migration was performed for this environment. Other deployments must preserve their DataProtection keys and restore them with the encrypted DB data. Restart verification does not replace a backup/restore plan.
- The configuration store retains the established synchronous service port and serializes saves within each service instance. This work does not add cross-instance cache invalidation for simultaneous administration across multiple replicas. The independent prompt should exercise the target deployment topology if multiple Payment instances are used.

No commit/push or database-schema change was made. Existing application volumes/data were preserved. Review historical reports as context; use the new evidence and independent test results for acceptance.
