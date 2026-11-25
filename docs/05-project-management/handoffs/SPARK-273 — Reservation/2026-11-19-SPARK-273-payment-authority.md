# SPARK-273 · Payment authority and reservation settlement

Working tree on `feature/SPARK-273-driver-book-parking-ahead-of-arrival`, based on `41dd3e7522c6658d48d3617fcb0578bf39bb09c9`. Its parent is `f06ff1a96`. The committed Operations and Facility implementation is the integration baseline. The index was empty. Pre-existing router, user-instructions, SPARK-243 session and seed-script edits were excluded.

## Owner agreement and wire contracts

Operations authenticates its internal HTTP calls using a short-lived RS256 JWT with its service identity as issuer, subject and sender, the Payment audience, and the permission for the route. The sender attests that the local hold was committed. The final Operations database checkpoint requires an issued quote and a time strictly before validity. After that checkpoint the accepted commercial terms stand. The authenticated order call binds the immutable Payment record to the Operations identities and H. Payment consults its own records. Operations owns capacity, entitlement and the final confirmation or release.

| HTTP operation | Permission | Request and successful evidence |
|---|---|---|
| POST `/internal/reservation-quotes/v1` | `reservation:quote` | Flat Account, canonical plate, tenant/site/slot, UTC interval, PAYOS, coverage and version, and both policy IDs/versions. Operations now sends the approved B2 shape. The existing Payment validator still requires FacilityVersion until the [Payment teammate cutover](SPARK-273-B2-Payment-Handoff.md). Payment returns the binding, ID, opaque version, ISSUED, validity, effective tariff revision ID, pricing segments and rounding breakdown, booked/covered/deposit amounts, VND, FULL prepayment and disclosure. |
| GET `/internal/reservation-quotes/v1/{quoteId}?accountId=…&version=…` | `reservation:quote` | Returns the stored issuance with its current consumption state. The original validity remains visible. An unspent quote can still have status ISSUED after its deadline. New admission must compare the deadline. Missing, wrong-version and foreign proofs give the same 404 `QUOTE_NOT_FOUND`. A missing route, failed signature, outage or malformed envelope provides no proof. |
| PUT `/internal/reservation-deposit-orders/{operationId}` | `reservation:deposit-order` | Operations sends operationId, reservationId, accountId, tenantId, siteId, quoteId, termsVersion, amount, currency, paymentMethod and expiresAt (H). The accepted quote supplies the rest of the paid binding. Zero orders also require zeroSelection with slotId, plate, start, end, isCovered and coverageVersion. The reply contains state, orderId and the persisted acceptedRequest. A zero acceptance additionally includes receiptId, the linked invoice ID. |
| GET `/internal/reservation-deposit-orders/{orderId}/collections` | `reservation:deposit-order` | The existing collection list returns each verified payment transaction. A reviewed item includes collectionId, orderId, reservationId, accountId, tenantId, siteId, quoteId, termsVersion, amount and currency, plus evidence with version 1, eventId, operationId, paymentTransactionId, paymentMethod, provider, providerReference, providerVerifiedAt and occurredAt. |
| POST `/internal/reservation-reconciliations` | `reservation:deposit-order` | reservationId, collectionId, exact collected amount and reason. A reviewed transfer gets one refund-request disposition keyed by its transaction ID. Duplicate deliveries recover its original reference and current status. REFUND_REQUESTED acknowledges the disposition. Payout requires its own evidence. |
| PUT `/internal/reservation-deposit-orders/{operationId}/zero-disposition` | `reservation:deposit-order` | operationId, reservationId, accountId, orderId, receiptId, quoteId, quoteVersion and reason. Payment records the first reason against the same zero order and returns the validated receiptId. |

Successful responses use the existing `{ "success": true, "data": … }` envelope. Invalid input, missing proof and binding conflict are distinct from an unavailable commercial owner. An absent or unusable tariff revision cannot issue a price. Operations supplies a versioned plate/site/interval coverage assertion. The real entitlement implementation still returns uncovered.

