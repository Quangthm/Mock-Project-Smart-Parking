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

Unknown query names retain the current parser behavior. Results within the radius are sorted by straight-line great-circle distance in meters, then site ID. The service evaluates up to 1,000 catalogue sites, reads owner batches of at most 100 sites, and applies filters and pagination after assembly. A continuation re-evaluates availability and price.

## Availability and pricing

Current CAR availability counts compatible, eligible, registered named slots at the evaluation instant using Facility physical state and Operations effective commitments, allocations, and located sessions. It does not apply advance quota, prospective buffer, or future occupancy projection.

Interval CAR availability requires Facility eligibility, compatibility, and maintenance coverage for the requested half-open stay `[S,E)`. Operations checks named-slot conflicts and applicable commercial reservation quota over `[S,E+B)`, where `B` is the site's `INTER_BOOKING_BUFFER`. It applies the shared SPARK-273 occupancy projection. No active quota adds no commercial cap. Active zero quota yields zero advance capacity. An unlocated walk-in creates no hypothetical named-slot claim.

Maintenance beginning exactly at `E` does not overlap Facility's stay check. A competing reservation beginning at `E` conflicts when `B > 0`; a competing reservation beginning at `E+B` does not. Payment supplies the current tariff (`kind=tariff`, `billingUnit=hour`) or an estimate for `[S,E)` (`kind=estimate`, `billingUnit=total`). Search creates no quote or Payment order.

The success envelope is `{success:true,data:{items,nextCursor,asOf},traceId}`. Each item includes public site ID, name, address, coordinates, lot type, supported categories, `distanceMeters`, `availability`, `price`, `sourceAsOf`, and `fieldStatus`. `availability` contains `kind` (`current` or `interval`), nullable `availableCount`, nullable `asOf`, and nullable `reason`. The count unit is vehicles/compatible named CAR slots, capped by the remaining commercial quota in interval mode. Zero is a known full result. Null means the count is unknown or stale. Published closure produces known zero with reason `closed`. A closed facility still appears when `availableOnly` is false.

`sourceAsOf.facility` is the catalogue observation. `sourceAsOf.operations` matches the Operations snapshot time used for a known count. `sourceAsOf.payment` is the price or price-batch observation. `fieldStatus` reports `fresh`, `unknown`, or `stale` independently for Facility, availability, and price. A general slot row update timestamp is metadata and does not prove physical-observation age. A missing estimate or tariff leaves `price=null` with an unknown reason. Unknown and stale availability keep `availableCount=null`. Full, unknown, stale, and unsupported outcomes remain distinct.

## Errors and handoff

Errors use `{success:false,code,message,errors:[],traceId}` without private owner details. Invalid input is 400 `VALIDATION_FAILED` or `INVALID_CURSOR`. Catalogue or publication drift and expired cursors return 409 `SEARCH_CHANGED` or `CURSOR_EXPIRED`. Rate limiting returns 429 `RATE_LIMITED`. Dependency failure, contradictory owner evidence, or an exceeded owner limit returns 503 `DEPENDENCY_UNAVAILABLE`. The five-second deadline returns 504 `DEPENDENCY_TIMEOUT`. Caller cancellation propagates.

Select a real slot through an authoritative slot read before booking. Preserve the site, category, and requested interval. The booking flow in [SPARK-273](../../04-testing/postman/SPARK-273-manual-test-guide.md) performs final account, plate, slot, policy, commitment, and payment checks. A displayed count does not reserve a slot or guarantee admission.

## Private Facility evidence

Operations calls `POST /internal/discovery/v1/slot-candidates:read` with its existing service JWT and `discovery:slot-candidates:read` permission. The body contains 1–100 distinct `sites[{siteId,configurationVersion}]`, `vehicleType:"CAR"`, UTC `at`, and optional UTC `startAt/endAt` for the requested stay. The body limit is 32 KiB. The reply has `success`, `traceId`, and `data{asOf,at,startAt,endAt,items}`. Each item contains verified `siteId`, `tenantId`, `configurationVersion`, `complete`, and slots with `slotId`, `outcome`, `physicalState`, and `slotUpdatedAt`. A complete empty list establishes zero candidates. Indeterminate evidence sets `complete=false`. A changed publication returns 409 `SEARCH_CHANGED`. Conflicting attribution or more than 2,048 registered slots in a batch fails instead of returning a partial list. This private evidence does not enter the public response.

Facility and Operations observe independent snapshots. Large batch latency under the five-second public deadline has not been measured.
