# Service boundaries and durable contracts — RC1

Derived from the approved baseline decision record. Historical code/schema findings describe the previously reviewed commit; this document update does not implement repository changes.

| Confirmed deployable service | Authoritative ownership | Logical persistence boundary |
|---|---|---|
| **User & Access Service** | Identity, authentication/credentials/sessions, roles/current access, organization identity/membership, Operator grants/scopes, profiles and registered vehicles/binding. Facility validates referenced site existence. | UserDb |
| **Facility & Visualization Service** | Sites, floors/zones/slots, access paths, supported categories, configured physical capacity, layout/coordinates, maintenance/service configuration, open/closed state and fixed 3D deployment-asset references. React/Three.js renders the visualization. | FacilityDb |
| **Parking Operations Service** | Reservations and competing claims, accepted occupancy/uncertainty/manual verification/reconciliation, allocations, visits/admission/checkout/departure, tickets/QR credentials and reusable-card assignment/release/loss/disabling/history, incidents/appeals, monthly plan definitions/fee-coverage entitlements, Search and authoritative availability. | OperationsDb |
| **Payment & Pricing Service** | Versioned tariffs, pricing/accepted financial snapshots, payment orders/attempts/providers/cash records, verification/idempotency/reconciliation, financial refunds/invoices; monthly orders, payments and financial paid periods. | PaymentDb |
| **IoT & Recognition Service** | Device/readers, raw observations, recognition/evidence and audited corrections; separate Assistant module with provider integration, conversations and tool/confirmation history. Never grants domain rights. | IoTDb |
| **Notification & Audit Service** | Separate Notification delivery, Audit projection and General Support case/handoff modules. Source services retain authoritative business facts and reports. | NotificationAuditDb |

These are six main deployable services. **Search/discovery is an Operations module; Assistant and conversation storage are an IoT & Recognition module; General Support is a Notification & Audit module.** Parking incidents/appeals remain in Operations. No separate Media, Monthly, Policy, Search, Support or AI service is introduced. Each domain owns its reports: Operations visits/occupancy/reservation utilization; Payment collected money/refunds/revenue; Facility configuration/layout; IoT device/recognition status; Notification & Audit audit search/delivery and its own support metrics.

**Boundary:** Facility owns what resources exist and their structural/service configuration; Parking Operations owns rights, claims, visits and business availability. IoT & Recognition supplies observations and never grants admission, payment clearance or gate business authorization. Facility/3D projections do not become a second occupancy or availability authority. Combining IoT/Recognition does not bring deferred real hardware integration into MVP.

**Policy ownership is confirmed:** User & Access owns security/session/access rules; Facility & Visualization owns structure/service configuration; Parking Operations owns reservation hold/protection/allocation/late/reallocation/backup/admission/capacity rules; Payment & Pricing owns tariffs/financial rounding/refund calculation/provider rules; IoT & Recognition owns recognition/device configuration. Shared catalogue conventions do not create a shared policy database or require Payment for unrelated policy decisions. Preserve applicable Admin defaults/bounds → Owner/lot override → supported zone/specific scope → resolved booking context. Individual values, override permissions, effective-date and snapshot mechanics remain subject to existing approval gates.

**Monthly boundary is confirmed:** Operations owns plan definitions, accepted coverage periods, entitlements and visit admission; Payment owns linked financial terms, prices, orders, paid periods and refunds. The selected v1.0 product is **when-space-available only**: fee coverage with no capacity or named-slot guarantee. Earlier guaranteed-capacity/slot and delayed-slot-activation proposals are superseded for this baseline. Money, entitlement and physical occupation remain separate. See §1.2 and B05.

**Data/communication rules are confirmed:** Each service owns its logical persistence boundary and credentials; one local PostgreSQL server is permitted. Logical DB names above are naming guidance, not an executed migration. No cross-service joins, foreign keys, repository or DbContext access. Cross-service IDs are references validated through contracts/workflows/events or justified local projections. Prefer authenticated HTTP/OpenAPI for immediate requests and durable asynchronous outcomes for propagation. Important audit facts are committed durably with the originating operation/outbox, then projected into Audit; a synchronous central audit call is not a global prerequisite for business success. Notification failure does not roll back the original operation. Use one coordinator per workflow and avoid same-workflow synchronous dependency cycles. RabbitMQ + MassTransit is selected for inter-service asynchronous messaging; MQTT remains for IoT/device messaging, with real hardware still deferred; Kafka is deferred. Exact event contracts and recovery implementation remain open.

