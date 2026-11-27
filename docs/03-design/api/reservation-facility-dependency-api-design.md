# Reservation Facility dependency — API design

The [18 November reservation contract amendment](../../05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md) established selected-slot metadata and interval eligibility. B4 retired the site-qualified facts route and limited token operations to confirmation; C subsequently retired the remaining reservation-specific confirmation token workflow.

## Current SPARK-273 status (2026-11-26)

- Facility eligibility itself was **not** removed. Quote review uses the selected-slot metadata HTTP lookup; reviewed admission uses one interval-aware MassTransit eligibility decision and keeps the structure guard/claim outcome in Operations.
- Checkpoint chronology: B4 retired the `ADMISSION`-purpose dependency token route, C retired the remaining reservation-specific `CONFIRMATION` acquire/complete/abort token workflow, and D retired the remaining authorization and unpaid hold-expiry polling workers. The acquire/complete/abort contract below is retained only as historical decommissioning evidence.
- Operations `OrderId` is unrelated to Facility eligibility and remains nullable through pending paid admission; checkout readiness is Payment-owned. Zero-deposit coverage confirmation does not create an unpaid hold.
- Current verification entry points: [manual test guide](../../04-testing/postman/SPARK-273-manual-test-guide.md), [deployment checklist](../../04-testing/reports/SPARK-273-deployment-checklist.md#checkpoint-d-coordinated-cutover-and-recovery), and [Checkpoint E tester handoff](../../04-testing/reports/SPARK-273-checkpoint-e-tester-handoff.md).

| Metadata | Value |
| --- | --- |
| Status | Implemented development contract; historical token sections retained for decommissioning evidence |
| Contract version / date | v1 / 2026-11-26 current status; token section dated 2026-11-11 |
| Owning service | Facility; Operations is the only caller |
| Implementation / verification | Selected-slot lookup and eligibility consumer implemented; confirmation token workflow retired by C |
| Requirements baseline | [Reservation requirements](../../01-requirements/business-rules/policy-catalogue.md), CAR / Specific Slot |

## Overview

Current confirmation relies on the committed Operations claim/guard outcome plus the correlated interval-aware Facility eligibility decision; the former durable Facility configuration hold token workflow was retired by C. Reviewed quote metadata and the interval eligibility decision are read-only. Service calls use a dedicated machine identity.

| Traceability | Reference |
| --- | --- |
| Booking use case / related contracts | [Reservation API](reservation-first-slice-api-design.md) |
| Approved direction and recovery | [Current reservation workflow](../../02-architecture/architecture-decisions/reservation-workflow-target.md), [durable dependency](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-09-reservation-facility-durable-dependency.md) |
| Classification / security | [Operation matrix](../../01-requirements/srs/authorization-operation-classification.md); no fabricated human actor |

## API specification

All paths start `/internal/reservation-dependencies/v1`. JSON uses camelCase; unknown properties are ignored. Successful responses are the DTO directly, without the public Driver API's `data` wrapper. UTC timestamps are ISO 8601. Each call requires `Authorization: Bearer <service JWT>`: RS256, issuer/subject/sender `SmartParking.OperationService`, audience `SmartParking.FacilityService`, permission `reservation:facility-dependency`, valid expiry. Operations issues 60-second tokens; validation clock skew is five seconds. Signature, issuer, audience and permission are independently validated. Driver JWT and `X-Service-Key` cannot authorize this capability. TLS is required except explicit protected/local HTTP opt-in on Operations. See [key setup](../../../LOCAL-SETUP.md#57-dedicated-operations--facility-service-jwt-reservation).

### Acquire (historical token contract; retired by Checkpoint C)

> The following acquire/complete/abort contract is retained as historical decommissioning evidence. Current Operations no longer calls these token operations for reservation admission or confirmation.

| Item | Contract |
| --- | --- |
| Method / URL | POST `acquire` |
| Availability / origin | Implemented draft / SERVICE |
| Request / response content | application/json |
| Idempotency | `token` and exact immutable intent; one token per workflow/purpose |
| Response | 200 with `acceptedIntent`, `state` HELD or ABORTED, nullable `snapshot` |

Request sample (synthetic identifiers; caller supplies a future deadline):

```json
{"token":"44444444-4444-4444-4444-444444444444","workflowId":"55555555-5555-5555-5555-555555555555","purpose":"CONFIRMATION","tenantId":"11111111-1111-1111-1111-111111111111","siteId":"22222222-2222-2222-2222-222222222222","slotId":"33333333-3333-3333-3333-333333333333","intentFingerprint":"immutable-intent-digest","deadline":"2026-12-01T08:00:00Z"}
```

All IDs are required nonempty UUIDs. Purpose must be `CONFIRMATION`. An `ADMISSION` intent is rejected before acquiring a token or recording an abort tombstone. Fingerprint is required, at most 128 characters; deadline is required. Tenant must match the site's owner. Every field must remain identical on replay. An unknown acquire at/after deadline records ABORTED; an already-held token replays its result even after deadline, without permitting Operations to ignore its own deadline.

HELD response contains the exact accepted intent plus `snapshot`:

```json
{"booking":{"tenantId":"11111111-1111-1111-1111-111111111111","siteId":"22222222-2222-2222-2222-222222222222","slotId":"33333333-3333-3333-3333-333333333333","version":"SHA256_CONFIGURATION_DIGEST","eligibleCarSlot":true,"participatesInStructureGuard":true},"resources":[{"slotId":"33333333-3333-3333-3333-333333333333","spatialUnitId":"66666666-6666-6666-6666-666666666666","vehicleType":"CAR","active":true,"reachable":true,"fixedBackup":false,"ancestorUnitIds":["66666666-6666-6666-6666-666666666666"],"compatibleVehicleTypes":["CAR"]}],"dynamicBackups":[],"complete":true}
```

The example is the `snapshot` property, not the whole response. Resources have unique slot IDs. `reachable` means MVP operational eligibility, not a route calculation. Unit ancestry includes the slot's own unit. A mixed-category resource appears once; compatibility is eligibility, not additional physical stock. Dynamic backups carry nullable `spatialUnitId`, `vehicleType`, integer `count`. ABORTED has `snapshot:null`.

### Complete (historical token contract; retired by Checkpoint C)

| Item | Contract |
| --- | --- |
| Method / URL | POST `complete` |
| Availability / origin | Implemented draft / SERVICE |
| Body | `{"intent": <exact acquire intent>, "outcomeJson": "<exact persisted outcome string>"}` |
| Validation | Intent validation as above; outcomeJson required, at most 8192 characters |
| Response | 200 `{"acceptedIntent": <intent>, "state":"COMPLETED", "outcomeFingerprint":"<SHA256 of exact UTF-8 outcome string>"}` |

Operations includes the persisted confirmation outcome in the completion request. Operations sends only an outcome already committed in its database. Complete requires an acquired token. Exact outcome replay succeeds; changed outcome or aborted token conflicts. Deadline expiry cannot undo a committed outcome. Acquire after completion replays the original HELD snapshot; it never creates another hold.

### Abort (historical token contract; retired by Checkpoint C)

| Item | Contract |
| --- | --- |
| Method / URL | POST `abort` |
| Availability / origin | Implemented draft / SERVICE |
| Body | Exact acquire intent |
| Response | 200 `{"acceptedIntent": <intent>, "state":"ABORTED", "outcomeFingerprint":null}` |

Abort records a tombstone even when the token is unknown. Operations must fence local uncommitted work before sending it. A completed token cannot be aborted. Replays retain the same result; there is no site-wide force-abort operation on this API.

## Errors and retry outcomes

All errors use actual HTTP status with JSON `success`, `code`, `message`, optional `errors`, and opaque `traceId`; no duplicated status field. Example: `{"success":false,"code":"STRUCTURE_BUSY","message":"Another request is managing this site.","traceId":"opaque-correlation"}`. Responses are not cacheable. HEAD is bodyless; 401 preserves `WWW-Authenticate: Bearer`.

| HTTP | Code | Trigger / caller action |
| --- | --- | --- |
| 400 | INVALID_REQUEST | Malformed JSON/binding; fix request |
| 400 | INVALID_STRUCTURE | Required fields, enum or length validation; inspect field errors |
| 401 | UNAUTHENTICATED | Missing/invalid/expired service JWT or disabled integration; fix configuration/token |
| 403 | FORBIDDEN | Wrong permission/subject/sender or mismatched tenant; do not retry as another actor |
| 404 | SITE_NOT_FOUND | Site absent; reconcile workflow, never infer remote effect absence from this alone |
| 409 | STRUCTURE_BUSY | Editor/pending synchronization blocks acquisition; durable caller retries same intent |
| 409 | STRUCTURE_CONFLICT | Changed intent/outcome, wrong terminal state or uniqueness conflict; reconcile recorded token |
| 503 | PROTOTYPE_SCHEMA_NOT_READY | Missing required schema; fix deployment |
| 500 | INTERNAL_SERVER_ERROR | Sanitized unexpected failure; retain unknown outcome and retry exact intent |

A timeout is an unknown result. Operations owns durable retries and sends exact identities, even after browser loss. HELD cannot be released solely because it is old; abort requires the owning local fence. No client receives confirmed state merely from this API.

## Deferred items and review checklist

- [x] Service-local contracts and dedicated JWT validation implemented; no shared project dependency.
- [x] Historical acquire/complete/abort persisted through Domain/repository/UoW; resource facts and opposite-direction writer coordination implemented, then the reservation-specific token workflow was retired by C.
- [ ] Fresh compile and real PostgreSQL race/restart verification.
- [ ] Deploy keys/topology and verify wrong sender/audience/signature/permission, errors and lost responses for the retained HTTP slot metadata contract.
- [ ] Runtime verification of reviewed admission, D08 ordering, and financial reconciliation before production enablement.
- [ ] SignalR relay and post-confirmation allocation/refund/maintenance runtime enforcement remain pending verification/teammate work where separately owned.

See [the deployment cases and durable event handoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md).

## 11 November 2026 — conditional confirmation dependency retirement (design note)

[Architecture decision](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md) approved a **target** aggregate-only fast path for an unexpired held claim. This 11 November note is historical: Checkpoint C subsequently retired the `/internal/reservation-dependencies/v1` `CONFIRMATION` acquire/complete/abort workflow for reservations. Existing Facility changes can still be coordinated with Operations ahead of reservation status changes.

The C retirement requires Facility and Operations to demonstrate that **every supported Facility structure/site/slot/unit mutation and physical Gate claim** either serializes against the live Operations claim or causes a durable, timely invalidation visible under the guarded Operations aggregate transition. Prove delayed/out-of-order Facility synchronization, callback loss/replay, crash/restart, same-slot concurrent edit/entry, maintenance and future writer behavior; document excluded direct-write bypasses. Expired/invalidated-hold reacquisition no longer uses `CONFIRMATION` acquire; current guarded evaluation uses committed Operations evidence plus the interval-aware eligibility decision and fails closed strictly before reservation start T when evidence is insufficient.

The 10 November single guarded database evaluation timestamp and no-entitlement-after-T rule remain unchanged. This note authorizes no new Facility endpoint, key reuse, schema reset or production behavior claim.

## Reviewed admission interfaces

### Internal HTTP slot resolution for quote review

| Item | Contract |
| --- | --- |
| Method / URL | GET `/internal/reservation-dependencies/v1/slots/{slotId}` |
| Availability | Implemented for the coordinated development workflow. |
| Authentication / origin | SERVICE; dedicated authenticated Operations identity, audience and permission checked by Facility |
| Scope | Selected Facility slot; derive authoritative tenant/site in the owning repository |
| Content type / headers | No body; JSON response; service bearer credential; no browser JWT |

**Request sample:** slot path identifier `33333333-3333-4333-8333-333333333333` (required UUID); no site or tenant input. **Response sample** (conceptual HTTP 200, known slot):

```json
{"slotId":"33333333-3333-4333-8333-333333333333","siteId":"22222222-2222-4222-8222-222222222222","tenantId":"11111111-1111-4111-8111-111111111111"}
```

Facility derives the identities from the live slot and owning site/tenant. Preserve the typed 404 `SLOT_NOT_FOUND`, invalid-input and service-authorization handling. Operations treats a missing route, incoherent identity or inaccessible owner as dependency failure. Quote review creates no claim.

### MassTransit slot validation for admission

Operations sends `SlotCheckV1(workflowId, attemptId, accountId, fingerprint, requestedSlotId, start, end)` to `facility-reservation-slot-v1`. Facility validates the selected slot over the UTC half-open parking interval and sends `SlotDecisionV1(request, outcome, siteId?, tenantId?)` to `operations-reservation-slot-results-v1`. The reply echoes the full request. Only `ELIGIBLE` carries the derived site and tenant. `INELIGIBLE` and `DEPENDENCY_UNAVAILABLE` carry null scope. Operations accepts only the exact pending branch.

An overlapping effective bounded site maintenance window is ineligible. A materially uncertain overlap returns dependency unavailable. Cancelled, ended and nonoverlapping evidence follows the existing Facility lifecycle. Scoped/open-ended producer support and durable affected-slot reconciliation belong to the coordinated SPARK-244 and D08 integration. See the [Facility handoff](<../../05-project-management/handoffs/SPARK-273 — Reservation/SPARK-273-B2-Facility-Handoff.md>).

### Protected claim verification

Reviewed admission uses its correlated Facility outcome, Payment proof and the guarded Operations transaction. Current confirmation uses the committed Operations claim/guard outcome; the token snapshots and acquire/complete/abort replay in this document are historical evidence retained after C. Keep independent structure-edit coordination. The production rollout of the reviewed admission path requires verification of both D08 event orderings and financial reconciliation.
