# SPARK-272 parking search API

The canonical discovery route is `GET /api/parking-search` on Operations. Its authorization classification is PUBLIC. It accepts no body and returns JSON with `Cache-Control: no-store`. Search provides observed availability for a published site and category. Reservation admission makes the final booking decision.

## Query

| Parameter | Requirement |
| --- | --- |
| `lat`, `lon` | Required finite decimal coordinates. Latitude is −90 to 90 and longitude is −180 to 180. Supply GPS or explicitly chosen manual coordinates. |
| `vehicleType` | Required `CAR` or `MOTORCYCLE`. Registered CAR slot availability is supported. Motorcycle pooled availability is unknown. |
| `radiusKm` | Optional, default 5, greater than 0 and at most 50. |
| `lotType` | Optional `outdoor`, `basement`, or `multi_storey`. |
| `availableOnly` | Optional Boolean, default false. Only a fresh positive count passes when true. |
| `startAt`, `endAt` | Optional paired ISO timestamps with offsets. They are normalized to UTC. `startAt < endAt`, duration at most 24 hours, start at most seven days ahead. Omit both for current availability. |
| `timeZone` | Optional, default and only accepted value `Asia/Ho_Chi_Minh`. |
| `minTotalVnd`, `maxTotalVnd` | Optional nonnegative integers for interval searches only. Require a known interval estimate within the specified bounds. |
| `limit` | Optional 1–50, default 20. |
| `cursor` | Optional opaque continuation from `data.nextCursor`. Preserve every filter and coordinate when continuing. Expires after five minutes or catalogue revision change. |
| `keyword` | Optional facility-name substring. Trimmed length at most 100 characters; empty or whitespace-only means no keyword filter. |
| `sort` | Optional `distance` (default), `price`, or `availability`. Values are lowercase and case-sensitive. |

Unknown query names are ignored by the current parser. An explicitly empty value for a recognized parameter other than `keyword` is invalid; omit an unused parameter. The service evaluates up to 1,000 catalogue sites, reads owner batches of at most 100 sites, and applies filters and pagination after assembly. A continuation re-evaluates availability and price.

### Keyword and ranking

`keyword` matches **published facility names only** as a literal substring after outer whitespace trimming, case folding, and Vietnamese accent folding, including `đ`/`d`. It does not search addresses, use fuzzy matching, or expand the geographic radius. The trimmed input limit is 100 characters. It combines with radius, vehicle category, lot type, `availableOnly`, interval and interval-price filters. For example, with real local coordinates substituted:

```text
/api/parking-search?lat=10.75&lon=106.70&vehicleType=CAR&radiusKm=5&keyword=B%E1%BA%BFn%20%C4%90%E1%BB%97
/api/parking-search?lat=10.75&lon=106.70&vehicleType=CAR&radiusKm=5&keyword=ben%20do&sort=price
/api/parking-search?lat=10.75&lon=106.70&vehicleType=CAR&keyword=&sort=availability
/api/parking-search?lat=10.75&lon=106.70&vehicleType=CAR&startAt=2026-12-01T09%3A00%3A00%2B07%3A00&endAt=2026-12-01T10%3A00%3A00%2B07%3A00&minTotalVnd=0&availableOnly=true&keyword=b%E1%BA%BFn&sort=price
```

`distance` orders nearest first by straight-line `distanceMeters`, then site ID. `price` orders the lowest **known fresh** `price.amountVnd` first; it compares hourly tariffs in current mode and total estimates in interval mode. `availability` orders the highest **known fresh** `availability.availableCount` first. For price and availability, unknown or stale values follow all known values. A genuine zero remains known and participates in ranking. Equal rank values break by distance, then site ID. Unknown or stale values also break by distance, then site ID.

### Cursor behavior

`data.nextCursor` is opaque. Repeat the same effective coordinates, filters, keyword and sort on continuation, including `limit`; changing a filter or sort with an old cursor returns 400 `INVALID_CURSOR`. The protected cursor expires after five minutes (409 `CURSOR_EXPIRED`), and a catalogue revision change returns 409 `SEARCH_CHANGED`. A cursor issued before this enhancement uses an earlier protection purpose and requires a fresh search.

Distance continuation retains its distance/site-ID keyset behavior while re-evaluating live owner data. Price and availability continuation additionally bind a digest of the ordered matching site IDs, primary rank values and distances. Relevant rank or membership changes, including changes beyond the displayed page, return 409 `SEARCH_CHANGED`; restart from page one rather than appending old results. Unrelated observation timestamp or trace-ID changes do not themselves alter that digest. Neither mode is a persistent result snapshot.

## Availability and pricing

Current CAR availability counts compatible, eligible, registered named slots at the evaluation instant using Facility physical state and Operations effective commitments, allocations, and located sessions. It does not apply advance quota, prospective buffer, or future occupancy projection.

Interval CAR availability requires Facility eligibility, compatibility, and maintenance coverage for the requested half-open stay `[S,E)`. Operations checks named-slot conflicts and applicable commercial reservation quota over `[S,E+B)`, where `B` is the site's `INTER_BOOKING_BUFFER`. It applies the shared SPARK-273 occupancy projection. No active quota adds no commercial cap. Active zero quota yields zero advance capacity. An unlocated walk-in creates no hypothetical named-slot claim.

Maintenance beginning exactly at `E` does not overlap Facility's stay check. A competing reservation beginning at `E` conflicts when `B > 0`; a competing reservation beginning at `E+B` does not. Payment supplies the current tariff (`kind=tariff`, `billingUnit=hour`) or an estimate for `[S,E)` (`kind=estimate`, `billingUnit=total`). Search creates no quote or Payment order.

