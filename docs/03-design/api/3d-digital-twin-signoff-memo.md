# SPARK-228 — 3D Digital Twin Contract Sign-Off Memo

**Date:** 2026-10-26
**Prepared by:** Independent Backend/API Review (orchestrator review pass, 6 verification rounds)
**Prepared for:** Project Lead / PM
**Requested decision:** Approve the SPARK-228 implementation baseline (scope + contract + baseline re-alignment)
**Status:** `READY TO CIRCULATE` — see §7 pre-circulation checklist (all items closed 2026-10-27)
**Package commits:** `f22959f6` (3D contract split) · `112ea5cc` (RabbitMQ + legacy compose removal) · `1f97dc75` (requirements / decision-log sync)

---

## 1. What is being signed

| Artifact | Path | Version | State |
|---|---|---|---|
| API design — hub | `docs/03-design/api/3d-digital-twin-api-design.md` | Contract `v1.2.0` | Normative hub (shared §3.2 state model, §3.3 identity, §7 errors) |
| API design — SPARK-228 | `docs/03-design/api/3d-digital-twin-spark-228-api-design.md` | Contract `v1.2.0` | `READY WITH CONDITIONS — PENDING PM SIGN-OFF` |
| API design — SPARK-244 | `docs/03-design/api/3d-digital-twin-spark-244-api-design.md` | Contract `v1.2.0` | `REQUIRES DELTA DESIGN PASS — NOT IMPLEMENTATION READY` |
| OpenAPI spec | `docs/03-design/api/3d-digital-twin-openapi.yaml` | OpenAPI 3.1.0 / `v1.2.0` (29,242 B) | Complete except §7 item 1 |
| Test fixtures | `FE/src/fixtures/slots-operational-fixture.json` (42,801 B)<br>`FE/src/fixtures/slots-operational-stale-fixture.json` (42,802 B) | — | Verified, see §4 |

Document ID `SPARK-API-3D-TWIN`. Owning service today: `ParkingService`; target service: `Facility & Visualization Service`.

---

## 2. Decisions requested from PM

### D-1 — Scope: SPARK-228 is a read-path + frontend binding story

Approved scope for Sprint 2 is exactly:

- `GET /api/parking-lots/{id}/operational-layout` (read path, authoritative projection)
- Three.js binding layer consuming it, plus deterministic fixtures

**Not in SPARK-228:**

| Item | Disposition | Reason |
|---|---|---|
| `POST /api/parking-lots/{id}/structure` (batch ingestion) | Separate backlog epic | SPARK-228 has no ingestion deliverable; its confirmed scope names only "existing Sprint 2 3D assets". Also **not** SPARK-244 (SPARK-244 is operator monitoring, no ingestion requirement) |
| `CUSTOM` archetype + S3/MinIO signed upload + CDN/SSRF allowlist | Phase 2 | Only `OUTDOOR \| INDOOR \| UNDERGROUND` in Sprint 2; `CUSTOM` must return `422 ARCHETYPE_PHASE_2` |
| SignalR / WebSocket push | SPARK-244 | SPARK-228 explicitly allows "current API / deterministic fixtures" and keeps live IoT synchronization outside the prototype |
| Public / anonymous access to the layout | **No ticket requests it** | Neither SPARK-228 nor SPARK-244 requires anonymous access; SPARK-244 requires *assigned-scope* access (stricter). Sprint 2 keeps the existing `Operator` + `SLOT_OVERRIDE` check |

### D-2 — Re-baseline four 3D documents modified after the approved v1.1.0 baseline

| Document | Change requiring PM acknowledgement |
|---|---|
| `3D_BACKEND_SLOT_STATE_BINDING.md` | Hub path superseded to `/ws/parking-lots/{id}/layout` (`/hubs/parking-lot` retained only as legacy annotation); `slotCode` normalization removed (raw mesh name) |
| `3D_ENTERPRISE_MODEL_INGESTION_RULES.md` | §6.2 slot-name regex made multi-archetype; `PARKING_` prefix-stripping rule superseded |
| `3D_MODEL_IMPLEMENTATION_GUIDE.md` | Hub path superseded (2 places) |
| `3D_INTEGRATION_REVIEW_FEEDBACK.md` | Hub path annotation aligned |
| `docs/03-design/api/README.md` | New design registered in the Active API Designs table |

Without this acknowledgement the repository would carry two conflicting "approved" contracts for the same naming and real-time path.

### D-3 — Open three backend tickets (none of them part of SPARK-228 DoD)

| Ticket | Content |
|---|---|
| BE-1 — DTO / enum projection | Add C# `PhysicalState` and `ReservationState` enums (today only DB CHECK constraints exist); expose `slotId`, `slotCode`, `physicalState`, `updatedAt` from the correct columns; stop serving `features` as a JSON string |
| BE-2 — Non-blocking read path | `ReadAsync` is shared by **both** `GET /structure` and `GET /operational-layout` and currently performs `PrepareAsync` + `AcquireAsync` (exclusive commitment hold) on every read: `ParkingStructureService.cs:57-58, 60-61, 70-71`; fix both routes, add a test proving no pending hold is created |
| BE-3 — Facility structure ingestion epic | `POST /structure`, 19 validation rules, SafeChange, idempotency, audit; includes `422 ARCHETYPE_PHASE_2` |

