# SPARK-273 B2 | Facility eligibility and quote metadata

**Implementation scope:** SPARK-273 owns the reservation metadata endpoint and interval-aware admission integration. SPARK-244 owns maintenance writers, lifecycle, enforcement, and affected-slot publication. Coordination on those capabilities and D08 remains an activation gate.

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

Use one Facility database snapshot for live ancestry, structural scope, CAR compatibility, backup designation and applicable restriction evidence. An acknowledged, bounded, uncancelled site-wide window blocks an overlapping interval even if it starts in the future. Pending/failed or materially inconsistent applicable evidence yields `DEPENDENCY_UNAVAILABLE`. A separate definitive conflict may still yield `INELIGIBLE`. Adjoining half-open intervals are eligible with respect to the window. Unsupported scoped/open-ended representations are not promoted to effective restrictions on this branch.

Send to `operations-reservation-slot-results-v1` with the same workflow correlation and the exact request echo. `ELIGIBLE` carries nonempty site and tenant. `INELIGIBLE` and `DEPENDENCY_UNAVAILABLE` carry null scope. Preserve bounded retries and duplicate semantics. Operations fences stale replies and accepts the first matching outcome. The new contract carries no Facility version.

## Implementation and verification boundary

- In `SmartParking.FacilityService.API/Controllers/ReservationDependenciesController.cs`, change the slot-only response. In `SmartParking.FacilityService.Application/Reservations/`, revise `ResolveReservationSlotQuery.cs`, `ResolveReservationSlotHandler.cs` and `IReservationDependencyRepository.cs`. Add the selected-slot metadata DTO and separate eligibility query, handler and validator. Keep one named type per file.
- In `SmartParking.FacilityService.Persistence/ReservationDependencyRepository.cs`, implement selected identity and authoritative interval reads. In `SmartParking.FacilityService.Infrastructure/ReservationSlotConsumer.cs`, switch the admission call to that query. Update both records under `SmartParking.FacilityService.Application/Contracts/ReservationAdmission/`.
- The existing maintenance store writes bounded site-wide windows as `pending` and can cancel them. Admission reads `structure_maintenance_windows` only for the selected interval. The test uses the existing writer/canceller and seeds `acknowledged` to exercise the effective state. It does not establish a producer for acknowledged, scoped, open-ended or emergency restrictions.
- `ReservationSlotEndpointTests.cs` exercises signed HTTP and the typed missing-slot response. `PostgresStructureWorkflowTests.cs` exercises real metadata reads and the actual Facility consumer with the same slot, different intervals, adjacency, uncertain and cancelled windows, malformed bounds, CAR compatibility, occupied-slot handling, and backup exclusion. Existing site-qualified facts and token replay retain their own coverage.
- Before production activation, coordinate the teammate's authoritative maintenance and restriction event contracts. The scheduled-maintenance conflict policy and the future event shape remain open. See the active SPARK-273 session for the actual extent of integration evidence.
