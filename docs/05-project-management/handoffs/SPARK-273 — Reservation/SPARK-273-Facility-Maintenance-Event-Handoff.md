# SPARK-273 — Facility maintenance/emergency event contract handoff

**Status:** Proposed contract for teammate review; no repository changes authorized or made.  
**Owners:** FacilityService teammate — authoritative maintenance state, event publication and enforcement on physical edits. OperationService/SPARK-273 — event consumption, reservation reconciliation, and booking-interval integration. PaymentService teammate — refund/disposition execution.

## 1. Agreed business behavior

1. Facility may proceed with an authorized physical edit without waiting for Operations to complete cancellation, reallocation, or financial reconciliation. **No Operations acknowledgement is a prerequisite to the physical edit.**
2. Facility must publish a durable, authoritative maintenance/emergency restriction **when that restriction is committed**, not only after a later slot edit. Operations processes it asynchronously; temporary cross-service inconsistency is acceptable, while eventual durable reconciliation is required.
3. A scheduled maintenance restriction applies to **all future reservation intervals that overlap its effective interval as soon as the schedule is committed**, even if its start is in the future. Emergencies apply from the declared effective start.
4. `EndAtUtc = null` means **open-ended from `StartAtUtc` until a later authoritative revision removes or bounds the restriction**. It must not mean zero duration, an automatically inferred duration, or an unknown-but-permissive period.
5. User/Vehicle/Facility eligibility accepted at admission remains a historical fact. New maintenance facts are separate authoritative business events; Operations does not repeatedly authorize admission at confirmation.
6. Physical-slot changes can proceed regardless of whether the Operations consumer is temporarily unavailable; the event must remain durable for redelivery. Refunds and reallocation follow independently.

## 2. Proposed canonical event

This is a **proposed wire contract**, not a claim that the event already exists. Reuse a working teammate-owned event if it already supplies equivalent semantics. Agree on an actual shared contract location and RabbitMQ topology before enabling the producer or consumer.

```csharp
namespace SmartParking.Contracts.Facility;

// Suggested internal wire DTO; details subject to teammate agreement.
public sealed record FacilityMaintenanceChangedV1(
    Guid EventId,                 // Stable message/event identity; unchanged on replay
    Guid MaintenanceId,           // Stable identity of the maintenance/emergency restriction
    long Revision,                // Strictly increasing within MaintenanceId
    DateTimeOffset OccurredAtUtc, // When this authoritative change committed
    Guid TenantId,
    Guid SiteId,
    string ChangeType,            // "UPSERTED" | "CLEARED"
    string Source,                // "SCHEDULED" | "EMERGENCY"
    string Scope,                 // "SITE" | "SLOTS" (only SITE is proven supported in current Facility code)
    IReadOnlyList<Guid> SlotIds,  // Empty for SITE; nonempty for SLOTS
    DateTimeOffset StartAtUtc,    // Required inclusive beginning
    DateTimeOffset? EndAtUtc);    // Optional exclusive ending; null = unbounded
```

### Semantic rules

- `UPSERTED`: This event contains the **complete current effective restriction**, not a partial delta. It covers initial scheduling, emergency declaration, extension, and end-time assignment. A higher `Revision` replaces a lower revision for the same `MaintenanceId`.
- `CLEARED`: The source has cancelled or ended that restriction. Consumers must retain the highest revision/tombstone and ignore any older `UPSERTED` delivered later. Clearing does **not** resurrect already-cancelled bookings.
- `Scope = SITE`: Every selected slot in the site is covered; `SlotIds` must be empty. This aligns with currently verified site-wide maintenance.
- `Scope = SLOTS`: Only the listed slot IDs are covered. Facility must prove authoritative slot-level maintenance support before using this shape. For an affected unit, Facility may later map the unit to a stable list of affected slots or agree on a versioned scope extension; do not silently assume Operations can derive current unit membership.
- `EndAtUtc` on an `UPSERTED` event must either be null or strictly greater than `StartAtUtc`. All timestamps are UTC instants. No duplicate or zero revisions; idempotency is scoped by `MaintenanceId` and `Revision`, while `EventId` handles transport duplicates.
- `OccurredAtUtc` is metadata, **not** the effective start of the maintenance window. A future scheduled restriction must be enforced before its `StartAtUtc` if a booking overlaps it.
- A `CLEARED` event must retain the same `MaintenanceId`, applicable scope, and a higher revision. The consumer's last-seen revision prevents stale messages from reactivating the restriction.
- Never expose personal booking, plate, PaymentOrder, or account information in the Facility maintenance event.

