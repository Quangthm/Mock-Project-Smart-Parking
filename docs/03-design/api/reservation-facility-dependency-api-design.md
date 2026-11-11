# Reservation Facility dependency — API design

> **SPARK-273 architecture alignment (11 November 2026).** This document includes historical implementation notes. For the current approved workflow and confirmation boundary, see the [11 November decision](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md). Existing Facility confirmation checks remain required until the aggregate-only fast-path invariant gate is verified.

| Metadata | Value |
| --- | --- |
| Status | Approved direction; implemented draft, build/runtime UNVERIFIED |
| Contract version / date | v1 / 2026-11-11 |
| Owning service | Facility; Operations is the only caller |
| Implementation / verification | SPARK-273 Commit 1 working tree; deployment verification pending |
| Requirements baseline | [Reservation requirements](../../01-requirements/business-rules/policy-catalogue.md), CAR / Specific Slot |

## Overview

Operations obtains immutable Facility resource facts with a durable configuration hold while deciding a booking. Read-only review creates no hold. This is a SERVICE interaction with a dedicated machine identity, not Driver authorization.

| Traceability | Reference |
| --- | --- |
| Booking use case / related contracts | [Reservation API](reservation-first-slice-api-design.md) |
| Approved direction and recovery | [R-ARCH-05](../../02-architecture/architecture-decisions/reservation-first-slice-implementation-proposal.md), [durable dependency](../../05-project-management/handoffs/2026-11-09-reservation-facility-durable-dependency.md) |
| Classification / security | [Operation matrix](../../01-requirements/srs/authorization-operation-classification.md); no fabricated human actor |

## API specification

All paths start `/internal/reservation-dependencies/v1`. JSON uses camelCase; unknown properties are ignored. Successful responses are the DTO directly, without the public Driver API's `data` wrapper. UTC timestamps are ISO 8601. Each call requires `Authorization: Bearer <service JWT>`: RS256, issuer/subject/sender `SmartParking.OperationService`, audience `SmartParking.FacilityService`, permission `reservation:facility-dependency`, valid expiry. Operations issues 60-second tokens; validation clock skew is five seconds. Signature, issuer, audience and permission are independently validated. Driver JWT and `X-Service-Key` cannot authorize this capability. TLS is required except explicit protected/local HTTP opt-in on Operations. See [key setup](../../../LOCAL-SETUP.md#57-dedicated-operations--facility-service-jwt-reservation).

### Read facts

| Item | Contract |
| --- | --- |
| Method / URL | GET `sites/{siteId}/slots/{slotId}` |
| Availability / origin | Implemented draft / SERVICE |
| Scope | Requested Facility site/slot; dedicated Operations permission |
| Request | No body; both path IDs must be nonempty UUIDs |
| Response | 200 `BookingFacts`; no claim, order or dependency created |

Example response:

```json
{"tenantId":"11111111-1111-1111-1111-111111111111","siteId":"22222222-2222-2222-2222-222222222222","slotId":"33333333-3333-3333-3333-333333333333","version":"SHA256_CONFIGURATION_DIGEST","eligibleCarSlot":true,"participatesInStructureGuard":true}
```

`eligibleCarSlot=false` covers unavailable/incompatible/missing slot at an existing site. A missing site returns 404. `version` is an opaque configuration fingerprint; it is not a monotonically increasing sequence. A read can become stale immediately; acquire is required before acceptance.

### Acquire

| Item | Contract |
| --- | --- |
| Method / URL | POST `acquire` |
| Availability / origin | Implemented draft / SERVICE |
| Request / response content | application/json |
| Idempotency | `token` and exact immutable intent; one token per workflow/purpose |
| Response | 200 with `acceptedIntent`, `state` HELD or ABORTED, nullable `snapshot` |

Request sample (synthetic identifiers; caller supplies a future deadline):

```json
{"token":"44444444-4444-4444-4444-444444444444","workflowId":"55555555-5555-5555-5555-555555555555","purpose":"ADMISSION","tenantId":"11111111-1111-1111-1111-111111111111","siteId":"22222222-2222-2222-2222-222222222222","slotId":"33333333-3333-3333-3333-333333333333","intentFingerprint":"immutable-intent-digest","deadline":"2026-12-01T08:00:00Z"}
```

