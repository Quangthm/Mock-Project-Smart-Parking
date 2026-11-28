# Reservation gate check-in and slot observation

## 1. Session information

2026-11-27, develop at 0a1655677c3369441e9b31380df7fd7a3b5679b9. Frontend acceptance pending.

## 2. Objectives

Implement approved checkpoints A through C from the supplied gate correction.

## 3. Work completed

Extracted the reservation arrival and departure logic. The scan uses the supplied slot.

## 4. Technical decisions

Gate entry uses the existing reservation transition. Scanning associates a matching effective allocation. Departure selects session-owned active rows and unlinked reserved rows belonging to the completing reservation within its tenant and site.

## 5. Files changed

| Area | Updated | Added |
|---|---|---|
| OperationService Application, ReservationGate | GateWriteHandlers.cs, ScanParkingSessionCommandHandler.cs, GateSlotOccupancy.cs | CheckInReservationCommandHandler.cs, GateDeparture.cs |
| OperationService Application/Persistence | ISlotAllocationRepository.cs, SlotAllocationRepository.cs | |
| FacilityService tests | ReservationGateUseCaseTests.cs | GateTestRepositories.cs, GateTestSlotLease.cs |
| OperationService tests | | SlotAllocationRepositoryTests.cs |
| Session | | This file |

The grouped GateWriteHandlers.cs retains the two existing checkout handlers and their projection helper. Only the two changed types were extracted.

## 6. Issues and blockers

The shared capacity evaluator can require reconciliation after a different-slot scan. Facility release uses slot identity only, including on departure replay.

## 7. Validation results

The final OperationService API build passed with zero errors and four NU1900 warnings from unavailable NuGet advisory metadata. The FacilityService test project also reported existing AutoMapper NU1903 advisories.

- Gate mediator and shared capacity tests passed, 49/49. Filter: `FullyQualifiedName~ReservationGateUseCaseTests|FullyQualifiedName~ReservationAvailabilityTests`.
- Payment-driven departure/realtime and allocation repository tests passed, 5/5. Filter: `FullyQualifiedName~CheckoutRealtimeTests|FullyQualifiedName~SlotAllocationRepositoryTests`.
- Builds used `--no-restore -m:1 -p:BuildInParallel=false`. The sandboxed test hosts timed out connecting. The successful runs used the compiled assemblies outside the sandbox.
- The tests use repository/Facility doubles and EF in-memory persistence. Live PostgreSQL, Facility and frontend acceptance are [UNVERIFIED].

## 8. Git status

The pre-existing scripts/seed-site-car-tariff.ps1 edit belongs to the user. All implementation changes are unstaged.

## 9. Handoff

PENDING USER VERIFICATION. Exercise no allocation, scan A when assigned A, scan B when assigned A, and departure with no scan. Confirm gate entry leaves physical location unknown, scans show the actual bay, and departure releases the observed bay.

CheckInOut.tsx line 1020 claims immediate occupancy. Its reservation event at line 391 uses updatedBooking.slotId and ignores the development picker. useLotStatus.ts lines 496 and 540 prefer this ID among available candidates and fall back to a random compatible candidate.

The tested mismatch causes OCCUPANCY_RECONCILIATION_REQUIRED for competing claims on either bay while the differing facts are effective. The pre-existing Facility release contract cannot identify a new occupant after slot reuse. A session-aware release contract requires separate approval.

## 10. Recovery context

The request excludes reallocation, new events, schema changes and frontend implementation. Exercise the gate mediator fixtures in the FacilityService test project.
