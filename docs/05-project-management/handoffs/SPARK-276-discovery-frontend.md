# SPARK-276 Driver discovery integration

The Driver client currently calls the legacy `/parking-lots` path in `FE/src/lib/driverDiscoveryApi.ts`. Connect its GPS and user-entered coordinate searches to Operations `GET /api/parking-search`. Send explicit `lat`, `lon`, and `vehicleType` (`CAR` for supported named-slot counts). Preserve the user's selected origin, site, vehicle category, and requested interval through detail and booking.

Supported query names are `radiusKm` (0–50 exclusive of zero, default 5), `lotType` (`outdoor`, `basement`, `multi_storey`), `availableOnly`, paired `startAt/endAt` with offsets, `timeZone=Asia/Ho_Chi_Minh`, `minTotalVnd/maxTotalVnd` for interval estimates, `limit` (1–50), and opaque `cursor`. The maximum radius is 50 km. Cursor continuation must repeat all search filters and coordinates. Distance is straight-line great-circle meters from the supplied origin, not route distance.

Render `availability.availableCount=0` as known full and `null` as unknown or stale according to `fieldStatus.availability`. A published closed site has known zero and reason `closed`. A fresh positive count passes `availableOnly=true`. Preserve full facilities in the default result. `price=null` is an unknown price. Current price is a tariff per hour; interval price is a total estimate for the requested stay. Handle 400 validation, 409 search change/expired cursor, 429 rate limit, 503 dependency failure, and 504 timeout as distinct UI states.

An aggregate count does not identify bookable slot IDs. Obtain a real slot through an authoritative slot read before starting SPARK-273 booking. **Never generate synthetic slot IDs from aggregate availability counts.** Admission may reject a slot after search as state changes.

No frontend code was changed in SPARK-272. The API details and response fields are in [the search contract](../../03-design/api/SPARK-272-parking-search.md).
