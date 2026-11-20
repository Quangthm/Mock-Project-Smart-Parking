# PaymentService handoff for SPARK-273 Slice 2

**19 November delivery.** Payment implements reviewed issuance, scoped proof and guarded order creation. Vehicle-based checkout continues to serve historical reviews. See [Payment authority and paired verification](2026-11-19-SPARK-273-payment-authority.md).

## 1. Issue the reviewed price

Implement `POST /internal/reservation-quotes/v1` with an authenticated Operations machine token. Validate signature, issuer, Payment audience, lifetime, sender, and dedicated `reservation:quote` permission. The authenticated Operations assertion supplies the initiating driver Account, Facility scope and Operations coverage/policy revisions. Public pricing callers cannot assert these facts. Keep the current preview estimate-only.

The body is the flat camel-case serialization of `ReservationQuoteBinding`. Example:

```json
{
  "accountId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  "canonicalPlate": "51A12345",
  "tenantId": "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
  "siteId": "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
  "requestedSlotId": "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
  "start": "2026-12-01T08:00:00Z",
  "end": "2026-12-01T10:00:00Z",
  "paymentMethod": "PAYOS",
  "coverage": { "isCovered": false, "version": "none-v1" },
  "facilityVersion": "opaque-facility-version",
  "platformPolicyId": "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee",
  "platformPolicyVersion": 4,
  "sitePolicyId": "ffffffff-ffff-4fff-8fff-ffffffffffff",
  "sitePolicyVersion": 2
}
```

Return HTTP 200 with `success: true` and `data` containing all fields of `PaymentReservationQuote`. Include the exact persisted `binding`, nonempty `quoteId`, opaque `quoteVersion`, `status: "ISSUED"`, `validUntil`, real `tariffVersion`, whole-VND `bookedAmount`, explicit `coveredAmount: 0`, `depositAmount`, `currency: "VND"`, `prepaymentMode: "FULL"` and nonblank `disclosure`. Preserve the pricing engine’s tariff/revision, rounding and disclosure evidence. For this existing full-prepayment flow deposit equals booked amount. A genuine configured zero and missing configuration have different outcomes. Never issue a successful quote from absent tariff data. Changing selection starts a new review. A retried issuance may return a fresh, independent quote with its own expiry. Only the ID/version the driver accepts is eligible for admission.

## 2. Recover exact proof after review

Payment exposes the read-only service operation `GET /internal/reservation-quotes/v1/{quoteId}?version=…&accountId=…`. Require the same machine trust with authorized Account scope. Query by immutable ID, exact version and Account. Give unknown and foreign-Account queries indistinguishable missing results. Return the original binding, amounts, currency, disclosure and tariff revision plus current consumption state. Preserve the original values after expiry for authorized replay and reconciliation. Unknown routes, owner outages and malformed evidence cannot prove expiry or absence. Exact scoped misses return QUOTE_NOT_FOUND. A returned proof carries its persisted status and validity. Dependency failure yields an unavailable outcome.

Operations must resolve idempotent request replay by original Account and key first. A replay with identical canonical intent returns its receipt even after expiry. Different intent conflicts. Before the guarded booking transaction, load the trusted proof. Under the guards compare the original selection, Facility configuration, operational-policy revisions, current plate/site/interval entitlement and database time. Reject new acceptance at or after validity, or if the binding has changed. Enforce single use of the quote across concurrent booking requests with the owning booking transaction and a durable uniqueness guarantee. Persist only the immutable quote reference, consent/decision context, independently accepted policy and required booking facts/amounts. This feeds Slice 3 without HTTP under database locks.

**Order acceptance.** Operations checks quote validity at the guarded hold commit. Once accepted, the signed post-commit order request supplies the stable identities and H. Payment validates its own saved quote and exact binding. First provisioning may proceed after quote expiry strictly before H. Existing orders replay after H. Changed bindings conflict. The termsVersion field carries the Payment quote version. Covered orders use the same PUT and return a linked invoice ID.

## 3. Owner implementation and acceptance tests

