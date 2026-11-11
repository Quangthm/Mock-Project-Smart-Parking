# Reservation availability, Facility integration and realtime handoff

Approved scope: booking through confirmation, CAR / Specific Slot. Payment producers stay with the Payment teammate; Facility producer and Operations enforcement are implemented in this task. SignalR relay/hub and frontend implementation belong to teammates. Code is not deployment-verified.

## Acceptance and concurrency

Facility evaluates active tenant and site, non-deleted unit ancestry, slot compatibility and operational status. AVAILABLE/OCCUPIED represent operational slots; occupancy remains a separate Operations fact. This MVP does not calculate a route from an entrance. Closing a parent makes a slot effectively ineligible without rewriting every child status.

The first successfully committed hold exclusively claims the selected slot for its interval, including the accepted inter-booking buffer. An overlapping second request is rejected even if another slot is free. This is selection protection, not a promise that the slot can never fail; later recovery/reallocation remains a separate workflow. Identical request replay returns the same workflow.

Operations serializes new booking acceptance by account, canonical plate, then site. Confirmation/physical visits use plate then site. It reads authoritative quotas from operational_capacity_configs, requires explicit site/category configuration, applies narrower limits, and derives demand from active claims, allocations and visits. Pending holds block competing claims but are not reported as confirmed bookings. Expired unpaid holds do not retain acceptance rights. Physical occupancy is never erased by the occupancy projection horizon. Linked booking/visit and occupied backup identities are not deducted twice.

Database guards and constraints decide whether a write wins. Broker or SignalR delivery never grants a claim and never replaces backend checks. Direct SQL, bulk writes, direct DbContext saves or a future writer bypassing the prescribed guards can bypass application-only slot/pending-hold/quota protection. Confirmed plate interval exclusion and live plate uniqueness add database enforcement. Pending holds cannot use a clock-dependent SQL exclusion predicate; their deadline check remains guarded C#.

New cancellation handlers must write reservation_cancellation_evidence with the cancellation in one transaction under reservation-account:<accountId>. Persist reason, accepted policy and any computed rapid-restriction deadline. Facility failures are excluded from voluntary abuse. Current admission checks daily counts and persisted restriction deadlines. Legacy CANCELLED rows without evidence fail closed for new booking creation until deployment reconciliation; do not label them voluntary automatically. Existing login and reservation rights remain usable.

## Facility v1 internal contract

Full request/error examples: [Facility API design](../../03-design/api/reservation-facility-dependency-api-design.md). Base: /internal/reservation-dependencies/v1. Each endpoint requires the dedicated OperationsService bearer policy. The existing X-Service-Key and Driver JWT are not substitutes.

| Method/path | Body / result |
|---|---|
| GET sites/{siteId}/slots/{slotId} | Read-only booking eligibility and configuration fingerprint; no hold |
| POST acquire | DependencyIntent → acceptedIntent, HELD/ABORTED and immutable complete resource snapshot |
| POST complete | intent + exact persisted outcomeJson string → COMPLETED and SHA256 outcome fingerprint |
| POST abort | DependencyIntent → durable ABORTED acknowledgement, including an unknown token |

DependencyIntent contains token, workflowId, purpose ADMISSION/CONFIRMATION, tenantId, siteId, slotId, intentFingerprint and deadline. Replay must retain every field. Resources carry unique slot IDs, category compatibility, live unit ancestry, operational eligibility and fixed/dynamic backup configuration. Snapshot scope is one workflow, not a mirrored Facility catalogue.

Operations persists intent before HTTP. Remote acquire/complete/abort occurs outside Operations transactions and capacity guards. Facility serializes acquisition with structure edits using the same site key; a competing editor returns STRUCTURE_BUSY/409 instead of waiting in a cross-service cycle. Existing pending structure synchronization must finish before acquisition.

