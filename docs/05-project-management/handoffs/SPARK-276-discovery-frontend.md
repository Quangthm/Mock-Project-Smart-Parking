# SPARK-276 discovery frontend handoff

This is an integration requirement, not a claim that the guest or Driver map has shipped. SPARK-272 delivers the backend reads. Replace the legacy `/parking-lots` integration in `FE/src/lib/driverDiscoveryApi.ts` with Operations `GET /api/parking-search`; retain the project's existing HTTP, session and authentication conventions. See [the canonical contract](../../03-design/api/SPARK-272-parking-search.md).

## Driver origin and first search

1. Open Find Parking, initialize the map, and show a location-resolution loading state.
2. Request permitted browser/device geolocation through the existing frontend mechanism. It may use GPS or another browser-supported positioning technology. Accept only usable coordinates and accuracy for the intended map experience.
3. If device location is usable, use it as the search origin. Otherwise, call authenticated `GET /api/me/discovery/last-booked-location` once for the current account/session generation.
4. If `data` contains a usable site, use its coordinates and label it **previous booking location**. These coordinates are not the Driver's current position. If `data:null`, offer manual address or map selection. If the historical request fails, show a distinct lookup-failed state and still offer manual selection.
5. Resolve the selected registered vehicle's supported category, or ask the Driver to select `CAR` or `MOTORCYCLE` when necessary.
6. Search with `lat`, `lon`, explicit `vehicleType` and initial `radiusKm=5`. Render returned public facilities on the map and list.

Handle granted, denied, unsupported, timed-out and inaccurate/unusable geolocation distinctly. Do not repeatedly prompt after denial or track location continuously. Manual address geocoding and explicit map-center selection are both valid origins. Manual selection always wins over automatic initialization: tag each origin request with the current selection/session generation and ignore delayed GPS or historical replies from an older generation. Account change, logout or a newer manual selection must invalidate pending historical and search responses.

## Guest workflow

A guest can open the intended public map, use permitted device location or manually chosen coordinates, explicitly select a supported vehicle category, and call the **same public search endpoint**. Current and interval search, filters, sorting and radius expansion are public. A guest can inspect public facility details and prices but must authenticate before creating a reservation. Never call the authenticated historical-location endpoint for a guest. Preserve selected facility, category and stay across authentication where appropriate; after login obtain a real compatible slot and perform fresh SPARK-273 quote review and admission. The guest map remains frontend work.

## Canonical request mapping and controls

| Legacy/client concern | Required Operations contract |
| --- | --- |
| Legacy `/parking-lots` route and `latitude`/`longitude` query names | `GET /api/parking-search` with required `lat` and `lon`. |
| Implicit category and legacy lot types | Explicit `vehicleType=CAR` or `MOTORCYCLE`; optional lowercase `lotType=outdoor`, `basement`, or `multi_storey`. |
| Offset pages | Opaque `data.nextCursor`; no offset pagination. |
| Availability and prices | Optional `availableOnly`; nullable `availability.availableCount` and `price`, with `fieldStatus` and `sourceAsOf`. |
| Stay and price bounds | Paired `startAt`/`endAt` with offsets, optional `timeZone=Asia/Ho_Chi_Minh`; `minTotalVnd`/`maxTotalVnd` only for interval estimates. |
| Response | `{success:true,data:{items,nextCursor,asOf},traceId}`; items carry site identity, address, coordinates, straight-line `distanceMeters`, availability and price. |

Provide facility-name `keyword`, nearest `sort=distance`, lowest `sort=price` and most available `sort=availability`. Keyword is a literal case/accent-insensitive substring of published names within the **chosen geographic radius**, not a nationwide or address search. Debounce input. Start at 5 km and let the user expand deliberately to 10, 20 or 50 km; do not broaden automatically. Allow searching around a manually selected map position. Use an explicit “Search this area” action or another bounded interaction rather than querying continuously during map movement. Offer category, lot-type and `availableOnly` filters, current versus interval mode, and interval total-price bounds.

Changing the effective origin, radius, category, keyword, sort or any other filter starts a new search and clears the old cursor. A continuation must repeat the bound request parameters. Cancel or ignore superseded responses. On `SEARCH_CHANGED` or `CURSOR_EXPIRED`, restart pagination from page one rather than appending incompatible items. A ranked continuation can restart after a relevant price, count or matching-site change beyond the current page.

## Rendering and failure states

Keep distinct loading, available, full, closed, unknown, stale, empty-match, invalid-input, rate-limited, dependency-failed and timeout states. `availableCount=0` is a known full count; `null` is unknown/stale according to `fieldStatus.availability`. `price.amountVnd=0` is a known zero price; `price=null` is unknown/stale according to `fieldStatus.price`. Current `price` is an hourly tariff (`billingUnit=hour`); interval `price` is an estimated total (`billingUnit=total`). `distanceMeters` is straight-line distance, not driving distance or travel time. Motorcycle pooled availability is unsupported and must not be shown as an exact zero or positive count. Do not load all operational layouts to render aggregate search results.

Search results are observations, not admission guarantees. Preserve selected site, vehicle/category and stay, then obtain detailed layout and an actual compatible slot through the authoritative slot-selection flow. Never synthesize slot IDs from aggregate counts. Continue through SPARK-273 quote review and reservation admission, which rechecks live rules and can reject a previously displayed site or slot. No frontend files were changed by SPARK-272.