SPARK-228 itself stays frontend-only, consistent with its Definition of Done (FE/Three.js conventions, build/lint clean, PR reviewed).

---

## 3. Technical decisions already resolved inside the contract

| # | Decision | Basis |
|---|---|---|
| T-1 | Orthogonal **5 physical × 4 reservation** state space; `DISABLED` and `OVERSTAY` are not API axis values | Decision log 2026-10-25 (RA-04); `OVERSTAYED` is a `session_status` of ReservationService; `DISABLED` is a legacy FE enum |
| T-2 | `slotCode` = **raw, case-preserving Blender mesh name**; API length capped at 32 (DB column is `VARCHAR(50)`); uniqueness checked case-insensitively | Real mesh names carry `" 2"`-style suffixes and mixed case; `UPPER(slot_code)` indexes exist |
| T-3 | `isFresh` in Sprint 2 comes from a deterministic fixture, labelled a prototype approximation | No Redis container, no `IDistributedCache`, and no IoT heartbeat endpoint exist in the repository; claiming otherwise would be false |
| T-4 | `sessionType` is nullable; Phase 1 derives `EV_CHARGING` only when `slotType = EV` **and** `physicalState = OCCUPIED`; this is explicitly a heuristic, not a real session record | No `session_type` column anywhere; parking sessions live in another service's database |
| T-5 | `isBookable` is derived (`AVAILABLE` + `UNRESERVED`), forced `false` for `UNKNOWN` and `BACKUP` | Prevents optimistic availability; consistent across design, OpenAPI, fixtures |

---

## 4. Evidence that the package was actually verified