HELD protects configuration until exact completion or fenced abort; deadline alone does not release it. Operations fences expired local work and retries abort. A committed READY outcome must complete even after deadline. Do not manually clear holds based only on age. Investigate/recover the owning workflow after an outage. Changed token payloads/outcomes conflict. Completion and abort are mutually exclusive, and abort tombstones prevent delayed acquisition.

New Facility producer workflows are MediatR handlers using Domain entities, repository and the owning IUnitOfWork. Existing structure/physical writers participate in the same gate. Structure edit/synchronization coordination is now in Application MediatR handlers. Gate claims use a repository and handler-owned UoW transaction.

## Durable event contracts

Source DTOs: [ReservationLifecycleEvents.cs](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationLifecycleEvents.cs). Broker contracts are service-local and independently versioned, with no shared project reference.

| Exchange / message | Emission and fields | Intended consumers |
|---|---|---|
| smartpark.operations.reservation-hold-changed.v1 / ReservationHoldChangedV1 | Same transaction as hold creation or expiry: eventId, occurredAt, workflowId, reservationId, accountId, tenantId, siteId, slotId, start, end including buffer, holdExpiresAt, state PENDING_PAYMENT/EXPIRED, version | Private account SignalR relay; audit consumer |
| smartpark.operations.reservation-availability-changed.v1 / ReservationAvailabilityChangedV1 | Same outbox transaction as hold change or confirmation/reacquisition: eventId, occurredAt, tenantId, siteId, slotId, start, end, reason | Site availability invalidation relay |
| smartpark.operations.reservation-confirmed.v1 / ReservationConfirmedV1 | Confirmed state, receipt and outbox commit together: eventId, reservationId, accountId, tenantId, siteId, confirmedAt (evaluation checkpoint) | Private status relay; audit/notification consumers |
| smartpark.operations.facility-structure-applied.v1 / FacilityStructureAppliedV1 | Operations applies Facility's durably committed structure outcome: eventId, occurredAt, synchronizationToken, tenantId, siteId, siteActive, removedSlotIds, removedUnitIds | Site invalidation relay; future affected-booking recovery consumer |

FacilityStructureApplied is an **Operations acknowledgement fact**, not a claim that Facility publishes directly to RabbitMQ. The existing durable Facility ready row bridges its physical commit to Operations; Operations records its receipt/outbox atomically and replays the receipt after response loss. If messaging is unconfigured, completion remains pending and recovery must retry. A broker outage retains captured facts in the Operations outbox.

Any successfully applied structure outcome invalidates the whole site, including maintenance/compatibility/backup edits whose removed-ID arrays are empty. A metadata-only edit may also invalidate; no-op transport retries do not publish again. occurredAt is application/observation time, not a reconstructed physical-change time. This fact alone is insufficient to calculate refund entitlement.

All deliveries are at least once. Consumers deduplicate eventId, persist processing outcomes, tolerate reordering and resume after restart. MassTransit message identities are `urn:message:SmartParking.OperationService.Application.Reservations:<type name from the table>`. Consumers must use that identity explicitly (or independently reproduce namespace/type); sharing an assembly is unnecessary. The wire envelope follows MassTransit, with the DTO under `message`. Exchange names alone do not replace that identity. Provision durable bound queues and service-restricted ACLs before enabling relay. A durable exchange without a queue does not retain a subscriber backlog. Do not publish credentials, plates, images or provider payloads.

## SignalR relay and frontend contract (teammate implementation)

Use these v1 client message names: reservationHoldChanged, reservationConfirmed, reservationAvailabilityChanged, facilityStructureApplied. Preserve eventId/schemaVersion=1 and scope; private booking messages use authenticated Account subscriptions, never a client-provided account/group name. Site channels receive only public invalidation fields, never booking/account/payment IDs.