**Decision effect:** SB-01 and the supplied microservice data-ownership guide settle service/module ownership, including ARCH-DEF-02/04. The consolidated 8—9 October confirmations below additionally settle billing, checkout, monthly product, broker, facility-change protocol, version preservation, authentication direction, media-access principles and the core local acceptance targets. Remaining details are enumerated in §12. Approval of behavior never means code, schema or tests are complete.

## Implementation layering

ASP.NET Core/.NET target direction; API → Application → Domain with Persistence and Infrastructure implementing ports. API validates transport/authentication and maps errors; Application orchestrates use cases and authorization; Domain owns invariants; Persistence owns local transactions/repositories/migrations; Infrastructure owns provider/broker/media adapters. No Domain dependency on provider SDK, DbContext or HTTP. React/Tailwind/Material UI and three.js remain frontend directions; UI never becomes authority.

## Command/event contract matrix

Names are proposed stable technical labels, subject to schema review without changing business outcomes. Common envelope: event ID, schema version, aggregate ID/version, occurred/accepted time, correlation/causation/operation IDs, resource scope and minimal payload. No access token, provider secret, unnecessary plate/image/chat text in broadcast events. Publish originating fact with local outbox; consume with durable inbox/version checks.

| Publisher / command outcome | Consumers | Required fact / pending recovery |
|---|---|---|
| User & Access: AccessChanged, OwnerApproved, OperatorGrantChanged | Resource services, authorized Admin projection | Current resource checks prevail over cached JWT; revoke subscriptions/private results |
| Facility: ChangeProposed, EmergencyRestrictionRecorded, ChangeCommitted | Operations, authorized visualization | Resource IDs/config version; planned acknowledgement vs immediate emergency reality |
| Operations: ReservationCreated/Confirmed/Cancelled/Expired/NoShow | Payment, Notification/Audit | Accepted terms and referenced claim/result; never independent capacity authority in consumer |
| Operations: AllocationChanged, ArrivalRecorded, VehicleEntered/Departed | Facility/3D projection, Notification/Audit | Scoped physical/claim versions; gate-open is not passage |
| Operations: CheckoutAccepted/Invalidated/Settled | Payment, authorized views | Original cutoff and checkout revision; unknown money interlock |
| Payment: PaymentVerified/Failed/Unknown, RefundCompleted | Operations, Notification/Audit | Order/transaction identity, verified amount/currency and operation; money is not fulfillment |
| Operations: MonthlyEntitlementCommitted/Cancelled/Expired | Payment, scoped views | Bound vehicle/location, immutable coverage/anchor/version; no capacity hold |
| Facility/Operations: CoverageUnavailableVerified | Payment | Paid-period/union interval references; reserve/refund once |
| IoT: ObservationReceived/RecognitionRecorded | Operations | Original source/event/version/time, uncertain outcome/evidence reference; never authorization |
| Asset owner: HoldAcknowledged/Released, AssetDeleted | Authorized case owner | Asset/version/hold ID/outcome; failure stays explicit |
| All source owners: AuditFact, NotificationRequested | Notification/Audit | Originating actor/scope/action/result and minimal notification context; no synchronous global audit gate |

API/event schema implementation, wire compatibility, retries/backoff and storage indexes remain team design work. See state-transitions for mandatory invariants. One local PostgreSQL server may host six separate databases/credentials, no cross-service foreign keys, SQL joins or repository/DbContext access. RabbitMQ/MassTransit selected; MQTT devices future, Kafka deferred; Redis optional. SignalR is client projection, not durable command authority.


## Frontend and deployment choices retained

Unified React role-based routing, Tailwind CSS, Material UI and three.js remain the current direction. Zustand versus Redux Toolkit and @react-google-maps/api versus another permitted map library remain technical choices, not a PM policy blocker. Docker is the initial local deployment; cloud/AWS/Azure region selection and production scaling are later deployment work. No fixed production cost, team allocation or cloud latency claim is created here.


## SEC-02 identity/support coordination

General Support in Notification & Audit owns ACCOUNT_RECOVERY case intake/routing/review history and scoped references. User & Access authorizes and executes credential changes; no support database can directly update password/factor tables. Suggested outcomes: AccountRecoveryReviewed, RecoveryGrantIssued, PasswordResetCompleted, TotpResetRequired, TotpReplaced, SecuritySessionsRevoked; payloads contain operation/ticket/account/version references and minimal result, never secret/code/token. Use authenticated commands, local atomic writes/outbox and deduped projections. Timeout queries same operation; ticket approval is not proof of successful reset. Current security/session-version checks at resource services prevent revoked JWT use; do not rely on eventual notification delivery for revocation.
