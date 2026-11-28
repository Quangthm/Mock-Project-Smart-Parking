# SPARK-272 manual search checks

Import [the collection](SPARK-272-Parking-Search.postman_collection.json) and [local example environment](SPARK-272-Local.postman_environment.example.json). The MVP examples below use the deterministic A/B/C dataset. Live rehearsal remains pending. The original requests also support other published sites when the tester supplies their coordinates and fixture prerequisites.

Use the [implementation register](../../05-project-management/decision-log/deferred-and-implementation-register.md#mvp-seed-production-follow-ups--28-november-2026) for production follow-ups, including the source and policy evidence behind the fixture limitations below.

## MVP rehearsal preflight

Use the example environment with the existing collection. Requests 01–32 retain the original regression coverage. The MVP folders add requests 33–49. Run individually and record status, trace ID, observation time, fixture T, Account role and site. Redact bearer tokens, passwords and private service evidence.

1. Confirm the isolated Compose project and volumes with `docker compose --project-name <project> -f compose.schema-integration.yml ps`. Confirm the existing schemas, active Platform Admin, restored UserService BCrypt dependency, and compatible installed policy history. The schedule needs about 51h20m of compatible history. Ask the environment owner to resolve any gap using approved Strategy A. Keep APIs, host clients and writers disconnected while seeding. Check [local setup](../../../LOCAL-SETUP.md) and the [reservation workflow prerequisites](SPARK-273-manual-test-guide.md#1-prepare-the-local-stack).
2. Use PowerShell running on .NET 10 or later, with built net10.0 Payment and Operations Domain/Application assemblies from the current checkout. Preview does not access PostgreSQL. It generates the manifest, runs the offline ledger and evaluator checks, and prints identities, timestamps and discovery bindings. The preview window is assumed. First persistence, installed schema constraints, replay, reset, and runtime behavior still require authorized rehearsal.
3. After seed verification, configure the discovery dependencies below, then start the approved local APIs. Use **33** at `/health` for User, Facility, Operations and Payment. Their example ports are 5035, 5045, 5055 and 5065. Verify actual URLs. A health response establishes process reachability. Then sign in, verify role scope, and use **36** to confirm all five publication grants. Test Payment and Operations reads only after this preflight.

```powershell
# Offline example used for the identifiers and UTC timestamps in this guide
./scripts/seed-mvp-data.ps1 -Preview -ReferenceTime '2026-11-28T10:00:00Z'

# The commands below write only when the operator has separately authorized this target.
$project = 'your-approved-isolated-project'
$T = [DateTimeOffset]::UtcNow.ToString('o')
./scripts/seed-mvp-data.ps1 -IsolatedProject $project -ReferenceTime $T
./scripts/seed-mvp-data.ps1 -IsolatedProject $project
./scripts/seed-mvp-data.ps1 -IsolatedProject $project -Reset
$newT = [DateTimeOffset]::UtcNow.ToString('o')
./scripts/seed-mvp-data.ps1 -IsolatedProject $project -Reset -ReferenceTime $newT
```

Use an offset-qualified T near rehearsal time and save the actual committed T in local `referenceTime`. Current queries use the service clock. For the interval baseline set `startAt=T+5h` and `endAt=T+6h` in ISO UTC; do not send the fixed preview timestamp if that interval is now past. The offline availability table assumes evaluation at T. Expect real-time differences if claims, occupancy, pending expiry or protection timing have advanced.

Normal execution inserts the deterministic User, Facility, Payment and Operations records and verifies the completed phases. Replay checks established records and preserves their state, including credentials, timestamps and publication versions. Missing or progressed established records require review. Explicit reset checks later and nonfixture dependencies and rebaselines matched fixture fields in place. It performs no broad deletes, truncation, auth-session cleanup, or provider cleanup. It can restore a fixture publication under its explicit reset checks. An active client, later use of the same fixture, or ambiguous occupancy can block reset. The runner reports its failed phase and confirmed commit prefix. Keep the project out of use until recovery and verification finish. A changed-time reset interrupted after a commit requires explicit recovery at the committed User T; ambiguous Facility occupancy needs reconciliation against the original Operations scan and current bay state before any recovery. [Seed session](../../../ai/tasks/active/SESSION-20261128-mvp-seed.md)

### Accounts and bearer tokens

Use **34** against User `POST /api/auth/login` with the seeded email in `fixtureEmail` and the same password entered in the seed's masked prompt. The script accepts an optional `-FixturePassword` SecureString. Passwords must be 8–15 characters with upper and lowercase letters, a digit and a special character. An established seed requires the existing fixture password. The seeded users and Accounts are active and email verification timestamps are populated. There are no fixture auth sessions. Existing password login still enforces configured rate limiting, bot protection and MFA. If the response has `mfaRequired=true`, complete the challenge through the normal application. Use a legitimate `turnstileToken` only when the deployment requires it.

| Role | Seeded accounts and scope |
| --- | --- |
| Owner | `owner01@mvpseed.example.test` owns MVP-01–03. `owner02@mvpseed.example.test` owns MVP-04–05. |
| Operator | `operator01@mvpseed.example.test` through `operator10@mvpseed.example.test`. Pairs 01/02, 03/04, 05/06, 07/08 and 09/10 belong to MVP-01 through MVP-05 respectively. |
| Driver | `driver01@mvpseed.example.test` through `driver06@mvpseed.example.test`. Drivers 01–05 each have three registered CARs. Driver 06 has one CAR and empty reservation/session history. |

After completed authentication, copy `data.accessToken` into the appropriate private Postman value. Use `driverBearerToken` for Driver reads, `ownerBearerToken` for Owner revenue, `operatorBearerToken` for assigned-site reads, and `wrongRoleBearerToken` for request 31 with the Owner and Operator in separate runs. Replace the token when changing accounts, and clear the reservation page cursor values. The checked-in values are blank. Browser login is also suitable when a challenge is enabled. Do not export populated credentials or refresh/access tokens. **35** verifies Account-scoped vehicle registration. Media uploads are deferred.

### Publication and Payment discovery

All five sites have deterministic development catalogue grants. MVP-01 and MVP-04 have seeded initial-site onboarding evidence. MVP-02, MVP-03 and MVP-05 use the approved synthetic additional-site exception. Production publication requires the normal Facility authority flow. Publication is separate from structural readiness and commercial configuration. An edited public profile can invalidate a grant. Use the matching built-in model in the Owner's rehearsal browser and retain the current public profile. Verify the floor/bay overlays and catalogue membership afterward.

Configure the **Payment API** with these key/value pairs. For a .NET environment variable replace `:` with `__`, giving `DiscoveryPricing__SiteTenants__<site-id>`. Use your local Payment process environment or its existing development configuration provider. Restart a host process or recreate the API container after setting values. The PostgreSQL container does not consume them.

| Site | Payment configuration key | Tenant UUID |
| --- | --- | --- |
| MVP-01 | `DiscoveryPricing:SiteTenants:382476ad-8670-5fd0-9f8a-0c56327886a3` | `382476ad-8670-5fd0-9f8a-0c56327886a3` |
| MVP-02 | `DiscoveryPricing:SiteTenants:fa46a0d8-acdf-51ae-a828-912566f02ca6` | `382476ad-8670-5fd0-9f8a-0c56327886a3` |
| MVP-03 | `DiscoveryPricing:SiteTenants:946dac07-2749-5149-a24a-36cb71ce8f38` | `382476ad-8670-5fd0-9f8a-0c56327886a3` |
| MVP-04 | `DiscoveryPricing:SiteTenants:0d8ba3b2-b297-5ee8-b26e-ea4f31636aa8` | `0d8ba3b2-b297-5ee8-b26e-ea4f31636aa8` |
| MVP-05 | `DiscoveryPricing:SiteTenants:8ed1e2ae-92c9-5efa-8232-c00a3389176c` | `0d8ba3b2-b297-5ee8-b26e-ea4f31636aa8` |

Operations needs its enabled discovery configuration, HTTPS Facility and Payment origins, the existing Facility service JWT scopes for catalogue, slot candidates and site location, and `Discovery:PaymentServiceKey` matching an unexpired Payment `DiscoveryPricing:Operations` credential slot. Use the environment's matching signing and verifier material. For an authenticated internal Payment caller, a missing binding returns 403 `SERVICE_SCOPE_DENIED`; a bound site lacking a price returns `not_configured`. The public endpoint can present upstream authorization failure as a dependency error. SQL rows alone do not establish these dependencies or transport. Explicit binding per site remains a production scalability limitation. [API contract](../../03-design/api/SPARK-272-parking-search.md)

## Prepared geography

These values come from the seed preview and the Facility seed's site manifest. Search uses decimal degrees in `lat` and `lon`, kilometres in `radiusKm`, and returns straight-line `distanceMeters`.

| Site | Seeded site name | Latitude | Longitude | Address |
| --- | --- | ---: | ---: | --- |
| MVP-01 | MVPSEED Bến Đỗ Ven Kênh | 10.785 | 106.705 | Cổng mô phỏng 01, khu thử nghiệm MVPSEED, TP.HCM |
| MVP-02 | MVPSEED Nhà Xe Nhiều Tầng | 10.748 | 106.69 | Cổng mô phỏng 02, khu thử nghiệm MVPSEED, TP.HCM |
| MVP-03 | MVPSEED Hầm Đỗ Phía Bắc | 10.838 | 106.715 | Cổng mô phỏng 03, khu thử nghiệm MVPSEED, TP.HCM |
| MVP-04 | MVPSEED Sân Đỗ Phía Nam | 10.66 | 106.75 | Cổng mô phỏng 04, khu thử nghiệm MVPSEED, TP.HCM |
| MVP-05 | MVPSEED Hầm Đỗ Đông Bắc | 10.995 | 106.86 | Cổng mô phỏng 05, khu thử nghiệm MVPSEED, TP.HCM |

Use **36** with `mvpSort=distance` and `vehicleType=CAR`. Copy the relevant site environment values into `lat` and `lon`, set `mvpRadiusKm`, and paste the ordered suffixes into `expectedMvpSites` (e.g. `02,01`). Expected 200 and the following membership in order. Keyword `MVPSEED` isolates the five fixtures.

| Origin | `lat` | `lon` | Radius km | Ordered site suffixes |
| --- | ---: | ---: | ---: | --- |
| MVP-01 | 10.785 | 106.705 | 5 | 01,02 |
| MVP-02 | 10.748 | 106.690 | 5 | 02,01 |
| MVP-03 | 10.838 | 106.715 | 5 | 03 |
| MVP-04 | 10.660 | 106.750 | 5 | 04 |
| MVP-05 | 10.995 | 106.860 | 5 | 05 |
| Midpoint of MVP-01 and MVP-02 coordinate pairs | 10.766500 | 106.697500 | 3 | 01,02 |
| About 1 km north of MVP-01 | 10.793983 | 106.705 | 5 | 01 |
| Same offset, wider radius | 10.793983 | 106.705 | 5.1 | 01,03 |
| MVP-01 just below MVP-02 distance | 10.785 | 106.705 | 4.428 | 01 |
| MVP-01 just above MVP-02 distance | 10.785 | 106.705 | 4.429 | 01,02 |
| Comparison origin | 10.775 | 106.700 | 5 / 10 / 20 / 50 | 01,02 / 01,02,03 / 01,02,03,04 / 01,02,03,04,05 |

The midpoint averages each coordinate. It is a coordinate midpoint, with approximate API distances 2,214.242 m to MVP-01 and 2,214.260 m to MVP-02. For the north offset, `10.785 + 1000/111320 ≈ 10.793983` and longitude stays `106.705`. The implementation uses the haversine formula with a 6,371,000 m Earth radius. At the offset, the calculated distances to MVP-01, MVP-03 and MVP-02 are about 999 m, 5,015 m and 5,369 m. A 5 km radius includes only MVP-01 and 5.1 km adds MVP-03. Facility applies an inclusive radius predicate. MVP-02 is about 4,428.502 m from MVP-01, so the bracket tests use a metre difference and avoid comparing rounded display distances. Comparison-origin distances rounded to metres are **1,239 / 3,195 / 7,194 / 13,905 / 30,061**. [Catalogue radius predicate](../../../src/Services/FacilityService/SmartParking.FacilityService.Persistence/DiscoveryCatalogueStore.cs)

At the comparison origin and 50 km, the offline expected orders are `01,02,03,04,05` for distance, `01,02,04,03,05` for price and `02,01,04,03,05` for current availability. Price ties at 15,000 VND/hour go to nearer MVP-02. Current availability ties at 43 also use distance. Unknown MVP-05 ranks last for availability. Update `expectedMvpSites` to match the sort before sending **36**.

## Availability and exceptional cases

**37** uses the comparison origin, 50 km, and the installed T+5h to T+6h interval. Compare with the values below. All values are offline expectations at the fixture reference, pending live acceptance. Inspect `data.items[].availability.kind`, `availableCount`, field freshness, `price.kind`, `amountVnd` and `billingUnit`. Current price is hourly. Interval price is the total for the requested stay.

| Site | Offline current CAR | Offline CAR at T+5h for one hour | Hourly VND | Monthly CAR VND |
| --- | ---: | ---: | ---: | ---: |
| MVP-01 | 43 | 45 | 10,000 | 600,000 |
| MVP-02 | 57 | 4 | 15,000 | 750,000 |
| MVP-03 | 39 | 2 | 20,000 | 900,000 |
| MVP-04 | 43 | 0 | 15,000 | 750,000 |
| MVP-05 | Unknown | 4 | 25,000 | 1,000,000 |

- **MVP-02 protection.** Reservation `9901473b-220a-5b34-bcf6-46a1460ab1b8` requests bay `7e1227b1-fb22-5b69-9957-9e1bb3a66996`. Protection begins T−2h and the booked interval starts T+2h. The bay remains AVAILABLE without an allocation. Offline current behavior is **57**, while approved policy expects **56**. This is an outstanding SPARK-272 correction. The named-bay evaluator rejects a stay crossing the reservation start and accepts a stay ending exactly at it under the zero buffer. An aggregate search result cannot establish which bay is eligible.
- **MVP-05 mismatch.** Reservation `e2591a65-2448-5d15-9942-158e77bf7cfc` expects bay `fe32a437-aaa2-5496-a4ea-8617923721eb`. Session `c18186bd-5c54-554f-8133-c30618daa88c` scanned into `5c9de5c3-a8a0-5afd-ac95-7a677e3ba3c5`, which is the physical occupation. Unknown is a null count caused by contradictory evidence. MVP-04’s interval zero is known quota exhaustion. Leave the deliberate mismatch in place during baseline rehearsal.
- **MVP-04 overstay.** Reservation `05970774-83e5-5f87-9199-2b2b32f8eeb9` and its occupied allocation end at T−5min. Session `3b997793-3418-59ca-8cc7-d9c3bce5a3c8` remains open with an actual scan in bay `e2589e4f-7f4a-55ab-9eb2-66314813bc18`. Count that physical occupation in current mode. Do not extend the allocation. At T+5h, the known zero in the table comes from the new reservation consuming the quota, rather than the expired allocation.
- **Pending hold.** Driver 04 reservation `a760e240-a84d-5ed8-ba9e-b6fe1680bfa6` has four minutes left at T, in MVP-01. Read **40** for the booking and **46** with `pendingOrderId`. Payment reads PAYABLE before the deadline and CLOSED afterward. The availability read drops the expired hold even though the seeded reservation can still read PENDING_PAYMENT. The seed schedules no expiry transition. For automatic expiry, create a fresh unpaid hold on the normal workflow immediately before the authorized demo, leave it unpaid, and observe the real transition. Keep provider status off.
- **Early arrival and replacement.** MVP-01 has an observed early entry. The record does not establish early-arrival threshold enforcement. Driver 02 reservation `c991c21b-e6e4-546f-a2ae-de28c715d7bd` originally requested `48c7ac4a-7311-52f3-8be1-377fd2f68a0e` and was scanned into replacement bay `b71dfdbb-e1e5-53eb-97b8-a4e34ee69f9b`. Both historical allocations are released. Verify the stored visit separately from the request and allocation.

## MVP request map and expected results

Use the URLs in the imported collection. Unless specified, **200** is the expected HTTP status after live prerequisites are satisfied.

The baseline has 131 reservations. Each of Drivers 01–05 has ten future confirmations, fifteen historical reservations, and one additional PARKING reservation. Driver 04 also has the pending-payment fixture. Per Driver, the historical set has nine completed, two cancelled, two expired unpaid, and two no-show. Latest booking location orders qualifying confirmations by `confirmedAt` descending and then reservation ID descending. Future confirmations participate. Session chronology does not drive that read.

The 113 sessions include 100 completed and 13 active sessions. Active counts by Driver are 3/3/3/2/2. The twelve located active visits produce physical occupancy. One MVP-04 walk-in has no observed location. The 50 Account-attributed reservation sessions and 63 plate-attributed walk-ins use separate attribution rules.

The example environment includes the five coordinate pairs as `mvp01_lat` through `mvp05_lat` and their corresponding `_lon` keys. Copy the selected pair into `lat/lon`. Copy a site’s `mvpNNSiteId` value into `mvpSiteId` for the scoped requests. The fixed scenario variables hold the prepared IDs. Copy the desired `*ReservationId` into `scenarioReservationId` and select its owning Driver. Use `*RequestedSlotId`, `*ScannedSlotId`, and `*SessionId` for comparison with the matching response, and use the monthly subscription and order IDs for local evidence lookup. Account IDs are comparison values. The tested Driver reads derive identity from the token.

| Test | Role | Method and path | Prepared expectation |
| --- | --- | --- | --- |
| 33 | Public | GET /health | Repeat on the four configured APIs by changing `healthBaseUrl`. |
| 34 | Public sign-in | POST /api/auth/login | `fixtureEmail`, private `fixturePassword`, optional local `turnstileToken`. Check `mfaRequired` before reading `data.accessToken`. |
| 35 | Driver | GET /api/vehicles | Match fixture plates and vehicle IDs. Five primary Accounts have three CARs, Driver 06 has one. No Account override. |
| 36 | Public | GET /api/parking-search | Use the geography table and expected sort. Inspect `distanceMeters` and published fixture IDs. |
| 37 | Public | GET /api/parking-search | 50 km and installed T+5h/T+6h interval. See baseline above. |
| 38–39 | Driver | GET /api/me/reservations | `pageSize=10`; for 39 set `before` and `beforeId` from the last row's `createdAt` and `reservationId`. Expect an empty array for Driver 06. |
| 40 | Driver | GET /api/me/reservations/{id} | Default Driver 01 zero fixture `c8d237c8-171f-5cfb-9648-5bee151bbe8d`. Inspect `state`, `holdExpiresAt`, `requestedSlotId`, `allocatedSlotId`, `parkingSessionId`, `confirmedAt`, `orderId` and `zeroReceiptId` as applicable. Another Driver's ID returns 404. Actual arrival/departure belongs to the session read and cancellation evidence to local Operations persistence. |
| 41 | Driver | GET /api/sessions/current/location | Driver 05 has a located Account-attributed active session with `data.location.slotId` at the mismatched bay. Driver 06 gets 404 `SESSION_NOT_FOUND` before fresh booking. Walk-ins are plate-attributed and the Driver query does not use them as Account history. |
| 42 | Assigned Operator | GET /api/parking-sessions?siteId={id} | Compare `reservationId`, `accountId`, `currentSlotId`, `locationScannedAt`, `entryTime` and `exitTime`. Unlocated MVP-04 session `c02cdb33-d6a9-5b61-8e40-42b3f58f9a1f` has null slot, scan and exit. A wrong-site Operator is rejected. |
| 43 | Assigned Operator | GET /api/parking-lots/{id}/operational-layout | Inspect `data.slots` and the physical states for the assigned site. For MVP-05 compare the scanned occupied bay in 41/42 to the expected reservation allocation in 40. Check-in does not itself establish actual location. |
| 44 | Public | GET /api/v1/sites/{siteId}/pass-offers?vehicleType=CAR | One fixture monthly CAR offer where Payment readiness is configured. Check `fullPeriodPrice`, `entitlementType=WHEN_SPACE_AVAILABLE`, `guaranteedCapacity=false` and `yearlyBlockedByQ05=true`. |
| 45 | Assigned Operator | GET /api/checkout/monthly-coverage?siteId={id}&plateNumber={plate} | Covered gives `isCovered=true, amountDueVnd=0` within entitlement. Wrong plate/site with authorized Operator gives false/null. |
| 46 | Owning Driver | GET /api/v1/payments/orders/{id}/reservation | Local order state by ID only. Use `zeroOrderId` (Driver 01), `protectionOrderId` (Driver 01), or `pendingOrderId` (Driver 04). PAID or deadline-dependent PAYABLE/CLOSED. Another Driver sees MISSING. |
| 47 | Owning Driver or assigned Operator | GET /api/v1/payments/orders/{id}/invoice | Default Driver 01 zero receipt `1908fa3d-63f0-5d2c-8ca0-cd50eec29854`. For MVP-01 paid walk-in use `walkInOrderId`, `walkInInvoiceId` and a local MVP-01 Operator token. Confirm `invoiceIdentity`, `financialStatus.isSettled` and amount. |
| 48 | Driver | GET /api/v1/payments/invoices/my-history | `pageNumber=1&pageSize=10`. `items` is scoped to your Account. Driver 01 has the zero receipt and monthly purchase invoice. Walk-in invoices without an Account do not appear. |
| 49 | Owning Owner | GET /api/v1/payments/owners/lots/{id}/pass-revenue-summary?productType=MONTHLY | Check `data.collectedAmount`. Fixture revenue by site is 600,000 / 0 / 900,000 / 750,000 / 0 VND. |

### Financial safety and manual finish

The 109 paid positive reservation orders use 109 synthetic reviewed payOS collections. There is one paid zero reservation with one receipt and no collection. The 21 unpaid orders include one pending attempt. The 52 CASH walk-in checkouts bring C to 162 transactions (161 successful, one pending) and 53 invoices. The three B monthly purchase chains are separate. The walk-in example is order `88cde9d0-b058-55ed-b85e-a2011b17203d`, invoice `901bfb97-ae0f-538d-899e-0d9e13196c47`, and session `ff08facc-e985-5e7b-9415-301221d2f9ba` at MVP-01. Its amount is 10,000 VND.

**Use local reads only.** Reviewed collection evidence is available through Payment persistence or the service-authorized local collections read `GET /internal/reservation-deposit-orders/{orderId}/collections`. Keep service authorization material outside Postman. For the positive protection example, compare the order in **46** to collection `264eabee-e3f6-5b03-847d-b0fc94fe6a65` in the authorized local read. Synthetic payOS references are development evidence, never real transactions. Development status GET can call payOS for qualifying PENDING/UNKNOWN transactions. Do not use it with synthetic IDs or initiate payment, provider checkout, cancellation, refund or event publication as part of these reads. The 42 uncovered linked completions have observed departures and existing prepayment only. The current checkout client omits deposit deduction and can duplicate charges. Leave checkout preparation uncalled on these records.

For **45**, use Driver 01 vehicle 1 at MVP-01, Driver 03 vehicle 1 at MVP-03 and Driver 04 vehicle 1 at MVP-04, as identified by `driver01Plate`, `driver03Plate` and `driver04Plate`. The entitlements cover six completed visits, with one linked visit and one walk-in per entitlement, wholly inside their validity period. Repeat with another registered plate, or the same plate at another site using an Operator assigned there, and set `expectedCovered=false`. This check uses current coverage by tenant, site and plate and establishes no advance discount. Yearly passes are deferred, and the Driver purchase-to-entitlement workflow remains separate.

Run baseline reads before the **Driver 06 fresh-booking** demonstration. Confirm **35** returns the one CAR, **38** is empty, **29** returns `data:null`, and **41** is 404. Confirm site MVP-01 and the available CAR bay `slot/01/CarParkingSpace_013` (`freshBookingSlotId`). On the separately authorized local rehearsal only, use the existing SPARK-273 vehicle selection, reviewed quote and fresh admission steps with that plate and bay. Set a new future interval, accept the returned quote and use a new idempotency key. For the automatic-expiry test choose a positive quote, leave it unpaid and record the live expiry transition without initiating provider checkout. These bookings change baseline results; run them last. [Existing admission workflow](SPARK-273-manual-test-guide.md)

### Manual acceptance status

PostgreSQL first apply, A/B upgrade, reset, replay, interrupted phase recovery, installed historical policy and live service reads are pending. Check authentication and role isolation, public membership, interval timing, named-bay acceptance and browser model binding before the demo. Record discrepancies without altering baseline occupancy or publication. Cancellation, no-show and historical replacement are approved synthetic lifecycle evidence. The protection and prepayment gaps remain production corrections. No browser or Postman rehearsal has yet passed.


## Original search request instructions

Use the SPARK-273 [local setup and reservation flow](SPARK-273-manual-test-guide.md) for service startup, Driver authentication, actual slot selection, and admission after search. Search itself is PUBLIC and sends no Driver or service key. The stack needs the published Facility catalogue, Facility candidate route, Operations reservation database and policy, Payment discovery pricing, and matching Operations/Facility service signing configuration. Only run controlled outage, publication change, maintenance, and quota fixtures in an isolated development environment.

Requests 01–28 are public `GET /api/parking-search` and inherit `lat` and `lon` from the environment. Start at the comparison origin unless a row calls for another center. For an all-five comparison on an original request that omits radius or keyword, make a local one-off Params edit to add `radiusKm=50` and `keyword=MVPSEED` or use 36/37. Make the same local edits on both requests of a cursor pair.

| ID | Additional inputs and prerequisites | Expected HTTP and fixture result |
| --- | --- | --- |
| 01 | CAR, current, default 5 km | 200. MVP-01 then MVP-02 at the comparison origin on an MVP-only target. |
| 02 | CAR, `startAt/endAt` set to installed T+5h/T+6h | 200. Interval kind and first two counts 45 and 4 under the offline baseline. For five sites use 37. |
| 03 | CAR, `radiusKm=10`, `lotType=basement` | 200. MVP-03, the basement within 10 km at the comparison origin. |
| 04 | CAR, `availableOnly=true` | 200. Only fresh positive rows. At 50 km MVP-05 is excluded while the mismatch is relevant. |
| 05 | Interval, `minTotalVnd=10000`, `maxTotalVnd=15000` | 200. The one-hour fixture estimate is in range for MVP-01 and MVP-02 at the comparison origin. At 50 km MVP-04 also meets the price filter, even though its interval availability is zero. |
| 06 | CAR, `limit=1` | 200. First MVP-01 and a nonempty `nextCursor` if MVP-02 is visible. |
| 07 | Same filters and `cursor` from 06 | 200. MVP-02 follows. Restart for expired or changed results. |
| 08 | Controlled current full site | 200. Fresh zero. C’s known zero is an **interval** quota case through 12/37. Current full needs a separate fixture. |
| 09 | Controlled published closure | 200. Zero with reason `closed`. The default MVP fixture has no such closure. |
| 10 | Center on MVP-05 with the deliberate mismatch | 200. Inspect null `availableCount` and unknown freshness while evidence remains relevant. |
| 11 | MOTORCYCLE | 200. MVP is published for CAR only. Expect no MVP matches. Use a separately configured site for positive motorcycle catalogue/pricing checks. |
| 12 | Center on MVP-04, use the T+5h/T+6h interval | 200. Known zero under quota one. |
| 13 | Separate controlled maintenance and positive-buffer policy fixture | 200. Compare the half-open Facility stay check with the named-slot buffer behavior. MVP’s default buffer is zero. |
| 14 | `radiusKm=51` | 400 `VALIDATION_FAILED`. |
| 15 | Controlled publication change after catalogue acquisition | 409 `SEARCH_CHANGED` if the race is induced. Keep baseline grants intact. |
| 16 | Controlled required owner dependency failure | 503 `DEPENDENCY_UNAVAILABLE` for the requested fault scenario. Record actual timing and code. |
| 17 | `keyword=ven kenh` | 200. Name match MVP-01; an address-only term gives no match. |
| 18 | Supplied accented `Bến Đỗ` | 200. MVP-01. |
| 19 | Supplied unaccented `Ben Do` | 200. Same site IDs as 18. |
| 20 | Supplied 101-character keyword | 400 `VALIDATION_FAILED`. |
| 21 | `sort=price`, current | 200. At 50 km with the MVP filter, 01,02,04,03,05. MVP-02 wins the hourly tie by distance. |
| 22 | `sort=price`, one-hour baseline interval | 200. Rank by total VND. Separately use a valid 15-minute interval at MVP-01 and check known zero price. |
| 23 | `sort=availability`, current | 200. At 50 km with the MVP filter, 02,01,04,03,05 at T. |
| 24 | `sort=price&limit=1` | 200. MVP-01 first. Save `priceCursor` when present. |
| 25 | Same filters and `priceCursor` | 200. MVP-02 next when data stays stable. |
| 26 | `sort=availability&limit=1` | 200. MVP-02 first at T. Save `availabilityCursor` when present. |
| 27 | Same filters and `availabilityCursor` | 200. MVP-01 next when data stays stable. |
| 28 | Change price cursor sort to distance | 400 `INVALID_CURSOR`. |
| 29 | Driver token for `GET /api/me/discovery/last-booked-location` | 200. Driver 01 returns MVP-01 and Driver 02 MVP-02. Driver 06 returns `data:null`. Inspect `siteId`, coordinates and `confirmedAt` where present. |
| 30 | Same location path, anonymous | 401 `UNAUTHENTICATED`. |
| 31 | Same path, valid Owner or Operator token | 403 `FORBIDDEN`. Repeat for each role. |
| 32 | Same path, Driver with qualifying history and controlled Facility outage | 503 `OWNER_UNAVAILABLE` for the prescribed outage. |

Run collection requests individually. Requests 01–07 check current and interval CAR responses, radius and lot type, positive only, price bounds, and cursor continuation. The first cursor request stores `data.nextCursor` when present. Request 07 needs at least two matching published sites and unchanged filters. Requests 08–13 require the fixture described in each request. Request 14 needs no fixture. Requests 15–16 require a controlled publication change or owner failure after catalogue discovery. Their stated HTTP codes are expected only when the corresponding condition is actually induced. The collection asserts safe envelopes and selected invariants; inspect site-specific counts and pricing against the fixture.

For request 13, establish maintenance beginning exactly at the requested `endAt` and an applicable positive `INTER_BOOKING_BUFFER`. Facility should consider the slot structurally eligible for `[startAt,endAt)`. Operations should still block a competing named reservation that starts between `endAt` and `endAt+B`. Compare the same real slot through the SPARK-273 admission flow. Do not infer a slot ID from the aggregate count.

Record actual status, `traceId`, `data.asOf`, each source timestamp and field status, count, price, cursor, and the fixture state. A known zero must have fresh complete evidence or a published closure. Unknown or stale availability has a null count. An unavailable owner or exceeded candidate limit returns an error rather than a partial 200 result. Keep private tokens and internal slot evidence out of public reports.

## Enhancement A: keyword, ranking and cursor scenarios

The collection adds requests 17–28. Keep `lat`/`lon` near a **published** test site; keyword never broadens the radius. Set `keyword` to a real partial facility name for request 17. Requests 18 and 19 compare URL-encoded `Bến Đỗ` and `Ben Do` using the **same** published-name fixture, and should return the same matching site IDs. A mixed-case form such as `bEn%20dO` should match identically. `keyword=` means no keyword filter; a whitespace-only keyword also means no filter after trimming. Request 20 sends 101 characters and should return 400 `VALIDATION_FAILED`. Match literal substrings of facility **names**, not addresses or fuzzy alternatives. Combine a keyword with `radiusKm`, `vehicleType`, and `lotType`; repeat with `availableOnly=true`, `startAt/endAt` and interval `minTotalVnd/maxTotalVnd`. Set unused optional parameters by **omitting** them, not by sending empty values (except the intentionally empty keyword case).

Request 21 sorts current `price.amountVnd` as hourly tariffs ascending; request 22 sorts interval total estimates ascending; request 23 sorts fresh CAR `availableCount` descending. Use controlled sites with unequal known values, equal values, a genuine zero, unknown price/count and stale evidence. Known zero ranks with known values; unknown/stale ranks after known values. On ties check `distanceMeters` ascending then site ID. Explicit `sort=distance` should agree with the default distance ordering. `sort=unsupported` should return 400 `VALIDATION_FAILED`. These requests do not create zero/stale fixtures automatically; document the actual Payment and availability observations used.

Requests 24–27 provide price and availability first-page/continuation pairs. Use at least two matching sites and a nonempty `nextCursor`; preserve **all** original parameters. Request 28 deliberately changes sort and expects 400 `INVALID_CURSOR`. Also change keyword, radius or coordinates with an existing cursor and expect `INVALID_CURSOR`. For ranked continuation, deliberately change a relevant price, count or matching-site membership between pages, including a site beyond the visible page, and expect 409 `SEARCH_CHANGED`. Unrelated observation timestamp changes alone are not a required invalidation. Wait beyond the five-minute cursor lifetime for 409 `CURSOR_EXPIRED`; a pre-enhancement cursor also requires a fresh search. Do not claim a persistent snapshot or append results after either 409 outcome. A request lacking a cursor or using `cursor=` does **not** test continuation; first verify a nonempty value was stored.

## Enhancement B: historical booking origin scenarios

Requests 29–32 use the existing local Operations base URL. The example environment holds **blank** `driverBearerToken` and `wrongRoleBearerToken` values; provide valid tokens through your local secure workflow and do not commit them. Search remains public/no-auth. Historical location requires a Driver bearer token and returns `{success:true,data:{siteId,siteName,latitude,longitude,confirmedAt},traceId}` or `{success:true,data:null,traceId}` with no-store. Do not send an Account ID. Request 29 needs a qualifying reservation whose latest confirmed site remains published, active and has usable coordinates. Compare the response to the actual Facility site and reservation `ConfirmedAt`.

Run the same Driver read with these controlled histories, recording the expected `data` shape:

| Fixture / request | Expected result |
| --- | --- |
| No qualifying booking, or only unpaid, failed, expired or cancelled bookings | 200 with `data:null`; no Facility lookup should be needed for empty qualifying history. |
| Multiple qualifying bookings | Latest `ConfirmedAt` wins, not latest `CreatedAt`; equal confirmation times use reservation ID descending. States `CONFIRMED`, `ALLOCATED`, `PARKING`, `COMPLETED` qualify. |
| Latest qualifying site withdrawn, missing, inactive or missing usable coordinates | 200 with `data:null`; do not fall back to an older booking. |
| Another Account owns a more recent booking | The authenticated Driver's result is unchanged; no other Account history appears. |
| Request 30 without authentication | 401 `UNAUTHENTICATED`. |
| Request 31 with a valid non-Driver token | 403 `FORBIDDEN`. |
| Request 32 with Facility unavailable after a qualifying booking is selected | 503 `OWNER_UNAVAILABLE`, not a successful null. |

If the owner replies with malformed or contradictory site data in a controlled test, expect a 503 dependency error; capture the actual code, which may be `OWNER_UNAVAILABLE` or `OWNER_CONTRACT_INVALID` depending on where validation fails. A missing Operations reservation schema can produce 503 `PROTOTYPE_SCHEMA_NOT_READY`. Do not simulate outages in shared or production environments. The private Facility route requires the Operations service JWT with `discovery:site-location:read`; do not put that signing key or private token in this collection. Its authoritative 200 `data:null` differs from authentication, transport or malformed-reply failure. Historical coordinates describe a previous booking location, not current physical presence.

## Browser and final acceptance still owned by testers

SPARK-276 must verify permission granted/denied/unsupported/timed-out/unusable device location, historical `data` versus `data:null` versus dependency failure, manual address/map origin, and delayed automatic responses never overriding a newer manual choice. Verify guest search with explicit category and no historical endpoint call, initial 5 km and deliberate 10/20/50 km expansion, keyword/sort controls, and cursor reset on every filter/origin change. Test search-to-reservation with a **real** compatible slot and fresh SPARK-273 admission. Live chatbot, full cross-service runtime, large-catalogue and large-history performance, and the five-second public deadline are not established by this guide or collection.
