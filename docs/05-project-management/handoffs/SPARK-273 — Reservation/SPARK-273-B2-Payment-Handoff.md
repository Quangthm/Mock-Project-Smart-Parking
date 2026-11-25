# SPARK-273 B2 | Payment reviewed quote binding

**Owner:** PaymentService. Operations now omits `facilityVersion` from reviewed quote issuance. The current Payment validator still requires it. Coordinate activation after the signed POST, proof GET and order tests pass with fresh quotes.

## Contract change

- In `SmartParking.PaymentService.Application/Reservations/ReviewedQuoteBinding.cs`, remove `FacilityVersion`. In `IssueReviewedQuoteValidator.cs`, remove its required-field rule. Update the positional fixture in `tests/Services/SmartParking.PaymentService.Tests/Persistence/ReviewedOrderDatabaseTests.cs`.
- The authenticated POST `/internal/reservation-quotes/v1` accepts account ID, canonical plate, tenant/site/requested slot IDs, both UTC instants, payment method, coverage decision/version and both platform/site policy IDs and versions. Return the exact accepted binding. Keep existing tariff revision, pricing breakdown, booked/covered/deposit amounts, currency, prepayment mode, disclosure, status and expiry.
- `IssueReviewedQuoteHandler.cs` prices using tenant/site, CAR, interval and the authoritative tariff. It serializes the typed proof into `reviewed_evidence`. `ReviewedQuoteProof.From` reads it back with current consumption status. The field removal needs no new serializer, digest, relational column, migration or rewrite of previous financial evidence.
- Keep `QuoteId` and `QuoteVersion`. The current `rq1-` prefix dispatches reviewed orders in `EnsureReservationDepositOrderCommandHandler.cs`. Its future removal requires a separate decision covering legacy creation, order routing and financial replay.

## Acceptance

1. Issue a fresh reviewed quote with a real signed Operations service request. Verify both the response and newly persisted JSON omit the removed member. Recover the exact same binding, immutable tariff, amounts, breakdown and validity using GET `/internal/reservation-quotes/v1/{quoteId}?accountId=…&version=…`. Exercise authorization, wrong account, quote and version. New admission requires an issued, unexpired quote.
2. Exercise one-time consumption, idempotent retry and conflicting replay under the same operation key. Preserve the exact Operations hold expiry H, its closed fence, and the existing distinction between quote validity at admission and provisioning against the committed H.
3. Verify the ordinary paid order and checkout readiness, and the zero-value order with exact selection and durable receipt. Preserve genuine collection, invoice and reconciliation evidence. Use isolated fixtures and a provider double.
4. Run `ReviewedOrderKeepsTheQuoteBindingAndCheckoutEnforcesRuntimeCapability`, `ExpiredProvisioningHasNoBusinessStatusAndCannotBecomeAnOrder`, and `ZeroValueReceiptDispositionIsProviderIndependentAndIdempotent`. Run Operations’ paired `SignedQuoteOrderAndVerifiedTransfersReachGuardedConfirmation` with the actual revised Payment owner. Report any further production change with the failing source path or test that demonstrates the need.
