# Reservation first slice — API design

| Metadata | Value |
| --- | --- |
| Status | Approved target semantics; conceptual field names and compatibility mapping pending |
| Contract version / date | SPARK-273 Slice 1 target / 2026-11-18; wire version to agree |
| Owning service / author | Operations / SmartPark architecture |
| Jira design / implementation / verification | [SPARK-273](https://mangamanagement.atlassian.net/browse/SPARK-273) / follow-on slices / [QA](../../04-testing/requirements-traceability/reservation-first-slice-scenarios.md) |
| Requirements baseline | SRS v1.1 and [18 November amendment](../../05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md) |
| Implementation | Operations plate review and Facility slot resolution implemented. Payment issuance, proof and booking cutover pending. See the [Payment quote handoff](../../05-project-management/handoffs/2026-11-18-SPARK-273-payment-quote-handoff.md). |

## Overview

Driver reviews an authoritative quote, submits durable admission, and observes committed progress. Booking detail exposes the persisted reservation lifecycle and payment readiness.

| Traceability | Reference |
| --- | --- |
| Applicable FR / BR / UC / US | [FR-RES-01–05, 10, 12 / UC-RES-01 / US-D01](../../01-requirements/srs/modules/04-reservations-allocation.md#fr-res-01); [BR-RES-01, 02, 07 / BR-POL-04](../../01-requirements/business-rules/04-reservations-allocation.md#br-res-01); [FR-PAY-04, 09](../../01-requirements/srs/modules/06-pricing-payments.md#fr-pay-04) |
| Permission / service ownership | [Operation matrix](../../01-requirements/srs/authorization-operation-classification.md); [ownership and sequence](../../02-architecture/architecture-decisions/reservation-workflow-target.md#ownership) |
| Approved decisions | [18 November amendment](../../05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md) |

## API Specification

Examples use conceptual wire names and the existing Operations `success/data` convention. Existing paths are reuse candidates. Target endpoints accept/return JSON and use authenticated Driver JWT, original Account from trusted actor context and owner-scoped reads. IDs are nonempty UUIDs unless stated otherwise. Required values cannot be omitted or null. Reject contradictory server-owned fields and agree unknown-field handling/version migration before publishing the DTOs.

### Commercial review

| Item | Contract |
| --- | --- |
| Method / URL | POST `/api/reservation-quotes` |
| Availability | Operations consumer implemented. Returns 503 until the versioned Payment owner route is available. The current checkout continues to use `/api/reservation-terms`. |
| Authentication / freshness | Driver JWT / TOKEN and local policy; estimate and quote review have no booking effect |
| Resource scope | Caller’s quote; authoritative slot resolution via Facility |
| Content type / headers | JSON; bearer auth |

**Request sample** for plate review. Additional fields, including actor, location, eligibility and amounts, fail binding. The separate legacy price preview remains estimate-only.

```json
{"licensePlate":"51A12345","requestedSlotId":"33333333-3333-4333-8333-333333333333","start":"2026-12-01T08:00:00Z","end":"2026-12-01T10:00:00Z","paymentMethod":"PAYOS"}
```

| Body field | Format | Contract |
| --- | --- | --- |
| licensePlate | string | Normalized using the agreed UserService rules. Operations independently canonicalizes/validates it and resolves ownership in admission. |
| requestedSlotId | UUID | Facility resolves site/tenant and CAR eligibility. |
| start, end | ISO 8601 with offset | Ordered interval. Server normalizes instants and applies effective policy. |
| paymentMethod | enum | `PAYOS` for the current paid review. |

**Response sample** (200, review). Preview is estimate-only and cannot supply an accepted quote reference.

```json
{"success":true,"data":{"quoteId":"22222222-2222-4222-8222-222222222222","quoteVersion":"v1","validUntil":"2026-12-01T07:55:00Z","canonicalPlate":"51A12345","siteId":"77777777-7777-4777-8777-777777777777","requestedSlotId":"33333333-3333-4333-8333-333333333333","start":"2026-12-01T08:00:00Z","end":"2026-12-01T10:00:00Z","paymentMethod":"PAYOS","tariffVersion":"tariff-1","isCovered":false,"coverageVersion":"none-v1","bookedAmount":20000,"coveredAmount":0,"depositAmount":20000,"currency":"VND","disclosure":"Full prepayment"}}
```

Quote ID/version and validity are required. The reviewed result exposes the immutable selection and applicable terms, including the authoritative location, interval and method, for consent. Monetary fields are Payment-owned decimal amounts in the stated currency. Coverage and tariff versions are opaque. Stale or changed terms require fresh review. Dependency unavailability cannot be priced as zero.

The response also includes `operationalPolicy` with `platformRevisionId`, `platformVersion`, `siteRevisionId`, `siteVersion`, effective integer `values`, `arrivalLateToleranceEnabled` and `cancellationTimezone`. Display these policies and Payment’s disclosure before consent. Clearing or changing the canonical plate, slot, either instant or method clears consent. A change of driver Account, expiry, changed policy, tariff terms or coverage that invalidates the binding requires a new review. A preview cannot restore consent.

Malformed input returns 400. An owner-confirmed missing slot returns 404. Ineligible slot or stale quote returns 409. Missing owner routes, credentials, incomplete pricing evidence and mismatched owner responses fail closed with 503. Review creates no reservation, dependency token or order.

**Client transition.** Keep the current review/create pair together. The current create route consumes legacy `termsId/termsVersion`. Enable submission of plate reviews only when Operations can verify the exact Payment proof, recheck coverage under its guards and consume the quote once. A quote ID cannot be passed as a terms ID.

### Submit durable admission

| Item | Contract |
| --- | --- |
| Method / URL | POST `/api/reservation-requests` |
| Availability | Existing path; target input and Saga pending |
| Authentication / freshness | Driver JWT / REQUEST, current Account and role determined asynchronously |
| Permission / resource scope | Initiating Account only; server-controlled actor, site and tenant |
| Content type / headers | JSON; bearer auth; required `Idempotency-Key` (UUID per existing header) |

**Request sample** uses the review fields above plus the accepted quote reference. Specific Slot/CAR is the scoped flow; confirm compatibility for the legacy mode field.

```http
POST /api/reservation-requests
Idempotency-Key: 11111111-1111-4111-8111-111111111111
Content-Type: application/json
```

```json
{"licensePlate":"51A12345","requestedSlotId":"33333333-3333-4333-8333-333333333333","start":"2026-12-01T08:00:00Z","end":"2026-12-01T10:00:00Z","paymentMethod":"PAYOS","quoteId":"22222222-2222-4222-8222-222222222222","quoteVersion":"v1"}
```

`quoteId` is a required UUID and `quoteVersion` a required opaque version. Submission of the exact reviewed binding records consent. Client VehicleId and SiteId are not required or authoritative. Client amounts, coverage and actor fields confer no authority.

**Response sample** (202 Accepted after durable capture). Preserve the existing `Location: /api/reservation-requests/{requestId}` receipt link or version a change.

```json
{"success":true,"data":{"requestId":"44444444-4444-4444-8444-444444444444","admissionState":"PENDING"}}
```

Request ID is required and stable for replay. The software may return the same request and current receipt on replay per the agreed wire version. Scope idempotency to Account and canonical intent. Same key and same intent resolves the original request, even if the quote subsequently expires. Same key and different intent conflicts. The 202 receipt gives no booking or payment entitlement.

### Read own progress

| Item | Contract |
| --- | --- |
| Method / URL | GET `/api/reservation-requests/{requestId}/progress` |
| Availability | Existing route; expanded target snapshot pending |
| Authentication / freshness / scope | Driver JWT / TOKEN and local Account ownership |
| Content type / headers | No request body; JSON response; bearer auth; uncached status |

**Request sample** `GET /api/reservation-requests/44444444-4444-4444-8444-444444444444/progress`. Required UUID path parameter.

**Response sample** (200).

```json
{"success":true,"data":{"requestId":"44444444-4444-4444-8444-444444444444","version":4,"admissionState":"PENDING","checks":{"account":"SUCCEEDED","vehicle":"SUCCEEDED","facility":"PENDING","booking":"PENDING"},"reservationId":null,"reservationState":null,"paymentReadiness":null,"commercialReadiness":null,"outcomeCode":null}}
```

Version is a required monotonic status revision per request. Checks are required persisted states, such as pending, succeeded, rejected, dependency failure or skipped. The reservation ID/lifecycle and readiness are nullable until a hold exists. Outcome code is nullable machine-readable detail. A terminal dependency code distinguishes timed out from unavailable. Payment readiness, commercial readiness and booking lifecycle remain independent of admission status. Admission may terminate as rejected, dependency unavailable/timed out, or hold committed. Readiness can be preparing, available, closed, or unknown. Subscription commercial readiness applies only on the future covered branch. The progress snapshot includes the scoped authoritative order ID when ready and current booking status for reconnection.

### Read own booking

| Item | Contract |
| --- | --- |
| Method / URL | GET `/api/me/reservations/{reservationId}`; existing own list remains separate |
| Availability | Existing detail; target compatibility pending |
| Authentication / freshness / scope | Driver JWT / TOKEN and original Account ownership |
| Content type / headers | No request body; JSON response; bearer auth |

**Request sample** `GET /api/me/reservations/55555555-5555-4555-8555-555555555555`. Required UUID path parameter.

**Response sample** (200, after paid order readiness).

```json
{"success":true,"data":{"reservationId":"55555555-5555-4555-8555-555555555555","state":"PENDING_PAYMENT","holdExpiresAt":"2026-12-01T07:59:00Z","paymentReadiness":"AVAILABLE","orderId":"66666666-6666-4666-8666-666666666666","confirmedAt":null}}
```

State and reservation ID are required. H is the persisted hold deadline (nullable when inapplicable). Order ID is nullable until authoritative readiness and only payable when still eligible. `confirmedAt` is nullable until committed confirmation and records guarded evaluation time. Paid checkout uses Payment’s existing `POST /api/v1/payments/orders/{orderId}/checkout` under its own authorization and expiry contract. Future `PENDING_COMMERCIAL_ACCEPTANCE` is an internal lifecycle state. `PROCESSING_SUBSCRIPTION` is presentation terminology. Neither is an admission outcome.

### Validation and error responses

Codes below are semantic examples to map to the existing envelope and finalized wire version. Example `{"success":false,"code":"INVALID_REQUEST","message":"Invalid booking interval.","errors":{"end":["Must be after start."]},"traceId":"opaque-id"}`.

| HTTP status | Machine outcome | Trigger / recovery |
| --- | --- | --- |
| 400 | INVALID_REQUEST | Malformed body, key, identifier, interval or unsupported selection. Correct inputs. |
| 401 / 403 | UNAUTHENTICATED / FORBIDDEN | Invalid JWT or rejected ingress permission. Reauthenticate or stop. |
| 404 | NOT_FOUND | Missing or out-of-scope request/booking. Avoid Account enumeration. |
| 409 | IDEMPOTENCY_CONFLICT / QUOTE_STALE | Key reused for changed normalized intent, or synchronous review conflict. Use same key for original intent; renew stale review. |
| 503 | DEPENDENCY_UNAVAILABLE | Review dependency unavailable or durable submission unavailable. Retry with original key if submission is ambiguous. |
| 500 | INTERNAL_SERVER_ERROR | Sanitized unexpected error and correlation. Resolve ambiguous submission under original key. |
| 200 status read | Terminal outcome code | Definitive ineligibility, stale quote/coverage, guard rejection, branch timeout or dependency failure after 202 are persisted outcomes, distinct from HTTP-layer errors. |

### Authorization workflow and retry outcomes

[Authorization ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md) and [saga design](../../02-architecture/architecture-decisions/reservation-workflow-target.md#request-to-confirmation) define trusted actor, durable start, read-only concurrency, branch/attempt fences and effect ownership. Current role/Account, plate and Facility all gate the hold. Use ADR §9 budgets with owner-reviewed overrides. Unknown commits or commercial effects resolve under the original identity. Timeouts on read-only checks may terminate admission without implying ineligibility. Paid confirmation uses one guarded Operations database timestamp strictly before T. For the future covered branch, H is `min(hold creation + effective reservation hold duration, T)`. Require authoritative acceptance and a protected claim at the guarded evaluation strictly before H. If expiry wins, release the hold and require fresh review and a new request. Later acceptance is for reconciliation only. See [owner recovery handoff](../../05-project-management/handoffs/2026-11-18-SPARK-273-reservation-owner-handoffs.md#covered-acceptance-recovery).

### SignalR progress contract

Authenticate the connection and authorize subscription against the persisted initiating Account and request on every join/rejoin. Derive group membership server-side. Apply the existing revocation/forced logout contract and close or remove unauthorized subscriptions, including expired authentication. Reauthorize on reconnect with a valid token. Treat group IDs as routing and prohibit caller-selected cross-Account membership. Avoid exposing plates or financial evidence to unintended recipients.

Conceptual notification content is event ID, request ID, optional reservation ID, monotonic request revision and the committed UI-safe progress snapshot. Relay only after Operations commits. Repeated delivery may reuse the event ID. Discard duplicate or older revisions, and apply a notification only to its scoped request. Snapshots must carry enough state to handle skipped revisions. Failed hub delivery leaves backend outcomes intact. Show disconnected/stale or unknown until authoritative data resumes. Rejoin and perform one authoritative progress read per initial connection, reconnect or explicit page restoration, merging by revision to handle races with arriving events. Deduplicate recovery when triggers coincide. No timer-based status reads or polling in admission, payment return or covered processing. Connection retry and local display-only countdowns can run without status requests. Render `CONFIRMED` only from committed Operations evidence. The implemented hub is `/hubs/reservation-progress`. Call `Subscribe(requestId)` and handle `ReservationProgress` snapshots.

## Sequence diagram

See the [HTTP, MassTransit and SignalR sequence and state table](../../02-architecture/architecture-decisions/reservation-workflow-target.md#request-to-confirmation).

## Cross-owner interfaces to agree

[Facility API](reservation-facility-dependency-api-design.md) · [Service handoffs](../../05-project-management/handoffs/2026-11-18-SPARK-273-reservation-owner-handoffs.md) · [Payment paid/covered owner gates](../../05-project-management/handoffs/2026-11-18-SPARK-273-payment-confirmation-handoff.md)

## Open questions and deferred items

| Item | Gap | Affected operation | Owner / approval |
| --- | --- | --- | --- |
| Wire compatibility | Route reuse, field/state names, expiry/error mappings and compatibility with legacy terms/receipts | Review, create, reads, hub | Operations, Payment, frontend / SPARK-273 |
| Owner contracts | Slot lookup route, plate and slot message versions, trust and bounded deadlines | Review and admission | Facility, UserService, Operations / SPARK-273 |
| Paid event | Prove immutable quote and distinct collection identity under the 11 November contract, or agree a versioned event | Paid confirmation | Payment and Operations / SPARK-273 |
| Covered integration | Exact HTTP route/fields and receipt implementation; entitlement logic deferred | Covered acceptance | Payment, Operations / SPARK-273 |

## Review checks

- [x] Requirements, ownership, contracts and target failure/recovery semantics linked.
- [x] REQUEST gates and TOKEN resource scope documented.
- [ ] Owners approve wire contracts and compatibility mapping before implementation.
- [ ] Runtime and release evidence pending.

## Assistant reads of driver bookings

The receipt remains at `GET /api/reservation-requests/{requestId}`. Committed progress is at its `/progress` subresource. Driver history and detail include the nullable requestId for reconnection and a nullable vehicle object with vehicleType and displayLabel from the stored booking.

### API specification

`GET /api/me/reservations` keeps its Driver JWT, Account ownership, `ReservationAdmission:Enabled` gate and `success/data` array response. It reads booking rows (`RequestId != null`), not the older all-reservations projection. `pageSize` remains 20 by default, range 1–100; `before` and `beforeId` must be supplied together. Order remains `createdAt DESC, reservationId DESC`. Unknown query fields confer no identity authority and follow existing MVC binding behavior.

Optional `from` and `to` are ISO date-time query values (clients send explicit UTC offsets), must be paired and satisfy `to > from`. Operations applies strict overlap `start < to && end > from` **before** pagination. Touching an interval boundary does not overlap. Invalid pairs/order return 400 `INVALID_REQUEST`; no new statuses are excluded.

Example (synthetic): `GET /api/me/reservations?pageSize=20&from=2032-02-28T00:00:00Z&to=2032-02-29T00:00:00Z`. No body; validated Driver Bearer required. Response is HTTP 200 with `Cache-Control` prohibiting storage:

```json
{"success":true,"data":[{"reservationId":"11111111-1111-4111-8111-111111111111","state":"CONFIRMED","siteId":"22222222-2222-4222-8222-222222222222","requestedSlotId":null,"start":"2032-02-28T01:00:00Z","end":"2032-02-28T02:00:00Z","holdExpiresAt":null,"orderId":null,"bookedAmount":null,"depositAmount":null,"currency":null,"confirmedAt":null,"createdAt":"2032-02-27T00:00:00Z","vehicle":{"vehicleType":"CAR","displayLabel":"SYNTHETIC"}}]}
```

Both list rows and `GET /api/me/reservations/{id}` (single booking object in `data`) add nullable `vehicle: {vehicleType, displayLabel}` from the owning reservation row. Missing vehicle evidence remains null; displayLabel uses stored original/normalized plate and can be null. No cross-service enrichment or invented plate. 401/403 remain JWT/role failures, foreign/missing detail remains 404, admission disabled remains 503. A dependency error never becomes an empty 200; existing error middleware determines the specific safe code.

### Assistant mapping, continuation and retry

Assistant requests `pageSize=20`, displays at most 19 rows and uses the twentieth solely as lookahead. Its protected cursor carries Account, interval, and the nineteenth row's `createdAt/reservationId`; the next request translates those into `before/beforeId`. Tampering, other Accounts, and changed filters fail before owner I/O. Cursor protection uses the existing Assistant Data Protection key ring. Old cursors require a fresh search after deployment. No frozen snapshot is promised.

Assistant maps `reservationId/state/start/end` into its existing card fields. Card/page `asOf` means the Assistant's observation time after the successful owner response, never booking creation time or a transactional DB snapshot. Created time is used only for pagination. Missing vehicle evidence remains null. Detail always re-reads the owner and verifies the returned ID. Canonical arrays/objects are required; the old `{items,nextCursor,asOf}` and `{reservation,asOf}` wire forms are no longer accepted. Existing errors, cancellation, bounded response/deadline and no-retry behavior are retained.

### Review and rollout

Deploy Operations' additive filter/vehicle extension together with the Assistant adapter. Keep admission and JWT trust requirements; do not enable legacy endpoints or alter production configuration to satisfy tests. Tests exercise the production controller/JWT/actor/handler path with synthetic external dependencies, plus consumer pagination, protected cursor, malformed payload, detail and observation-time regressions. Live PostgreSQL/broker acceptance is separate from these offline CI checks.
