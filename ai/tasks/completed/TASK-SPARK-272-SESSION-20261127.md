# Session Review — 2026-11-27

## 1. Session Information

SPARK-272, main agent, branch `feature/SPARK-272-driver-view-parking-information-and-availability`. Completed and accepted for the AI backend implementation phase.

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
## Checkpoint 3 — approved interval ownership

Facility checks structural eligibility and maintenance on the requested half-open stay [S,E). Payment prices the same stay. Operations applies its site buffer B to named-slot conflicts and quota on [S,E+B). Admission sends intent.Start and intent.End in ProcessAdmissionSignalHandler and constructs the buffered claim in CommitQuoteHoldHandler. The earlier proposal to reorder reads and split intervals was declined.

The chatbot client requires a fresh timestamp matching sourceAsOf.operations for a known count, consistent field state, positive availableOnly results and ordering by distance then site ID. The Driver web client still queries /parking-lots in FE/src/lib/driverDiscoveryApi.ts. Moving Driver discovery to the canonical Operations endpoint remains with SPARK-276.

### Checkpoint 3 implementation and verification

The existing GET /api/parking-search handler reads the paged catalogue and processes batches of up to 100. Published closures retain their existing result. For applicable CAR sites the owner client sends the requested UTC stay or current instant to the Facility candidate route with a one-minute Operations JWT and the dedicated permission. Its HTTP client retains the 512 KiB response limit, sanitized errors and search deadline. The handler validates echoed times, site IDs, versions, tenant attribution, completeness, unique slot identities across the search and bounded slot outcomes. A Facility 409 propagates as SEARCH_CHANGED. A site or batch exceeding the Facility 2,048 registered-slot limit can fail the search. There is no splitting or retry.

Complete fresh CAR evidence enters the read-only Operations snapshot. The reader compares Facility occupied eligible slots with open sessions located on the eligible candidates. A disagreement yields unknown. The shared availability rule handles commitments, allocations, located visits and quota. Current counts use the instant. Interval counts intersect free slots across the buffered Operations interval and apply the minimum remaining commercial quota. A missing quota adds no commercial limit and zero quota gives zero. Pricing still receives the requested stay. The original projection, filters, distance sort and cursor complete the search. Motorcycle retains its catalogue, pricing and published closure handling with unknown pooled capacity. No reservation, session, allocation, financial order or quote is created by search.

Facility and Operations snapshots can diverge between reads. Freshness checks cover the Facility snapshot and Operations database time through delivery. A known Operations count carries its snapshot time in both availability.asOf and sourceAsOf.operations. The slot row timestamp is metadata. It cannot establish physical observation age. Existing Operations service signing and Facility key verification must be enabled and the configured Facility origin must reach the private route. Large-batch latency under the five-second deadline remains unmeasured.

The fixed-resource evaluator, unsupported source and their old standalone evaluator tests were retired. A C# caller search returned no references to the retired source, evaluator entry point or evidence interface. Validation moved to the owner contract and search HTTP tests; the shared-rule and read-only database baseline remains recorded above.

The Operations API and changed projects built through the focused test runs. The final parking-search HTTP suite passed 41/41. An earlier run passed 34/36 and revealed a multi-site fixture reusing slot IDs. The fixture now supplies distinct identities outside its deliberate duplicate case. The final run also covers malformed tenant, bounds, contradictory slot evidence, empty capacity and motorcycle search. The unchanged chatbot client's focused owner-response contract checks passed 9/9. The new Facility/Operations checkout-boundary test passed 1/1 on disposable PostgreSQL, and the Operations physical-evidence agreement test passed 1/1 on a separate disposable database. The boundary test invokes the two Facility persistence readers and the two shared Operations domain entry points; the Saga's requested-end and buffered-claim wiring were verified in source. Builds emitted existing dependency-audit and compiler warnings.

Checkpoint 3 adds 52 distinct passes. The 57 earlier passes were retained without re-execution. Cumulative result is 109 distinct passes, zero outstanding failures or skips. No architecture deviation or blocker remains. Review precedes Checkpoint 4. Cross-service runtime, Postman, browser/Driver integration, live chatbot, load and comprehensive reservation/gate scenarios remain deferred to the final tester handoff.

### Checkpoint 3 file inventory

