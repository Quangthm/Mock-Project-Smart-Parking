# SPARK-273 Reservation owner handoff

Use this document for Operations, UserService and Facility ownership and release gates. The reviewed backend and linked order path are implemented. Current architecture decisions govern the contract. The [frontend handoff](2026-11-09-reservation-frontend-handoff.md) covers the browser cutover and the [Payment handoff](2026-11-19-SPARK-273-payment-authority.md) covers financial evidence and settlement. [18 November contract amendment](../decision-log/2026-11-18-SPARK-273-reservation-contract-amendment.md) · [Authorization freshness ADR](../../02-architecture/architecture-decisions/authorization-freshness-and-current-access.md)

## Review, admission and guarded hold

| Owner | Current responsibility |
| --- | --- |
| Operations | Authenticate the Driver and canonicalize the submitted CAR plate. Resolve the slot via signed Facility HTTP, check the effective platform/site policy and prospective coverage, and ask Payment for the immutable quote. Durable submission captures original User and Account, exact quote/version and selection, and idempotency identity. Same Account and key replay the stored receipt for identical intent. A conflicting intent fails. |
| UserService | Independently attest Account-current access and active CAR plate ownership during Saga admission. Preserve once-per-workflow Account decisions, branch binding, and the vehicle registration revision. |
| FacilityService | Own slot identity, tenant/site, version and eligibility. Use signed HTTP for quote lookup and a read-only broker consumer for admission. Keep acquire/complete/abort protection for owner snapshots and competing structure writers, including the projection site fence. |
| PaymentService | Issue and attest the exact quote and create or replay a genuine linked order after the hold. Payment owns monetary values, collection verification and disposition. Operations owns guarded capacity, confirmation and late-money entitlement. |

The EF-backed Saga joins independent Account, plate and slot facts before enqueuing final evaluation. Persist receipt and start through the existing outbox. Correlate attempts to the original request and reject stale, foreign, malformed, and duplicate decisions. The existing due-work sweep wakes the Saga with persisted deadlines. Preserve its budget and attempt fencing. [Activation and recovery evidence](../../04-testing/reports/SPARK-273-reviewed-admission-integration.md)

Final evaluation obtains immutable Payment proof and Facility protection. Under the Operations guards, recheck effective policy, eligibility, quote validity and one-time use, interval overlap, and live capacity. Future claims conflict by canonical plate and site over the effective interval, including its buffer. Unended physical sessions and active PARKING remain global protections. Commit the hold, receipt and durable hold/progress effects atomically. Token completion or abort remains a recovery obligation after a terminal receipt. [Workflow target](../../02-architecture/architecture-decisions/reservation-workflow-target.md) · [Facility protocol](../../03-design/api/reservation-facility-dependency-api-design.md)

## Owner queues and broker trust

| Queue | Writer | Consumer |
| --- | --- | --- |
| `user-account-access-v1`, `user-reservation-plate-v1` | Operations | UserService |
| `operations-account-access-results-v1`, `operations-reservation-plate-results-v1` | UserService | Operations |
| `facility-reservation-slot-v1` | Operations | FacilityService |
| `operations-reservation-slot-results-v1` | FacilityService | Operations |
| `operations-reservation-admission-v1`, `operations-reservation-evaluate-v1`, `operations-reservation-progress-v1` | Operations | Operations |

