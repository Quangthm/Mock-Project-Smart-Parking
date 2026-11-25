# SPARK-273 B2 | Facility eligibility and quote metadata

**Owner:** FacilityService. Operations now consumes the approved contracts below. The current Facility producer still uses a site inventory snapshot for both reads. The coordinated development activation requires owner implementation and joint verification.

## Quote review

Keep the signed, uncached GET `/internal/reservation-dependencies/v1/slots/{slotId}` and its existing Operations service authorization. Return only `{slotId, siteId, tenantId}` from one coherent lookup of the selected live slot and its owning site/tenant. All identities must be nonempty. Reject incoherent relationships. Use the existing typed 404 `SLOT_NOT_FOUND` for a definitively absent slot. A missing route, inaccessible owner or invalid response provides no authoritative identity. Review pricing needs no inventory, eligibility judgment, hold or token. Existing site-qualified reads and acquire/complete/abort still serve legacy callers.

## Admission

Consume `facility-reservation-slot-v1`. Both services declare local records in `SmartParking.Contracts.ReservationAdmission`. Match the Operations definitions exactly.

```csharp
SlotCheckV1(Guid WorkflowId, Guid AttemptId, Guid AccountId, string Fingerprint,
    Guid RequestedSlotId, DateTimeOffset Start, DateTimeOffset End)
SlotDecisionV1(SlotCheckV1 Request, string Outcome, Guid? SiteId, Guid? TenantId)
```

Validate trusted transport, correlation, nonempty identities/fingerprint and both required UTC bounds with `Start < End`. Resolve the actual requested slot, verify its ID, and derive its site and tenant from Facility data. Check active structure and ancestry, CAR compatibility, and designated backup exclusion. Resolve slot, zone, floor and site restrictions inside Facility. Evaluate the actual parking interval. Operations owns the inter-booking buffer and local occupancy conflicts.

A restriction overlaps when its start precedes the requested end and its effective end is absent or follows the requested start. Evaluate scheduled and open-ended restrictions, including a future start. Honor authoritative cancellation and effective clearing/end instants. Adjoining half-open windows do not overlap. A positive decision requires complete owner evidence. Prove which records and enforcement states are authoritative. Return definitive business rejection for a known incompatible slot or overlapping restriction. Report dependency unavailability when the required facts cannot be established.

Send to `operations-reservation-slot-results-v1` with the same workflow correlation and the exact request echo. `ELIGIBLE` carries nonempty site and tenant. `INELIGIBLE` and `DEPENDENCY_UNAVAILABLE` carry null scope. Preserve bounded retries and duplicate semantics. Operations fences stale replies and accepts the first matching outcome. The new contract carries no Facility version.

## Implementation and verification

- In `SmartParking.FacilityService.API/Controllers/ReservationDependenciesController.cs`, change the slot-only response. In `SmartParking.FacilityService.Application/Reservations/`, revise `ResolveReservationSlotQuery.cs`, `ResolveReservationSlotHandler.cs` and `IReservationDependencyRepository.cs`. Add the selected-slot metadata DTO and separate eligibility query, handler and validator. Keep one named type per file.
- In `SmartParking.FacilityService.Persistence/ReservationDependencyRepository.cs`, implement selected identity and authoritative interval reads. In `SmartParking.FacilityService.Infrastructure/ReservationSlotConsumer.cs`, switch the admission call to that query. Update both records under `SmartParking.FacilityService.Application/Contracts/ReservationAdmission/`.
- `SmartParking.FacilityService.Domain/MaintenanceWindow.cs` currently requires a finite end and rejects scoped creation. `SmartParking.FacilityService.Persistence/PostgresMaintenanceWindowStore.cs` creates site-wide bounded ranges. Identify and demonstrate the actual source for scoped, future and open-ended facts, including cancellation, clearing, and uncertain enforcement. A database column or seeded test row alone does not demonstrate a supported writer. Request separate approval for any necessary writer, schema or D08 changes.
- Exercise the signed GET and generic versus owner-confirmed 404 in `ReservationSlotEndpointTests.cs`, repository behavior in `PostgresStructureWorkflowTests.cs`, and boundary cases in `MaintenanceWindowTests.cs`. Adjust `EmptyReservationDependencies.cs` to the changed repository API. Preserve token replay, legacy site-qualified and physical projection coverage.
- Demonstrate the real broker producer returning eligible for one interval and ineligible for a second interval on the **same slot**. Verify a different zone, exact adjacency, canceled/cleared, future and unbounded restrictions, wrong correlation, and missing/default/reversed bounds. A missing interval must never yield positive evidence. Verify the Operations result and absence of a hold after rejection. Coordinate with Payment before enabling the development workflow. Service-local type names and deserialization alone cannot prove interval support.
