# SPARK-273 — Walk-in occupancy, scanning and simulation handoff

**Audience:** Operations gate and simulation owner  
**Baseline:** `7e1414601`  
**Status:** Teammate implementation and review pending

Checkpoint A corrected how reservation availability interprets walk-in data. The gate producer still writes the legacy allocation described below. This handoff covers the producer, its readers and departure behavior. Reservation-linked allocation and fulfillment remain part of the reservation workflow.

## Current behavior and dependencies

1. `CheckInWalkInCommandHandler` creates an active `ParkingSessionRow` with an entry time, no reservation and no known slot. [Gate handlers](../../../../src/Services/OperationService/SmartParking.OperationService.Application/Usecase/ReservationGate/GateWriteHandlers.cs)
2. `ParkingSessionsController` dispatches `ScanParkingSessionCommand`. The handler acquires a Facility slot lease, takes the Operations plate and site guards, reloads the session and validates the scan. The walk-in branch creates a session-linked `SlotAllocationRow` with no reservation, sets `AllocatedEndTime` to `DateTimeOffset.MaxValue`, calls `Occupy`, and records the observed slot and scan time on the session. [Controller](../../../../src/Services/OperationService/SmartParking.OperationService.API/Controllers/ParkingSessionsController.cs) · [Scan handler](../../../../src/Services/OperationService/SmartParking.OperationService.Application/Usecase/ReservationGate/ScanParkingSessionCommandHandler.cs) · [Parking session](../../../../src/Services/OperationService/SmartParking.OperationService.Domain/Entities/ParkingSessionRow.cs) · [Slot allocation](../../../../src/Services/OperationService/SmartParking.OperationService.Domain/Entities/SlotAllocationRow.cs)
3. Session history and active walk-in reads can fall back to the allocation for the slot. Vehicle-location reads use its status. Structure snapshot and impact reads consider active allocations alongside sessions. Ordinary and simulated paid departure use `GateDeparture` to close the session, release active linked rows and release the scanned physical slot. [Session repository](../../../../src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/ParkingSessionRepository.cs) · [Vehicle-location repository](../../../../src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/VehicleLocationRepository.cs) · [Structure repository](../../../../src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/StructureRepository.cs) · [Paid departure handler](../../../../src/Services/OperationService/SmartParking.OperationService.Application/Usecase/ReservationGate/SimulatePaidGateDepartureCommandHandler.cs)

The walk-in row conflates an observed parking location with an advance reservation commitment. Its open-ended interval also participates in the database exclusion constraint for active slot allocations. Before removing the insert, verify that the physical slot lease and guarded writes prevent two open sessions from recording occupancy of the same slot without relying on that constraint. [Operations schema](../../../../scripts/database/microservices/03b-reservation-operations-service-db.sql)

## Target workflow

- **Check-in.** Create an open walk-in session with actual entry time. Leave `ReservationId` and `CurrentSlotId` null until the scan identifies the vehicle’s slot. Keep `ReservationId` null throughout the walk-in visit.
- **Random simulation.** For an admitted, unlocated vehicle, select a compatible, physically available slot to represent where it parked. Submit that location through the scan transition.
- **Explicit simulation.** Validate the operator-selected slot against the same compatibility and physical availability rules. Submit it through the same scan transition.
- **Scan.** Record `CurrentSlotId`, scan time and applicable physical occupancy through the guarded transition. Check for conflicting occupancy under concurrency and preserve repeat-scan behavior. Both simulation paths produce a physical observation without inserting a walk-in `SlotAllocationRow` or fabricating an end timestamp.
- **Departure.** Set `ExitTime` on verified departure, release physical occupancy and preserve the existing occupancy notification. Make duplicate delivery safe. For reservation-linked visits, retain the checks that bind the visit to its effective allocation and release it through the existing reservation departure path.

An unlocated open visit still consumes projected compatible capacity. Once located, count its observed slot once. Availability projects occupancy through evaluation time plus the effective `OCCUPANCY_VALIDITY_TIMESPAN`, with a configurable four-hour default. Reaching that horizon does not close the session or release the slot. Same-site plate reservation conflicts continue to use reservation commitments only. [Policy catalogue](../../../01-requirements/business-rules/policy-catalogue.md) · [Checkpoint A session](../../../../ai/tasks/active/TASK-SPARK-273-SESSION-20261108.md)

## Implementation order and existing data

1. Trace read and departure behavior for both legacy and session-only records. Make a session's observed slot sufficient for location and physical release. Preserve cleanup of active legacy rows on verified departure.
2. Prove physical compatibility and exclusivity for competing scans. Then remove the walk-in allocation insert and route both simulations through the observation transition.
3. Leave historical rows intact during the producer cutover. If an actual environment requires cleanup, first inventory active and historical rows by reservation ownership, session linkage and status. Verify physical occupancy independently before proposing a data change. A maximum timestamp alone does not classify a row.

Checkpoint A already accounts for both walk-in representations. Any later dependency retirement must preserve physical occupancy, backup and unlocated-visit evidence used in reservation admission. This handoff does not authorize a schema change or start Checkpoint B.

## Acceptance tests

| Scenario | Expected result |
|---|---|
| Walk-in check-in | One open session with actual entry time, no slot and no reservation allocation. |
| Random and explicit simulation | Both record the observed slot and applicable occupancy. Invalid, incompatible or occupied choices cannot displace an occupant. No walk-in allocation is inserted. |
| Competing scans and repeated delivery | At most one active occupant for a slot. Repeating a successful scan does not add another commitment. |
| Unlocated to located | The projected capacity calculation counts the same visit once before and after the scan. |
| Projection horizon and verified departure | The session stays open at projection expiry. Verified departure releases occupancy once and preserves the notification for legacy and session-only records. |
| Paid simulated departure | A matching settlement drives the established departure transition. Replay preserves the first departure time. The current test asserts legacy allocation release. Update that assertion with the producer change while retaining settlement, departure, outbox and physical-release checks. |
| Reservation-linked entry, scan and departure | A valid reservation allocation is still required for linked fulfillment, is occupied on scan and is released on departure. |

[Existing gate regressions](../../../../tests/Services/SmartParking.FacilityService.Tests/ReservationGateUseCaseTests.cs) · [Availability regressions](../../../../tests/Services/SmartParking.FacilityService.Tests/ReservationAvailabilityTests.cs)