Verify the versioned queue and exchange bindings, error transport, restricted service identities, consumers and outbox delivery in the target vhost. Confirm Account-access type exchange permissions as well as queues. Use the hold/payment exchange and owner authentication matrices in the [deployment checklist](../../04-testing/reports/SPARK-273-deployment-checklist.md) and [activation report](../../04-testing/reports/SPARK-273-reviewed-admission-integration.md#activation).

## Confirmation and recovery

Quote validity is checked at guarded acceptance. Provision the order with the original operation identity, accepted binding and exact deadline H. New order creation and checkout are fenced at H. Recover committed effects by exact replay or authoritative lookup. For paid reservations, Operations consumes the existing `PaymentSucceededV1` signal and verifies the scoped collection through Payment before deciding confirmation. Normal confirmation evaluates the held claim strictly before H. Verified paid money may compete for capacity again strictly before reservation start T under fresh guards. Absent eligible capacity or a valid cutoff, record the reconciliation outcome with Payment. A disposition acknowledgement records the obligation.

### Covered acceptance recovery

The production entitlement source is unavailable. The controlled backend path uses the same owner order PUT and durable zero order and receipt evidence. Absence or contradictory evidence keeps the outcome unknown. Recover lost acknowledgements with exact operation replay or authoritative lookup. Confirm only with a protected claim and a guarded checkpoint strictly before H. Expiry or cancellation releases the claim and late acceptance follows reconciliation. A fresh request is required for another attempt. Retain the due-work path for unknown outcomes when the consumer acknowledges after persisting recovery state. [Covered owner contract](2026-11-19-SPARK-273-payment-authority.md#owner-agreement-and-wire-contracts)

## Rollout and legacy retirement

- Verify feature flags, User JWT and signed owner trust, owning schemas, RabbitMQ permissions and outbox relay, effective reservation policies, eligible slot and active CAR tariff. Rehearse the owner-approved transition on representative populated volumes. Capture rollback and in-flight handling before release.
- Move new bookings to the reviewed client after acceptance. Inventory unsubmitted reviews, admitted requests, held/confirmed bookings, open orders and late or unresolved collections. Preserve version-aware reads, original amounts, policy snapshots, terms/vehicle references and replay keys during the compatibility window. Retire terms ingress, serializers, persistence and recovery workers only after consumer inventory and drain or migration evidence. Preserve financial reconciliation, Gate linkage, and history.
- Keep token workers and supported deadline/continuation paths until replacement coverage is proven, including restart after acknowledgement, unknown token outcomes and all claim-invalidating writers. The aggregate-only confirmation fast path still requires the approved writer-invariant proof. Treat allocation/arrival, physical Gate and later lifecycle acceptance as independent checks.

## Reservation cleanup and fixture follow-ups

| ID / disposition | Source and finding | Functionality to keep and safe boundary | Decision and verification |
| --- | --- | --- | --- |
| R1 · Retire candidate | Operations API `OwnReservationHosting.cs`, Application `OwnReservations.cs`, Persistence `OwnReservationStore.cs`. A second auth scheme, handler, cursor and store implement the same customer list/detail paths. Its query includes all Account rows and pages by start ascending. Its fallback display label can be a vehicle type. Only its DI dependencies are wired because MediatR scans the assembly. | Use `ReservationBookingController`, `ReadOwnBookingsHandler` and `ReservationRepository.Bookings` for scoped booking rows, created-time paging, interval filtering and stored vehicle evidence. The live Assistant adapter is IoT `OwnerReadClient.cs`. Preserve its Account/filter-bound protected continuation. | Operations and Assistant owners confirm older clients and any direct mediator callers. Remove the unused requests/handlers, store and registration together once approved. Exercise DI validation with admission enabled and disabled, exact list/detail JSON, foreign-Account reads and pagination. |
| R6 · Fixture decision | `scripts/database/seeds/demo/03-reservation.sql` changes fresh CAR capacity from 6 to 48 and retains quota 4. Existing-row guards make fresh and already seeded environments potentially different. | Keep demo data separate from authorization, public capacity evidence and reservation race fixtures. | QA/Facility owners confirm intended demo inventory. Pin explicit capacities in tests that depend on them. |

## Verification and historical evidence

- The [local manual run](../../04-testing/postman/SPARK-273-manual-test-guide.md#6-optional-cases-and-verification) passed through **05.1** as reported by the project lead. The [four-service integration run](../../04-testing/reports/SPARK-273-reviewed-admission-integration.md#live-no-payment-evidence) verified unpaid order readiness and restart recovery after quote expiry and before H.
- The earlier Slices 2 and 4 run recorded 166 passing selected tests (85 Operations, 78 Facility, 3 User), including token/projection contention and confirmed, expired and late-money paths with controlled owner facts. The Payment handoff records the broader paired settlement run, its provider double, and pending browser, merchant and deployed-environment verification.
- The develop merge review recorded 39 User-project HTTP checks, 8 Facility checks and 10 Operations history checks on that merge baseline. Historical counts describe those runs.

- [9 November producer and compatibility findings](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-09-reservation-producer-handoff.md)
- [9 November Facility protection design](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-09-reservation-facility-durable-dependency.md)
- [10 November lifecycle checklist](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-10-reservation-completion-checklist.md)
- [10 November Facility, event and realtime findings](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-10-reservation-events-and-facility-integration.md)
- [11 November owner alignment and deferred backlog](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-11-SPARK-273-implementation-alignment.md)
- [19 November develop merge evidence](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-19-SPARK-273-develop-merge-review.md)
- [19 November admission Saga tests and file inventory](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-19-SPARK-273-slice-3.md)
- [19 November held-claim and confirmation tests](https://github.com/Quangthm/Mock-Project-Smart-Parking/blob/6b98692b29edc421a7b282823e347fb3fa79b975/docs/05-project-management/handoffs/2026-11-19-SPARK-273-slices-2-and-4.md)