The paid and zero order paths use the same operation key and transaction. Sorted PostgreSQL advisory locks serialize the operation, reservation and quote. Unique indexes cover operation identity and one reviewed order per quote and reservation. The first evaluation at or after H records a closed fence. An exact retry recovers an earlier order or the fence. Changed bindings conflict. H is a PostgreSQL UTC instant at microsecond precision. Zero acceptance commits a settled zero invoice with its order. Checkout requires a positive amount and the agreed method.

## Settlement and compatibility

The existing `PaymentSucceededV1` type, message URN, exchange `smartpark.payment.payment-succeeded.v1` and queues keep their existing wire contract. The earlier dedicated reservation event proposal is retired. Parking-session checkout uses ReferenceType PARKING_SESSION and its session/order binding. Legacy reservations retain their terms and financial replay. NotificationAudit uses EventId for audit and notification deduplication.

For reviewed reservations, a signed payOS transfer supplies the bank reference, order code, payment link, success code, VND amount and transaction time. Payment binds it to the recorded checkout and deduplicates by payment method and bank reference. If a callback arrives while the checkout attempt is still awaiting its link ID, Payment responds with HTTP 503 and accepts a retry after the link is saved. A known-link mismatch, ambiguous checkout binding or conflicting transfer evidence fails closed. A link summary from provider polling cannot attest an individual reviewed transfer. A permanently missing link, undelivered callback or transfer omitted by the provider requires provider-side reconciliation. Automatic itemized recovery for those cases remains unverified for the configured merchant channel.

For a positive reviewed order, one correctly bound collection for the full accepted amount commits the successful transaction, the `PENDING` to `PAID` transition and the `PaymentSucceededV1` outbox record together. Payment can settle after H. Checkout creation and Operations entitlement evaluation still use hold timing. Payment reuses the bound checkout row when it is still pending or unknown. The settlement event ID is the collection transaction ID. Verified lookup uses this same ID in its existing `eventId` field for correlation. A non-settling collection's lookup evidence can exist without a corresponding broker event. The lookup status `SETTLED` describes the presence of successful collections. The order status is the commercial settlement authority.

Additional collections after an order is paid and transfers against a cancelled order are saved with their own transaction and one collection-keyed `REFUND_REQUESTED` disposition in the same commit. This acknowledges manual financial follow-up and leaves payout execution deferred. The reservation consumer uses the one settlement message to trigger authenticated lookup and the existing guarded deposit command. Operations continues to reconcile denied entitlement. Covered acceptance after release records the receipt disposition. Paid reacquisition remains subject to current eligibility and capacity strictly before T.

The settlement-only guarantee here covers reviewed reservation orders. Legacy PayOS callbacks can still suppress a distinct same-link receipt or publish a success event for other late or extra money. Legacy cash replay deliberately re-stages a success event with a new event ID for parking-session recovery. Correcting those paths requires a separate decision covering legacy late-reservation wakeup, ledger reconciliation and cash redrive.

### Exact-amount checkout and deferred underpayment

The reviewed payOS checkout and QR use the fixed VND amount from the accepted quote. Exact-amount settlement is the supported workflow. A fixed-amount QR does not establish a universal bank-transfer guarantee. A signed, bound amount mismatch is preserved as a separate verified collection and does not satisfy a pending order or emit `PaymentSucceededV1`. Payment leaves the pending checkout available for an independently verified exact payment. An unmatched callback cannot be safely assigned to the order and requires provider-side investigation. Underpayment handling is deferred. A provider that reports partial or underpaid collections needs a separate design for aggregation, disposition, refunds, recovery, and settlement event semantics. Partial payment alone must never emit `PaymentSucceededV1`.

## Schema and rollout

The original Payment `04-payment-pricing-service-db.sql` initializes the final schema. Reviewed evidence extends reservation_quotes. Reviewed method/currency, H fences and zero disposition extend payment_orders. The bank reference and verification time remain on payment_transactions. The reference is the EF concurrency token protecting first acceptance from a stale checkout update. The existing nullable unique transaction FK on refund_requests identifies reviewed dispositions. Zero receipts use invoices.

