# S06 read integration — decision packet

| Metadata | Value |
| --- | --- |
| Status | D01 bounded read delegation + D05 minimal G03 reads APPROVED in this chat; G01 and other D01/D05 scopes OPEN |
| Version / date | 0.1 / 2026-11-05 |
| Owners / author | IoT, User, Operations; Facility/Payment source owners / Codex |
| Jira | OPEN |
| Baseline | HEAD 3d3def763870f66f8da71386357a66f776fa61e5 plus preserved S01–S05 working tree |
| Implementation | User vehicles adapter and Operations minimal own list/detail implemented; search NOT IMPLEMENTED; offline evidence in S06 handoff |

Approval on 2026-11-05: after the two concrete D01-S06 and D05-S06/G03 questions, user answered “Phương án nào ổn an toàn và hiệu quả thì áp dụng” and “Phương án nào ổn thì áp dụng vào”. Selected A for both: request-memory delegated Bearer and minimal Operations DTO. This approval includes the required local Operations JWT ingress for these reads only, not unrelated Operator/Payment authorization changes. No runtime trust acceptance, new dependency, migration or provider egress permission is implied. [Implementation handoff](../ai/business-data-integration.md).

## Overview

Scoped approval packet for S06 reads only. No mutation, quote, hold, payment, S07 draft or S08 UI permission is requested.

| Traceability | Reference |
| --- | --- |
| Requirements | [AI module](../../01-requirements/srs/modules/10-ai-assistance.md), [AI rules](../../01-requirements/business-rules/10-ai-assistance.md) |
| Contracts | [S01 §§11–14 / D01 / D05](assistant-s01-end-to-end-api-design.md), [matrix G01–G03](../ai/assistant-s01-api-matrix.md) |
| Authorization | [Approved classification](../../01-requirements/srs/authorization-operation-classification.md); TOKEN/REQUEST/SERVICE unchanged |

## Current source evidence

The bullets below preserve the original pre-implementation decision evidence (not current G03 status). G03 own reads were subsequently implemented as described above. For the current G01 route/auth/handler/storage revalidation and concrete owner approval gates, use the [2026-11-05 search proposal](assistant-s06-search-owner-proposal.md). In particular, Facility coordinate columns, sanitized public layout and Payment calculator exist; complete public discovery and approved pricing/availability integration remain missing.

- G02 exists: User `AccountWorkflowController.VehiclesController.List` → `ListOwnVehiclesQuery` → `VehicleRequestHandler.RequireDriverAsync` → `VehicleRegistrationService.ListAsync` → `VehicleRepository.ListAsync` → User `AppDbContext`. Authentication uses `ApiAuthenticationExtensions`, RS256 issuer/audience/expiry and `CurrentAccountBearerEvents`. The handler resolves current User from authoritative access; `DriverAccountsAsync` decides memberships. Repository filters the returned Account set, active vehicles, supported type and nonempty canonical plate. IoT must neither replace that membership with its own Account filter nor select a User from input.
- G01 missing: Operations controller/host scan has no `/api/parking-search`. Facility `/api/parking-lots` → `ScopeAsync` → owner scope → `ParkingStructureService` → `PostgresParkingStructureRepository`; restricted structures are not public discovery, authoritative price or interval availability.
- G03 missing: Operations `/api/reservations` and `/lookup` → list/find handlers → `IOperatorIdentityClient.RequireSiteAsync` → `ReservationGateStore` → `ReservationDbContext`; tenant/site predicates are not own-Account predicates. Host does not register local JWT authentication. Do not expose these results to Driver then filter in IoT.
- G03 contract mismatch before any new code: `ReservationRow` has AccountId, SiteId, original/normalized plate, vehicle type and expected start/end, but no vehicle ID, site name, terms version or linked payment order. Do not fabricate required S01 fields or derive a vehicle ID from plate. D05 must approve a smaller read projection or supply authoritative enrichment contracts.
- Both User and IoT validators support configured RS256 issuer/audience. Actual runtime values/trust compatibility were not inspected (credentials prohibited); deployment acceptance remains UNVERIFIED. No audience expansion is implied.

## API Specification and choices requiring approval

### D01-S06 — delegation for existing vehicles

Recommended A: delegate the incoming, already validated Driver Bearer only to fixed configured User origin and exact `GET /api/vehicles`; no query/body, no owner headers. Disable redirects, no generic URL/tool dispatch, bounded response and cancellation. Keep credential in request memory only, never ledger/history/model/log. Owner validates token and local membership each call. Recheck ingress actor expiry before call/delivery. The S05 admitted execution may retain request-memory credential only until its deadline; restart never redispatches. Runtime enablement requires owner audience acceptance evidence; mismatch fails closed.

Alternative B: scoped token exchange/service delegation. Better audience/service isolation, but requires a new issuer/exchange/trust contract and owner support; no existing implementation found. More scope than vehicles reuse. Never use a shared Admin token or `X-Account-Id` authority.

Decision requested: approve A for S06 source/offline integration, or keep G02 disabled pending B. Neither option weakens User local access checks or changes TOKEN freshness elsewhere.

