# SPARK-370 independent P4 verification — 14 November 2026

**Decision — changes requested. P4 acceptance remains open.**

Reviewed the modified and untracked working tree on `feat/SPARK-340-admin-platform-policies`, HEAD `055da459`. The P4 implementation report is historical evidence; the results below were executed independently after rebuilding the current code.

## Executed checks

| Check | Actual outcome |
| --- | --- |
| `dotnet build SmartParking.slnx --no-restore --nologo -m:1 -v:q` | PASS, zero errors, six existing NuGet warnings |
| `dotnet test SmartParking.slnx --no-build --no-restore --nologo -m:1 -v:q` | PASS, 1,126 passed, 102 skipped, zero failed |
| Operation / Payment / Notification | 56 / 291 / 215 passed; Payment has three skipped |
| `npm --prefix FE run build` | PASS |
| `node --test tests/frontend/*.test.cjs` | PASS, 261 passed, zero failed |
| Compose infrastructure and rebuilt application | Started successfully using the existing local environment and persistent volumes; all five backend containers healthy, frontend running |
| Real UserService login | Admin, Owner and Driver all returned 200 and access tokens |
| Newman 6, unmodified P4 collection | FAIL, 38 HTTP requests executed, 37 assertions evaluated, 27 assertions failed, six test scripts failed |

Sanitized Newman evidence is `evidence/SPARK-370-Newman-UAT-Result.json`. It contains status codes and assertion results. Tokens, expanded request bodies, headers and populated environments were excluded. The pre-existing NOT_EXECUTED evidence was replaced with this actual failing execution. Two runs reproduced the same results. Runtime helper was removed after the run.

## Runtime blocker

UserService issued `role=admin`, `role=owner` and `role=driver`. The genuine Admin token returned 403 on a direct GET to OperationService catalogue, and on the three Admin GET requests in Newman. The three controllers require uppercase `ADMIN` with a case-sensitive role check.

- `UserService.Application/Authorization/AccessRoles.cs` maps the stored role to the lowercase access-token vocabulary.
- `OperationService.API/Controllers/PlatformPoliciesController.cs:12`.
- `PaymentService.API/Controllers/AdminPaymentIntegrationController.cs:26`.
- `NotificationAuditService.API/Controllers/AdminNotificationIntegrationController.cs:9`.

Align these consumers with the authoritative token contract and add real UserService-token integration coverage. Tests that construct uppercase ADMIN claims failed to expose this incompatibility. Anonymous GET 401 and Driver/Owner GET 403 checks passed, but positive Admin access failed. No successful Admin mutation occurred during this collection run.

## Collection defects

1. Six scripts failed at runtime with `Identifier 'data' has already been declared`. Static syntax validation did not reproduce the Newman sandbox binding environment. Scope response variables inside callbacks or use a non-conflicting variable and execute the scripts in Newman.
2. PLATFORM effective-policy assertions at collection lines 742–743 expect `inherited=true` and `source=PLATFORM`. The approved API returns `false` and `PLATFORM_DEFAULT` for platform self-resolution. Assert the actual contract and separately prove SITE inheritance.
3. Snapshot assertions at lines 1232 and 1256–1266 read `platformRevision.revisionId` instead of `id`, compare an old revision ID with the request URL, and include an always-true assertion. No confirmed booking/payment snapshot is compared before and after activation. This acceptance criterion is NOT PROVEN.
4. The optional SANDBOX credential request accepts both 200 and 400. A failed credential update can pass the test. Explicitly skip when prerequisites are absent and require 200 plus masked-result checks when valid credentials are supplied.
5. SCHEDULED listing can pass with an empty list; the collection never creates a scheduled activation or proves temporal collision rejection. RBAC covers reads, but lacks unauthorized Owner/Driver platform writes.
6. Catalogue assertions assume seed v1 state although the collection itself replaces the active platform revision. Supply isolated reproducible fixtures and make repeated runs deterministic.
7. Example vehicle/slot IDs are empty. Terms request returned 400 in this run. Set up valid owned fixtures before snapshot tests and verify admission availability; retain NOT RUN for missing prerequisites.
8. Exporting raw Newman JSON with a populated environment can retain bearer tokens and credentials. Keep only sanitized per-request results in tracked evidence.

## Remaining implementation findings

| Priority | Evidence | Defect and required verification |
| --- | --- | --- |
| P1 | `LotPolicyRepository.cs:440–443,527–532`; `ReservationPolicyRepository.cs:248,313–318` | Owner and Admin still lock different resources. Both transactions can validate old state and commit incompatible policies. The named concurrency test uses EF InMemory and executes a tighter platform save before Owner publish; it is sequential and cannot prove PostgreSQL serialization. Use a shared protocol and two-connection PostgreSQL regression. |
| P1 | `ProcessPayOsWebhookCommandHandler.cs:56`; `PaymentIntegrationSettingsStore.cs:263–275` | Callback verification selects a key by order creation time and prefers the currently selected mode. With both modes preconfigured, switching mode can choose another mode's key. Creating an order before rotation and issuing checkout after rotation also selects the wrong key. Persist the mode/key version actually used for checkout. |
| P1 | `PaymentIntegrationSettingsStore.cs:489–501,527–529` | Only the selected mode is persisted and only the latest revision loaded. Save LIVE credentials, switch to FIXTURE, restart, then switch back to LIVE with omitted credentials: saved LIVE credentials and historical keyring are not restored. Verify multiple modes and rotations across a new store/container instance. |
| P1 | `PaymentIntegrationSettingsStore.cs:224–230,507–517`; `PaymentIntegrationHistoryRepository.cs:54–64` | Save still acknowledges failed persistence, history allocation uses non-atomic MAX+1, and hydration marks itself complete before a possibly failed read. Test failed append, retry after initial outage and competing saves against real PostgreSQL. |
| P1 | `PayOsAdapter.cs:102,105` | Initiation redaction now includes its immutable snapshot, but cancellation redaction does not. An old API key echoed by the provider can be logged after concurrent rotation. Extend snapshot-aware sanitization to cancellation and test its error path. |
| P2 | `LotPolicyRepository.cs:35–57`; `ReservationPolicyRepository.cs:333–337` | Legacy DRIVER_REALLOCATION_REQUEST_LIMIT=2 is tolerated by Admin compatibility but still rejected on Owner republish. Reconcile catalogue/publish ownership and preserve supported historical records. |

The previous PostgreSQL CHECK values and camelCase/PascalCase hydration bugs were corrected in source. Future interval and composed-invariant checks were added. These are improvements, but the checks above remain necessary.

## P4 completion conditions

- Resolve positive Admin authorization and rerun the collection with genuine UserService tokens.
- Correct false-positive/incorrect assertions, establish owned fixtures and run every required folder with explicit skip accounting.
- Execute actual PostgreSQL concurrent Owner/Admin writes and payment persistence/restart tests.
- Demonstrate confirmed booking/payment snapshot immutability through observable persisted facts.
- Attach the user's FE+BE UAT observations and rerun affected tests after corrections.

The Docker stack remains running at frontend `http://localhost:5173` for the user's manual verification. No production implementation was changed by this independent review. Existing databases and volumes were reused, and no volume was removed.