The success envelope is `{success:true,data:{items,nextCursor,asOf},traceId}`. Each item includes public site ID, name, address, coordinates, lot type, supported categories, `distanceMeters`, `availability`, `price`, `sourceAsOf`, and `fieldStatus`. `availability` contains `kind` (`current` or `interval`), nullable `availableCount`, nullable `asOf`, and nullable `reason`. The count unit is vehicles/compatible named CAR slots, capped by the remaining commercial quota in interval mode. Zero is a known full result. Null means the count is unknown or stale. Published closure produces known zero with reason `closed`. A closed facility still appears when `availableOnly` is false.

`sourceAsOf.facility` is the catalogue observation. `sourceAsOf.operations` matches the Operations snapshot time used for a known count. `sourceAsOf.payment` is the price or price-batch observation. `fieldStatus` reports `fresh`, `unknown`, or `stale` independently for Facility, availability, and price. A general slot row update timestamp is metadata and does not prove physical-observation age. A missing estimate or tariff leaves `price=null` with an unknown reason. Unknown and stale availability keep `availableCount=null`. Full, unknown, stale, and unsupported outcomes remain distinct.

When present, `price` contains `{kind,amountVnd,billingUnit,policyVersion,asOf}`. `kind` is `tariff` in current mode and `estimate` in interval mode; `billingUnit` is `hour` or `total` respectively. Each `fieldStatus` member contains `{freshness,reason}`. Timestamps use offset-aware date-time values; nullable observations and reasons remain nullable rather than being replaced by a fabricated zero.

## Errors and handoff

Errors use `{success:false,code,message,errors:[],traceId}` without private owner details. Invalid input is 400 `VALIDATION_FAILED` or `INVALID_CURSOR`. Catalogue or publication drift and expired cursors return 409 `SEARCH_CHANGED` or `CURSOR_EXPIRED`. Rate limiting returns 429 `RATE_LIMITED`. Dependency failure, contradictory owner evidence, or an exceeded owner limit returns 503 `DEPENDENCY_UNAVAILABLE`. The five-second deadline returns 504 `DEPENDENCY_TIMEOUT`. Caller cancellation propagates.

Select a real slot through an authoritative slot read before booking. Preserve the site, category, and requested interval. The booking flow in [SPARK-273](../../04-testing/postman/SPARK-273-manual-test-guide.md) performs final account, plate, slot, policy, commitment, and payment checks. A displayed count does not reserve a slot or guarantee admission.

## Private Facility evidence

Operations calls `POST /internal/discovery/v1/slot-candidates:read` with its existing service JWT and `discovery:slot-candidates:read` permission. The body contains 1–100 distinct `sites[{siteId,configurationVersion}]`, `vehicleType:"CAR"`, UTC `at`, and optional UTC `startAt/endAt` for the requested stay. The body limit is 32 KiB. The reply has `success`, `traceId`, and `data{asOf,at,startAt,endAt,items}`. Each item contains verified `siteId`, `tenantId`, `configurationVersion`, `complete`, and slots with `slotId`, `outcome`, `physicalState`, and `slotUpdatedAt`. A complete empty list establishes zero candidates. Indeterminate evidence sets `complete=false`. A changed publication returns 409 `SEARCH_CHANGED`. Conflicting attribution or more than 2,048 registered slots in a batch fails instead of returning a partial list. This private evidence does not enter the public response.

Facility and Operations observe independent snapshots. Large batch latency under the five-second public deadline has not been measured.

## Historical booking origin

`GET /api/me/discovery/last-booked-location` is an authenticated Driver read. The caller supplies no Account ID. Operations derives the Account from the validated actor, selects one reservation in `CONFIRMED`, `ALLOCATED`, `PARKING`, or `COMPLETED` with a non-null `ConfirmedAt`, orders by `ConfirmedAt` descending then reservation ID descending, and asks Facility for the selected site's current published location. It does not paginate history or try older bookings when that site's location is missing, withdrawn or unusable. No-store applies to responses. The coordinates describe a **previous booking site**, not the Driver's live location.

Successful responses use the normal `{success:true,data,traceId}` envelope. Illustrative IDs and coordinates below are examples, not fixture values:

```json
{"success":true,"data":{"siteId":"00000000-0000-0000-0000-000000000001","siteName":"Bến Đỗ Trung Tâm","latitude":10.75,"longitude":106.7,"confirmedAt":"2026-11-20T08:00:00+00:00"},"traceId":"example-trace"}
```

```json
{"success":true,"data":null,"traceId":"example-trace"}
```

No qualifying reservation requires no Facility call. A missing, withdrawn or unusable selected site yields `data:null`. Missing authentication is 401 `UNAUTHENTICATED`; a role other than Driver is 403 `FORBIDDEN`. Facility unavailability or a malformed/contradictory owner reply is a 503 dependency error (`OWNER_UNAVAILABLE` or `OWNER_CONTRACT_INVALID`); a missing Operations reservation schema can return 503 `PROTOTYPE_SCHEMA_NOT_READY`. These failures must not be interpreted as absence of history.

### Private Facility location read

Operations sends only the trusted site ID to `GET /internal/discovery/v1/sites/{siteId}/location`; it sends no Account ID, reservation ID, plate or vehicle history. Facility requires the existing Operations service JWT with the dedicated `discovery:site-location:read` permission. Successful replies use `{success:true,data,traceId}`, where `data` is either `{siteId,siteName,latitude,longitude}` or `null`. `null` authoritatively means no currently published, active, usable site location. Facility verifies publication, active undeleted site and tenant, matching tenant attribution, nonempty name and valid latitude (−90 to 90) and longitude (−180 to 180). Authentication, routing, transport and malformed-reply errors are failures, not authoritative absence. This read performs no writes and does not alter the SPARK-273 reservation Saga.
