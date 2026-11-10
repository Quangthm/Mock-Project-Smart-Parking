# SPARK-246 Branch Verification — Test Guide for PM & QA Approval to Merge into `develop`

**Branch:** `feat/SPARK-246-driver-maps-vehicle-location`  
**Jira Task:** `SPARK-246` — [US-D16] Driver — Use maps and recorded vehicle location  
**Prepared for:** Tester / QA → PM Sign-off before merge into `develop`  
**Date:** 2026-11-10  
**Status:** Ready for Verification  

---

## 1. Scope of This Branch

This branch delivers the complete **Driver Maps & Recorded Vehicle Location** capability for SPARK-246, alongside resolving **Note 3 (Driver Full Lot Availability Blocker)**:

1. **Backend Driver Projection & Redaction (EP-03):**
   - Updated `OperationalLayoutProjectionDto` to v1.3.0 supporting `DriverReservationDto` and `TelemetryHeartbeatAt` with `JsonIgnore(Condition = WhenWritingNull)` for backward compatibility.
   - Redaction choke point in `OperationalLayoutRedaction.cs`: strictly scrubs `OperatorContext`, `SessionType`, and non-primitive features; masks `BACKUP` slots to `RESERVED` for public viewers while injecting caller-private reservations.
   - Stale telemetry rule (AC-9): Forces `IsBookable = false` across all slots when `!dto.IsFresh`.

2. **Backend Recorded Vehicle Location Query (EP-04):**
   - Added `CurrentSpatialUnitId UUID` to `ParkingSessionRow` (Option B: non-breaking, nullable).
   - Inward port `IVehicleLocationReadStore` and persistence adapter `VehicleLocationReadStore` (`AsNoTracking()`).
   - CQRS MediatR query/handler `GetCurrentVehicleLocationQuery`/`GetCurrentVehicleLocationQueryHandler` mapping 3 precision tiers (`SLOT`, `ZONE`, `UNKNOWN`).
   - Invariant guards: `ZONE` precision enforces `slotId = null` and `slotCode = null` (AC-5); `UNKNOWN` enforces `recordedAt = null` (AC-4); `RELEASED` allocations mapped to `null`.
   - API Controller: `GET /api/sessions/current/location` and alias `GET /api/parking-sessions/current/location` with 401 unauthenticated and 404 session-not-found handling.

3. **Frontend Discovery & External Navigation Handoff (EP-01, EP-02):**
   - `FE/src/lib/driverDiscoveryApi.ts`: REST serializer `buildDiscoveryQuery`, `searchDiscoveryLots`, `createHandoffContext`, and `buildExternalDirectionsUrl`.
   - `FindParking.tsx`: Dynamic MapTiler map, fallback to manual address geocoding (`originSource = "MANUAL"`) when GPS is denied or unsupported, preserving authoritative `siteId` (UUID) verbatim (AC-1, AC-2).
   - `ParkingDetailsPanel.tsx`: Added "Open in Google Maps" external navigation button (`EXTERNAL_MAP`).

4. **Frontend Find My Car & Precision Renderer:**
   - `FE/src/lib/driverVehicleLocationApi.ts`: State normalizer, precision labels, and detail line generator.
   - `FindMyCarCard.tsx` + `find-my-car.css`: Adaptive card rendering Exact Slot, Zone-only, or Pending status with color + text + dot badge, amber stale banner with "Tải lại" button, and 44px touch targets.
   - `ActiveBookings.tsx`: Integrates `<FindMyCarCard />` at the top of active bookings.

5. **Note 3 Resolution (Driver "Full bãi đỗ xe" Unblocking):**
   - Root cause: Owner-created lots are stored in local storage with `slots: []`.
   - `FE/src/lib/driverSlotMapping.ts`: Pure mapper converting backend `SlotVisualStateDto[]` to `ParkingSlot[]` with raw `slotCode` (for 3D mesh binding) and fail-closed status.
   - `FindParking.tsx`: Hydrates slot-less lots from `parkingApi.operationalLayout(lot.id)` on mount and selection, updating both store and local state.
   - `BookingFlow.tsx`: Implemented `preferPopulatedLot` so populated lots with 14/48 slots are never overwritten by an empty store record.

