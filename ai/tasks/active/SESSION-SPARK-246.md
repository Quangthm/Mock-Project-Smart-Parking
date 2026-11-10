# Session Tracking — SPARK-246 (Driver Maps & Recorded Vehicle Location)

## 1. Session Information
- **Session ID:** SESSION-SPARK-246
- **Date:** 2026-11-07
- **Role:** Lead Architect & Task Orchestrator (Main Orchestration Session)
- **Branch:** `feat/SPARK-246-driver-maps-vehicle-location` (synced with `feat/SPARK-238-facility-structure-port` at `02957fd2`)
- **Related Jira Task:** SPARK-246 — [US-D16] Driver — Use maps and recorded vehicle location
- **Status:** In Progress (Phase 1: Offline / Mock & Unit Test Mode)
- **Docker Constraint:** Docker disabled (SPARK-244 exclusive lock during P4 testing). All Phase 1 work is 100% offline/in-memory.

---

## 2. Objectives & Acceptance Criteria Traceability (v1.1)

### Acceptance Criteria Checklist (10 ACs v1.1)
- [x] **AC-1 (Map Navigation & Fallback):** Driver navigates via MapTiler or manual fallback (`originSource=MANUAL` on GPS failure/denial). *(FE Complete & Tested)*
- [x] **AC-2 (Preserve Lot Identity):** Selection in Discovery preserves canonical `lotId`/`siteId` through to internal 2D/3D layout projection. *(FE Complete & Tested)*
- [x] **AC-3 (Separate Positions):** Clear separation between `requestedPosition` (Driver intent) vs `allocatedPosition` (Backend authoritative allocation). *(BE Complete)*
- [x] **AC-4 (Backend Precision Integrity):** Vehicle/session location displayed strictly at backend recorded precision (`SLOT | ZONE | UNKNOWN`). *(BE & FE Complete)*
- [x] **AC-5 (Preserve Zone-Only):** Zone-only data (e.g. motorcycles, non-sensor zones) NEVER synthesizes an invented physical slot or halo. *(BE & FE Complete)*
- [x] **AC-6 (Zero Allocation Privilege on UI):** UI marker/slot selection is informational context only; booking backend revalidates capacity independently. *(BE & FE Complete)*
- [x] **AC-7 (Lifecycle Authority):** `Allocated -> Occupied -> Released` strictly follows authoritative backend state transitions. *(BE & FE Complete)*
- [x] **AC-8 (Departure Releases Location):** Payment/checkout does NOT release parking slot; release requires authoritative physical departure. *(BE Complete, E2E in P4)*
- [x] **AC-9 (Stale & Reconnect Protocol):** Stale/disconnected state flagged clearly (`isFresh: false` / `freshness.isStale: true`); reconnect triggers full authorized reload. *(BE & FE Complete)*
- [x] **AC-10 (Zero Mutation Privilege):** Map/3D Driver UI is strictly a read-only projection (`GET` only; no write/allocation authority). *(BE & FE Complete)*

---

## 3. Work Breakdown Structure (WBS) & Delegation Lanes

```
+-----------------------------------------------------------------------------------+
|                        SPARK-246 Lead Architect & Orchestrator                    |
+-----------------------------------------------------------------------------------+
       |                                |                               |
       v                                v                               v
+-----------------------+   +-----------------------+   +-----------------------+
|  Gói 1: BE Projection |   |  Gói 2: FE Discovery  |   |  Gói 3: FE Find Car   |
|  - Driver Layout DTO  |   |  - MapTiler Map       |   |  - Recorded Location  |
|  - Redaction pipeline |   |  - Manual Fallback    |   |  - Zone vs Slot view  |
|  - Precision mapping  |   |  - 2D/3D Lot Handoff  |   |  - Stale banner & rec |
|  - In-memory xUnit    |   |  - Mock Fixture Tests |   |  - Mock Fixture Tests |
+-----------------------+   +-----------------------+   +-----------------------+
                                        |
       (Khi SPARK-244 hoàn tất P4 & bàn giao quyền Docker)
                                        v
                            +-----------------------+
                            |  Gói 4: E2E & Verify  |
                            |  - Docker Integration |
                            |  - Gateway & Real DB  |
                            +-----------------------+
```

---

## 4. Technical Architecture & Key Findings

### A. Service Boundaries & Endpoints Mapping
1. **EP-01 (`GET /api/discovery/parking-lots`)**:
   - Owning Service: `OperationService` (Parking Operations).
   - Coordinates from `FacilityDb`, availability from `OperationsDb`, tariffs from `PaymentService`.
   - Batch pricing (`calculate-batch`) to eliminate N+1 queries.
2. **EP-02 (`GET /api/discovery/parking-lots/{siteId}`)**:
   - Owning Service: `OperationService` (handoff context revalidation).
3. **EP-03 (`GET /api/parking-lots/{siteId}/operational-layout`)**:
   - Owning Service: `FacilityService`.
   - Baseline: `OperationalLayoutProjectionDto` (v1.2.0) with single redaction choke point `OperationalLayoutRedaction.cs`.
   - Driver Projection (v1.3.0 proposed): adds `driverReservation` (caller-private requested vs allocated position); public `slots[]` redacted from caller identities.