All IDs are required nonempty UUIDs. Purpose is ADMISSION or CONFIRMATION. Fingerprint is required, at most 128 characters; deadline is required. Tenant must match the site's owner. Every field must remain identical on replay. An unknown acquire at/after deadline records ABORTED; an already-held token replays its result even after deadline, without permitting Operations to ignore its own deadline.

HELD response contains the exact accepted intent plus `snapshot`:

```json
{"booking":{"tenantId":"11111111-1111-1111-1111-111111111111","siteId":"22222222-2222-2222-2222-222222222222","slotId":"33333333-3333-3333-3333-333333333333","version":"SHA256_CONFIGURATION_DIGEST","eligibleCarSlot":true,"participatesInStructureGuard":true},"resources":[{"slotId":"33333333-3333-3333-3333-333333333333","spatialUnitId":"66666666-6666-6666-6666-666666666666","vehicleType":"CAR","active":true,"reachable":true,"fixedBackup":false,"ancestorUnitIds":["66666666-6666-6666-6666-666666666666"],"compatibleVehicleTypes":["CAR"]}],"dynamicBackups":[],"complete":true}
```

The example is the `snapshot` property, not the whole response. Resources have unique slot IDs. `reachable` means MVP operational eligibility, not a route calculation. Unit ancestry includes the slot's own unit. A mixed-category resource appears once; compatibility is eligibility, not additional physical stock. Dynamic backups carry nullable `spatialUnitId`, `vehicleType`, integer `count`. ABORTED has `snapshot:null`.

### Complete

| Item | Contract |
| --- | --- |
| Method / URL | POST `complete` |
| Availability / origin | Implemented draft / SERVICE |
| Body | `{"intent": <exact acquire intent>, "outcomeJson": "<exact persisted outcome string>"}` |
| Validation | Intent validation as above; outcomeJson required, at most 8192 characters |
| Response | 200 `{"acceptedIntent": <intent>, "state":"COMPLETED", "outcomeFingerprint":"<SHA256 of exact UTF-8 outcome string>"}` |

An example outcome string is `{"reservationId":"55555555-5555-5555-5555-555555555555","outcome":"HOLD_COMMITTED"}`. Operations sends only an outcome already committed in its database. Complete requires an acquired token. Exact outcome replay succeeds; changed outcome or aborted token conflicts. Deadline expiry cannot undo a committed outcome. Acquire after completion replays the original HELD snapshot; it never creates another hold.

### Abort

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
- [x] Acquire/complete/abort persisted through Domain/repository/UoW; resource facts and opposite-direction writer coordination implemented.
- [ ] Fresh compile and real PostgreSQL race/restart verification.
- [ ] Deploy keys/topology and verify wrong sender/audience/signature/permission, errors, lost responses and delayed acquire after abort.
- [ ] Complete Operations commercial recovery and Payment producers before enabling paid reservations.
- [ ] SignalR relay and post-confirmation allocation/refund/maintenance enforcement remain teammate/next-phase work.

See [the deployment cases and durable event handoff](../../05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md).

## 11 November 2026 — conditional confirmation dependency retirement (design note)

[Architecture decision](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md) approves a **target** aggregate-only fast path for an unexpired held claim. This **does not change or remove** the current `/internal/reservation-dependencies/v1` `CONFIRMATION` acquire/complete/abort contract today. In the current flow, `ADMISSION` dependency finishes when Operations commits `PENDING_PAYMENT`, and the Facility lock is released during Driver payment. Existing Facility changes may be coordinated with Operations without immediately mutating reservation status; therefore `PENDING_PAYMENT` alone is not evidence that all external facts remain valid.

For a future `CONFIRMATION`-free fast path, Facility and Operations must demonstrate that **every supported Facility structure/site/slot/unit mutation and physical Gate claim** either serializes against the live Operations claim or causes a durable, timely invalidation visible under the guarded Operations aggregate transition. Prove delayed/out-of-order Facility synchronization, callback loss/replay, crash/restart, same-slot concurrent edit/entry, maintenance and future writer behavior; document excluded direct-write bypasses. Only after the gate passes may Operations skip the redundant confirmation dependency for a **still-valid protected hold**. `CONFIRMATION` acquire and fresh capacity checks continue for expired/invalidated-hold reacquisition strictly before reservation start T.

The 10 November single guarded database evaluation timestamp and no-entitlement-after-T rule remain unchanged. This note authorizes no new Facility endpoint, key reuse, schema reset or production behavior claim.