This settlement change targets newly initialized development databases and messaging state. The project lead handles recreation. There is no historical event ID migration or compatibility layer. `CREATE TABLE IF NOT EXISTS` leaves existing table definitions intact. An existing schema can still require the removed event ID in its old constraints and must not be used with the changed writer. A populated environment needs its own approved transition. Direct SQL, bulk updates and direct DbContext writes can bypass handler locks, application immutability and atomic publication. Restrict writers to the owning service.

Reviewed quote issuance and the Operations adapter require `ReservationIntegration:ReviewedSettlementEnabled` plus the existing authenticated integration configuration. The production admission starter remains gated. Deployment owners must verify actual TLS, key rotation, ingress, broker credentials and topology, schema transition, drain/redrive, host restart and rollback. Product and entitlement owners must approve subscription eligibility before enabling real coverage.

## Manual Postman and browser regression

1. In an approved test environment, prepare and pay a parking-session order, refresh checkout and status, then replay the provider callback. Verify one financial settlement, gate departure, invoice and notification. Open the historical invoice PDF under the purchasing Account.
2. Review a registered car and slot. Compare driver disclosure and VND amounts to Payment proof using Operations credentials. Change the tariff and retrieve the same proof. Repeat using another Account and a nonexistent quote. Compare status and error body. Try a driver token and a service token without the route permission.
3. Accept before quote expiry, delay hold delivery until after expiry and before H, and confirm the same order and QR on retry. Pay using an owner-approved payOS test merchant. Verify the merchant statement, bank reference and signed callback, the Operations confirmation timestamp, and the driver's scoped reservation history.
4. Suppress the first response or stop the consumer after Payment commits. Replay the same operation before and after H. Change amount, currency, method, identity or H and verify conflict. Deliver the same callback twice, then another real transfer on the same link. Check the two transfer IDs and a single disposition for additional funds.
5. With the entitlement owner's controlled assertion, accept a fully covered review. Check both the invoice ID and the absence of payment attempts. Exercise rejection, late recovery after expiry, cancellation and an occupied slot during paid reacquisition. Compare the reservation, receipt and reconciliation status on refresh.

## Regression risk and verification

| Component | Producers, consumers and risk | Test evidence |
|---|---|---|
| Shared PaymentSucceededV1 · HIGH | payOS webhook, status reconciliation, cash collection and the existing publisher feed the two Operations reference-type routes and NotificationAudit. EventId deduplicates notifications. The parking handler matches the original session/order and records checkout settlement. | Payment outbox, cash, webhook and status suites, live broker publication, Operations checkout realtime and reservation suites, Facility gate suite, NotificationAudit consumers and deduplication. The paired test delivered the original message type across the actual restricted broker. |
| Order provisioning and checkout · HIGH | The legacy and reviewed reservation PUT share a MediatR handler. Legacy order/history fixtures retain their previous shape. The reviewed path locks the identities and recovers its saved response. Driver checkout uses the original provider adapter. | Contending consumers, conflicting method/coverage, exact replay after H, closed fence against a changed H, lost zero-order acknowledgement, historical VehicleId/terms booking and generic settlement. Separate regression coverage for checkout initialization, parking orders and replay. |
| Provider callbacks and financial recovery · HIGH | Reviewed orders require a signed itemized VND transfer. The public status endpoint cannot manufacture one from a paid-link total. Link sharing across two collections, duplicate callbacks and terminal states are financially significant. | Paired signature verification, checkout/QR via a provider HTTP double, commit failure after SQL saves, rollback readback, subsequent real outbox relay, consumer retry after injected lookup failure, same-link additional transfer, stable lookup, one disposition. |
| Quote persistence and scoped proof · MEDIUM | The original quote repository serves both the legacy quote handler and new reviewed issuance. JSONB retains the original breakdown, revision, selection, disclosure and validity. | PostgreSQL issuance and proof after tariff withdrawal and expiry, missing/foreign proof equivalence, missing effective pricing, competing order consumption, signed service calls and original vehicle-quote regression tests. |
| Invoices, zero acceptance and disposition · MEDIUM | Payment invoice reads, history, PDF and cash settlement share the invoice aggregate. The reviewed zero order links one real invoice. | Invoice repository, download and history regressions. Paired invoice/order ID replay, no provider transaction, late zero receipt reconciliation after Operations releases the claim, and guarded zero confirmation. |

