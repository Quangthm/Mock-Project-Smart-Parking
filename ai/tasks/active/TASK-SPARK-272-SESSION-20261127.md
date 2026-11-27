# Session Review — 2026-11-27

## 1. Session Information

SPARK-272, main agent, branch `feature/SPARK-272-driver-view-parking-information-and-availability`. Active.

## 2. Objectives

Implement the approved Operations rule and read-only facts extraction for Checkpoint 1 and the private Facility candidate read for Checkpoint 2.

## 3. Work Completed

Extracted a single capacity query used by the guarded admission/physical readers and by the CAR discovery reader. At each evaluation boundary the same domain rule now identifies claims, occupied and uncertain slots, and commercial quota usage. Discovery intersects whole-interval free slots and caps the result by the smallest remaining quota.

The new reader accepts a trusted tenant, site, complete eligible candidate IDs and an optional stay. It obtains database time, local edit/site state and capacity facts in a read-only repeatable-read transaction. Interval mode resolves the existing policy, checks the booking window, adds the prospective buffer, and reads the optional CAR quota. Current mode skips policy and quota. Evaluation occurs after releasing the snapshot. The repository issues no network calls.

## 4. Technical Decisions

Admission and late reacquisition still use the actual proposed row and their original account/plate/site checks as applicable. The gate retains exclusions for its own reservation/session and its guarded physical read. Discovery includes every relevant session. A live unassigned CAR commitment with no effective allocation, or uncertain evidence on a candidate, gives an unknown aggregate. An unlocated walk-in creates no speculative claim.

One private query implementation suffices. Admission and the batch count share the same start-inclusive/end-exclusive boundaries, allocation precedence, occupancy projection and quota contribution per live matching named commitment. No hold, session, allocation, account or quote is constructed for discovery. Public integration remains a separately approved checkpoint.

## 5. Files Changed