- Modified `ai/tasks/active/TASK-SPARK-272-SESSION-20261127.md`
- Modified `src/Services/OperationService/SmartParking.OperationService.API/DiscoveryHosting.cs`
- Removed `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/CapacitySource.cs`
- Modified `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/IDiscoveryCapacityReader.cs`
- Removed `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/ParkingSearch.cs`
- Removed `src/Services/OperationService/SmartParking.OperationService.Domain/DiscoveryCapacity.cs`
- Modified `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Clients/DiscoveryOwners.cs`
- Modified `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/ReservationFacilityClient.cs`
- Modified `src/Services/OperationService/SmartParking.OperationService.Persistence/Repositories/DiscoveryCapacityReader.cs`
- Modified `tests/Services/SmartParking.FacilityService.Tests/DiscoverySlotCandidateTests.cs`
- Modified `tests/Services/SmartParking.OperationService.Tests/DiscoveryCapacitySnapshotTests.cs`
- Removed `tests/Services/SmartParking.UserService.Tests/DiscoveryCapacityTests.cs`
- Modified `tests/Services/SmartParking.UserService.Tests/ParkingSearchHttpTests.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/Availability.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/CandidateBatch.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/CandidateSite.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/CandidateSlot.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/DiscoveryCursor.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/DiscoveryPolicy.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/DiscoveryPrice.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/DiscoverySite.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/FacilityPage.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/FacilityWindow.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/FieldState.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/FieldStates.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/IDiscoveryCursors.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/IDiscoveryOwners.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/ParkingSearchHandler.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/ParkingSummary.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/PriceBatch.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/SearchFilter.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/SearchPage.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/SearchParking.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/SitePrice.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Application/Discovery/SourceTimes.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Clients/DiscoveryEnvelope.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Clients/DiscoveryOrigins.cs`
- Added `src/Services/OperationService/SmartParking.OperationService.Infrastructure/Reservations/FacilityServiceToken.cs`
- Added `tests/Services/SmartParking.UserService.Tests/SearchTestCapacity.cs`
- Added `tests/Services/SmartParking.UserService.Tests/SearchTestClock.cs`
- Added `tests/Services/SmartParking.UserService.Tests/SearchTestHost.cs`
- Added `tests/Services/SmartParking.UserService.Tests/SearchTestLogs.cs`
- Added `tests/Services/SmartParking.UserService.Tests/SearchTestOwners.cs`

## Checkpoint 4 — final delivery

Checkpoint 3 was approved. A focused source review confirmed the canonical public Operations `GET /api/parking-search`, Facility publication and structural candidate read, Operations shared SPARK-273 availability evaluation, Payment-owned price read, and existing public response and chatbot validation. The Facility request and Payment estimate use the requested half-open stay `[S,E)`; Operations applies its site booking buffer to named-slot conflicts and applicable quota over `[S,E+B)`. Search uses read-only local facts and owner HTTP reads. Reservation admission remains a separate authoritative path. The obsolete fixed-resource evaluator and unsupported production source are gone. This review made no production-code changes.

Added `docs/03-design/api/SPARK-272-parking-search.md` and marked the older S06 availability proposal as historical in `docs/03-design/api/assistant-s06-availability-authority.md`. Added `docs/04-testing/postman/SPARK-272-Parking-Search.postman_collection.json`, `SPARK-272-Local.postman_environment.example.json`, and `SPARK-272-manual-test-guide.md`. Added frontend and chatbot handoffs in `docs/05-project-management/handoffs/SPARK-276-discovery-frontend.md` and `SPARK-272-chatbot-search.md`. Added `docs/04-testing/reports/SPARK-272-final-tester-handoff.md`.

The Postman environment contains blank coordinates, interval, price bounds and cursor. Its localhost Operations URL follows the existing SPARK-273 local example and must be checked against the running stack. Fixture-specific requests describe their prerequisites and do not assert invented counts or prices. Postman and live acceptance were not run. Checkpoint 4 only validated artifact syntax and repository diff hygiene. The cumulative automated result remains 109 distinct passes, zero outstanding failures and zero skips; no earlier tests or builds were repeated.

Backend is ready for manual acceptance, subject to the tester's runtime checks. No deployment readiness claim is made. Remaining limitations are independent snapshots and accepted eventual consistency, lack of physical-observation age in slot row timestamps, 100-site/2,048-slot Facility bounds with failure on overflow, unmeasured large-batch latency under five seconds, unsupported motorcycle pooled counts, unlocated walk-ins without a speculative named claim, and SPARK-276 Driver integration. Final admission remains necessary. There was no deviation from the approved interval ownership or service contracts.

Suggested commit summary for the user's later Git action: `feat(discovery): integrate CAR availability and add SPARK-272 handoffs`.

Suggested pull request description for the user's later Git action: `Integrates Facility's registered CAR candidates with Operations shared SPARK-273 availability in the canonical parking search. Reuses Payment pricing, retires the obsolete capacity evaluator, and documents public and private contracts, Postman scenarios, and tester handoffs. Automated verification across checkpoints passed 109 distinct tests. Manual cross-service, UI, chatbot, and load acceptance remains with the tester.`

## Completion classification

The user accepted all four SPARK-272 implementation checkpoints and explicitly ended further AI implementation in this session. The 109 distinct automated passes remain the recorded baseline; no tests were rerun for this classification. Manual acceptance, full cross-service runtime checks, live chatbot verification, performance testing and SPARK-276 frontend integration remain NOT RUN or delegated as recorded in the final tester handoff. The accepted architectural limitations remain. The worktree is left for user review and commit, with no staging, commit, push, pull request or Jira status change by the agent.