Six independent verification rounds were run against the files (not against the author's description). Final state:

- Fixtures cover **20 / 20** state combinations across 48 slots: `AVAILABLE` 22, `OCCUPIED` 13, `UNKNOWN` 4, `MAINTENANCE` 5, `UNAVAILABLE` 4.
- `isBookable` mismatches vs the stated rule: **0 / 48**. Duplicate `slotCode` (case-insensitive): **0**.
- Two slot codes deliberately carry the space+digit suffix (`CarParkingSpace_008 2`, `CarParkingSpace_012 3`) to prove raw-name mapping.
- `metrics` reconcile: total 48, occupied 13, available car slots 16, motorcycle 20 (8 occupied / 12 available), zone capacity 2 × 24.
- Stale variant ships with `isFresh: false` so FR-MAP-04 (30 % dim + banner) is testable in Sprint 2.
- All five 3D documents: no BOM, no replacement characters, 0 broken relative links.

Defects found and corrected during review (highlights): a fictional `DISABLED` reservation state and a 4 × 5 matrix in one artifact while the others had 5 × 4; a normalization rule that could never match the real mesh names; a fabricated unique-index name; an invented `is_overstay` column; a `GET /structure` lock on every read; a claim that Redis was "already provisioned in docker-compose.yml"; and a verification report asserting that design and OpenAPI were "in sync" when they were not.

---

## 5. Explicitly deferred (not silently dropped)

| Item | Deferred to |
|---|---|
| SignalR hub, live transition envelopes (`eventId`, `sequenceNumber`), reconnect snapshot | SPARK-244 delta design pass |
| Redis + IoT Gateway heartbeat ingest endpoint | Sprint 3 |
| `POST /structure` ingestion | BE-3 epic |
| Public/Driver access to the layout | Not scheduled — no approved requirement |
| Requested-vs-allocated slot distinction, release-exactly-once, zone-only projection, duplicate-event idempotency | SPARK-244 delta pass (5 design gaps identified) |

---

## 6. Known risks accepted with this approval

1. **Both read routes still lock.** `GET /structure` and `GET /operational-layout` share `ReadAsync`, which takes an exclusive hold and writes `structure_synchronizations` on every call. Mitigation: BE-2, before Sprint 2 demo load.
2. **No C# state enums today.** `physicalState` is currently collapsed into `operationalStatus` by the mapper; the API cannot serve the contract until BE-1 lands.
3. **`parking_slots.updated_at` is never bumped** by the existing slot update (`PostgresParkingStructureRepository.cs:147-150`), so any freshness logic must not depend on it.
4. **Production freshness is unproven.** Fixture-based `isFresh` satisfies the prototype acceptance criteria, not a production guarantee.
5. **The contract is sufficient for SPARK-228 but not for SPARK-244.** Sprint 3 needs a delta pass; do not treat this sign-off as Sprint 3 approval.

---

## 7. Pre-circulation checklist (must be closed before this memo is circulated)

- [x] `3d-digital-twin-openapi.yaml` `reservedCount` — **CLOSED**: now defines "slots whose reservationState is exactly RESERVED; PROTECTED and BACKUP excluded", and states that an occupied+reserved slot counts once in each axis.
- [x] `3d-digital-twin-api-design.md` §12 Test Suite 1B — **CLOSED**: SPARK-244 AC-3 relabelled to "Allocation/Claim State vs Physical Occupancy vs Visualization State" (no "Payment").
- [x] `3d-digital-twin-api-design.md` §12 Test Suite 1B — **CLOSED**: AC-10 no longer cites the driver-redaction case; it records the missing mutation-authority test as an explicit gap owned by the SPARK-244 delta pass.
- [x] Re-ran the consistency audit after the three edits: 20/20 state combinations, 48 slots, 0 `isBookable` mismatches, 0 BOM, 0 U+FFFD across hub + both extracts.
- [x] Committed as three changesets and recorded the SHAs in the header: `f22959f6` (3D Twin hub + SPARK-228/SPARK-244 extracts + openapi + memo + fixtures), `112ea5cc` (RabbitMQ service + legacy single-database compose removal + `scripts/database/README.md`), `1f97dc75` (requirements, 3D design notes, decision log, PM explainers).

---

## 8. Sign-off

| Role | Name | Decision | Date |
|---|---|---|---|
| Project Lead / PM | | ☐ Approved ☐ Approved with conditions ☐ Rejected | |
| Backend owner | | ☐ Acknowledged | |
| Frontend owner (SPARK-228) | | ☐ Acknowledged | |

Conditions / comments:

---

**Note on reading this memo:** sections 1–5 are the approval surface; section 6 is the risk you accept by approving; section 7 must be empty of open boxes before circulation. The design document itself is long and is not expected to be read line by line — §2 and §4 here are its distilled contract.

---

## Appendix A — Contract surface at a glance (informative)

This appendix makes the memo readable on its own. It is a summary only: `3d-digital-twin-openapi.yaml` remains normative, and any difference must be resolved in favour of the OpenAPI file.

**Endpoint in SPARK-228 scope**

```text
GET /api/parking-lots/{id}/operational-layout
Authorization: Bearer <JWT>     (Sprint 2: Operator with SLOT_OVERRIDE; Driver served by fixture)
Response: 200  Cache-Control: no-store, no-cache, must-revalidate
          401 UNAUTHENTICATED (malformed/expired token) · 403 FORBIDDEN · 404 SITE_NOT_FOUND
```

**Response envelope (fields shown, not values)**

```text
success, data {
  siteId, siteCode, siteName, contractVersion, generatedAt,
  isFresh, stalenessThresholdSeconds,
  metrics { totalPhysicalCapacity, occupiedCount, reservedCount, availableCarSlots,
            motorcycleCapacity { maxCapacity, occupiedCount, availableCount },
            zoneCapacities[ { zoneId, capacity, occupiedCount, zoneOnlyReservedCount } ] },
  floors[ { floorId, floorCode, name, level, maxCapacity } ],
  slots[ { slotId, slotCode, floorId, floorCode, zoneCode, rowCode,
           supportedVehicleType, slotType, physicalState, reservationState,
           sessionType, isEvCharging, isBookable, coordinates3d, features,
           operatorContext, updatedAt } ]
}
```

**State matrix (5 × 4 = 20 combinations, all exercised by the fixtures)**

| | `UNRESERVED` | `RESERVED` | `PROTECTED` | `BACKUP` |
|---|---|---|---|---|
| `AVAILABLE` | bookable | not bookable | not bookable | not bookable |
| `OCCUPIED` | not bookable | not bookable | not bookable | not bookable |
| `UNKNOWN` | not bookable | not bookable | not bookable | not bookable |
| `MAINTENANCE` | not bookable | not bookable | not bookable | not bookable |
| `UNAVAILABLE` | not bookable | not bookable | not bookable | not bookable |

Only the first cell is bookable. `UNKNOWN` never renders as available; `BACKUP` never renders as bookable; reservation state never overwrites physical state.

**Non-negotiable invariants**

| Invariant | Consequence if violated |
|---|---|
| 3D client is never an authority | 3D clicks must not create occupancy, reservation, allocation or payment |
| No optimistic availability | Missing/invalid state ⇒ `UNKNOWN` ⇒ rendered as disabled |
| Raw mesh identity | `slotCode` is the exact Blender mesh name; no prefix stripping, no invented slots, no array-index fallback |
| Graceful degradation | Stale > 30 s ⇒ 30 % dim + banner; reconnect ⇒ full snapshot, never a substituted fixture |
| Two orthogonal axes | Physical and reservation/protection are independent; no combined status field |

**Out-of-scope endpoints** (listed so PM does not look for them in this approval): `POST /api/parking-lots/{id}/structure`, any `/api/bookings` or reservation-creation route, and the SignalR hub `/ws/parking-lots/{id}/layout`.