---

## 2. 10 Acceptance Criteria Traceability Matrix (AC-1 → AC-10)

| Mã AC | Tiêu chí Nghiệm thu v1.1 | Triển khai trong mã nguồn | Kiểm chứng tự động & Thủ công |
| :---: | :--- | :--- | :--- |
| **AC-1** | Driver điều hướng tới bãi đỗ bằng bản đồ hoặc fallback thủ công khi GPS bị từ chối | `FindParking.tsx`, `driverDiscoveryApi.ts`: `originSource="MANUAL"`, MapTiler geocode fallback, Google Maps link. | `driver-discovery.test.cjs`: Test AC-1 serialization & directions URL; UI manual address entry. |
| **AC-2** | Chọn bãi đỗ từ Discovery bảo toàn chính xác định danh `siteId` (UUID) | `FindParking.tsx:mapDiscoveryLot`, `createHandoffContext`: gán `id = item.siteId` verbatim. | `driver-discovery.test.cjs`: `assert.strictEqual(ctx.siteId, item.siteId)`. |
| **AC-3** | Vị trí yêu cầu (`requestedPosition`) và thực tế (`allocatedPosition`) hiển thị tách biệt | `OperationalLayoutProjection.cs`: `DriverReservationDto` chứa 2 trường riêng biệt; public `slots[]` che sạch. | `OperationalLayoutRedactionTests.cs`: `Sanitized_WithDriverReservation...`. |
| **AC-4** | Vị trí xe chỉ hiển thị đúng cấp độ backend ghi nhận (`SLOT`, `ZONE`, `UNKNOWN`) | `GetCurrentVehicleLocationQueryHandler.cs`, `FindMyCarCard.tsx`: badges Exact Slot, Zone-only, Pending. | `VehicleLocationQueryTests.cs`, `driver-find-my-car.test.cjs`. |
| **AC-5** | Dữ liệu Zone-only tuyệt đối không được tự ý vẽ thành ô đỗ cụ thể | `GetCurrentVehicleLocationQueryHandler.cs` gán `slotId=null`, `slotCode=null`; `FindMyCarCard.tsx` hiển thị text note. | `driver-find-my-car.test.cjs`: Negative test confirms DOM never synthesizes slot number for ZONE. |
| **AC-6** | Thao tác chọn marker/slot trên UI không có quyền cấp phát tài nguyên | Mọi endpoint Discovery & Layout đều là HTTP `GET` read-only. Không có route ghi nào trong controller. | Code inspection: Zero POST/PUT mutation routes on driver map. |
| **AC-7** | Trạng thái `Allocated -> Occupied -> Released` tuân thủ backend | `GetCurrentVehicleLocationQueryHandler.cs`: `allocationStatus` chỉ nhận `RESERVED` hoặc `OCCUPIED`. | `VehicleLocationQueryTests.cs:ReleasedAllocationStatus_IsNotPassedThrough`. |
| **AC-8** | Thanh toán/checkout không giải phóng vị trí đỗ; chỉ rời bãi thực tế mới giải phóng | `VehicleLocationReadStore.cs`: Session chỉ kết thúc khi `ExitTime != null && SessionStatus != "ACTIVE"`. | Live probe EP-04: Active session returns 200 until vehicle physically exits gate. |
| **AC-9** | Mất kết nối (stale) hiển thị rõ ràng, kết nối lại tải lại đầy đủ | `OperationalLayoutRedaction.cs`: `!dto.IsFresh` ép `IsBookable=false`; `FindMyCarCard.tsx`: Stale banner + Tải lại. | `OperationalLayoutRedactionTests.cs:Sanitized_WhenStale_ForcesIsBookableFalseAcrossAllSlots`. |
| **AC-10**| Bản đồ và 3D của Driver hoàn toàn không có thẩm quyền đột biến tài nguyên | Tất cả endpoint đều là read projection. Role-based security chặn truy cập cấu hình của Owner. | OpenAPI contract verification & Controller RBAC scope. |