### Reproducing the checks

Use fresh disposable PostgreSQL 16 and RabbitMQ 3.13. The final run used host loopback ports 55437 and 55673. Initialize the Payment database with `scripts/database/microservices/04-payment-pricing-service-db.sql`. Initialize **two separate** Operations databases with `scripts/database/microservices/03b-reservation-operations-service-db.sql`. Use separate non-owner Payment and Operations login roles, schema USAGE, DML on their own tables and sequence USAGE/SELECT. Remove PUBLIC CONNECT on the fixture databases and grant CONNECT only to the respective role. The administrator is used for fixture provisioning.

Provide the three disposable Npgsql connections through `SMARTPARK_SETTLEMENT_DB`, `SMARTPARK_QUOTE_TEST_DB` and `SMARTPARK_SAGA_TEST_DB`. The last two contain Operations data. Include a nonblank fixture-only password in the Payment connection string. The production Payment registration validates it even if the isolated PostgreSQL listener uses local trust authentication. The paired test generates its own RSA key, uses loopback Kestrel, and hosts the payOS HTTP double in that process. It injects a test driver identity for customer checkout and controlled Facility, policy and entitlement facts. Its service calls use the actual Operations signer and Payment authentication policy.

| Fixture | Environment |
|---|---|
| Paired financial workflow | `SMARTPARK_SETTLEMENT_PORT`, `SMARTPARK_SETTLEMENT_VHOST`, `SMARTPARK_SETTLEMENT_PAYMENT_USER`, `SMARTPARK_SETTLEMENT_OPERATIONS_USER` and `SMARTPARK_SETTLEMENT_PASSWORD`. Supply separate users on this single-purpose vhost with the same disposable test password. |
| Existing Operations Saga | `SMARTPARK_SAGA_TEST_BROKER_PORT`, `SMARTPARK_SAGA_TEST_VHOST`, `SMARTPARK_SAGA_TEST_USER`, `SMARTPARK_SAGA_TEST_PASSWORD`. Use a different vhost from the settlement pair. |
| Existing Payment live-broker checks | `SMARTPARK_RABBIT_TEST_PORT`, `SMARTPARK_RABBITMQ_VHOST`, `SMARTPARK_RABBITMQ_USER`, `SMARTPARK_RABBITMQ_PASSWORD`. The tests create uniquely named queues on the fixture vhost. |

The paired vhost used these actual `rabbitmqctl set_permissions` patterns (configure, write, read). Temporary MassTransit bus resources are included. Operations may bind/read the Payment exchange and publish its own hold trigger. Payment cannot write the Operations queues. Production owners must validate deployed ACLs independently.

```text
Payment
^(smartpark\.payment\.payment-succeeded\.v1|.*_bus_.*)$
^(smartpark\.payment\.payment-succeeded\.v1|.*_bus_.*)$
^.*_bus_.*$

Operations
^(smartpark-operation-reservation-(payment-succeeded|hold-changed)-v1.*|smartpark\.operations\.reservation-hold-changed\.v1|smartpark\.payment\.payment-succeeded\.v1|.*_bus_.*)$
^(smartpark-operation-reservation-(payment-succeeded|hold-changed)-v1.*|smartpark\.operations\.reservation-hold-changed\.v1|.*_bus_.*)$
^(smartpark-operation-reservation-(payment-succeeded|hold-changed)-v1.*|smartpark\.operations\.reservation-hold-changed\.v1|smartpark\.payment\.payment-succeeded\.v1|.*_bus_.*)$
```

Run from the repository root. Offline restore used the local package cache. Logs from the normal-verbosity runs contain individual test names.

