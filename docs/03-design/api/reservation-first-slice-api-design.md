# Reservation first slice — API design

| Metadata | Value |
| --- | --- |
| Status | Implemented SPARK-273 reviewed-admission contract; remaining verification and deployment evidence listed separately |
| Contract version / date | SPARK-273 reviewed admission / 2026-11-26; receipt `contractVersion` 3 for reviewed admission, 2 for legacy |
| Owning service / author | Operations / SmartPark architecture |
| Jira design / implementation / verification | [SPARK-273](https://mangamanagement.atlassian.net/browse/SPARK-273) / follow-on slices / [QA](../../04-testing/requirements-traceability/reservation-first-slice-scenarios.md) |
| Requirements baseline | SRS v1.1 and [18 November amendment](../../05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md) |
| Historical Slice 1 checkpoint | Operations plate review and Facility slot resolution were implemented at this checkpoint. For subsequent Payment and admission delivery, see the [current owner handoff](<../../05-project-management/handoffs/SPARK-273 — Reservation/2026-11-18-SPARK-273-reservation-owner-handoffs.md>). See the [historical Payment quote handoff](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-18-SPARK-273-payment-quote-handoff.md). |

## Current SPARK-273 status (2026-11-26)

- The reviewed quote and admission contracts below are implemented with `quoteId` only; there is no `quoteVersion` or `FacilityVersion` wire field. `acceptedQuoteId` is a Postman variable used to pass the selected `quoteId`, not a wire property.
- Operations `OrderId` is intentionally nullable. A positive admission atomically commits `PENDING_PAYMENT`, a concrete hold with exact H and durable order-creation intent; Payment creates the payable order independently. Operations may still report null `OrderId` after Payment creates the order, and binds the order only when it consumes authoritative successful settlement data. Checkout readiness comes from Payment, never from Operations `OrderId` or `HoldExpiresAt`.
- Covered/zero-deposit admission is implemented: the committed admission handler evaluates coverage binding and authoritative accepted commercial evidence, and a zero-deposit quote confirms immediately without an unpaid H. Only specifically deferred subscription functionality remains pending; coverage, tariff, and policy revisions remain meaningful.
- Checkpoint chronology: B4 retired the `ADMISSION`-purpose dependency token path, C retired the remaining reservation-specific `CONFIRMATION` acquire/complete/abort token workflow, and D retired the remaining authorization and unpaid hold-expiry polling workers. HTTP slot metadata lookup remains separate from messaging-based interval-aware Facility eligibility.
- Current verification entry points: [manual test guide](../../04-testing/postman/SPARK-273-manual-test-guide.md), [deployment checklist](../../04-testing/reports/SPARK-273-deployment-checklist.md#checkpoint-d-coordinated-cutover-and-recovery), and [Checkpoint E tester handoff](../../04-testing/reports/SPARK-273-checkpoint-e-tester-handoff.md).

> The remainder of this document preserves the approved target flow, corrected to the implemented DTO field names. It is not runtime or deployment evidence.

## Overview

Driver reviews an authoritative quote, submits durable admission, and observes committed progress. Booking detail exposes the persisted reservation lifecycle and payment readiness.

| Traceability | Reference |
| --- | --- |
| Applicable FR / BR / UC / US | [FR-RES-01–05, 10, 12 / UC-RES-01 / US-D01](../../01-requirements/srs/modules/04-reservations-allocation.md#fr-res-01); [BR-RES-01, 02, 07 / BR-POL-04](../../01-requirements/business-rules/04-reservations-allocation.md#br-res-01); [FR-PAY-04, 09](../../01-requirements/srs/modules/06-pricing-payments.md#fr-pay-04) |
| Permission / service ownership | [Operation matrix](../../01-requirements/srs/authorization-operation-classification.md); [ownership and sequence](../../02-architecture/architecture-decisions/reservation-workflow-target.md#ownership) |
| Approved decisions | [18 November amendment](../../05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md) |

## API Specification

Examples use the implemented wire names and the Operations `success/data` convention. The endpoints accept/return JSON and use authenticated Driver JWT, original Account from trusted actor context and owner-scoped reads. IDs are nonempty UUIDs unless stated otherwise. Required values cannot be omitted or null. Review DTOs disallow unmapped members. `quoteId` is the only accepted quote reference; `acceptedQuoteId` remains a Postman variable name only.

### Commercial review

| Item | Contract |
| --- | --- |
| Method / URL | POST `/api/reservation-quotes` |
| Availability | The development implementation uses Facility's identity-only metadata and Payment's reviewed binding. Production activation is gated by the coordinated maintenance and D08 verification. |
| Authentication / freshness | Driver JWT / TOKEN and local policy; estimate and quote review have no booking effect |
| Resource scope | Caller’s quote; authoritative slot resolution via Facility |
| Content type / headers | JSON; bearer auth |

**Request sample** for plate review. Additional fields, including actor, location, eligibility and amounts, fail binding. The separate price preview remains estimate-only.

```json
{"licensePlate":"51A12345","requestedSlotId":"33333333-3333-4333-8333-333333333333","start":"2026-12-01T08:00:00Z","end":"2026-12-01T10:00:00Z","paymentMethod":"PAYOS"}
```

| Body field | Format | Contract |
| --- | --- | --- |
| licensePlate | string | Normalized using the agreed UserService rules. Operations independently canonicalizes/validates it and resolves ownership in admission. |
| requestedSlotId | UUID | Facility resolves the exact slot, site and tenant for pricing. The admission Saga checks CAR eligibility for the requested interval. |
| start, end | ISO 8601 with offset | Ordered interval. Server normalizes instants and applies effective policy. |
| paymentMethod | enum | `PAYOS` for the current paid review. |

**Response sample** (200, review). Preview is estimate-only and cannot supply an accepted quote reference.

```json
{"success":true,"data":{"quoteId":"22222222-2222-4222-8222-222222222222","validUntil":"2026-12-01T07:55:00Z","canonicalPlate":"51A12345","siteId":"77777777-7777-4777-8777-777777777777","requestedSlotId":"33333333-3333-4333-8333-333333333333","start":"2026-12-01T08:00:00Z","end":"2026-12-01T10:00:00Z","paymentMethod":"PAYOS","tariffVersion":"tariff-1","isCovered":false,"coverageVersion":"none-v1","bookedAmount":20000,"depositAmount":20000,"currency":"VND","disclosure":"Full prepayment","operationalPolicy":{"platformRevisionId":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","platformVersion":1,"siteRevisionId":"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb","siteVersion":3,"values":{"CANCELLATION_CUTOFF_MINUTES":30},"arrivalLateToleranceEnabled":true,"cancellationTimezone":"Asia/Ho_Chi_Minh"}}}
```

`quoteId` and `validUntil` are required. The reviewed result exposes the immutable selection and applicable terms, including the authoritative location, interval and method, for consent. Monetary fields are Payment-owned decimal amounts in the stated currency. Coverage and tariff versions are opaque and remain meaningful; platform/site policy revisions are independently meaningful and must not be collapsed into a single version. Stale or changed terms require fresh review. Dependency unavailability cannot be priced as zero. There is no `quoteVersion` field.

The response also includes `operationalPolicy` with `platformRevisionId`, `platformVersion`, `siteRevisionId`, `siteVersion`, effective integer `values`, `arrivalLateToleranceEnabled` and `cancellationTimezone`. Display these policies and Payment’s disclosure before consent. Clearing or changing the canonical plate, slot, either instant or method clears consent. A change of driver Account, expiry, changed policy, tariff terms or coverage that invalidates the binding requires a new review. A preview cannot restore consent.

Malformed input returns 400. An owner-confirmed missing slot returns 404. A stale quote returns 409. Definitive interval ineligibility is a later admission outcome. Missing owner routes, credentials, incomplete pricing evidence and mismatched owner responses fail closed with 503. Review creates no reservation, dependency token or order.

**Reviewed hold.** The Driver submits the reviewed `quoteId` with the same selection. Operations retrieves Payment’s immutable proof before taking its transaction guards, then checks the exact proof, joined Account/Vehicle/Facility results and current local facts. One Operations transaction commits the named reservation, accepted buffer, exact H, terminal `HOLD_CREATED`, progress and outbox effects. Reviewed admission uses one interval-aware Facility eligibility decision and creates zero Facility tokens. Checkpoint C retired the former separate `CONFIRMATION` token workflow; HTTP slot metadata lookup and messaging-based interval-aware Facility eligibility remain. Payment order readiness follows the committed hold independently.

| Quota lookup during reviewed admission | Receipt outcome |
| --- | --- |
| Successful lookup with no active CAR quota for the resolved tenant/site | Continue with named-slot and other admission checks. |
| Active quota exceeded by overlapping effective advance commitments, including the proposed booking | `REJECTED` / `RESERVATION_QUOTA_EXCEEDED`. Active zero rejects all new quota-bearing reservations. Equality is allowed. |
| Invalid or contradictory active quota evidence | `FAILED_DEPENDENCY` / `RESERVATION_QUOTA_CONFIGURATION_INVALID`, subject to the existing admission deadline. |
| Quota storage read fails | The evaluation faults and the existing retry/deadline flow applies. No hold or order intent commits in the failed evaluation. |

Physical slot eligibility, overlap, occupancy reconciliation and same-plate/site checks apply irrespective of quota presence. [Approved quota decision](../../05-project-management/decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md#optional-commercial-quota--26-november-2026)

### Submit durable admission

| Item | Contract |
| --- | --- |
| Method / URL | POST `/api/reservation-requests` |
| Availability | Existing path; target input and Saga pending |
| Authentication / freshness | Driver JWT / REQUEST, current Account and role determined asynchronously |
| Permission / resource scope | Initiating Account only; server-controlled actor, site and tenant |
| Content type / headers | JSON; bearer auth; required `Idempotency-Key` (UUID per existing header) |

**Request sample** uses the review fields above plus the accepted quote reference. Specific Slot/CAR is the supported flow. Terms-only bodies return `400 INVALID_REQUEST` without creating a request. `POST /api/reservation-terms` returns 404.

```http
POST /api/reservation-requests
Idempotency-Key: 11111111-1111-4111-8111-111111111111
Content-Type: application/json
```

```json
{"licensePlate":"51A12345","requestedSlotId":"33333333-3333-4333-8333-333333333333","start":"2026-12-01T08:00:00Z","end":"2026-12-01T10:00:00Z","paymentMethod":"PAYOS","quoteId":"22222222-2222-4222-8222-222222222222"}
```

`quoteId` is a required UUID and is the only accepted quote reference. In Postman collections the selected quote may be held in the `acceptedQuoteId` variable, but the serialized body property remains `quoteId`. Submission of the exact reviewed binding records consent. Client VehicleId and SiteId are not required or authoritative. Client amounts, coverage and actor fields confer no authority. Unknown fields are rejected by the reviewed request DTO.

**Response sample** (202 Accepted after durable capture). Preserve the existing `Location: /api/reservation-requests/{requestId}` receipt link or version a change.

```json
{"success":true,"data":{"requestId":"44444444-4444-4444-8444-444444444444","state":"PENDING","reservationId":null,"code":null,"contractVersion":3}}
```

Request ID is required and stable for replay. The receipt carries a `contractVersion` (`3` for the reviewed-admission mapping, `2` for legacy receipts); this is a compatibility version, not a quote version. Scope idempotency to Account and canonical intent. Same key and same intent resolves the original request, even if the quote subsequently expires. Same key and different intent conflicts. HTTP `202` means durable admission processing was accepted; it gives no booking, coverage, or payment entitlement.

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
{"success":true,"data":{"requestId":"44444444-444444444444","version":4,"admissionState":"PENDING_PAYMENT","checks":{"account":"SUCCEEDED","vehicle":"SUCCEEDED","facility":"SUCCEEDED","booking":"SUCCEEDED"},"reservationId":"55555555-5555-4555-8555-555555555555","reservationState":"PENDING_PAYMENT","outcomeCode":null,"orderId":null,"holdExpiresAt":"2026-12-01T07:59:00Z","bookedAmount":20000,"depositAmount":20000,"currency":"VND","eventId":"44444444444484444444444444444444/4"}}
```

`Version` is a required monotonic status revision per request; `EventId` is `{requestId:N}/{Version}` and may repeat for redelivery. `Checks` are required persisted states: `account`, `vehicle`, `facility`, `booking`. Reservation lifecycle, `outcomeCode`, `orderId`, `holdExpiresAt`, amounts and currency are nullable until the corresponding committed evidence exists. `orderId` is **not** checkout readiness: a positive admission records durable order-creation intent and exact H while `orderId` remains null until authoritative settlement binding; Payment is authoritative for checkout readiness. Admission may terminate as rejected, dependency unavailable/timed out, paid hold committed, or zero-deposit confirmed coverage. The snapshot carries the current booking state for reconnection.

There is no recurring reservation-progress or reservation-checkout-readiness polling contract: SignalR `ReservationProgress` and `ReservationCheckoutChanged` notifications drive updates, with one authoritative read on initial join, reconnect, or explicit page restoration. Independent Payment QR/transaction status checks are Payment-scoped and are not reservation lifecycle polling.

The same authenticated subscription also receives `ReservationPaymentSucceeded` for an explicitly classified, committed reviewed Payment settlement. Its payload contains `eventId`, `reservationId`, `paymentOrderId`, `paymentTransactionId`, `amount`, `currency`, `outcome` (`SUCCEEDED`) and `occurredAt` (verified provider transfer time). The client matches its reservation and Payment order before showing financial success. Reservation entitlement continues to come from committed Operations progress. `ReservationCheckoutChanged` remains the order-readiness hint.

Payment application API errors use the canonical `code` field, with `message`, `errors` and `traceId` where applicable. The external payOS callback uses its provider acknowledgement contract. [Troubleshooting guide](../../04-testing/postman/SPARK-273-manual-test-guide.md#trace-reviewed-payos-settlement)

### Read own booking

| Item | Contract |
| --- | --- |
| Method / URL | GET `/api/me/reservations/{reservationId}`; existing own list remains separate |
| Availability | Existing detail; target compatibility pending |
| Authentication / freshness / scope | Driver JWT / TOKEN and original Account ownership |
| Content type / headers | No request body; JSON response; bearer auth |

**Request sample** `GET /api/me/reservations/55555555-5555-4555-8555-555555555555`. Required UUID path parameter.

**Response sample** (200, after durable paid admission but before Payment creates the payable order).

```json
{"success":true,"data":{"reservationId":"55555555-5555-4555-8555-555555555555","state":"PENDING_PAYMENT","siteId":"77777777-7777-4777-8777-777777777777","requestedSlotId":"33333333-3333-4333-8333-333333333333","start":"2026-12-01T08:00:00Z","end":"2026-12-01T10:00:00Z","holdExpiresAt":"2026-12-01T07:59:00Z","orderId":null,"bookedAmount":20000,"depositAmount":20000,"currency":"VND","confirmedAt":null,"createdAt":"2026-11-30T00:00:00Z","zeroReceiptId":null,"requestId":"44444444-4444-4444-8444-444444444444","vehicle":{"vehicleType":"CAR","displayLabel":"51A12345"},"siteName":"North garage","siteAddress":"1 Example Road","licensePlate":"51A12345","allocatedSlotId":null,"allocationStatus":null,"parkingSessionId":null,"paymentStatus":null}}
```

The sample follows `BookingView`: all nullable fields are present and explicit. `state` and `reservationId` are required. H (`holdExpiresAt`) is the persisted hold deadline and is null for zero-deposit confirmation or otherwise inapplicable cases. `orderId` is nullable: positive admission commits order-creation intent without waiting for Payment, and Operations binds the concrete order when it consumes authoritative successful settlement data; it may still be null after Payment creates the order. `confirmedAt` is nullable until committed confirmation. Paid checkout uses Payment’s existing `POST /api/v1/payments/orders/{orderId}/checkout` under its own authorization and expiry contract. `PENDING_COMMERCIAL_ACCEPTANCE` and `PROCESSING_SUBSCRIPTION` are presentation/internal terminology, not admission outcomes.

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

[Authorization ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md) and [saga design](../../02-architecture/architecture-decisions/reservation-workflow-target.md#request-to-confirmation) define trusted actor, durable start, read-only concurrency, branch/attempt fences and effect ownership. Current role/Account, plate and Facility all gate the hold. Use ADR §9 budgets with owner-reviewed overrides. Unknown commits or commercial effects resolve under the original identity. Timeouts on read-only checks may terminate admission without implying ineligibility.

Positive paid confirmation uses one guarded Operations database timestamp strictly before T. For covered terms with an unpaid hold, H is `min(hold creation + effective reservation hold duration, T)`; require authoritative acceptance and a protected claim at the guarded evaluation strictly before H. If expiry wins, release the hold and require fresh review and a new request. Later acceptance is for reconciliation only. Zero-deposit accepted evidence confirms immediately and creates no unpaid H. Checkpoint B4 retired the `ADMISSION` token path, C retired the remaining reservation-specific `CONFIRMATION` token workflow, and D retired the remaining authorization and unpaid hold-expiry polling workers; the guarded evaluation and interval-aware Facility eligibility remain. See [owner recovery handoff](<../../05-project-management/handoffs/SPARK-273 — Reservation/2026-11-18-SPARK-273-reservation-owner-handoffs.md#covered-acceptance-recovery>).

### SignalR progress contract

Authenticate the connection and authorize subscription against the persisted initiating Account and request on every join/rejoin. Derive group membership server-side. Apply the existing revocation/forced logout contract and close or remove unauthorized subscriptions, including expired authentication. Reauthorize on reconnect with a valid token. Treat group IDs as routing and prohibit caller-selected cross-Account membership. Avoid exposing plates or financial evidence to unintended recipients.

Conceptual notification content is event ID, request ID, optional reservation ID, monotonic request revision and the committed UI-safe progress snapshot. Relay only after Operations commits. Repeated delivery may reuse the event ID. Discard duplicate or older revisions, and apply a notification only to its scoped request. Snapshots must carry enough state to handle skipped revisions. Failed hub delivery leaves backend outcomes intact. Show disconnected/stale or unknown until authoritative data resumes. Rejoin and perform one authoritative progress read per initial connection, reconnect or explicit page restoration, merging by revision to handle races with arriving events. Deduplicate recovery when triggers coincide. There is no timer-based reservation progression polling and no recurring reservation checkout-readiness polling; independent Payment QR/transaction status checks are Payment-scoped and are not authorization for reservation lifecycle polling. Connection retry and local display-only countdowns can run without status requests. Render `CONFIRMED` only from committed Operations evidence. The implemented hub is `/hubs/reservation-progress`. Call `Subscribe(requestId)` and handle `ReservationProgress` and `ReservationCheckoutChanged` events.

## Sequence diagram

See the [HTTP, MassTransit and SignalR sequence and state table](../../02-architecture/architecture-decisions/reservation-workflow-target.md#request-to-confirmation).

## Cross-owner interfaces to agree

[Facility API](reservation-facility-dependency-api-design.md) · [Service handoffs](<../../05-project-management/handoffs/SPARK-273 — Reservation/2026-11-18-SPARK-273-reservation-owner-handoffs.md>) · [Payment paid/covered owner gates](<../../05-project-management/handoffs/SPARK-273 — Reservation/2026-11-19-SPARK-273-payment-authority.md#owner-agreement-and-wire-contracts>)

## Open questions and deferred items

| Item | Gap | Affected operation | Owner / approval |
| --- | --- | --- | --- |
| Wire compatibility | Route reuse, field/state names, expiry/error mappings and recovery of existing committed receipts | Review, create, reads, hub | Operations, Payment, frontend / SPARK-273 |
| Owner contracts | Slot lookup route, plate and slot message versions, trust and bounded deadlines | Review and admission | Facility, UserService, Operations / SPARK-273 |
| Paid event | Prove immutable quote and distinct collection identity under the 11 November contract, or agree a versioned event | Paid confirmation | Payment and Operations / SPARK-273 |
| Covered integration | Coverage binding, authoritative accepted quote evaluation, and immediate zero-deposit confirmation are implemented; only specifically identified subscription functionality remains deferred | Covered acceptance | Payment, Operations / SPARK-273 |

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

## Reviewed admission timing (SPARK-273 D)

A new QuoteId-only admission captures one absolute Operations database deadline (default 15 seconds). One logical request per Account, plate and Facility branch flows through the existing Saga. Missing evidence at D gives FAILED_DEPENDENCY / ADMISSION_TIMED_OUT. Joined work reaching D or reservation start gives REJECTED / BOOKING_PREPARATION_EXPIRED. A stale authoritative quote while both cutoffs remain open gives QUOTE_STALE. A timely blocked site gives REJECTED / STRUCTURE_BUSY, including on same-key replay. Retry a fresh attempt through normal quote review and idempotency rules. The positive unpaid hold has its independent policy-derived H. The Driver receives committed lifecycle changes through the existing progress subscription.