4. **EP-04 (`GET /api/sessions/current/location`)**:
   - Owning Service: `OperationService`.
   - Option B adopted: `current_spatial_unit_id` in `parking_sessions` to support `ZONE` precision. Authenticated Driver only (`account_id IS NOT NULL`).

### B. Sandboxed Build & Test Commands
- **Dotnet build command (sandbox compatible):**
  `dotnet build <project.csproj> --no-restore -m:1 -nr:false`
- **Frontend test command:**
  `node --test tests/frontend/*.test.cjs`

---

## 5. Work Log & Progress
- [x] Pre-flight inspection: verified `git status`, branch `feat/SPARK-246-driver-maps-vehicle-location`, sync with SPARK-238.
- [x] Analyzed API spec: `docs/03-design/api/driver-maps-vehicle-location-api-design.md` (Rev 4), `driver-maps-vehicle-location-api-revision-report.md`.
- [x] Verified existing FE test baseline: `node --test tests/frontend/operational-layout-adapter.test.cjs tests/frontend/operational-layout-visual.test.cjs` (28/28 PASS).
- [x] Verified C# build stability: `FacilityService.API` and `OperationService.API` compiled successfully.
- [x] Created session tracking log: `ai/tasks/active/SESSION-SPARK-246.md`.
- [x] Created Implementation Planning document: `D:\My file\FA26 - OJT FSoft\Planning\SPARK-246-planning.md` (20KB, 6 Phases, WBS, 10 ACs).
- [x] Soạn Prompt giao việc Gói 1 (Backend Driver Projection & Redaction).
- [x] **Phase 1 (Gói 1) BE Complete & Verified:**
  - FacilityService EP-03: `OperationalLayoutProjectionDto` mang `DriverReservationDto` + `TelemetryHeartbeatAt`; `OperationalLayoutRedaction` hoàn thiện che sạch public slots, inject caller reservation, cưỡng chế stale `IsBookable = false`.
  - OperationService EP-04: Entity Option B `CurrentSpatialUnitId`, port `IVehicleLocationReadStore`, handler `GetCurrentVehicleLocationQueryHandler` (mapping 3 precision: SLOT/ZONE/UNKNOWN, UNKNOWN forces RecordedAt=null, RELEASED status filtered), controller endpoint `GET /api/sessions/current/location` (401 unauth, 404 not found).
  - Test suites: In-memory test suites passed 100% (160 passed, 0 failed, 24 Docker tests skipped).
- [x] Soạn Prompt triển khai Phase 2 (Gói 2: Frontend Discovery & MapTiler Handoff).
- [x] **Phase 2 (Gói 2) FE Complete & Verified:**
  - Mock fixture: `FE/src/fixtures/driver-discovery-fixture.json` chuẩn Rev 4.
  - API Client: `FE/src/lib/driverDiscoveryApi.ts` với `buildDiscoveryQuery`, `searchDiscoveryLots`, `buildExternalDirectionsUrl`, `createHandoffContext`.
  - UI FindParking: Fallback MANUAL khi GPS từ chối/không hỗ trợ, geocoding form, bảo toàn `siteId` nguyên vẹn (`mapDiscoveryLot` gán id = item.siteId verbatim).
  - UI ParkingDetailsPanel: Nút "Open in Google Maps" (`EXTERNAL_MAP`).
  - Unit tests: `tests/frontend/driver-discovery.test.cjs` 6/6 pass; toàn bộ 214 test FE pass.
- [x] Soạn Prompt triển khai Phase 3 (Gói 3: Frontend Find My Car & Precision Renderer).
- [x] **Phase 3 (Gói 3) FE Complete & Verified:**
  - Mock fixture: `FE/src/fixtures/driver-vehicle-location-fixture.json` (4 kịch bản: slotPrecision, zonePrecision, unknownPrecision, staleVariant).
  - API Client: `FE/src/lib/driverVehicleLocationApi.ts` với `getCurrentVehicleLocation`, `normalizeRecordedLocation`, `normalizeAllocationStatus`, `locationDetailLines`.
  - UI Component: `FE/src/roles/driver/parking/FindMyCarCard.tsx` + `find-my-car.css` hiển thị thẻ vị trí thích ứng (Exact Slot, Zone-only, Pending, Stale Banner có nút Tải lại).
  - Tích hợp: nhúng vào `ActiveBookings.tsx` khi có xe check-in.
  - Unit tests: `tests/frontend/driver-find-my-car.test.cjs` 6/6 pass; toàn bộ 220 test FE pass.
- [ ] **Phase 4 (Gói 4: E2E Integration) — TRẠNG THÁI: TẠM DỪNG (HALT) CHỜ DOCKER TỪ SPARK-244.**

---

## 6. Blockers, Risks & Mitigations
- **Docker Prohibition:** Strict Phase 1 policy. Docker compose must NOT be started. All work verified via mock fixtures and in-memory unit tests.
- **IPC in AppContainer Sandbox:** `vstest.console` socket timeout occurs in standard sandbox when spawning testhost across containers. Worker agents running xUnit tests must use in-memory testing or test runners without external IPC dependencies.