```powershell
dotnet restore SmartParking.slnx --source "$env:USERPROFILE\.nuget\packages" -p:NuGetAudit=false -m:1 -v:q
dotnet build SmartParking.slnx --no-restore -m:1 --nologo -v:q
dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --no-restore -m:1 --nologo -v:q --logger 'console;verbosity=normal'
dotnet test tests/Services/SmartParking.NotificationAuditService.Tests --no-build --no-restore -m:1 --nologo -v:q --logger 'console;verbosity=normal'
dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --no-restore -m:1 --nologo -v:q --logger 'console;verbosity=normal'
dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --no-restore -m:1 --nologo -v:q --filter "FullyQualifiedName~SignedQuoteOrderAndVerifiedTransfersReachGuardedConfirmation"
dotnet test tests/Services/SmartParking.FacilityService.Tests --no-build --no-restore -m:1 --nologo -v:q --filter FullyQualifiedName~ReservationGateUseCaseTests
```

**Verification boundaries.** The paired workflow replays the stored Operations hold outbox payload over RabbitMQ to control its delivery relative to quote expiry. It also reads the real financial event from the durable Operations queue without acknowledging it, closes the channel, and verifies RabbitMQ redelivers the same message ID and payload with its redelivery flag set. The test then requeues the message and starts the real Operations financial consumer to recover it. Payment’s EF outbox relay and Operations financial consumer run on RabbitMQ. Existing Operations tests independently exercise the Saga transport. The payOS SDK-facing HTTP protocol and signature verifier use local test credentials and a provider double. No test in this delivery contacts a real payOS merchant or moves money. Real provider reference uniqueness, merchant callbacks, settlement statements, physical gate hardware, browser checkout, deployment ACLs/TLS, hard process termination at commit and populated-database upgrade/rollback remain unverified.

**Run history.** The first solution build needed a local NuGet restore. Sandboxed VSTest runs aborted before test discovery could complete the test-host connection. Tests ran on the host against isolated fixtures. An intermediate Operations run passed 85/86. The paired test observed the committed collection before the separate confirmation-token transaction was visible. Waiting for the actual token checkpoint fixed that test race. The solution build then succeeded with zero errors and one existing NU1510 warning. Both database CONNECT probes for the opposite service returned false. After adding the broker-level redelivery assertion, the focused paired test passed 1/1 in 1m27s.

### Columns for the database-owner review

| Table | New columns and guarantees |
|---|---|
| reservation_quotes | Nullable reviewed_evidence JSONB. The EF mapping rejects changes to the stored issuance after insert. The existing status field records consumption or closure. |
| payment_orders | Nullable reviewed_method, reviewed_currency and zero_disposition_reason, plus is_provisioning_fence default false. A provisioning fence has no business status. Partial unique indexes cover reviewed quote_id and reference_id. Existing idempotency_key uniqueness also covers the reviewed operation. |
| payment_transactions | Nullable provider_collection_reference and provider_verified_at. A unique method/reference pair, positive successful payment and paid_at identify the verified transfer. The transaction primary key provides event identity on reviewed settlement. The gateway transaction index still protects the checkout link. |
| refund_requests | Nullable unique collection_id referencing payment_transactions. This allows two distinct transfers on one order to receive separate dispositions with the same reason. |
| invoices and MassTransit tables | Existing declarations and relationships serve zero receipts and durable publication. |

Outstanding reviews or historical rows must keep their original values. The legacy lookup may still have no provable quote reference for old unbound records. Operations rejects incomplete evidence. Do not infer a quote or a bank reference during backfill. Keep settlement recovery available for admitted operations when pausing new ingress. A production decision is still needed for the inventory, transition procedure, compatibility window and cutover date.

### Verification totals

| Target | Passed | Failed | Skipped |
|---|---:|---:|---:|
| PaymentService, whole test project | 258 | 0 | 0 |
| OperationService, whole test project including paired settlement | 86 | 0 | 0 |
| NotificationAuditService, whole test project | 202 | 0 | 0 |
| FacilityService, ReservationGateUseCaseTests | 8 | 0 | 0 |

The solution build succeeded. The full cross-service test selection totals 554. The paired test was then rerun separately after adding the broker-level redelivery assertion and passed 1/1, covering the amended test on the final source. No existing application database was used. The only live external dependencies in the paired test were the disposable PostgreSQL and RabbitMQ instances and the local HTTP test hosts.

