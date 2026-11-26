# S06 G01 — source revalidation and bounded owner proposal

| Metadata | Value |
| --- | --- |
| Status | P01–P04 source/offline implemented under user authorization; S06 PARTIAL; deployment OPEN |
| Version / date | 0.1 / 2026-11-05 |
| Owners / author | Operations discovery; Facility publication/configuration; Payment pricing / Codex |
| Jira | OPEN |
| Source baseline | AI-ChatBot, HEAD 3d3def763870f66f8da71386357a66f776fa61e5 plus preserved staged/unstaged/untracked S01–S06 |
| Implementation | Facility publication, Payment price read, Operations search and IoT adapter implemented; see current S06 handoff; runtime disabled/unverified |

## Overview and traceability

**Implementation direction, 2026-11-06.** The lead asked to record the decisions and implement P01–P04 together. Selected source/offline scope: (P01) explicit versioned Facility publication by an authorized lot owner after a verified finalized approval, invalidation on changes, default private; (P02) isolated Operations→Payment machine credential, trusted deployment site binding and strict versioned effective tariff; (P03) Operations evaluates published candidates, ranks globally, accepts only owner-supported counts/closures and returns unknown for unresolved capacity, uses protected live cursors; (P04) fixed public Operations GET with local explicit origin/filters, typed cards and Account-bound S05 recovery. Production site bindings, credentials, freshness thresholds, SQL application and release activation remain deployment-owner decisions. All examples/test keys and origins are synthetic. Historical OPEN labels below record design review; consult the final handoff for implemented and pending subsets.

This packet narrows the missing G01 owner work. It is not approval to implement it. Existing D01/D05 approval covers vehicles and minimal own reservations only. TOKEN/REQUEST/SERVICE decisions remain unchanged. No new Search service, booking quote, hold, mutation, S07 or S08 is included.

