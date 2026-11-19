# SPARK-273 develop merge review

The five conflicts between SPARK-273 `0cb90d4cd` and develop `8ce02cd61` are resolved. The combined test hosts serve the incoming discovery functionality alongside the reservation routes. The merge is staged for review. The [implementation catalogue](2026-11-19-SPARK-273-develop-refactor-catalogue.md) records the source files, behavior worth keeping, reason for each revision and checks needed before retirement.

## Resolution and reservation impact

| Conflict | Resolution and reason |
| --- | --- |
| Operations API Program.cs | Keep the thin entry point. Register and map develop's public parking search in OperationHost alongside SPARK-273's reservation endpoints, messaging and progress setup. Register dependencies needed by assembly-scanned legacy handlers. The active customer list/detail routes have one owner. |
| Facility API Program.cs | Keep the thin entry point. Register and map develop's discovery catalogue in FacilityHost alongside the signed reservation slot lookup. |
| BookingView.cs and BookingContracts.cs | Retain the reservation request ID used to recover progress and the assistant's vehicle display projection. List and detail derive both from the same scoped booking. |
| Reservation first-slice API design | Keep the receipt and progress contracts and document the assistant's scoped reservation reads. |

Operations, Facility and UserService now use ASP.NET Core's registered IExceptionHandler on the affected active routes. Operations and Facility translate discovery failures centrally and use their API error writers. They preserve discovery status codes, cache controls and applicable retry headers. UserService separates mapping into ApiExceptionMapper. Operations and Facility still map within their handlers. The unused Operations own-read module contains a separate error pipeline, tracked under R1. This review covers the affected routes and host wiring.

## Verification

| Test project and class filters | Result | Evidence |
| --- | --- | --- |
| SmartParking.UserService.Tests · ParkingSearchHttpTests, OwnReservationReadHttpTests | 39 passed | Public search and authenticated customer-read HTTP contracts in test hosts |
| SmartParking.FacilityService.Tests · DiscoveryCatalogueTests, ReservationSlotEndpointTests | 8 passed | Catalogue paging, publication and withdrawal, errors and throttling, signed slot lookup |
| SmartParking.OperationService.Tests · ReservationHistoryTests | 10 passed | Scoped history, pagination and list/detail parity, including the stored request ID and vehicle label |

Use `dotnet test tests/Services/<project> --filter 'FullyQualifiedName~<class>'` with the project and class names above. Two filters can be joined with `|FullyQualifiedName~`. The sandboxed VSTest handshake timed out on Facility and Operations. Both completed outside the sandbox using the built binaries. These are merge-specific results. The [Slice 3 handoff](2026-11-19-SPARK-273-slice-3.md) records the earlier concurrency, saga and reconnect verification. Full live-stack recovery and deployed database compatibility were outside this verification.

## Work left untouched and decisions for review

* **Legacy reads (R1).** The old Operations handlers, persistence store and alternative route setup remain in source. Confirm direct consumers before removing this group and its DI registrations.
* **Discovery design (R2, R3, R7).** Facility's catalogue workflow, transaction and cursor contracts remain in place. Choose when to move catalogue reads and writes behind MediatR, split the grouped types, and narrow the exceptions classified as dependency outages.
* **Availability (R4).** Operations retains the unsupported-capacity fallback. Choose the authoritative evidence and freshness requirements before enabling positive availability claims. A search result does not reserve capacity.
* **Database and fixtures (R5, R6).** Incoming migration scripts, mounts and demo seed changes remain as supplied by develop. Decide how to support fresh and populated databases and whether 48 CAR spaces with quota 4 is the intended demo fixture.
* **Other work.** Chatbox/UI, IoT assistant orchestration, UserService, Payment, physical check-in, and the remaining Slice 2/4 implementation received no edits as part of this resolution. Existing developer edits to repository guidance, the SPARK-243 session and the tariff seed helper remain separate. No database migration, merge commit, push or deployment was performed.