## File inventory

Paths are relative to each directory shown below. Grouped historical C# files still contain their unrelated legacy types. The DTO and command extractions preserve their namespaces and wire names. All changes listed here are unstaged.

- `ai/tasks/active/` — `TASK-SPARK-273-SESSION-20261108.md`.
- Reservation documentation is indexed in the [handoff README](README.md).
- `scripts/database/microservices/` — `04-payment-pricing-service-db.sql`.
- `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/` — `AcceptZeroPriceTermsHandler.cs`, `BookingWorkflowContracts.cs`, `CollectionLookup.cs`, `DepositOrderRequest.cs`, `DepositOrderResult.cs`, `IReservationQuoteOwner.cs`, `ProvisionReservationOrderHandler.cs`, `RecordReservationPaymentCommand.cs`, `RecordReservationPaymentHandler.cs`, `ReservationPaymentEvidence.cs`, `VerifiedCollection.cs`, `ZeroQuoteSelection.cs`.
- `src/Services/OperationService/SmartParking.OperationService.Infrastructure/` — `InfrastructureDependencyInjection.cs`.
- `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/` — `CollectionItem.cs`, `CollectionResponse.cs`, `DispositionAcknowledgement.cs`, `OrderResponse.cs`, `PaymentEnvelope.cs`, `PaymentErrorEnvelope.cs`, `PreviewResponse.cs`, `QuoteResponse.cs`, `QuoteSelection.cs`, `ReservationCommercialClient.cs`, `UnavailableReservationQuoteOwner.cs`, `VerifiedTransferEvidence.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.API/Authentication/` — `OperationsServiceAuthentication.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.API/Controllers/` — `InternalReservationOrdersController.cs`, `ReviewedReservationQuotesController.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.API/Middleware/` — `ExceptionHandlingMiddleware.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/` — `ApplicationDependencyInjection.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Common/Interfaces/Persistence/` — `IPaymentOrderRepository.cs`, `IReservationQuoteRepository.cs`, `IUnitOfWork.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Common/Interfaces/Ports/` — `PaymentCallbackResult.cs`, `PaymentGatewayModels.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/DTOs/Reservations/` — `CollectionLookupResponseDto.cs`, `EnsureReservationDepositOrderRequestDto.cs`, `EnsureReservationDepositOrderResponseDto.cs`, `ReconcileReservationRequestDto.cs`, `ReconcileReservationResponseDto.cs`, `ReservationPricingDtos.cs`, `VerifiedCollectionDto.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Reservations/` — `IReviewedTariffRepository.cs`, `IssueReviewedQuote.cs`, `IssueReviewedQuoteHandler.cs`, `IssueReviewedQuoteValidator.cs`, `QuoteCoverage.cs`, `ReadReviewedQuote.cs`, `ReadReviewedQuoteHandler.cs`, `RecordZeroDisposition.cs`, `RecordZeroDispositionHandler.cs`, `ReviewedQuoteBinding.cs`, `ReviewedQuoteProof.cs`, `VerifiedTransferEvidence.cs`, `ZeroQuoteSelection.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/CollectCashPayment/` — `CollectCashPaymentCommandHandler.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/ConfirmCashPayment/` — `ConfirmCashPaymentCommand.cs`, `ConfirmCashPaymentCommandHandler.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/GetPaymentStatus/` — `GetPaymentStatusQueryHandler.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/InitiateCheckout/` — `InitiatePaymentCheckoutCommandHandler.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/ProcessWebhook/` — `ProcessPayOsWebhookCommandHandler.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/Reservations/Commands/EnsureReservationDepositOrder/` — `EnsureReservationDepositOrderCommandHandler.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/Reservations/Commands/ReconcileReservation/` — `ReconcileReservationCollectionCommandHandler.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/Reservations/Queries/GetReservationCollections/` — `GetReservationCollectionsQueryHandler.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Domain/Entities/` — `Invoice.cs`, `InvoiceLineItem.cs`, `InvoicePricingSnapshot.cs`, `PaymentOrder.cs`, `PaymentTransaction.cs`, `RefundRequest.cs`, `ReservationQuote.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Domain/Pricing/` — `PricingCalculationRequest.cs`, `PricingCalculationResult.cs`, `PricingModels.cs`, `PricingPolicySnapshot.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Adapters/Outbound/` — `PayOsAdapter.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Persistence/Configurations/` — `PaymentOrderConfiguration.cs`, `PaymentTransactionConfiguration.cs`, `RefundRequestConfiguration.cs`, `ReservationQuoteConfiguration.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Persistence/` — `PersistenceDependencyInjection.cs`.
- `src/Services/PaymentService/SmartParking.PaymentService.Persistence/Repositories/` — `PaymentOrderRepository.cs`, `ReservationQuoteRepository.cs`, `ReviewedTariffRepository.cs`, `UnitOfWork.cs`.
- `tests/Services/SmartParking.OperationService.Tests/` — `PairedSettlementFactAttribute.cs`, `PaymentAcknowledgementFault.cs`, `QuoteBookingDatabaseTests.Paired.cs`, `QuoteBookingDatabaseTests.cs`, `SmartParking.OperationService.Tests.csproj`.
- `tests/Services/SmartParking.PaymentService.Tests/TestDoubles/` — `FakePaymentOrderRepository.cs`, `FakeReservationQuoteRepository.cs`, `FakeUnitOfWork.cs`, `Fakes.cs`.