Paths below are relative to `src/Services/PaymentService/SmartParking.PaymentService.` followed by the project suffix and file.

| Files | Required owner work and customer effect |
| --- | --- |
| `API/Authentication/OperationsServiceAuthentication.cs`, `API/Controllers/PricingController.cs`, `API/Controllers/InternalReservationOrdersController.cs` | Add machine-only issuance and scoped proof. Retain the existing pricing and order contracts during rollout. A plate or guessed quote ID grants no customer access. |
| `Application/DTOs/Reservations/ReservationPricingDtos.cs`, `Application/Usecase/Reservations/Commands/CreateReservationQuote/CreateReservationQuoteCommand{,Validator,Handler}.cs` | Validate complete trusted selection, resolve the CAR tariff and issue immutable proof. Today the handler requires VehicleId, sets terms version from policy version and echoes request method. Move the new contract to canonical plate and persist the actual method, tariff revision and disclosure. Keep previews independent. |
| `Domain/Entities/ReservationQuote.cs`, `Persistence/Configurations/ReservationQuoteConfiguration.cs`, `Application/Common/Interfaces/Persistence/IReservationQuoteRepository.cs`, `Persistence/Repositories/ReservationQuoteRepository.cs` and the original schema `scripts/database/microservices/04-payment-pricing-service-db.sql` | Store immutable commercial and binding evidence, including coverage source/version and outcome. Add scoped read and concurrency-safe consumption. The current repository’s internal ID-only read is insufficient for service proof. Preserve historic rows and their status. |
| `Application/Usecase/Reservations/Commands/EnsureReservationDepositOrder/EnsureReservationDepositOrderCommand{,Validator,Handler}.cs`, `Domain/Entities/PaymentOrder.cs`, `Persistence/Configurations/PaymentOrderConfiguration.cs` | Validate binding/version, exact amount/currency/method, original operation, reservation, scope and H. Persist authoritative evidence. The current replay echoes caller currency/method and compares amounts with a tolerance. Conflict must never reprice the booking. |
| `Application/Usecase/Reservations/Queries/GetReservationCollections/GetReservationCollectionsQueryHandler.cs` | Coordinate verified-money evidence with the separate confirmation owner. Existing placeholder quote identity cannot close a legacy financial obligation. |

Under `tests/Services/SmartParking.PaymentService.Tests/`, extend `Domain/ReservationQuoteTests.cs`, `Application/ReservationPricingHandlerTests.cs`, `Application/EnsureReservationDepositOrderCommandHandlerTests.cs`, `Authentication/OperationsServiceAuthenticationTests.cs` and `Application/ReservationCollectionsAndReconciliationHandlerTests.cs`. Cover wrong service, foreign Account, immutable read after tariff change, missing configuration, exact expiry boundary, altered plate/slot/interval/method, coverage version, consumed quote, exact replay, competing operations and old paid orders. Verify the actual service-to-service exchange with Operations before enabling new checkout.

## 4. Legacy data and release sequence

1. Preserve issued VehicleId-era quotes and their original pricing. Let unused reviews expire. A new plate review requires fresh issuance. Do not infer historical plate ownership from current registration.
2. Drain or replay persisted legacy admission intents under the old serializer, terms ID/version and fingerprint. Continue servicing accepted unpaid bookings and existing orders under their original terms, hold deadlines and idempotency keys.
3. Keep Account-scoped customer history, notification consumers and historical reservations readable. Resolve late/unsettled collections against real persisted quote/order/financial identities. No synthetic quote references or repricing.
4. Prove the new issuance, lookup, guarded acceptance and EnsureOrder contract. Release the paired frontend review/create flow. Stop new legacy issuance only after existing clients have transitioned. Inventory pending requests, FK-linked reservations, orders and unresolved collections before retiring readers, demo seed dependencies and the physical table in an approved deployment/data change.

See the [Operations API](../../03-design/api/reservation-first-slice-api-design.md), [owner retirement inventory](2026-11-18-SPARK-273-reservation-owner-handoffs.md) and [separate paid confirmation handoff](2026-11-18-SPARK-273-payment-confirmation-handoff.md).