## 3. Reservation interval overlap

Use half-open booking intervals `[bookingStart, bookingEnd)` and maintenance intervals `[start, end)` or `[start, +infinity)`.

```text
overlap = bookingEnd > maintenanceStart
          AND (maintenanceEnd is null OR bookingStart < maintenanceEnd)
```

If an approved Operations policy includes an inter-booking claim buffer, document separately whether maintenance validation uses the requested `[Start, End)` interval or the longer claim interval. Do not invent buffer behavior without policy evidence.

**Examples** for a site-wide restriction beginning at `2026-11-24T10:00:00Z`, with `EndAtUtc = null`:

| Proposed booking (UTC) | Outcome |
|---|---|
| 08:00–10:00 | No overlap; eligible with respect to maintenance |
| 09:30–10:30 | Overlap; ineligible |
| 10:00–11:00 | Overlap; ineligible |
| 12:00–13:00 | Overlap; ineligible |

When an end time is later established (for example, 12:00), the window becomes `[10:00,12:00)`. Existing system-cancelled reservations are not automatically restored.

## 4. Durable producer requirements — Facility teammate

- Commit the maintenance-window change and corresponding outbox event **in the same Facility database transaction** (or demonstrate equivalent existing durability). Do not publish a success event for a rolled-back schedule.
- Send events for initial schedule, emergency restriction, extension/end-time change, cancellation, and early clearance. A single initial event is insufficient for nullable-end maintenance.
- Use stable `MaintenanceId`, monotonic `Revision` and replay-stable `EventId`; do not rely on broker arrival order.
- Enforce the declared maintenance policy independently of consumer delivery. Slot edits must use whatever Facility-only maintenance prerequisite is approved; do **not** wait on Operations cancellation/refund acknowledgements.
- The initial schedule must block **new overlapping interval checks** even if Operations is down or the outbox has not yet delivered the event.
- Do not mark a window as financially reconciled simply because a message was sent.
- If the existing maintenance event already provides a similar contract, supply its actual code, message topology and producer transaction to OperationService for adaptation; do not add a duplicate event by default.

## 5. Interval-aware Facility eligibility — integration handoff

The SPARK-273 developer owns Slice 2 / Operations-side booking-interval integration. Facility remains the source of truth for maintenance-window scheduling, persisted windows, and the authoritative overlap predicate.

- Existing `SlotCheckV1` only carries `WorkflowId`, `AttemptId`, `AccountId`, `Fingerprint`, `RequestedSlotId`. Operations must supply accepted `StartAtUtc` and `EndAtUtc` (and any approved buffer semantics) to the agreed broker validation contract.
- Facility's `SlotDecisionV1` must reflect the slot's booking-interval maintenance eligibility, not merely whether the slot is currently operational. A definitive overlap should produce an appropriate ineligible result; owner unavailable/unknown must not be reported eligible.
- The current quote-review call `ResolveSlotAsync(slotId)` also lacks an interval. The agreed Slice 2 plan must address its validation time and contract/transport without introducing a new HTTP Facility dependency.
- The same effective window rule must cover open-ended maintenance. Facility should not require `EndAtUtc` to be populated to answer an interval query.
- Avoid using a repeated Facility eligibility call during reservation confirmation. A newly delivered maintenance restriction is a separate authoritative invalidation fact.

## 6. Consumer requirements — OperationService (SPARK-273 owner)