## Payment recovery and legacy follow-ups

| ID / disposition | Source and finding | Functionality to keep and safe boundary | Decision and verification |
| --- | --- | --- | --- |
| R8 · Evidence and design pending | The reviewed PayOS webhook returns retryable HTTP 503 when checkout has yet to save its link. A permanently missing link or a callback that never arrives leaves a recovery gap. The current PayOS status adapter reads a payment-request summary, which cannot identify an individual transfer. [Reviewed webhook handler](../../../src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/ProcessWebhook/ProcessPayOsWebhookCommandHandler.cs#L214) · [PayOS lookup adapter](../../../src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Adapters/Outbound/PayOsAdapter.cs#L313) | Preserve the existing callback retry, immutable transfer evidence, and settlement-only publication. Keep reservation eligibility with Operations. Recover a transfer only from authoritative itemized evidence that proves its order and checkout binding. | Payment and the merchant-channel owner verify the lookup's coverage of missed, additional, and late transfers, the uniqueness scope of transfer references, and which fields establish the binding. Agree how recovery is initiated and where ambiguous items go. Test a lost checkout result, a missed callback, concurrent callback and lookup replay, and conflicting evidence. Require one ledger row per verified transfer and one settlement event per satisfied reviewed order. |
| R9 · Legacy regression and cleanup candidate | Legacy PayOS can acknowledge a distinct same-link transfer as a replay, or emit `PaymentSucceededV1` for late or extra money without a paid order transition. Legacy cash replay can publish a new event ID for an existing settlement to recover parking checkout. Consumers that deduplicate by event ID can process that replay again. [Legacy PayOS handler](../../../src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/ProcessWebhook/ProcessPayOsWebhookCommandHandler.cs#L103) · [Cash replay handler](../../../src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/CollectCashPayment/CollectCashPaymentCommandHandler.cs#L95) | Preserve ordinary legacy parking checkout, guarded late-reservation recovery, and cash redrive. Record genuine additional transfers independently and preserve their reconciliation path. Keep one logical settlement fact per settled order and the existing reviewed reservation contract. | Payment, Operations, and NotificationAudit owners agree how legacy reservations recover after late payment and how cash redrive reaches Operations without duplicating downstream effects. Test same-transfer replay, distinct same-link money, late and cancelled orders, cash replay after lost delivery, legacy reservation eligibility, and notification/audit deduplication. |

### Earlier Payment handoffs

- [18 November Payment quote contract](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-18-SPARK-273-payment-quote-handoff.md)
- [18 November Payment confirmation contract](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-18-SPARK-273-payment-confirmation-handoff.md)
- [Superseded Payment integration WIP snapshot](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/SPARK-273%20%E2%80%94%20Reservation/PaymentServiceIntegrationHandoff)