| State | Repository path |
|---|---|
| Modified | `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ReservationAvailabilityFacts.cs` |
| Removed after extraction | `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ResolveReservationPolicies.cs` |
| Modified | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationAvailability.cs` |
| Removed after extraction | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicies.cs` |
| Modified | `src/Services/OperationService/SmartParking.OperationService.Persistence/PersistenceDependencyInjection.cs` |
| Modified | `src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/ReservationAvailabilityRepository.cs` |
| Added | `ai/tasks/active/TASK-SPARK-272-SESSION-20261127.md` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/CarCapacityRead.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/IDiscoveryCapacityReader.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/IReservationPolicyRepository.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ResolveReservationPoliciesHandler.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Application/Reservations/ResolveReservationPoliciesQuery.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/EffectiveReservationPolicy.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/PolicyBound.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/PolicyRevisionReference.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicyResolver.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicyRevision.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicyUnavailableException.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/SlotAvailabilityAtInstant.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/CapacityFactsQuery.cs` |
| Added | `src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/DiscoveryCapacityReader.cs` |
| Added | `tests/Services/SmartParking.OperationService.Tests/DiscoveryAvailabilityTests.cs` |
| Added | `tests/Services/SmartParking.OperationService.Tests/DiscoveryCapacitySnapshotTests.cs` |

## 6. Issues and Blockers

No fixture connection was preconfigured. The focused Operations run uses a private loopback PostgreSQL 18 cluster and the existing 03b initializer. The first Windows launcher stalled after database startup; it was stopped and replaced with a launcher that redirects output to files. An initial test compile exposed a read-only test-fixture assignment; the fixture now uses ordinary claim intervals.

Owner snapshots are eventually consistent. A complete registered CAR candidate universe and Facility eligibility/physical evidence are still required at integration. Candidate-set size and multi-site latency under the search deadline have not been measured.

## 7. Validation Results

Checkpoint 1 completed. Operations API build succeeded (0 errors, 6 warnings). Focused Operations tests on a disposable PostgreSQL 18 instance passed 23/23, including the two-connection read-only repeatable-read test. Existing availability and gate regressions passed 26/26. Total: 49 distinct passes, zero failures or skips. Build output included existing compiler and NuGet audit warnings. The temporary fixture was stopped and removed. The sandboxed build initially exited without a compiler error; the single-node build with SDK/cache access succeeded.

## 8. Git Status

Starting HEAD `7e4ff8963f6050796440d29428535d1f753d9884`. Leave changes uncommitted.

## 9. Handoff / Next Steps

Checkpoint 2 approved on November 27. Proposed minimum private Facility read: authenticated Operations caller, bounded published site IDs plus expected publication versions, CAR, evaluation instant or start/end of the requested stay. Reply per site with trusted tenant/site, matching publication version, observation time, coverage/completeness and distinct registered slot IDs with structural/maintenance eligibility and existing physical state. Distinguish a definitive exclusion from incomplete or indeterminate evidence. Keep tenant and slot evidence private; retain the existing Saga reply.

Defer Facility and public-API wiring, Postman and manual browser/chatbot flows, multi-service load and end-to-end verification to the later approved checkpoints and tester.

## 10. Context for Next Session

Checkpoint 1 is complete. Checkpoint 2 results follow. Integration with public search requires Checkpoint 3 approval.

## 11. Checkpoint 2 — Facility candidate read

Completed on November 27. Operations can request Facility evidence through `POST /internal/discovery/v1/slot-candidates:read`. The caller supplies `sites[{siteId,configurationVersion}]`, `vehicleType="CAR"`, a UTC `at`, and optionally a paired UTC `startAt` and `endAt` for a stay of up to 24 hours. The instant selects effective maintenance in current mode. Interval mode checks overlap across the half-open stay. The request accepts 1–100 distinct published sites and up to 32 KiB. The route uses the existing Operations RS256 authentication scheme and a dedicated `discovery:slot-candidates:read` permission. Environments need the existing Operations public key and enabled service authentication.

The controller sends the validated request through MediatR to `ReadDiscoverySlotCandidatesHandler` and `DiscoverySlotCandidateReader`. The success envelope contains `success`, `traceId` and `data{asOf,at,startAt,endAt,items}`. Each item contains the verified site, tenant, publication version, `complete`, and slots ordered by ID. Each slot carries its ID, eligibility outcome, existing physical state and slot row update time. The batch returns sites in request order. `asOf` comes from the first data statement in the snapshot. `slotUpdatedAt` is the row timestamp and does not establish the age of a physical observation.

The reader validates CAR publication versions and checks the actual active site and tenant before reading all non-deleted registered slot rows for those sites in the same read-only PostgreSQL repeatable-read transaction. Slot attribution is checked against the verified site even for rows ultimately excluded by category. The shared SQL applies explicit compatibility rows with the existing legacy fallback, ancestry and cycle checks, maintenance decisions, backup exclusion and physical state. The selected-slot reservation reader uses the same decision with its original interval and eligible-only scope reply. Admission still calls the selected-slot reader directly.

A verified empty site returns `complete=true` and an empty list. Definitive incompatibility is excluded. A compatible backup or other definitive exclusion is returned as `INELIGIBLE`. Uncertain evidence, including a missing compatibility declaration or unknown physical state, yields `DEPENDENCY_UNAVAILABLE` and `complete=false` for its site. An `ELIGIBLE` slot can still be physically occupied. Operations must consider both fields when calculating availability. Missing, withdrawn or changed publications return HTTP 409 `SEARCH_CHANGED`. A conflicting slot/site tenant or publication/site tenant returns HTTP 503 `CATALOGUE_UNAVAILABLE`. The reader checks a 2,049th-row sentinel and fails the batch above 2,048 registered slots across all requested sites, including incompatible slots.

### Changed files

Paths below are relative to `src/Services/FacilityService/` for production code and `tests/Services/SmartParking.FacilityService.Tests/` for tests.

| Area | Added | Modified |
|---|---|---|
| `SmartParking.FacilityService.API/` | `Controllers/DiscoverySlotCandidatesController.cs` | `DiscoveryCatalogueEndpoints.cs`, `FacilityHost.cs`, `Middleware/FacilityExceptionHandler.cs` |
| `SmartParking.FacilityService.Application/Discovery/` | `DiscoveryCandidateBatch.cs`, `DiscoveryCandidateSite.cs`, `DiscoveryCandidateSlot.cs`, `DiscoverySiteRequest.cs`, `IDiscoverySlotCandidateReader.cs`, `ReadDiscoverySlotCandidatesQuery.cs`, `ReadDiscoverySlotCandidatesHandler.cs`, `ReadDiscoverySlotCandidatesValidator.cs` | |
| `SmartParking.FacilityService.Infrastructure/` | | `ReservationServiceAuthentication.cs` |
| `SmartParking.FacilityService.Persistence/` | `DiscoverySlotCandidateReader.cs`, `SlotEligibilitySql.cs` | `ReservationSlotReader.cs` |
| Facility tests | `DiscoverySlotCandidateTests.cs` | `OccupancyProjectionFixture.cs` |

This session file records both checkpoints. The Facility fixture accepts optional existing DDL so the new tests can load the publication schema.

### Verification and handoff

The Facility API build passed with zero errors and four NuGet audit warnings. A filtered Facility test run on an isolated PostgreSQL 18 instance passed 8/8 in 22 seconds with zero failures or skips. The six new tests covered batch attribution and completeness, half-open maintenance, indeterminate evidence, publication changes and oversize failure, request validation, and scoped JWT enforcement through the Facility host. Two existing regressions passed: `AdmissionReadsOnlyApplicableEffectiveMaintenanceForTheSelectedInterval` exercises the extracted eligibility SQL through the existing Saga consumer, and `SignedSlotOnlyReadDerivesScopeAndDistinguishesMissingSlot` exercises the reservation route under the shared JWT policy registration. The fixture was stopped and removed.

Checkpoint 1's successful Operations build and 49 distinct passing tests, including the 26 availability/gate regressions and two-connection PostgreSQL snapshot verification, were retained without re-execution. Cumulative total through Checkpoint 2 is 57 distinct passes.

Ready for Checkpoint 3 review. Facility and Operations observe independent snapshots. A physical-state change may occur between reads. The inventory cap can reject a large batch or a single oversized site, and latency under the search deadline is unmeasured. Checkpoint 3 must use the discovery permission, handle publication conflicts and incomplete evidence, and preserve Operations ownership of reservation calculations. Cross-service integration, Postman, performance and manual acceptance are deferred to the final tester handoff. Public search integration awaits approval. Work remains uncommitted.