| Item | Contract |
| --- | --- |
| Method / availability | `GET /api/vehicles`; exists in User, Assistant not connected |
| Auth/scope | Driver Bearer, TOKEN ingress plus User authoritative local access and Driver membership set |
| Request | No body/query; caller cannot select Account/User |
| Existing response | `200 {success:true,data:[RegisteredVehicle...]}`; project only id, vehicleType, displayLabel for authorized UI |
| Pagination | Existing endpoint unpaged. No cursor invented; Assistant card max20 requires explicit truncation indication or a separately approved owner paging change; never report first20 as complete |
| Privacy | Plate may be UI displayLabel only; never model input; discard AccountId/imageReference/other unnecessary owner fields |
| Errors/retry | Preserve 401,403,404,409,429,5xx distinctions with sanitized codes; timeout distinct from cancellation; no automatic retry initially; no error becomes empty |

### D05-S06-G03 — smaller own reservation read API proposal

Recommended A: add only these Operations-owned reads after explicit approval of the reduced DTO; no schema change or enrichment dependency:

| Item | Contract |
| --- | --- |
| Methods / availability | `GET /api/me/reservations`, `GET /api/me/reservations/{id}`; PROPOSED, absent |
| Auth/scope | Local validated RS256 Driver JWT, Account from trusted claim; TOKEN; local `AccountId` predicate on every list/detail; missing/foreign detail uniform404 |
| List query | optional paired from/to ISO offset, to>from; limit default20/max50; opaque cursor max2048 bound Account+filters+limit; no body |
| Filtering | `ExpectedStartTime < to && ExpectedEndTime > from`; include all persisted statuses by default, expose raw owner status with unknown display fallback; no silent cancelled/expired exclusion |
| Ordering | startAt ASC/id ASC; keyset cursor, not a frozen inventory snapshot; changes between pages explicitly possible |
| Proposed row | `{id,siteId,vehicle:{vehicleType,displayLabel},startAt,endAt,status,asOf}`; no invented vehicleId/siteName/paymentOrderId/termsVersion |
| Detail | `{reservation:<same row>,asOf}`; fresh owner read each time; stable ID only, no arbitrary URL |
| Content | JSON success/data/traceId; private no-store; unknown owner query fields rejected or ignored without authority; UUID nonempty |

Sample list: `GET /api/me/reservations?from=2026-11-05T17:00:00Z&to=2026-11-06T17:00:00Z&limit=20` (illustrative dates only; runtime computes bounds).

Sample200: `{"success":true,"data":{"items":[],"nextCursor":null,"asOf":"2026-11-05T03:00:00Z"},"traceId":"synthetic"}`. Empty only on successful owner query. Detail cannot return an empty list in place of404.

Alternative B: keep exact S01 rich DTO and wait for owner-approved site/vehicle/terms/order enrichment APIs. More useful cards, but crosses additional contracts; G03 stays false until available. A is smaller and honest but changes S01 wire and yields less friendly cards. No approval is inferred for either.

### D05-S06-G01 — parking discovery remains an owner dependency

Target `GET /api/parking-search?lat=...&lon=...&vehicleType=...&startAt=...&endAt=...&limit=20&cursor=...` inherits S01 §11 proposed bounds/DTO/order. PUBLIC, no body; explicit coordinates, optional paired interval. Missing/ambiguous origin requires clarification, no IP/GPS/geocoder inference. Operations must produce backend distance/ranking, source asOf, current/interval/unknown availability and Payment-authoritative price/unknown. Null is unknown, never free/zero or available. Cursor binds filters; failed dependencies never empty200. No source found for a complete public coordinate/price/interval projection.

Recommended: keep search false and hand off G01 until Facility public data and Payment price source contracts are approved; do not invent coordinates or use operational-layout fixtures. Alternative: separately approve the full owner discovery/enrichment slice after concrete source contracts are supplied. Merely approving the outward route cannot supply missing authority. No new Search service or cross-DB query.

### Shared error / retry / state outcomes for proposed owner reads

400 VALIDATION_FAILED/INVALID_CURSOR (correct input);401 INVALID_TOKEN (refresh through normal auth);403 FORBIDDEN;404 RESOURCE_NOT_FOUND (including invisible detail);409 CONFLICT (refresh data);429 RATE_LIMITED (bounded Retry-After);503 DEPENDENCY_UNAVAILABLE (explicit retry);504 DEPENDENCY_TIMEOUT;500 INTERNAL_ERROR. Safe envelope per S01 with existing IoT errors[] discrepancy retained/documented. Never relay raw upstream bodies. No retries within first adapter implementation; explicit subsequent read can return newer data. Client cancellation is not a successful empty result. Historical card is a snapshot, not proof of current status; detail reauthorizes and reloads.

## Independent work / acceptance boundary

Can proceed: source revalidation, tests proving false capabilities and conservative egress, pure clock/timezone/date-overlap utility and offline tests. New owner adapters/auth routes/DTOs wait for scoped approval above. S05 ledger/context remains Account-owned; D03 egress stays topic+published public excerpt only. No model parses raw private text. S02 routing unchanged.

Acceptance after approval requires real HTTP/DI with fake owner transport and synthetic JWTs: same User/different Account, different User, wrong role/guest/expiry, forged owner fields, every error above, timeout/cancel, paging, empty/outage, relative day across midnight, strict overlap, stale card/detail and off. Owner SQL and deployment/concurrency/restart tests remain separately authorized and UNVERIFIED.

## Open questions and review checks

- [x] Explicit D01-S06 scoped approval; runtime audience compatibility UNVERIFIED.
- [x] D05-S06-G03 reduced DTO approved; richer enrichment remains OPEN.
- [ ] G01 source contracts supplied; otherwise capability false.
- [x] Three independent reviewers and rereview recorded in S06 task record.
- G04–G07 remain unresolved independently. No publication, migration, credential/config/runtime/dependency changes, commit or push authorized.