---

## 3. Prerequisites & Environment Setup

1. **Docker Containers Running & Healthy:**
   ```bash
   docker ps
   ```
   Xác nhận các service đang chạy:
   - `smartpark-app-user-service-1` (Port `5035`)
   - `smartpark-app-facility-service-1` (Port `5045`)
   - `smartpark-app-operation-service-1` (Port `5055`)
   - `smartpark-app-frontend-1` (Port `5173`)
   - `operations-db-1` (Port `5443`)
   - `facility-db-1` (Port `5442`)
2. **Database Migration Applied:**
   Cột `current_spatial_unit_id UUID` trong bảng `parking_sessions` (`OperationsDb`) đã được thêm qua lệnh:
   ```sql
   ALTER TABLE parking_sessions ADD COLUMN IF NOT EXISTS current_spatial_unit_id UUID;
   ```
3. **Demo Site Data Seeded:**
   Bãi đỗ xe demo `b0000000-0000-4000-8000-000000000001` (`DEMO-HQ`) có sẵn 48 ô đỗ khả dụng trên `FacilityService`.

---

## 4. Automated Verification (All Must Be Green)

Chạy từ thư mục gốc repository:

| # | Lệnh kiểm thử | Kết quả kỳ vọng | Trạng thái |
|---|---|---|:---:|
| **A1** | `dotnet build src/Services/FacilityService/SmartParking.FacilityService.API/...csproj --no-restore -m:1 -nr:false` | `Build succeeded, 0 Warning(s), 0 Error(s)` | **PASS** |
| **A2** | `dotnet build src/Services/OperationService/SmartParking.OperationService.API/...csproj --no-restore -m:1 -nr:false` | `Build succeeded, 0 Warning(s), 0 Error(s)` | **PASS** |
| **A3** | `node --test tests/frontend/*.test.cjs` | **228 passed, 0 failed** (bao gồm cả test Note 3) | **PASS** |
| **A4** | `node FE/node_modules/typescript/bin/tsc -p FE/tsconfig.json --noEmit` | `Exit 0, 0 Errors` | **PASS** |

---

## 5. Postman Automated Integration Testing

Import 2 files trong thư mục `docs/04-testing/postman/`:
- **Collection:** `SPARK-246-Driver-Maps-Vehicle-Location.postman_collection.json`
- **Environment:** `SPARK-246.postman_environment.example.json`

### Danh sách Request & Assertion:
1. **`01 - Authentication / 1.1 Driver Login`:**
   - POST `http://localhost:5035/api/auth/login`
   - Assertion: HTTP 200, lưu tự động biến `driverBearerToken`.
2. **`02 - EP-01: Discovery Search Lots`:**
   - GET `http://localhost:5055/api/discovery/parking-lots?originSource=CURRENT_GPS&...`
   - Assertion: HTTP 200, `items[].directions.handoff === "EXTERNAL_MAP"`, `items[].siteId` là UUID.
3. **`03 - EP-03: Operational Layout Projection (FacilityService)`:**
   - GET `http://localhost:5045/api/parking-lots/b0000000-0000-4000-8000-000000000001/operational-layout`
   - Assertion: HTTP 200, `contractVersion === "1.2.0"`, `isFresh === true`, `slots.length === 48`, `isBookable === true`.
   - Redaction Assertion: `sessionType` is undefined, `operatorContext` is undefined, không rò rỉ `BACKUP`.
4. **`04 - EP-04: Recorded Vehicle Location (OperationService)`:**
   - GET `http://localhost:5055/api/sessions/current/location` (No Token) → Assertion: HTTP 401 `UNAUTHENTICATED`.
   - GET `http://localhost:5055/api/sessions/current/location` (Bearer Token) → Assertion: HTTP 404 `SESSION_NOT_FOUND` (nếu chưa có xe) hoặc HTTP 200 (kèm precision contract chuẩn).

---

## 6. Manual UI Verification — Step-by-Step Walkthrough

Tester mở trình duyệt và thực hiện theo kịch bản:

