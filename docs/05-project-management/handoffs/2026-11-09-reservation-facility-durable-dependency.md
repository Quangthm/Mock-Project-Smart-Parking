# Operations → Facility durable dependency

Approved design; producer and consumer implemented in the current Commit 1 draft, **build/runtime UNVERIFIED**. Updated 11 November 2026. The user selected Facility's durable slot-management pattern and a dedicated Operations credential. Facility implementation is included in this task; Payment producers remain with the Payment teammate. No full resource mirror or shared source project is introduced.

## Current call path

1. Operations persists `ReservationFacilityDependency` intent before HTTP, distinguishing ADMISSION and CONFIRMATION by unique workflow/purpose.
2. The independent dependency worker dispatches `AdvanceFacilityDependencyCommand`. Infrastructure calls Facility outside Operations database transactions and plate/site guards.
3. Facility's `ReservationDependenciesController` validates the dedicated bearer policy and dispatches MediatR. The handler uses `ReservationDependencyRepository` and the existing `IUnitOfWork`/`ParkingDbContext`; the Domain dependency owns state transitions.
4. Acquire uses the same Facility site advisory key as structure edits and physical Gate claims. A competing editor yields busy immediately, preventing a wait cycle with the opposite Facility→Operations protocol. Unacknowledged structure synchronization also blocks acquisition.
5. The acquired immutable snapshot contains eligible resource identities, compatible categories, live unit ancestry and fixed/dynamic backup configuration. MVP eligibility is active tenant/site, live units and compatible operational slots; no entrance/path graph is evaluated.
6. Under Account (new-booking restrictions), plate and site guards, Operations reads its claims/visits/quotas and evaluates capacity. Local payment hold or confirmation commits atomically with a READY dependency outcome. Confirmation also commits its receipt and outbox fact.
7. The worker replays the exact committed outcome to Facility and records acknowledgement. It remains active after the booking receipt is terminal. Fenced uncommitted work instead sends exact-token abort.

The snapshot is per-workflow evidence, not an editable or continuously mirrored Facility catalogue. Preview/review uses a read-only query with no hold. Admission's Facility hold is released after the Operations claim commits; it is not retained while the Driver fills the form or pays. Confirmation acquires its own dependency.

## Recovery rules

- Acquire replay with the same intent returns the original snapshot. A changed intent conflicts.
- HELD has no automatic time-only release. Operations first fences its local work, then durably retries abort. An unavailable coordinator may keep Facility edits blocked; alert/recover that workflow instead of deleting the row.
- Abort records a tombstone even for an unknown token. Delayed acquire cannot resurrect it.
- A READY Operations outcome must complete even after the deadline. Completion and abort are mutually exclusive; changed outcome fingerprints conflict.
- PostgreSQL evaluation strictly before T is the booking cutoff. Commit may finish later; state is exposed only after durable commit. Lost response replays the committed effect rather than selecting a new cutoff.
- Network errors retain durable work. Retry timing is worker scheduling, not an in-memory business policy.

## Existing Facility edits participate

[StructureEditHandlers](../../../src/Services/FacilityService/SmartParking.FacilityService.Application/Common/Synchronization/StructureEditHandlers.cs) owns authorization, recovery and distributed edit coordination in Application. It obtains the site gate, checks outstanding reservation dependencies, persists preparation before the opposite-direction HTTP call, then opens the local repository edit. Physical edits and their ready outcome commit atomically through the repository's edit transaction.

[StructureSynchronizationHandlers](../../../src/Services/FacilityService/SmartParking.FacilityService.Application/Common/Synchronization/StructureSynchronizationHandlers.cs) replays/fences durable outcomes through [the synchronization repository](../../../src/Services/FacilityService/SmartParking.FacilityService.Persistence/PostgresStructureSynchronizationRepository.cs). The recovery worker defaults to one second, rather than the previous daily sweep. Gate claim/release handlers own `IUnitOfWork` transactions and use `OperatorGateSlotRepository` with the same Facility site guard.

The existing opposite-direction protocol retains its previously established site-level recovery for abandoned preparing edits. The new Operations→Facility dependency uses exact-token tombstones only. Never use the legacy force-release endpoint to erase a reservation dependency.

No tenant-deactivation mutation was found in the current Facility source. A future tenant writer must acquire all affected site gates and honor held dependencies before changing eligibility. Direct SQL, bulk writes, direct DbContext changes, or new writers bypassing the protocol remain explicit bypasses; the schema adds no trigger to make them safe. Existing scheduled maintenance records are pending requests, not implemented physical enforcement. Allocation/reallocation and paid-customer refunds remain the later workflow.

## Transport and deployment

See [the v1 API specification](../../03-design/api/reservation-facility-dependency-api-design.md), [local key setup](../../../LOCAL-SETUP.md#57-dedicated-operations--facility-service-jwt-reservation), and [events/SignalR handoff and verification cases](2026-11-10-reservation-events-and-facility-integration.md). Operations signs short-lived RS256 JWTs with its own private key; Facility receives only the public key. UserService private keys and shared `X-Service-Key` are not accepted on these endpoints.

Schema declarations are in the original Facility and Operations scripts only. Existing volumes are not upgraded by `CREATE IF NOT EXISTS`; inventory/reconcile them explicitly. Do not reset a shared database or infer that a previously installed schema is aligned.

`ReservationFacilityDependencyTests` and `ReservationAvailabilityTests` are authored but NOT RUN. Verify acquire versus structure/physical writes, lost replies and restart at every boundary, immutable replay, abort tombstones, no circular waits, real outbox routing and error envelopes in the isolated deployment. Commercial acceptance/order/collection calls still need Commit 2 durable staging outside guarded transactions; the unavailable commercial adapter prevents live sales until that work and Payment delivery are complete.
