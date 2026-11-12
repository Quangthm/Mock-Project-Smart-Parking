# Driver Maps & Recorded Vehicle Location — API Design Revision Report

Doc revised: `docs/03-design/api/driver-maps-vehicle-location-api-design.md` (v1.0 → v1.1). This report is Deliverable B. No source code, database migration or unrelated document was modified. `docs/03-design/api/README.md` was modified (pre-existing) to add the "Driver Maps & Recorded Vehicle Location" row with owning service "OperationService (discovery/location) + FacilityService (layout projection)" — this corrects the stale single-owner value; I did not edit README.md per task constraint.

## Revision table

| ID | Original issue | Change made | Reason | Evidence |
| --- | --- | --- | --- | --- |
| P0-01 | EP-04 assumed ZONE-only recorded location was persistable; no DB column exists | Documented that `parking_sessions.current_slot_id` is the only recorded-location field; precision today derivable only as SLOT/UNKNOWN. Added Option A (SLOT+UNKNOWN) vs Option B (add nullable `current_spatial_unit_id`). Recommended Option B, kept as OQ-DB-01 | FR-MAP-03 (CONFIRMED) requires ZONE representability; Option A cannot satisfy it | `scripts/database/microservices/03b-reservation-operations-service-db.sql:85-117`; `src/Services/OperationService/SmartParking.OperationService.Domain/Entities/ReservationEntities.cs:22-42`; `src/Services/OperationService/SmartParking.OperationService.Persistence/ReservationGateStore.cs:93-99,195,199` |
| P0-02 | EP-03 had no authorization/redaction spec; per-caller reservation filter absent | Added authorization matrix (anonymous/Driver/operator/owner/admin), identity-via-`/api/auth/me` (never parse JWT), per-caller `driverReservation` filtering rules, redaction rules, Cache-Control/Vary verification | FacilityService has zero ASP.NET auth; identity resolved via User & Access; `LayoutCaller.UserId` captured but never read | `src/Services/FacilityService/SmartParking.FacilityService.Infrastructure/LayoutCallerResolver.cs:10-66`; `src/Services/FacilityService/SmartParking.FacilityService.Application/OperationalLayoutAccess.cs:1-15`; `src/Services/FacilityService/SmartParking.FacilityService.API/Program.cs:140-182`; `src/Services/FacilityService/SmartParking.FacilityService.Application/OperationalLayoutRedaction.cs:26-37` |
| P0-03 | Cross-service dependency/failure handling unspecified; MapTiler assumed backend | Added dependency/failure matrix with timeouts (10s/15s/3s), distinct failure codes, UNKNOWN fail-closed, cached-snapshot validity; confirmed MapTiler FE-only (no C# reference) | Prevent cascading failures and false AVAILABLE; honour "frontend/local cached markers are not authority" | `FE/package.json:13`; `FE/.env.example:8`; `src/Services/FacilityService/SmartParking.FacilityService.Infrastructure/ServiceClients.cs` (10s/15s); `src/Services/PaymentService/.../ReservationSnapshotHttpAdapter.cs` (3s); `src/Services/FacilityService/SmartParking.FacilityService.Application/OperationalStateMapping.cs:13-15` |
| P0-04 | EP-03 sample declared `contractVersion:"1.2.0"` while containing proposed fields | Added §Contract version strategy: v1.2.0 baseline (existing const `OperationalLayoutContract.Version`), proposed additions ship as coordinated v1.3.0 with SPARK-244 delta pass; examples annotate proposed fields as "not in v1.2.0" | Avoid implying shipped contract carries unshipped fields; one version, one change set | `src/Services/FacilityService/SmartParking.FacilityService.Application/OperationalLayoutProjection.cs:8-12`; `src/Services/FacilityService/SmartParking.FacilityService.Persistence/PostgresOperationalLayoutReader.cs:176`; commit 76630c39 (SPARK-238 v1.3, different contract) |
| P0-05 | `isBookable` semantics unspecified; SQL NULL handling undocumented | Documented isBookable derived (`physicalState==AVAILABLE && reservationState==UNRESERVED`), redaction freezes pre-mask value, SQL NULL→UNRESERVED, UNKNOWN fail-closed, 20 canonical combos, vehicle/commitment gates, informational-only | Aligns with twin §3.2 and `OperationalStateMapping.cs` | `src/Services/FacilityService/SmartParking.FacilityService.Application/OperationalStateMapping.cs:1-30`; `src/Services/FacilityService/SmartParking.FacilityService.Application/OperationalLayoutRedaction.cs:29-37`; `src/Services/FacilityService/SmartParking.FacilityService.Persistence/PostgresOperationalLayoutReader.cs:229` |
| P1-01 | EP-01 price filtering unspecified | Added §Price filtering: hourly-only Payment model, comparable basis = interval estimated total, PRICE requires interval, no raw-unit comparison, tariff unavailable ⇒ null | Payment has no `billingUnit`/`billingBasis` field; only hourly block rates | `src/Services/PaymentService/SmartParking.PaymentService.Domain/Entities/Tariff.cs:3-16`; `src/Services/PaymentService/SmartParking.PaymentService.Domain/Pricing/PricingModels.cs:31-47`; `src/Services/PaymentService/SmartParking.PaymentService.API/Controllers/PricingController.cs` |
| P1-02 | Pagination unspecified | Added offset/page (default 20, max 100) per existing UserService convention; stable sort + siteId tie-breaker; hasMore | Only existing backend pattern is offset/page | `src/Services/UserService/SmartParking.UserService.Application/DTOs/ManagedUserDto.cs:10-11,21` |
| P1-03 | Manual-origin geocoding unspecified | Added §Manual-origin geocoding: FE (MapTiler) resolves; BE validates coords only; no re-resolution | MapTiler is FE-only (no C# reference under src/) | `FE/package.json:13`; `FE/.env.example:8` |
| P1-04 | Freshness split (isStale vs isFresh) unspecified | Added §Freshness standardization: EP-03 inherits twin `isFresh`; EP-01/02/04 use `freshness`; mixed-source rule; reconnect full reload; prototype caveat (hardcoded true) | Prevent false "fully fresh" claims | `OperationalLayoutProjection.cs:18-25`; `PostgresOperationalLayoutReader.cs:178-180` |
| P1-05 | EP-04 session lifecycle ambiguous | Added §EP-04 session lifecycle: current = ACTIVE + exit_time null; released not returned; 404 when none; allocationStatus never RELEASED on /current | Code: checkout sets COMPLETED+exit_time together; current filters exit_time null | `ReservationGateStore.cs:60-64,120-137,218-229`; `StructureSafetyStore.cs:36` |
| P1-06 | Interval validation unspecified | Added §Interval validation: pair required, ISO-8601 UTC, start<end, past rejected, max duration TBD | Consistent machine codes and UTC handling | SRS FR-MAP-01; `v1.1-contracts.md:60` |
| P1-07 | Rate limits/operational constraints unspecified | Added §Rate limits & operational constraints, all as Open Questions (OQ-4) | Unapproved; documented rather than invented | `v1.1-contracts.md:81` (V11-DES-08) |
| P2-01 | EP-02 necessity questioned | Preserved EP-02 with justification (stable handoff context, permission-checked); added OQ-10 for inline alternative | Preserve existing business requirement unless documented reason | SRS UC-SEARCH-01 step 5 |
| P2-02 | Response consistency (envelope, UUID, UTC, enums, null-vs-omitted, codes) | Added §Response consistency conventions via field tables; envelope `success/data`, errors `success/code/message`; omitted-vs-null now explicit | Template requires omitted-vs-null; v1.1 convention `success/code/message` | `v1.1-contracts.md:11`; `FacilityService/Program.cs:157` |
| P2-03 | Error handling docs incomplete | Ensured all 4 ops have 401/403/404/400/422/503 rows as applicable; added missing rows (EP-02 403, EP-03 422 note, EP-04 403 trigger fix) | Complete error coverage for callers | `FacilityService/Program.cs:140-182` |
| P2-04 | EP-03 payload efficiency | Reused existing floor/vehicleType filters; zone/row proposed (not implemented); driverReservation only when caller has reservation; no new endpoints beyond existing route | Avoid payload bloat and new endpoints | `FacilityService/Program.cs:140-182`; `PostgresOperationalLayoutReader.cs:161-170` |

## Deliverable C — Implementation Readiness Matrix

| Endpoint | Existing implementation | Required changes | DB changes | External dependencies | Readiness |
| --- | --- | --- | --- | --- | --- |
| `GET /api/discovery/parking-lots` (EP-01) | None (route PROPOSED; no discovery aggregation; `search_facility_projections` schema-only at `03b:222-237`, no C# entity/repo) | New controller + service + query schema; availability from Operations; tariff from Payment; freshness source | DB32 (search projection source/version/time-aware); V11-DES-08 approval | Operations, Payment, Facility (logical), MapTiler (FE-only) | NOT IMPLEMENTED |
| `GET /api/discovery/parking-lots/{siteId}` (EP-02) | None | New controller + handoff payload; reuse EP-01 data | Same as EP-01 | Same as EP-01 | NOT IMPLEMENTED |
| `GET /api/parking-lots/{siteId}/operational-layout` (EP-03) | Route exists in FacilityService (`Program.cs:140-182`); DTO v1.2.0 (`OperationalLayoutProjection.cs`); sanitized Driver projection already implemented (`ReadOperationalAsync` + `OperationalLayoutRedaction`); dual-mode auth via `LayoutCallerResolver` | Per-caller `driverReservation` (proposed internal Operations read); requested/allocated position + locationPrecision fields (v1.3.0 bump, coordinated with SPARK-244); zone/row filters (proposed) | None for existing fields; proposed additions need Operations `reservations`/`slot_allocations` reads (no schema change) | User & Access (identity, 10s), Operations (occupancy/allocation, 10s), FacilityDb (local) | READY WITH MINOR CHANGES |
| `GET /api/sessions/current/location` (EP-04) | None (no single-session GET; `ParkingSessionsController` only list + walk-in) | New controller + handler; driver ownership check (none exists); recorded-location mapping | Option B (P0-01): add nullable `current_spatial_unit_id` to `parking_sessions` (Operations overlay) to support ZONE; otherwise SLOT+UNKNOWN only | OperationsDb (local), Facility (site code resolution), User & Access (auth) | BLOCKED BY DATABASE (for ZONE precision; SLOT/UNKNOWN subset is READY WITH MINOR CHANGES) |

## Remaining Open Questions (summary)

- Total: 12 open questions (OQ-1..OQ-11 plus OQ-DB-01). See the revised doc's Open Questions table for full detail (Question ID, description, why necessary, affected endpoint, responsible team, recommended options, implementation impact).
- Counts by category: approval/governance 2 (OQ-1, OQ-2); database 1 (OQ-DB-01); realtime 1 (OQ-3); operational constraints 1 (OQ-4); implementation gaps 5 (OQ-5, OQ-6, OQ-7, OQ-8, OQ-11); release coordination 1 (OQ-9); route design 1 (OQ-10).

## Files modified / created

- Modified: `docs/03-design/api/driver-maps-vehicle-location-api-design.md` (v1.0 → v1.1).
- Created: `docs/03-design/api/driver-maps-vehicle-location-api-revision-report.md` (this file).

## Database changes required

- Option B (recommended, P0-01): add nullable `current_spatial_unit_id UUID` to `parking_sessions` (Operations-owned, logical ref to Facility `spatial_units.id`, no cross-DB FK); add `Guid? CurrentSpatialUnitId` to `ParkingSessionRow`; set on admission. Additive-nullable ⇒ back-compatible; ships as Operations overlay SQL (no EF migrations exist).
- DB32 (existing backlog item): source/version/time-aware discovery projection (`search_facility_projections`) with filters/freshness/pricing.
- No schema change for EP-03 existing fields; proposed additions need only an internal Operations read (OQ-9).

## Endpoints ready for implementation

- EP-03 `GET /api/parking-lots/{siteId}/operational-layout` — READY WITH MINOR CHANGES (route + DTO + redaction already implemented; per-caller `driverReservation` and v1.3.0 bump pending).

## Endpoints blocked by decisions / architecture

- EP-01, EP-02 — NOT IMPLEMENTED (blocked by V11-DES-08 approval, DB32, tariff model agreement).
- EP-04 — BLOCKED BY DATABASE for ZONE precision (Option B pending); SLOT/UNKNOWN subset is READY WITH MINOR CHANGES.

## Conflicts with existing SRS / Architecture

- `docs/03-design/api/README.md` row now reads "OperationService (discovery/location) + FacilityService (layout projection)", which corrects the stale single-owner value. Not modified by me per task constraint.
- The revised doc does not resolve the pre-existing tension between `parking-structure-api-design.md:50,76` (Operator-only route) and the twin designs (dual-mode sanitized Driver projection). It follows the twin's dual-mode design and notes EP-03's Driver scope is proposed.
- `src/Services/ParkingService` does not exist (docs referencing it are stale; implementation is `FacilityService`). Cited accordingly.

## Recommended next Jira tasks

1. V11-DES-08 approval gate for Driver-facing discovery route/query/cache/DTO.
2. DB32: implement source/version/time-aware discovery projection with filters/freshness/pricing.
3. Operations + DB: decide OQ-DB-01 (Option B) and ship overlay SQL + entity change.
4. Facility + Operations: implement per-caller `driverReservation` internal read (OQ-9) and coordinate v1.3.0 bump with SPARK-244.
5. Payment: agree tariff `billingUnit`/`billingBasis` model (tracked with the EP-01 price-filtering contract; no dedicated OQ) and interval estimate shape for discovery.
6. OperationService: add `GET /api/sessions/current/location` with driver ownership check and 404-on-no-active-session.
7. FacilityService: add `zone`/`row` query params to operational-layout (EP-03 query filters; see P2-04).