- Subscribe to the agreed durable event. Validate tenant/site/scope/time/revision. Atomically apply an event-derived local restriction projection (or prove an existing equivalent) under the Operations site guard, then reconcile bookings overlapping the restriction.
- The projection must preserve a later `CLEARED` tombstone/revision so stale events cannot reinstate maintenance.
- Reuse existing booking, capacity, financial, and progress mechanisms. A paid collection may arrive after a reservation was invalidated; its event must trigger idempotent Payment-owned disposition, not confirmation.
- After a restriction is recorded, a hold created later must be fenced by that locally known restriction under the **same site lock**. This handles event-before-hold arrival without reauthorizing the historical Facility decision. If a hold commits first, the event consumer finds it and cancels/reconciles it.
- No new reservation polling worker or Facility HTTP validation. For exhausted delivery retries, keep an observable error/replay route; do not claim infinite automatic retry.

## 7. Acceptance test matrix (jointly owned)

1. Scheduled site maintenance with a known end and exact interval boundary overlaps.
2. Open-ended maintenance: all bookings ending after start overlap; pre-start bookings ending exactly at start do not.
3. Window initially open-ended, later receives an end; event revision/update is applied.
4. Scheduled window cancelled/cleared, then an older UPSERTED event arrives; restriction stays cleared.
5. Duplicate events and out-of-order revisions have no duplicate cancellation or financial effects.
6. Event processed before booking hold commits; local restriction prevents the later invalid hold.
7. Hold commits before event processes; consumer automatically cancels the hold and emits progress.
8. Already settled payment and payment racing with cancellation are eventually routed to the existing Payment disposition path.
9. Confirmed reservation is reallocated where an approved mechanism exists, otherwise system-cancelled with appropriate disposition; active parking sessions retain safety constraints.
10. Facility physical edit succeeds while Operations is unavailable; on recovery the durable event is processed, without an Operations acknowledgement blocking the edit.
11. Database rollback emits no event; crash after source commit still eventually delivers the same logical event.

## 8. Verified current-code gaps and reference paths

Against the committed SPARK-273 branch inspected on 2026-11-23:

- `FacilityService.Application/Contracts/ReservationAdmission/SlotCheckV1.cs`: no booking interval fields.
- `FacilityService.Infrastructure/ReservationSlotConsumer.cs`: uses `ResolveReservationSlotQuery(slotId)`, not the booking interval.
- `FacilityService.Domain/MaintenanceWindow.cs`: `End` is a required `DateTime`, and scoped windows are explicitly rejected; this **does not currently support null end**.
- `FacilityService.Persistence/PostgresMaintenanceWindowStore.cs`: create/extend APIs take required end; create persists `pending` enforcement. PostgreSQL ranges can represent unbounded ends, but the current API/domain logic does not.
- `FacilityService.Application/Usecases/Maintenance/Commands/ScheduleMaintenanceWindow/ScheduleMaintenanceWindowCommandHandler.cs`: persists the window; no maintenance broker publish is visible in this path.
- `FacilityService.Application/Common/Synchronization/StructureEditHandlers.cs`: uses current structure/commitment guards; maintenance-precondition enforcement is not established by this inspected path.
- `OperationService.Application/Reservations/ReservationLifecycleEvents.cs`: `FacilityStructureAppliedV1` is an Operations-originated structure result, not proof of a Facility maintenance producer.

The Facility teammate may have newer, unmerged changes. Compare against the exact teammate branch before creating any new contract or changing their code.

## 9. Required teammate response

Please confirm the actual existing event type, namespace, source file, publication transaction, queue/exchange binding, start/end-null support, scope support, revision semantics, and which structural mutation paths enforce maintenance. If any requirement is absent, propose only the missing Facility-owned change and return exact contract details for the SPARK-273 consumer. Do not modify SPARK-273 Operations code or implement Payment refunds.

**Approval gate:** Proposed contract and any edits must be reviewed by the SPARK-273 owner before implementation. No commit/push on their behalf.