- Form/slot selector: invalidate availability for the affected site/interval and refetch. A selected unavailable slot must be reconsidered before submission; the server still rejects stale submissions.
- Own PENDING_PAYMENT: show the server hold deadline and resume its receipt. A hold does not mean the Payment order exists yet. Enable payment only using the authoritative order returned by the backend.
- EXPIRED: stop offering a new checkout for that hold and refresh receipt/order state. Do not assume money was not collected; show reconciliation pending when appropriate.
- Payment page on Facility change: refresh the backend booking/receipt and Payment state. Do not auto-cancel an order, charge again, or promise a refund based on SignalR.
- After-payment page: payment success is not booking confirmation. Show pending until the own-booking read reports committed confirmation. A confirmed event prompts that read.
- Reconnection/missed/out-of-order events: reload authoritative state. Treat availability events as invalidations, not arithmetic deltas; older private versions must not overwrite newer displayed state. Expiry and successful reacquisition can occur in one commit; an expiry notification alone must never overwrite an authoritative confirmed read. Confirmed messages currently have no sequence version, so always refetch.

No frontend code or SignalR relay is changed in this task.

## Post-confirmation backlog and release boundary

1. Consume Facility changes with versioned usability history; identify affected held, confirmed and allocated reservations and physical visits.
2. Resolve planned closure/maintenance dependencies before applying incompatible changes; preserve stable assignments and actual occupancy.
3. Implement exceptional physical closure handling, allocation/reallocation at A, facility-failure eligibility at F and unfulfilled cancellation at T, with durable retries and idempotent financial intents. Do not silently discard affected paid rights.
4. Integrate Payment reconciliation/refund acceptance and observable payout status. A reallocation request or refund intent is not completed recovery/refund.
5. Implement scan/early-arrival/wrong-slot/checkout rules from the approved arrival handoff, including the full booked minimum and configured fee exemptions.

These workflows remain outside booking-through-confirmation. Existing guards rejecting incompatible edits must not be relaxed to simulate recovery. Emergency physical-safety procedures and recovery/refund automation require the later implementation.

## Deployment verification (not executed)

Use fresh isolated schemas declared in the original scripts. CREATE IF NOT EXISTS does not upgrade existing columns/constraints or reconcile legacy history. Inventory existing volumes before choosing a data transition; do not reset a shared database.

- Restore/build and execute relevant tests when the deployment is ready (not run by the agent):

  ```powershell
  dotnet build src/Services/FacilityService/SmartParking.FacilityService.API/SmartParking.FacilityService.API.csproj --nologo
  dotnet build src/Services/OperationService/SmartParking.OperationService.API/SmartParking.OperationService.API.csproj --nologo
  dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --filter "FullyQualifiedName~ReservationAvailabilityTests|FullyQualifiedName~ReservationFacilityDependencyTests|FullyQualifiedName~FacilityStructureEndpointTests" --nologo
  ```

  Expected: both builds succeed and all selected tests pass. These do not prove PostgreSQL/broker concurrency. No prior build proves these changes.
- With concurrent requests, accept one selected-slot hold; reject its overlapping competitor. Check adjacency, buffers, global same-plate overlap, unknown occupancy, linked-visit deduplication, quotas and occupied/backup union.
- Race acquire against closure/maintenance/delete and physical slot claims. Verify busy responses, no circular waits and no incompatible commit while HELD.
- Lose acquire/complete/abort replies; restart both hosts; verify immutable replay and no abandoned hold resurrection. Do not expose confirmation before durable commit.
- Test missing/wrong signature, sender, audience, permission, expired token, Driver JWT and old shared key against all new routes; expect 401/403. Inspect logs for absence of secrets/raw payloads.
- Disable outbox delivery or stop the broker after configuration, then restore it: each committed effect must eventually deliver; rolled-back effects must not.
- Test middleware malformed JSON, validation, 401/403/404/405/415, unknown exception, request cancellation and already-started response. HEAD remains bodyless and unknown errors remain sanitized.

Full paid booking still depends on [Payment producers](2026-11-09-reservation-producer-handoff.md) and Operations' second commit for durable commercial stages/recovery. Postman scripts follow that work.