### Kịch bản 1: Xác nhận sửa dứt điểm Note 3 (Driver Full Availability & Book Now)
1. Truy cập `http://localhost:5173/` và đăng nhập bằng tài khoản `driver@gmail.com` / `Password@123`.
2. Vào màn hình **Find Parking** (`/driver/find-parking` hoặc menu tìm bãi đỗ).
3. **Quan sát Card bãi đỗ xe:**
   - Thẻ bãi xe (`DEMO-HQ` hoặc bãi đã tạo) hiển thị số ô trống khả dụng (ví dụ: **"48 spaces available"** hoặc tương đương).
   - Card **KHÔNG** bị nhãn màu đỏ/xám *"Full"*.
4. Bấm vào bãi đỗ xe:
   - Panel chi tiết bãi đỗ trượt ra.
   - Nút **"Book Now"** sáng rõ (enabled), có thể bấm được.
5. Bấm **"Book Now"**:
   - Hệ thống chuyển vào màn hình **Booking Flow**.
   - Cả chế độ lưới 2D lẫn mô hình 3D đều hiển thị đầy đủ danh sách các ô đỗ thật (các ô `CarParkingSpace_001` đến `CarParkingSpace_048`).
   - Tester có thể click chọn 1 ô đỗ còn trống để tiếp tục luồng đặt xe.

### Kịch bản 2: Điều hướng Ngoài App (AC-1)
1. Trong Panel chi tiết bãi đỗ xe, quan sát nút **"Open in Google Maps"**.
2. Click vào nút:
   - Một tab mới mở ra trỏ tới Google Maps Universal Link dạng:
     `https://www.google.com/maps/dir/?api=1&destination=10.7721,106.698`
   - Đảm bảo có thuộc tính an toàn `rel="noopener,noreferrer"`.

### Kịch bản 3: Fallback Nhập Địa chỉ Thủ công khi GPS bị từ chối (AC-1)
1. Trên trình duyệt, chặn quyền truy cập Vị trí (Block Location Permission).
2. Tải lại trang Find Parking:
   - Xuất hiện thông báo: *"GPS access was denied. You can manually enter an address to search nearby parking."*
   - Thanh nhập địa chỉ thủ công xuất hiện.
3. Nhập `"Bến Thành"` hoặc địa chỉ bất kỳ và bấm Search:
   - Bản đồ bay (flyTo) về tọa độ địa chỉ tìm kiếm.
   - Danh sách bãi đỗ gần đó được tải và hiển thị.

### Kịch bản 4: Thẻ Vị trí xe Find My Car (AC-4, AC-5, AC-9)
1. Điều hướng tới trang **Active Bookings** (`/driver/bookings`).
2. Khi tài xế có phiên đỗ xe đang hoạt động:
   - Thẻ **Find my car (Tìm xe của tôi)** hiển thị ở đầu trang.
   - **Độ chính xác SLOT:** Hiển thị badge xanh *"Vị trí chính xác (Exact Slot)"*, đầy đủ Tầng, Khu vực, Số ô đỗ.
   - **Độ chính xác ZONE:** Hiển thị badge vàng *"Khu vực (Zone-only)"*, chỉ hiển thị Tầng và Khu vực, kèm ghi chú *"Khu vực đỗ xe tự do/xe máy. Không phân bổ ô đỗ cụ thể."* (Không bao giờ tự ý vẽ ô đỗ cụ thể).
   - **Khi mất kết nối (Stale):** Xuất hiện banner màu hổ phách cảnh báo dữ liệu có thể chưa cập nhật, kèm nút bấm **"Tải lại"**. Click "Tải lại" sẽ fetch lại dữ liệu mới nhất.

---

## 7. Kết luận & Khuyến nghị Bàn giao

- **Trạng thái:** Toàn bộ 10 Acceptance Criteria của SPARK-246 và lỗi Note 3 đã được giải quyết triệt để, kiểm chứng bằng 228 automated tests và live backend probes.
- **Khuyến nghị:** Kính chuyển QA / PM tiến hành kiểm tra theo hướng dẫn trên và phê duyệt Merge Pull Request vào nhánh `develop`.