| Trace | Canonical reference |
| --- | --- |
| Discovery / UC-SEARCH-01 | [Discovery requirements](../../01-requirements/srs/modules/03-discovery-visualization.md) |
| FR-AI-03/04/09/10/11; BR-AI-01/02/04/05 | [AI requirements](../../01-requirements/srs/modules/10-ai-assistance.md), [business rules](../../01-requirements/business-rules/10-ai-assistance.md) |
| Price and allocation authority | [Pricing requirements](../../01-requirements/srs/modules/06-pricing-payments.md), [ownership](../../02-architecture/service-boundaries/ownership-and-events.md#geographic-discovery-and-model-handoff) |
| Auth / existing target | [operation matrix](../../01-requirements/srs/authorization-operation-classification.md), [S01 §11](assistant-s01-end-to-end-api-design.md#11-get-apiparking-search--operations), [S06 scoped decisions](assistant-s06-read-integration.md) |

## Current source evidence: route → auth → application → storage/adapter

Paths below are repository-relative. Source presence does not prove deployed data, SQL execution or trust.

| Surface | Verified chain | G01 consequence |
| --- | --- | --- |
| Facility owner catalogue | `FacilityService/...API/Controllers/FacilityController.cs` GET `/api/parking-lots` → `ScopeAsync` / `OwnerIdentityClient` (`Infrastructure/ServiceClients.cs`, User owner-scope Bearer) → `ParkingStructureService.ListAsync` reauthorizes owner → `PostgresParkingStructureRepository.ListAsync`, active tenant + tenant-scoped nondeleted `parking_sites`, ordered site_code | Contains name/address/nullable latitude/longitude/status. This is an owner catalogue, not public discovery. No radius/ranking/cursor/publication projection. |
| Facility individual public layout | Same controller GET `/api/parking-lots/{id}/operational-layout` → `LayoutCallerResolver` (anonymous sanitized; authenticated identity via User, privileged telemetry separately scoped) → `ParkingStructureService.ReadOperationalAsync` → `PostgresOperationalLayoutReader` → `OperationalLayoutRedaction` | A real anonymous sanitized read exists. Reader checks active/nondeleted site and tenant; reads local slots/spatial units/capacities. It does not provide geographic enumeration, address/coordinates, Payment price or authoritative Operations interval availability. Existing layout visibility is not approval to publish all owner catalogue fields. |
| Facility stored configuration | `Domain/Entities/ParkingSiteEntity.cs`, `Domain/ParkingStructure.cs`, `Persistence/ParkingDbContext.cs` and raw repository map address/nullable coordinates; slots carry supported vehicle type; units/capacities carry configuration | Coordinates exist; publication/readiness and explicit discovery lotType/category mapping do not. Do not equate ACTIVE with approved/open/ready or infer whole-site categories solely from current visible slot counts. |
| Operations reservation/gate | `...API/Controllers/ReservationsController.cs` GET list/lookup → `ReservationUseCases` handlers → `IOperatorIdentityClient.RequireSiteAsync` → `ReservationGateStore` → `ReservationDbContext`, tenant/site predicates | Operator records are private and cannot supply public search by filtering in IoT. S06 own list/detail is a separate Account-scoped route and is not discovery. |
| Operations local state | `Domain/Entities/ReservationEntities.cs`, `ReservationDbContext.cs`: reservations, parking sessions, allocations, site capacity pools, structure state/holds; `StructureSafetyStore` uses pools for structural guards | Candidate authoritative inputs exist, including pool UpdatedAt. No discovery handler, interval evaluator, geographic ranking, source-freshness policy or paged read was found. Summing/subtracting counters is not an approved availability algorithm. |
| Payment calculation | `...API/Controllers/PricingController.cs` POST `/api/v1/pricing/calculate` `[Authorize]` → `CalculateParkingFeeQuery` / validator / handler → `IParkingTariffPort` (`TariffPersistenceAdapter`) → Payment `Tariffs` + `PolicyRevisions`; alternatively reservation snapshot port or privileged policy override → domain calculator | Real pricing logic exists. It is not a published discovery tariff/estimate API. Current adapter picks latest-created active/nondeleted tariff, not an established future-effective discovery contract. Reservation/override branches must not be exposed by discovery. |
| Payment authentication | `...API/Program.cs`: Development DevAuthenticationHandler; otherwise AddAuthentication without concrete scheme | No verified production SERVICE trust for Operations discovery. D01 read delegation does not approve reuse here. Do not use DevBearer/shared Admin token or broaden audiences. |
| IoT | `...Application/Assistant/GetAssistantCapabilities.cs`: SearchParking literal false; `Chat/ReadTools.cs` and `API/Http/ReadToolHosting.cs` expose only approved vehicles/reservations/detail | No wired search adapter or parking card. Existing fallback says live search unavailable. |

Revalidation searched current C# route registrations/controllers and discovery/Haversine/parking-search/estimate references across `src/Services`, then inspected the chains above. No `/api/parking-search` implementation was found. This supersedes only G01 survey precision: **data columns and a calculator exist; the approved public source projection and complete integration do not**. G04–G07 stay OPEN.

## Decision packet — concrete scope, not authority by implication

Recommended incremental slice: approve the following owner read contracts only after the mappings/trust below are supplied. Reuse each owner's existing storage/calculator behind inward Application ports. Operations assembles and ranks; IoT only displays typed results. No persistence mirror or cross-service SQL. All routes below are PROPOSED and absent.

Alternative: retain search disabled until owners deliver these independently (current safe state). It costs no new runtime/schema/trust work but cannot complete S06. Reusing the owner catalogue, layout counts or authenticated Payment calculate directly would be smaller mechanically but violates public scope/authority and is rejected. A new Search service or general service-auth migration is outside this slice.

### P01 — Facility public discovery candidates

| Item | Proposed contract |
| --- | --- |
| Method / URL | GET `/api/public/parking-sites` |
| Auth / scope | PUBLIC; only explicitly publication-eligible lots and explicitly public fields; no Bearer forwarded, no owner/contact/tenant/telemetry data |
| Query / no body | lat/lon/radiusKm and vehicleType/lotType as below; limit default50/max100; cursor max2048, bound canonical filters and publication/config revision |
| Response | success/data `{items:[{siteId,name,address,latitude,longitude,lotType,supportedVehicleTypes,configurationVersion,asOf}],nextCursor,asOf}`; UUID IDs, finite coordinates, nonempty name/address; required known categories and lotType; ISO UTC source asOf |
| Ordering / paging | siteId ASC keyset, candidates inside radius; Operations computes final distance ordering, never sort just the first candidate page |
| Source | Facility-owned sites, units and category configuration only. Exact approved/open/publication and lotType/category mapping is P01 OPEN; missing coordinates cannot become zero or a guessed geocode |
| Privacy / caching | Cache-Control no-store initially; service/application access logs must omit query strings and headers. No origin stored in durable logs |

Synthetic request: `GET /api/public/parking-sites?lat=10.77&lon=106.70&radiusKm=5&vehicleType=CAR&limit=50`.
Synthetic successful empty: `{"success":true,"data":{"items":[],"nextCursor":null,"asOf":"2026-11-05T03:00:00Z"},"traceId":"synthetic-facility"}`.

P01 must specify handling of otherwise published lots with missing/malformed required data: proposed fail the read with503 rather than silently omit them and claim a complete empty set. Intentionally unpublished lots remain invisible. Publication is a Facility business decision, distinct from S04 FAQ Admin publication. No new publication column/schema is authorized by this document.

#### P01 Facility decision record · 2026-11-06

Read path verified against Facility-owned source. The remaining decision is what durable evidence authorizes anonymous publication. [Facility requirements](../../01-requirements/srs/modules/02-facility-capacity.md) separate lot approval, activation and operational opening.

| Public fact | Current source and limitation |
| --- | --- |
| Site identity and location | `parking_sites` has tenant, status, soft deletion, name, address, nullable coordinate pair and updated_at. The owner profile write can edit these fields. The current read uses owner tenant scope. |
| Approval/opening | `OwnerOnboardingStore.ProvisionAsync` inserts an ACTIVE site with zero configured physical capacity and records an onboarding payload for idempotent replay. Neither the insertion nor the replay records a public listing decision. |
| Lot type | User `OwnerLotSetupDto` accepts outdoor/basement/multi-storey. Facility `OwnerOnboardingService` validates lot name/address and the store retains a provisioning snapshot. Owner-created sites can also be created directly. A snapshot alone cannot establish a current lot type for both creation paths. |
| Supported categories | Facility DDL distinguishes `spatial_unit_capacities.vehicle_type`, `parking_slots.supported_vehicle_type` and `slot_compatibilities.vehicle_type`. These describe structural compatibility. Publication needs explicit site configuration for CAR/MOTORCYCLE, independently of occupancy or free slot count. |
| Revision and service hours | The profile writer sets site updated_at. This timestamp alone cannot invalidate all pages on unpublication, category edits, tenant suspension or closure. No complete Facility mapping for public-list opt-in, effective service windows and a catalogue revision was found in the inspected Facility DDL, overlays or production writers. |

**Decision requested from the lead.** Recommended source is a Facility-owned explicit publication record. The alternative is to use an existing Facility publication writer/table supplied by the owner, if one exists outside this checkout. The former needs new persisted state and an authoritative producer. The latter needs a verified contract naming the writer, fields, revision and unpublication behavior. Both approaches consume the same public DTO.

Proposed rules for the new-record option:

1. An authorized Facility owner for the tenant chooses to publish a site with its current lot type and supported categories. Default is private for new and existing sites. Application approval or creation creates no publication grant. Facility validates current approved/provisioned identity, active tenant/site and configuration. Missing approval evidence blocks publication. Withdrawing the grant, deleting or suspending the tenant/site hides the site on all subsequent reads.
2. The grant binds the public identity, address, coordinate pair, lot type and category configuration to a reviewed version. A public-field edit invalidates the grant until republished, so an address change cannot expose a new location automatically. Existing records receive no synthetic backfill. The owner must identify the Facility writer that publishes, invalidates and withdraws the grant before implementation, including transactions and competing site/tenant writes.
3. Keep a separate Facility source for effective service restrictions. A published site can be listed while closed. Operations needs coverage for the requested instant/interval, with version and observation time. Incomplete coverage yields unknown. Public P01 returns only approved catalogue fields. Detailed closure events and reasons stay internal pending a separately approved restrictions read.
4. Increment a catalogue revision atomically on a change affecting candidate membership/public fields. Read page and revision in one consistent local view. Protect an opaque cursor binding canonical filters, revision and last UUID. Changed revision returns409 requiring page one. Invalid, oversized or mismatched cursor returns400. Current eligibility is rechecked on continuation. Missing source/schema or malformed published data returns503. A healthy configured catalogue with no eligible rows returns200 empty.

**Acceptance cases for P01 (pending implementation).** Use synthetic anonymous HTTP requests through the Facility host. Cover an approved/provisioned ACTIVE site without a grant, a published site with zero available spaces, absence vs zero for coordinates, category independent of occupancy, public-field edits/unpublication between pages, cross-tenant admin data exclusion, revision races, tampered cursors, cancelled reads, source outage and privacy of request logs. Verify only allowlisted fields in the response. No fixtures populate a production catalogue. Local database concurrency and deployment remain UNVERIFIED.

Suspension/deletion invalidates the grant. Restoration/reactivation requires deliberate republication, covered by a restore-without-grant case. Fresh reads suppress withdrawn listings from that point. A previously delivered card retains its historical asOf under the S05 history contract. The public configurationVersion is an opaque per-site publication version. The continuation cursor must protect confidentiality and integrity of the origin/filter binding and internal catalogue revision, without exposing tenant identifiers, global change counts or internal row versions. Publication is restricted to an Owner's own lot under the [permission matrix](../../01-requirements/srs/permissions.md).

**Final approval evidence is a separate dependency.** User `OwnerRegistrationService.ReviewAsync` records intent and calls Facility provisioning *before* starting the transaction that finalizes approval. A crash between those steps can leave an ACTIVE site and provisioning receipt while approval is pending. Test that case with valid coordinates and an attempted owner publication: grant denied, site absent. The proposed producer needs a trusted, finalized decision bound to the correct application/site/tenant, including additional lots. Proposed delivery is a narrowly scoped receipt of the committed decision from User to Facility, with replay and correlation checks. The owners must approve its transport/trust, lifecycle and recovery, or identify an already implemented contract. Catalogue reads stay Facility-local.

Operations `scripts/database/microservices/03b-reservation-operations-service-db.sql` declares `search_facility_projections` with default is_open=true and cached_available_spaces=0. No production C# writer/reader was found for that projection. Those defaults supply neither publication consent nor interval capacity. The proposed mapping during reviewed publication converts User's approved `multi-storey` spelling to the public `multi_storey` value, retaining outdoor/basement and rejecting unrecognized types. Exercise that mapping for both onboarding and direct-created sites with an explicit lot type.

Additional paging checks: fetch limit+1 in UUID order, with first page, last page, cursor exhaustion and no duplicates at a stable revision. Distinguish changed filters (400) from changed catalogue revision (409). For a granted record with missing coordinates, radius membership cannot be established: return503. For a complete valid coordinate pair provably outside the radius, exclude the row before validating unrelated display fields. At publication time validate the entire public snapshot before making any row discoverable.

### P02 — Payment discovery pricing (read-only, not booking quote)

| Item | Proposed contract |
| --- | --- |
| Method / URL | POST `/internal/discovery/prices:read` (batch query; no side effects) |
| Auth / origin | SERVICE; Operations-only intended caller with explicit credential/audience/route authorization contract still OPEN. No delegated customer Bearer or fabricated actor |
| Request | `{siteIds:[UUID],vehicleType,at,startAt?,endAt?}` max100 distinct IDs; server-controlled query time at, optional interval paired. No tenant/account/reservation/session/order IDs, policy override or supplied price |
| Scope | Approved published candidates only; resolve site→tenant inside Payment-owned trusted mapping, or owner-approved authenticated mapping contract. Such mapping is not currently established: P02 blocker, never guess or read Facility DB |
| Response | `{items:[{siteId,price:null|{kind:"tariff"|"estimate",amountVnd,billingUnit,policyVersion,asOf},reason:null|"not_configured"|"not_supported"}],asOf}` in success/data envelope |
| Semantics | No interval: currently applicable tariff/unit, no invented duration/total. Interval: Payment-owned nonbinding total estimate; no quoteId, expiry guarantee, hold, entitlement or booking terms acceptance |
| Paging | No cursor; bounded batch must return exactly one outcome per requested site, duplicates/missing rows are malformed dependency responses |
| Source | Reuse tariff parser/domain calculator only after owner approves effective-time mapping and discovery estimate semantics; walk-in calculator must not silently become a reservation quote |

Synthetic request: `{"siteIds":["11111111-1111-4111-8111-111111111111"],"vehicleType":"CAR","at":"2026-11-05T03:00:00Z"}`.
Synthetic response: `{"success":true,"data":{"items":[{"siteId":"11111111-1111-4111-8111-111111111111","price":null,"reason":"not_configured"}],"asOf":"2026-11-05T03:00:00Z"},"traceId":"synthetic-payment"}`.

Amount is nonnegative integer VND; zero only when Payment explicitly returns a valid zero tariff/estimate. Unit is Payment-defined, not normalized by IoT. Explicit missing policy can return unknown; transport/auth/DB outage must fail the call, never become not_configured. Proposed PUBLIC Payment endpoint is an alternative but would require independent publication/abuse approval; not selected implicitly.

#### P02 Payment decision record · 2026-11-06

The current calculate route serves checkout. Its handler can use a policy override, a reservation snapshot or an active walk-in tariff. The active-tariff adapter selects the newest-created active tariff, takes the highest revision number (or 1) and parses the mutable tariff row. The Payment DDL and entity do have effective_from, effective_to, scope and policy_values on revisions. The adapter does not evaluate those fields when choosing the revision. The site/category index permits multiple active rows. A row's creation time or revision number alone cannot establish its effective price.

There is also a concrete false-price risk. `TariffSnapshotParser` substitutes an all-day 20,000 VND/hour block when it produces no blocks and substitutes a zero rate for a block lacking its rate. It can default unrecognized vehicle types to CAR. Discovery must validate the underlying versioned policy strictly before any calculation. Keep checkout changes outside P02. The current domain calculator validates normalized day/night coverage and rounds the final amount, but applies walk-in grace from the supplied snapshot. The example revision insert copies tariff_rules into policy_values; the grace period is a separate mutable tariff column. The sample SQL supplies neither runtime evidence nor approval to reuse walk-in grace for discovery estimates.

**Proposed narrow pricing semantics for owner review**

- Current mode selects exactly one eligible site/category revision at `at`, using `effective_from <= at < effective_to` (null end means unbounded). Require an exact SITE scope and site match for this first slice. Read a validated explicit local time block and show its rate in VND/hour. The effective timestamp selects the block. The source asOf records the Payment observation time, independently of the request timestamp.
- Interval mode uses the explicitly approved nonbinding discovery pricing policy for the entire half-open interval. For the first slice, propose a single fully covering revision and Asia/Ho_Chi_Minh. Return `not_supported` for an otherwise valid schedule spanning a revision change. Decide whether grace or a minimum charge applies and supply its versioned evidence before enabling interval estimates. The existing walk-in calculator can only be reused if the agreed semantics match. Sum the applicable unrounded segments and round once using Payment's approved rule.
- Validate policy scope, category, explicit rates (zero is valid), time zone, complete block coverage and revision provenance. Conflicting tenants for one site, ambiguous effective revisions, malformed/partial policy data or an unexpected batch shape fail the dependency (503). A successful evaluation can return `not_configured` for a known published site with no configured tariff and `not_supported` for a known unsupported mode. Payment tariff rows alone cannot establish publication or resolve an unknown site's tenant merely because no tariff exists.
- Proposed trust boundary: Operations admits only P01-eligible sites, Payment independently validates the machine caller and an agreed site/tenant binding. Use a route-specific machine credential with intended Payment audience, Operations identity and discovery-read permission. Define issuer, verification, rotation, expiry/revocation and source of site-binding evidence before implementation. The current DevBearer accepts a nonempty token and synthesizes roles. D01 user delegation provides no P02 authority. An authorization failure at Payment maps to a sanitized dependency failure at the public search boundary.

**Synthetic HTTP/DI acceptance vectors, pending implementation.** A test policy with local day [06:00,18:00) at 10,000 VND/hour, night at 20,000 and explicitly approved zero grace gives 20,000/hour at 18:00. An interval [17:30,18:30) within one revision yields a 15,000 VND estimate. Test exact revision-end equality, a future revision with a higher number, multiple active matches, missing rate vs explicit zero, incomplete coverage, revision-crossing intervals and concurrent policy replacement. For trusted batch calls, test mixed known/unknown values, missing rows and duplicate IDs, invalid/expired machine credential, valid credential with wrong caller/scope, timeout/cancel and whole-batch outage. Assert no account IDs, coordinates, token, private URL or upstream body enter provider calls or logs.

**Decision required for implementation:** approve the SERVICE transport/identity and publication-to-tenant evidence contract, effective SITE revision selection and the versioned interval pricing rules. Without a verified source for these, P02 stays OPEN. No endpoint, credential, database change or discovery price was produced in this round.

P02 review clarifications incorporated into those proposed conditions:

- Selecting an exact SITE revision requires Payment to establish its uniform applicability across eligible locations for the category and time under the approved scope precedence. Exercise SITE plus a differing effective ZONE override, and a site with only a higher-level fallback. If valid scope resolution exceeds this first slice, return null/not_supported. An authoritative lookup finding no effective tariff for a known site/category at the requested instant returns null/not_configured. A contradictory schedule or incomplete required policy record fails503.
- For an accepted interval estimate, sum exact segment charges and round half-up **once** to the nearest 1,000 VND. A fractional segment test should sum two charges of 1,249.75 to 2,499.50 and yield 2,000 VND. Two charges of 1,250 sum to 2,500 and yield 3,000 VND. An incompatible configured rounding mode fails closed with503. Duplicate request IDs return400. Duplicate, missing or unrequested IDs in a success response are malformed and produce503 at Operations.
- Pin the machine scheme and policy exclusively to the discovery route, with separate authentication in both directions. Synthetic tests must reject customer JWTs and DevBearer at P02 and reject P02 credentials on protected customer/operator pricing and order routes. Existing Development DevBearer accepts arbitrary nonempty Bearer text, so merely registering a named scheme would not prove reverse isolation. Proposed transport: a dedicated machine credential header on fixed HTTPS between services; check its presence only for the approved internal route, reject fallback credentials and never put a machine credential into a customer Bearer header. Production trust, caller proof and credential lifecycle still need security-owner approval. Development with permissive DevBearer is not a deployment acceptance environment.
- The Operations client pins one HTTPS origin and exact path, disables redirects, cookie handling and sensitive request logging. Test 3xx with a distinct destination and verify zero second requests and zero credential disclosure. Verify caller cancellation and deadline. Existing S06 named Bearer clients are outside this trust decision.

### P03 — Operations public discovery

| Item | Proposed contract |
| --- | --- |
| Method / URL | GET `/api/parking-search` |
| Auth / scope | PUBLIC, no incoming Bearer forwarded to Facility/Payment; only publication-approved Facility candidates; no private Operations records in response |
| Request | No body. S01 lat[-90,90],lon[-180,180] finite required; vehicleType CAR/MOTORCYCLE required; radiusKm >0 default5/max50; paired offset-ISO startAt/endAt with end>start; timeZone default Asia/Ho_Chi_Minh validated; availableOnly defaultfalse; optional lotType outdoor/basement/multi_storey; limit default20/max50; cursor max2048 |
| Price filter addition | PROPOSED minTotalVnd/maxTotalVnd nonnegative integers, min≤max, interval required; reject current-mode total-price filter400 (unlike tariff units cannot be compared). No model ranking or silent unit conversion |
| Response | S01 ParkingSummary plus `lotType`, `supportedVehicleTypes`, `sourceAsOf:{facility,operations,payment}`, and explicit freshness/unknown reason. Preserve S01 availability.kind=current/interval; availableCount nullable means unknown, not a third time mode |
| Authority | Operations Application fetches all bounded candidate pages, queries own capacity facts, asks Payment; Domain evaluates resolved availability facts; Infrastructure HTTP and Persistence local queries only |
| Order | Haversine distanceMeters ASC/siteId ASC, straight-line not road distance/ETA. Apply category/type/radius/availability/interval-price filters before page selection. Unknown never passes availableOnly or a price predicate |
| Freshness | Top-level asOf is assembly time, not proof every source is current. Per-source UTC asOf retained; no relabeling historical values. Exact age/skew limits and accepted state mappings are P03 OPEN |
| Paging proposal | Live keyset with cursor bound all normalized filters, ordering version, query-time anchor and last distance/siteId; no distributed frozen snapshot guarantee. This deliberately narrows S01 snapshot wording and needs approval. Revalidate publication on every page; edits can move results between pages |

Synthetic current request: `GET /api/parking-search?lat=10.77&lon=106.70&vehicleType=CAR&limit=20`.
Synthetic result (illustrative DTO, not a fixture fallback):

```json
{"success":true,"data":{"items":[{"siteId":"11111111-1111-4111-8111-111111111111","name":"Synthetic lot","address":"Synthetic address","latitude":10.77,"longitude":106.70,"distanceMeters":0,"lotType":"outdoor","supportedVehicleTypes":["CAR"],"availability":{"kind":"current","availableCount":null,"asOf":"2026-11-05T03:00:00Z","reason":"unknown"},"price":null,"sourceAsOf":{"facility":"2026-11-05T03:00:00Z","operations":"2026-11-05T03:00:00Z","payment":"2026-11-05T03:00:00Z"},"fieldStatus":{"facility":{"freshness":"fresh","reason":null},"availability":{"freshness":"unknown","reason":"unknown"},"price":{"freshness":"unknown","reason":"not_configured"}}}],"nextCursor":null,"asOf":"2026-11-05T03:00:00Z"},"traceId":"synthetic-search"}
```

Proposal protection limits: max1000 candidates per search, five-second total owner deadline; exceeding bounds/deadline returns503/504, never a truncated result labelled complete. No server search snapshot store/dependency added. Cursor integrity, expiry and key management must be agreed before implementation (P03); no raw-coordinate token logged or passed to model. Old/expired cursor returns409, tampered/filter-mismatched cursor400. Unknown JSON/query fields are ignored without changing authority; recognized malformed values fail400.

Current availability cannot be inferred from free visual slots alone. Interval evaluation must account for applicable claims, allocations, sessions, restrictions, backup/quotas and overlapping intervals without double counting. Exact existing-state mapping, freshness and missing-input policy are owner deliverables, not an invented subtraction formula. Known zero remains full; explicit unknown stays nullable. Stale fields must be marked stale/unknown and cannot satisfy availableOnly/price filters. Upstream transport/storage failure fails search, including when filters would otherwise yield an empty page.

**Additional P01/P03 source blocker:** the P01 public candidate DTO does not supply opening/closure/service restrictions. Operations local structure state is not proof of all Facility-owned restrictions. Before implementation, Facility must specify an approved bounded read/projection for these facts (including effective interval, version and asOf), or extend P01 with explicitly public restriction facts. Route/trust/field mapping for any internal alternative needs separate review; no implicit extra owner call or assumption that ACTIVE means open. Until supplied, authoritative current/interval availability cannot be declared complete.

**Per-field status:** required `fieldStatus:{facility:{freshness,reason},availability:{freshness,reason},price:{freshness,reason}}`. Each freshness is `fresh|stale|unknown`; reason is null only for fresh known data, otherwise an allowlisted `stale|unknown|not_configured|not_supported`. Retain the Payment reason when price=null. `sourceAsOf` timestamps are nullable only when the owner explicitly reports no known source timestamp, never filled with assembly time. Stale/unknown availability has availableCount=null; stale/unknown price has price=null. Fresh price may coexist with unknown availability and vice versa. Filtering uses the status of the relevant field: availableOnly requires fresh availability >0; a price predicate requires fresh estimate. Missing/stale required Facility publication/configuration fails the read503 rather than returning a potentially unauthorized card.

#### P03 Operations decision record · 2026-11-06

**Status.** Proposed public availability semantics. The Operations host exposes gate, structural safety and own-reservation flows. None supplies a public discovery evaluator. P01 catalogue/restriction evidence and P02 public pricing remain dependencies. [Allocation requirements](../../01-requirements/srs/modules/04-reservations-allocation.md) and [Facility capacity requirements](../../01-requirements/srs/modules/02-facility-capacity.md) govern the business inputs.

| Operations evidence | Consequence for discovery |
| --- | --- |
| `ReservationGateStore` links an allocated reservation and a parking session on entry. On checkout it records ExitTime and releases matching occupied allocations. | A reservation, allocation and session may describe the same occupied space. Use correlated resource/claim identity under the same tenant/site, vehicle compatibility and applicable time coverage. A scheduled end alone never proves departure. |
| `StructureSafetyStore` unions protected slot IDs and checks live reservations, unclosed sessions, allocations and pool counters for structural changes. Its snapshot returns protected IDs, a live flag and pending/protected pool counts. | It answers an edit-safety question. Its predicates do not establish a complete time-indexed inventory of compatible units. Never invoke a structural hold for a public read. |
| `ReservationDbContext` maps `site_capacity_pools`. Its overlay stores category, optional spatial unit, total/quota and current counters with updated_at. | Parent/site summaries can overlap unit-level capacity. A timestamp on a pool does not prove freshness of its claim inputs. The DDL for `search_facility_projections` defaults open and cached_available_spaces to zero for the count; neither default is an observation. |

**Proposed meaning of a known count.** Current mode measures compatible capacity admissible at the Operations clock instant under current physical state and service restrictions. Interval mode measures how many additional compatible capacity claims can be honored for the *entire* requested half-open interval under the effective buffer, quota and resource constraints. Both are public, category-specific observations. Account eligibility and future booking confirmation use their own flows. The count must have an explicit owner-approved unit and resource policy, including how capacity-only pools and fixed slots combine. Until that mapping is supplied, the field is unknown.

| Case | Proposed observable result and synthetic oracle |
| --- | --- |
| Same physical space appears in multiple records | Two compatible physical spaces, one occupied session linked to its reservation/allocation, other space proven free, no other restrictions. Current count is 1. A second independent claim overlapping the relevant capacity is counted separately. |
| Adjoining intervals or policy buffer | With zero effective buffer, a claim ending exactly at the requested start has no time overlap. Test a positive effective buffer that creates overlap at that same boundary. Operations resolves the versioned buffer policy. |
| Fragmented availability | In a fixed-slot product with no mid-stay transfer, A is free only in the first half and B only in the second half. There is no feasible fixed slot for the whole stay even though one slot is free at every instant. A fungible pool needs its own approved rule. |
| Holds, overstays, unknown state | A pending-payment claim may block capacity. Resolve competing payment/expiry using committed owner state. Never release a claim merely because the chat clock crossed a deadline. An unclosed session remains evidence of occupancy even past expected end; unknown future release cannot establish a known future count. |
| Empty, full, unavailable | A verified count of zero can be returned on a published card and is excluded by availableOnly. Explicit incomplete/stale coverage yields null with fieldStatus; a failed mandatory owner read yields503/504. An empty filtered result means zero proven matches for that filter. |

**Freshness and pagination.** Capture one UTC evaluation instant per page and retain each source's observation time and revision from the read. Require authoritative coverage for the relevant period, including effective closures, service restrictions and configuration changes. The age/skew thresholds and equality boundaries need owner approval; tests use an injected clock just below, at and above each threshold. A future timestamp beyond allowed skew cannot qualify as fresh. Cancellation stops all remaining calls. For current-mode continuation re-evaluate current truth; bind the original search anchor in the protected cursor for query identity and expiry only. Every page rechecks publication. If the catalogue revision changes, restart with409. Under the proposed live-keyset policy, price/availability changes can move items into or out of subsequent filtered pages. Return page asOf and do not claim one cross-service snapshot.

**Ranking acceptance.** Given published candidates across two Facility pages, put the nearest qualifying lot on the second Facility page and verify it is first on Operations page one. Compute Haversine from user-supplied coordinates in Operations, compare the unrounded distance before display rounding and break exact ties by site UUID. Test radius equality, the longitude wrap at ±180°, equal-distance ties, nullable price versus a confirmed zero estimate, changed filters/cursor tampering, candidate ceiling and timeout after the first upstream page. Return a partial failure as a safe error, never a partial ranking presented as complete. Search uses fixed PUBLIC paths and redacted query logging. Facility/Payment outages and malformed source batches remain distinct from a successful unknown field.

**Decision for Operations.** Approve the product/unit for each count, authoritative claim correlation and interval/buffer policy, the source of dated restrictions, per-source age/skew thresholds, and live pagination versus frozen-snapshot semantics. Supply the actual read path and state mappings before wiring IoT. The proposed tests require real host/DI and synthetic owner responses; consistent PostgreSQL reads under concurrent check-in, departure, expiry, restriction changes and cursor-key rotation require separate isolated runtime verification.

The P01 public envelope currently lacks catalogue-wide comparison evidence on empty/final pages. P03 continuation is blocked until Facility approves either a scoped revision check or an opaque equality token on every P01 page, including empty and final pages. It must hide internal revision counts and bind to the same normalized candidate query. Any revision change during candidate traversal invalidates the page. The public P03 cursor needs separate cryptographic purpose, confidentiality and integrity; bind origin, radius, category, lot type, interval/timezone, availability/price filters, limit, ordering and anchor. Test cross-feeding Facility, own-reservation and search cursors. Send no-store on P03 success and errors; redact query strings, request bodies and sensitive headers at ingress, client diagnostics and proxies.

The public calls are IoT → Operations discovery and Operations → Facility candidates. Operations → Payment uses the separate restricted P02 SERVICE route. Facility effective restrictions still need their own approved source and trust. No customer credentials are forwarded on discovery hops.

**Physical evidence and projection horizon.** Operations must identify the trusted source of verified departure. Checkout writes ExitTime and releases allocations, but that writer alone cannot establish verified gate passage. Test checkout/payment/gate-open with no verified departure: do not increase availableCount. If another driver's protection covers the same occupied slot, union the unavailable resource once and retain that protection after verified departure. Resolve the effective Occupancy Validity Timespan independently of telemetry freshness. With a 10:00 evaluation and the default four hours, project the accepted occupied space through the 14:00 horizon, unless superseded by newer accepted evidence. Test just before, exactly at and just after that horizon and a different configured duration. Beyond coverage use unknown if other authoritative facts cannot establish the count. Expiry never frees physical occupancy.

**Bounded work.** Proposed discovery-only protection: duration up to 24 hours and start up to seven days ahead of the authoritative clock, inclusive (owner approval OPEN). These proposal limits are distinct from configurable booking policies. P02 and P03 validate independently before storage or calendar iteration. Test exact bounds and one tick beyond, backward intervals, cancellation between batches and during evaluation. The current Payment calculator iterates calendar days without a cancellation parameter. The P02 implementation must bound local CPU work and observe cancellation inside a multi-day/batch evaluation as well as cancel HTTP. A client timeout alone does not establish owner cancellation.

## Shared errors, retry and observable outcomes (all proposed routes)

Uniform proposed envelope: `{"success":false,"code":"VALIDATION_FAILED","message":"Invalid search input.","errors":[],"traceId":"synthetic"}`; HTTP status only in transport. Existing service error shapes are not silently migrated.

| HTTP | Code / trigger | Recovery |
| --- | --- | --- |
| 400 | VALIDATION_FAILED / INVALID_CURSOR; invalid bounds, missing pair, invalid enum/timezone, changed filters | Correct input; do not dispatch invalid owner reads |
| 401/403 | UNAUTHENTICATED / FORBIDDEN on P02 SERVICE trust | Fix authorized service setup; never fallback to DevBearer. P03 maps owner trust failure to503, not a guest login prompt |
| 404 | RESOURCE_NOT_FOUND for a requested source site no longer published/visible | No private existence leak; aggregate revalidates candidates, restart409 on concurrent removal rather than fabricate a card |
| 409 | SEARCH_CHANGED / CURSOR_EXPIRED / source version conflict | Explicit fresh search from first page; no automatic broadening |
| 429 | RATE_LIMITED | Sanitized bounded Retry-After; no automatic retry initially |
| 500/503/504 | INTERNAL_ERROR / DEPENDENCY_UNAVAILABLE / DEPENDENCY_TIMEOUT | Safe failure, explicit user retry; no raw upstream body, URL or header |

P01/P03 list with no eligible records returns200 items[] only after successful authoritative reads; no implicit404 for empty. P02 returns per-site explicit unknown only for successful owner evaluation, not outage. Missing batch rows or malformed200 cause503. Cancellation propagates and must not persist a success/empty answer. Proposed first slice has no automatic retry (narrowing S01 maximum-one allowance); subsequent user read is fresh, not a hold/price promise. Off/cancel/expiry checks and S05 terminal ledger/recovery remain mandatory in IoT.

## Approval and implementation gates

| Gate | Concrete unresolved decision | Owner |
| --- | --- | --- |
| P01 OPEN | Exact persisted publication/readiness eligibility, category and lotType mapping; handling nullable/invalid coordinates; any needed schema submitted separately | Facility / product lead |
| P02 OPEN | Minimal SERVICE trust (issuer/audience/credential provisioning/rotation/route permission), site→tenant authority, current/future-effective tariff and nonbinding interval estimate semantics | Payment / Operations / security lead |
| P03 OPEN | Availability state/counter mapping, age/skew thresholds, live cursor vs S01 snapshot, cursor expiry/key lifecycle, numeric limits and price-filter addition | Operations / product lead |
| P04 BLOCKED | After P01–P03 approved and implemented/tested, allowlist exact public search path in IoT without Bearer; typed parking cards from owner, no model dispatch for search | IoT |

Approval of route names alone does not close these gates. Existing D01/D05 and D03 permissions are not enlarged. No credentials/configuration read or changed, no schema applied, no runtime/container/dependency work. Independent work here is source evidence, this proposal, regression verification and review; search implementation waits.

P04 continuation is scoped to the current authenticated Account and conversation, using the existing maximum six turns/12000 characters local context budget. Never recover an origin/cursor from another conversation or Account, and never redispatch a search automatically after restart/replay. Missing or expired continuation context asks for explicit search parameters again. Origin/filter/cursor context stays server-local and outside provider inputs/logs; no new retention permission is implied. Any additional durable location storage needs a separate retention/privacy decision before implementation.

## Acceptance plan — search NOT EXECUTED / UNVERIFIED

Use synthetic JWT/service identities, fake provider/mock HTTP with real host/DI. No production fixture fallback. Tests must prove:

- Missing/ambiguous manual origin asks clarification locally before owner/model call; explicit coordinates only; no IP, GPS request or geocoder. Bounds/NaN/Infinity, interval pairing, timezone and invalid values/combinations of recognized filters rejected400. Separately assert unknown field names are ignored without granting filter semantics or authority.
- Candidates across multiple Facility pages rank globally; equal distances stable by ID, radius equality and chosen expansion; filters before pagination, cursor tamper/expiry/filter change, concurrent unpublication; budget overflow is unavailable, never partial-as-complete.
- Current tariff/unit vs interval estimate, full0 vs unknown null, stale/future-dated source asOf, no unlike-unit comparison, no invented free/available; interval-overlap and missing configuration cases signed off by owners.
- Successful empty distinct from every400/401/403/404/409/429/5xx/malformed200/timeout/cancel; P02 auth errors never guest login; no retries after cancellation/off; settings changed during preparation and dispatch.
- Provider capture contains only server FAQ topic + published public excerpt for FAQ and zero requests for search; raw text/history/location/IDs/token/plate/owner records absent. Capture HttpClient and request logs: no private URL/query/header. PUBLIC adapter never sends incoming Authorization or follows redirect.
- Replay/history retains typed cards/asOf, not a new owner read; explicit search is fresh. Preserve all vehicles/reservations/date/context/ledger/recovery regressions and false capability until all wiring/authorization gates pass.

Separately authorized real-integration runbook: isolated synthetic owner databases verify real SQL category/publication/interval and tariff translation; actual SERVICE trust denies wrong caller/audience; publication/tariff/capacity concurrent changes and cursor restart/key rotation; missing schemas and owner outage fail closed. No mock proves SQL, concurrency, restart or deployment. SQL S02/S05 remains unapplied, S04 corpus DRAFT, PostgreSQL/runtime UNVERIFIED.

## Review checks

- [x] Implemented source vs proposed APIs separated; service ownership and scope documented.
- [x] DTO/nullability/bounds/errors/retry and approval blockers made reviewable.
- [ ] P01–P03 explicitly approved with owner mappings and trust evidence.
- [ ] Search HTTP/DI tests, source wiring and authorization complete; capability may then be considered.
- [ ] Real database/runtime acceptance (separate authorization).
- Independent contract/privacy/test review results recorded in the S06 task record; review is not implementation or runtime proof.
