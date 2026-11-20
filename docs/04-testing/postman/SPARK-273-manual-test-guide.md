# SPARK-273 Driver reservation to Payment order

Use this collection manually against an approved test stack. The paid path ends when Operations shows one active `PENDING_PAYMENT` reservation and the genuine Payment order saved from its signed owner response. Start with **00.2 Driver login**. The collection has no checkout, QR, callback or payment step. The covered folder is disabled until real entitlement is available.

- [Collection v2.1](./SPARK-273-Reservation-Flow.postman_collection.json)
- [Blank local environment](./SPARK-273-Local.postman_environment.example.json)
- [Activation and integration evidence](../reports/SPARK-273-reviewed-admission-integration.md)

## Prepare the stack

1. Use [local setup](../../../LOCAL-SETUP.md) for UserService, FacilityService, OperationService, PaymentService, PostgreSQL and RabbitMQ. The standard local hosts are User `http://localhost:5035`, Facility `http://localhost:5045`, Operations `http://localhost:5055` and Payment `http://localhost:5065`. Each exposes `GET /health`. Health alone does not establish owner trust or queue readiness.
2. Provision the owning schemas for fresh databases. User uses `01-user-access-service-db.sql`, Facility uses `02-facility-service-db.sql` and `03a-parking-structure-ops-service-db.sql`, Operations uses `03b-reservation-operations-service-db.sql` and Payment uses `04-payment-pricing-service-db.sql`. Follow the existing upgrade procedure and obtain approval for the additive existing-data transition before applying these builds to an existing database.
3. For this approved test stack, start the hosts with the following opt-ins. They default to false in the example. Configure the separate User, Facility and Operations reservation broker credentials, Payment broker identity, expected vhost, queue and exchange permissions, and durable outbox delivery before enabling them. See the [integration report](../reports/SPARK-273-reviewed-admission-integration.md#activation) for the service matrix and Account access exchanges.
   - `SMARTPARK_RESERVATION_ADMISSION_ENABLED=true`
   - `SMARTPARK_RESERVATION_SAGA_ENABLED=true`
   - `SMARTPARK_RESERVATION_FACILITY_ENABLED=true`
   - `SMARTPARK_RESERVATION_REVIEWED_SETTLEMENT_ENABLED=true`
   - `Messaging__OutboxDeliveryEnabled=true`
4. Verify Operations trusts User’s JWT issuer, audience and JWKS. Configure the reviewed owner endpoints and dedicated Operations signing key for Facility and Payment. Use approved TLS and key custody. The local HTTP flags are explicit opt-ins for an isolated dev network. Never place a service key in Postman.
5. Use a test Driver with an active Account and a registered CAR. Select an actual reservable CAR slot with authoritative Facility data and an Operations capacity policy. Payment needs an effective CAR tariff revision for the same tenant and site. Pick future ISO 8601 `start` and `end`, including UTC or an explicit offset, within the site’s effective reservation window. Supply only the slot ID. Operations resolves the site.

## Run the paid scenario

Import both artifacts and select **SPARK-273 Reservation Flow - local**. Set `userBaseUrl` and `operationBaseUrl` if your local ports differ. Enter `driverEmail` and `driverPassword` in local secret values. Only supply `driverTurnstileToken` when the configured test login requires it, and use a genuine token. Do not export populated credentials or tokens.

Send each request individually. For 02.1, check the returned plate, slot, site, interval, tariff, disclosure, VND amount, coverage and deadline. Accept the reviewed selection by setting `acceptedQuoteId` to the exact returned `quoteId`. The next request sends its saved version and selection. Changing inputs requires another review.

| Request | Expected HTTP and observation |
|---|---|
| 00.2 Driver login | 200, `success=true`, `mfaRequired=false`, valid Driver claims. Stores `driverToken`, Account and User IDs. Later requests inherit bearer auth. |
| 00.3 Verify Driver identity | 200, role `driver`, same User as the login claims. |
| 01.1 Resolve registered CAR | 200, select the only eligible CAR or enter a specific returned `canonicalPlate` in `licensePlate` and resend. Configure `slotId`, `start`, `end` and `paymentMethod=PAYOS`. |
| 02.1 Review Payment quote | 200, exact selection, opaque quote version, positive whole-VND deposit, disclosure and future validity. Stores the quote and authoritative site. Explicitly accept it before proceeding. |
| 03.1 Accept quote and submit | 202, receipt contract v3 and stable `requestId`. A UUID idempotency key and the exact body are saved before the first send. |
| 03.2 Replay exact submission | 202, same request identity with the original key and body. |
| 04.1 Read Saga progress | 200, four `SUCCEEDED` checks and `HOLD_CREATED`. `CHECKING` or `EVALUATING` means the scenario is still in progress. |
| 04.2 Read durable receipt | 200, v3 and same request and hold as the progress snapshot. Saves `reservationId`. |
| 04.3 Read own reservation and order readiness | 200 with the same accepted commercial terms, `PENDING_PAYMENT`, null confirmation and H in the future. Requires `orderProvisioningState=CREATED` and a real order ID to pass the order-readiness assertion. |
| 05.1 Verify own Payment order association | 200, reverse association returns the original request for the same authenticated Account. Finish here. |

If login returns `mfaRequired=true`, the collection clears stale authentication and stops. Use the supported UserService sign-in flow, `POST /api/auth/mfa/login` with its unexpired `challengeId`, `challengeToken` and either `totp` or `recoveryCode`. After a successful MFA response, set the local `driverToken` from `data.accessToken`, `driverAccountId` from its `account_id` claim and `driverUserId` from `sub`. Then proceed to 00.3. Use the existing authentication policy for the test Account.

### Observe progress and recover safely

- Do one diagnostic read at a time. Repeat 04.1 or 04.3 manually after background processing. `PENDING_PAYMENT` with a preparing or unknown order is `ORDER_NOT_READY` for the test. A closed order or expired H cannot pass. Inspect the returned admission state, checks, outcome code and order code. SignalR delivery needs separate authenticated browser or hub verification.
- If a submit response is lost, resend **03.1** with the stored body and key. Re-sign in with the same Account if needed. The saved body is used even when the quote has since expired. For a genuinely new scenario, use a fresh copy of the blank environment or clear the prior review/consent, submitted intent, idempotency key, request/reservation IDs and order state. Resolve the original request before starting another one.
- Operations reports readiness from its authenticated Payment response. The Driver-facing invoice endpoint rejects pending orders. An independent check for zero provider transactions or verified collections requires separate owner-authorized evidence. The integration run additionally checked Payment’s persisted order, quote and zero transactions. The collection makes no payment or checkout request and its final assertion requires an active uncompromised hold and no Operations confirmation.
- For HTTP 401, check token expiry and the configured JWKS. For 403, check Driver role or owner permission. For 409, distinguish changed idempotent intent from an ineligible slot or stale quote. For 503, inspect the error. `RESERVATION_ADMISSION_DISABLED` indicates the route flag is off, and `PAYMENT_QUOTE_PROOF_UNAVAILABLE` at fresh submission indicates the starter prerequisites are missing. On review, `OWNER_UNAVAILABLE` can mean missing Facility or Payment schema, owner auth or pricing. A Saga timeout can mean owner queues are unbound or unreadable or the outbox failed to deliver. When quote expiry happens before guarded acceptance, review again and use a new scenario. An accepted quote can still provision before H after its review deadline.

## Optional cases

- **06 — Zero Amount / Subscription Coverage. Blocked.** Production eligibility still returns uncovered. A zero tariff does not make the Driver subscription-covered. When the entitlement owner supplies a genuine canonical-plate entitlement, run a separate account-scoped review and submission, verify zero payable and a genuine linked Payment order and receipt, then observe Operations `CONFIRMED` before H. Check provider traffic is absent. The enabled collection does not exercise this case.
- **07 — Negatives.** After completing the authenticated path, resend a changed interval under the original key (409 `IDEMPOTENCY_KEY_REUSED`) or intentionally omit authorization on the own booking read (401). Each runs independently and requires the original positive scenario variables.
- **08 — Legacy / Compatibility.** Run 00.2 first. Supply an existing owned `legacyRequestId` and `legacyReservationId`. The two GETs verify v2 receipt and historical booking binding. These requests do not create a legacy hold.

This collection is prepared for manual execution. The live acceptance run used the four real service hosts and separate disposable stores. Manual Postman and browser runs remain pending.
