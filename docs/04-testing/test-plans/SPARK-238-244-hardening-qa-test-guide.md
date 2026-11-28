# SPARK-238 & SPARK-244 — Hướng dẫn kiểm thử QA (Hardening & Improvements)

- **Branch:** `feat/SPARK-244-realtime-monitoring` (PR target: `develop`)
- **Phạm vi PR:** Sửa lỗi QA (`SPARK-395`, `SPARK-394`, `SPARK-393`), ràng buộc trần số tầng theo model 3D (`2b maxFloors`), vòng đời bảo trì có xác nhận xuyên service & đồng bộ sang Operator (`2a` / `2c` — `DE-3`/`DE-4`/`DE-13`), phong tỏa khẩn cấp / ép bảo trì + tự động hết hạn `effectiveUntil` & đối soát/hoàn tiền xuyên service (`2d`), và sửa toàn bộ nút Refresh thủ công trên màn hình Owner & Operator.
- **Tài liệu thiết kế liên quan:**
  - [Parking Structure API Design](../../03-design/api/parking-structure-api-design.md)
  - [3D Digital Twin SPARK-244 Delta Design](../../03-design/api/3d-digital-twin-spark-244-delta-design.md) (§16)
  - [SPARK-244 Realtime Recovery Tester Guide](./SPARK-244-realtime-recovery-tester-guide.md) (hồi quy realtime 20 TC)

> [!IMPORTANT]
> **LƯU Ý ĐẶC BIỆT VỀ CÁC TEST CASE LIÊN QUAN ĐẾN RESERVATION (`TC-08`, `TC-12`):**
>
> Hiện tại trên branch này, phần **Consumer, Local Projection và Đối soát Reservation/Payment trên `OperationService`** thuộc phạm vi sở hữu của **`SPARK-273`** (chỉ giữ phần **Producer phát event** `FacilityMaintenanceChangedV1`, `FacilityCapacityRestrictedV1`, `FacilityCapacityRestrictionReleasedV1` bên `FacilityService` theo đúng SPARK-273 handoff ownership boundary).
>
> Vì vậy, khi chạy kiểm thử:
> 1. **KHÔNG đánh `FAIL`** cho các kịch bản phụ thuộc consumer/reconciliation trên `OperationService` (`TC-08`, `TC-12`); hãy ghi rõ trạng thái **`BLOCKED (Thuộc phạm vi SPARK-273 — OperationService consumer/reconciliation giao lại cho owner SPARK-273)`**.
> 2. **Phạm vi đã kiểm chứng ở nhánh này:** Toàn bộ vòng đời Maintenance (`acknowledged`, `revision`, `EndAtUtc = null`), Emergency Restriction, auto-expiry `effectiveUntil`, overlay `FacilityService` và khả năng phát event outbox (`FacilityMaintenanceChangedV1`, `FacilityCapacityRestrictedV1`/`ReleasedV1`) đã được kiểm chứng đầy đủ bằng unit tests trong `SmartParking.FacilityService.Tests` (`EmergencyRestrictionTests`, `MaintenanceEnforcementFlowTests`, `OperationalLayoutMaintenanceProjectionTests`), cùng `DisabledReservationFactsPublisherTests` cho `SPARK-393`.

---

## 1. Chuẩn bị môi trường & Cấu hình bắt buộc trước khi test

### 1.1. Áp dụng migration DB (chạy an toàn / idempotent)
Nếu dùng volume PostgreSQL đã có sẵn dữ liệu, chạy 2 lệnh sau để áp dụng các section mở rộng đã hợp nhất trong `02-facility-service-db.sql` và `03b-reservation-operations-service-db.sql`:

```powershell
./scripts/apply-workflow-migrations.ps1 -Service Parking   -SchemaRoot .
./scripts/apply-workflow-migrations.ps1 -Service Operation -SchemaRoot .
```

### 1.2. Các biến môi trường `.env` cần bật khi chạy Docker Compose
Không commit `.env` lên Git. Đảm bảo file `.env` local có các cờ phi mật khẩu sau trước khi recreate app containers:

| Biến `.env` | Giá trị khuyến nghị khi QA test | Mục đích |
|---|---|---|
| `SMARTPARK_FACILITY_LAYOUT_OUTBOX_ENABLED` | `true` | Facility stage `LayoutInvalidatedV1` và `FacilityCapacityRestrictedV1`/`ReleasedV1` |
| `SMARTPARK_FACILITY_OUTBOX_DELIVERY_ENABLED` | `true` | Outbox worker của Facility phát event ra RabbitMQ |
| `SMARTPARK_OPERATION_LAYOUT_RELAY_ENABLED` | `true` | Operation relay tiêu thụ event từ RabbitMQ đẩy SignalR về FE Operator |
| `SMARTPARK_REALTIME_SHARED_HUB_ENABLED` | `false` | **Bắt buộc giữ `false`** theo invariant của SPARK-244 |
| `SMARTPARK_RESERVATION_ADMISSION_ENABLED` | `false` *(bật `true` nếu có full SPARK-273 + RSA keys)* | Chỉ bật khi test luồng Reservation đầy đủ có RSA service token |
| `SMARTPARK_RESERVATION_FACILITY_ENABLED` | `false` *(bật `true` nếu có full SPARK-273 + RSA keys)* | Như trên |
| `SMARTPARK_RESERVATION_ALLOW_HTTP_JWKS` | `true` | Cho phép local Docker đọc JWKS HTTP |
| `SMARTPARK_RESERVATION_FACILITY_ALLOW_HTTP` | `true` | Cho phép local Docker gọi Facility HTTP nội bộ |

### 1.3. Kiểm tra nhanh các service đang chạy

```powershell
docker compose --project-name smartpark-app --env-file .env -f compose.app.yml ps
foreach ($p in 5035,5045,5055,5065) { curl.exe -s -o NUL -w "$p %{http_code}`n" "http://localhost:$p/health" }
curl.exe -s -o NUL -w "5173 %{http_code}`n" http://localhost:5173/
```

Tài khoản demo đọc từ `.env` (`SMARTPARK_OWNER_EMAIL`, `SMARTPARK_OPERATOR_EMAIL`, `SMARTPARK_ADMIN_EMAIL`) — **không dán mật khẩu/token vào báo cáo test**.

---

## 2. Bảng tổng quan các Test Case (Quick Triage)

| TC ID | Nhóm / Ticket | Kịch bản chính | Trạng thái cho QA | Lưu ý |
|---|---|---|---|---|
| **TC-01** | Bugfix · `SPARK-395` | Chọn filter `Zone A` trên Operator `Lot Status & 3D Map` | `READY FOR QA` | Kiểm tra hiển thị đúng `48 / 48` (không phải `0 / 48`) & reset filter khi đổi site |
| **TC-02** | Bugfix · `SPARK-394` | Tìm kiếm theo mã slot `CAR-001`, `EV-001`, `CarParkingSpace_001` | `READY FOR QA` | Khớp đúng trên Grid, List và 3D Viewer |
| **TC-03** | Bugfix · `SPARK-393` | Tạo Floor L2 khi messaging/admission tắt (`AddUnit`) | `READY FOR QA` | Thành công `200/201`, không kẹt `503 COMMITMENTS_UNAVAILABLE` |
| **TC-04** | `2b` · Max Floors | Chặn vượt trần số tầng ở Owner Onboarding / Create Parking Lot (`outdoor=1`, `basement=2`, `multi-storey=3`) | `READY FOR QA` | Chặn cả UI lẫn BE; site cũ `max_floors = NULL` & `.glb` custom vẫn hợp lệ |
| **TC-05** | `2b` · Max Floors | Chặn sửa `floors` / `+ Floor` vượt trần trong `SiteForm` & `UnitDialog` (`MAX_FLOORS_EXCEEDED`) | `READY FOR QA` | Lỗi hiển thị rõ ràng từ BE & FE |
| **TC-06** | `2a` / `2c` · Maintenance | Tạo Maintenance (`SITE`/`UNIT`/`SLOT`) → `acknowledged` (`revision = 1`) → phát `FacilityMaintenanceChangedV1` & tự cập nhật qua `start`/`end` sang Operator 3D/2D & tab Maintenance | `READY FOR QA` | Không cần F5; Facility commit độc lập không chờ HTTP ack |
| **TC-07** | `2a` / `2c` · Maintenance | Bảo toàn xe đang đỗ (`OCCUPIED`) khi slot/zone vào giờ bảo trì | `READY FOR QA` | Slot `OCCUPIED` giữ nguyên trạng thái xe & checkout bình thường |
| **TC-08** | `2a` · Maintenance + Reservation | Booking trùng lịch khi Facility phát `FacilityMaintenanceChangedV1` (`UPSERTED`) → phía Operation chặn đơn & đối soát | **`BLOCKED`** *(SPARK-273)* | **Consumer/reconciliation phía `OperationService` thuộc sở hữu `SPARK-273`.** Đầu phát `FacilityMaintenanceChangedV1` bên `FacilityService` đã pass (`MaintenanceEnforcementFlowTests`) |
| **TC-09** | `2d` · Emergency | Operator bật `EMERGENCY` / `FORCED_MAINTENANCE` ở `SLOT`/`UNIT`/`SITE` + giữ nguyên Incident Logbook + xác nhận 2 bước cho `SITE` | `READY FOR QA` | Slot trống đổi sang `UNAVAILABLE`/`MAINTENANCE`; slot `OCCUPIED` giữ nguyên |
| **TC-10** | `2d` · Auto-Expiry | Đặt Emergency/Forced Maintenance có `effectiveUntil` (vd +1–2 phút) → hết giờ tự nhả slot & tự đồng bộ UI | `READY FOR QA` | Ticker BE quét mỗi 15s (`MaintenanceBoundaryTickerWorker.ExpireDueAsync`), FE dùng single-shot `setTimeout` |
| **TC-11** | `2d` · Manual Release | Bấm Release thủ công trước `effectiveUntil` | `READY FOR QA` | Slot hồi `AVAILABLE` (nếu không bị cửa sổ bảo trì/phong tỏa khác che phủ) |
| **TC-12** | `2d` · Emergency + Reservation | Emergency Closure phát `FacilityCapacityRestrictedV1` → phía Operation giữ `PARKING`, hủy đơn & hoàn tiền | **`BLOCKED`** *(SPARK-273)* | **Consumer/reconciliation phía `OperationService` & Payment thuộc sở hữu `SPARK-273`.** Đầu phát bên `FacilityService` đã pass (`EmergencyRestrictionTests`) |
| **TC-13** | Refresh · Owner | Owner `OperatorManagement`: đăng ký/verify account Operator mới rồi bấm nút **Refresh** | `READY FOR QA` | Danh sách Operator cập nhật ngay không cần hard-refresh F5 |
| **TC-14** | Refresh · Operator | Nút **Refresh** / **Retry now** tại `OperatorDashboard` (sites & slots), `LotStatus`, và `EmergencyPanel` | `READY FOR QA` | Dữ liệu site, slots và danh sách restrictions tải mới ngay |

---

## 3. Chi tiết các bước thực hiện cho nhóm trọng tâm

### Nhóm 1 — Bugfix Operator Filter, Search & Structure (`TC-01`..`TC-03`)
1. **TC-01 (`SPARK-395`):** Đăng nhập Operator được gán `DEMO-HQ` → vào **Lot Status & 3D Map** → ở bộ lọc Zone chọn `Zone A`.
   - **Kỳ vọng:** Bộ đếm & lưới hiển thị `48 / 48` slot thuộc Zone A (không còn `0 / 48`). Nếu tài khoản có >= 2 site, đổi sang site khác thì bộ lọc Zone tự reset về `All Zones`.
2. **TC-02 (`SPARK-394`):** Ở ô tìm kiếm slot tại `DEMO-HQ`, lần lượt nhập `CAR-001`, `EV-001`, `CarParkingSpace_001`.
   - **Kỳ vọng:** Khớp đúng slot trên Grid, List và highlight trên 3D Viewer.
3. **TC-03 (`SPARK-393`):** Với cấu hình messaging/admission tắt (`false`), vào Owner → **Parking Structure** → `AddUnit` tạo thêm `Floor` mới.
   - **Kỳ vọng:** Trả `200/201` thành công, không lỗi `503 COMMITMENTS_UNAVAILABLE`.

### Nhóm 2 — Trần số tầng theo 3D Model (`TC-04`..`TC-05`)
1. **TC-04:** Tại Owner Create Parking Lot / Onboarding: chọn `outdoor` (tối đa 1), `basement` (tối đa 2), `multi-storey` (tối đa 3), thử nhập số tầng lớn hơn trần.
   - **Kỳ vọng:** UI cảnh báo/chặn; gọi API trực tiếp bị BE từ chối.
2. **TC-05:** Tại Owner **Parking Structure** của bãi đã chạm trần tầng, bấm `+ Floor` (`UnitDialog`).
   - **Kỳ vọng:** Bị chặn với mã lỗi `MAX_FLOORS_EXCEEDED`. Các bãi cũ (`max_floors = NULL`) hoặc chọn custom `.glb` không bị chặn nhầm.

### Nhóm 3 — Bảo trì có xác nhận & Đồng bộ Operator (`TC-06`..`TC-08`)
1. **TC-06:** Ở Owner **Parking Structure → Service availability**, tạo cửa sổ bảo trì ngắn (vd `start` sau 1 phút, `end` sau 3 phút) cho `SITE`, `UNIT`, hoặc `SLOT`.
   - **Kỳ vọng:** Lưu thành công ngay (`Active schedule` / `acknowledged`, `revision = 1`) và phát event `FacilityMaintenanceChangedV1` qua outbox mà không phụ thuộc Operations online. Mở tab Operator **Lot Status & 3D Map** (không bấm F5): qua mốc `start`, slot chưa có xe tự chuyển `MAINTENANCE` trên 3D/2D và xuất hiện trong tab Maintenance; qua mốc `end`, tự hồi về `AVAILABLE`.
2. **TC-07:** Trước mốc `start`, cho 1 xe walk-in vào đỗ (`OCCUPIED`). Qua mốc `start`:
   - **Kỳ vọng:** Slot đang `OCCUPIED` **không bị ghi đè** thành `MAINTENANCE`, xe vẫn check-out bình thường; sau khi check-out mới phản ánh trạng thái trống/bảo trì còn hiệu lực.
3. **TC-08 (`BLOCKED` — `SPARK-273`):** Đầu phát `FacilityMaintenanceChangedV1` bên `FacilityService` đã sẵn sàng; phần consumer, local projection & đối soát trên `OperationService` giao lại cho owner `SPARK-273` triển khai theo handoff.

### Nhóm 4 — Khóa Khẩn Cấp (Emergency Closure), Auto-Expiry & Hoàn tiền (`TC-09`..`TC-12`)
1. **TC-09:** Vào Operator **Emergency & Incidents**, xác nhận mục Incident Logbook vẫn hoạt động bình thường. Tạo mới ở mục **Emergency Closure / Slot Restrictions**:
   - Thử `mode = EMERGENCY` và `mode = FORCED_MAINTENANCE` trên `SLOT`, `UNIT`, và `SITE` (scope `SITE` yêu cầu xác nhận 2 bước).
   - **Kỳ vọng:** Slot trống đổi sang `UNAVAILABLE` (Emergency) hoặc `MAINTENANCE` (Forced Maintenance) và đồng bộ ngay sang 3D/2D; slot `OCCUPIED` đang có xe đỗ giữ nguyên không bị đẩy.
2. **TC-10:** Tạo Emergency hoặc Forced Maintenance có đặt `effectiveUntil` = hiện tại + 1 phút. Quan sát UI không cần bấm F5:
   - **Kỳ vọng:** Sau khi qua `effectiveUntil` tối đa ~15 giây (`MaintenanceBoundaryTickerWorker`), restriction tự đóng (`released_at != NULL`), slot hồi `AVAILABLE` và danh sách UI tự cập nhật.
3. **TC-11:** Tạo restriction không đặt hạn (hoặc hạn dài), sau đó bấm **Release**.
   - **Kỳ vọng:** Slot hồi `AVAILABLE` ngay lập tức.
4. **TC-12 (`BLOCKED` — `SPARK-273`):** Đầu phát `FacilityCapacityRestrictedV1`/`ReleasedV1` bên `FacilityService` đã sẵn sàng; phần consumer trên `OperationService` & đối soát Payment giao lại cho owner `SPARK-273` triển khai.

### Nhóm 5 — Kiểm tra toàn bộ nút Refresh (`TC-13`..`TC-14`)
1. **TC-13:** Mở sẵn màn hình Owner **Business → Operators**; dùng một cửa sổ khác đăng ký & verify tài khoản Operator mới → quay lại tab Owner bấm **Refresh** (không F5 cả trang).
   - **Kỳ vọng:** Account Operator mới hiển thị ngay trên bảng.
2. **TC-14:** Kiểm tra nút **Refresh** trên `OperatorDashboard` (Reload sites & slots), nút **Retry now** ở banner freshness của `LotStatus`, và nút **Refresh** trong `EmergencyPanel`.
   - **Kỳ vọng:** Gọi lại API lấy dữ liệu mới nhất thành công, không cần hard-refresh trình duyệt.

---

## 4. Bộ kiểm thử tự động đi kèm (Baseline của PR)

QA hoặc Engineer có thể chạy lại toàn bộ bộ test tự động xác nhận trước khi nghiệm thu (đặc biệt để kiểm chứng nhánh Reservation/Reconciliation đang `BLOCKED` trên E2E):

```powershell
# 1. Build toàn solution
dotnet build SmartParking.slnx --nologo

# 2. Chạy toàn bộ unit tests các service liên quan
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --nologo
dotnet test tests/Services/SmartParking.OperationService.Tests/SmartParking.OperationService.Tests.csproj --nologo
dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --nologo
dotnet test tests/Services/SmartParking.PaymentService.Tests/SmartParking.PaymentService.Tests.csproj --nologo
dotnet test scripts/architecture/SmartParking.Architecture.Tests/SmartParking.Architecture.Tests.csproj --nologo

# 3. Chạy riêng nhóm test Maintenance & Emergency Producer trên FacilityService
dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --nologo --filter "FullyQualifiedName~MaintenanceEnforcementFlowTests|FullyQualifiedName~EmergencyRestrictionTests|FullyQualifiedName~OperationalLayoutMaintenanceProjectionTests"

# 4. Frontend unit tests & Vite production build
node --test tests/frontend/maintenance-lifecycle-and-operator-sync.test.cjs tests/frontend/max-floors-rules.test.cjs tests/frontend/spark-395-zone-search.test.cjs tests/frontend/emergency-restrictions.test.cjs tests/frontend/refresh-and-expiry.test.cjs tests/frontend/parking-structure-rules.test.cjs
npm --prefix FE run build
```

**Kết quả ghi nhận (sau khi tách phần `OperationService` consumer/reconciliation về `SPARK-273` và merge `develop`):**
- `dotnet build SmartParking.slnx --nologo`: **0 errors**
- `SmartParking.FacilityService.Tests`: **289 passed / 0 failed / 36 DB-gated skipped** (30/30 `EmergencyRestrictionTests`, 9/9 `MaintenanceEnforcementFlowTests`)
- `SmartParking.OperationService.Tests`: **81 passed / 0 failed** (5/5 `DisabledReservationFactsPublisherTests`)
- `SmartParking.UserService.Tests`: **415 passed / 0 failed / 70 skipped**
- `SmartParking.PaymentService.Tests`: **255 passed / 0 failed / 3 skipped**
- `SmartParking.Architecture.Tests`: **12 passed / 0 failed**
- Frontend `node --test` (6 suites): **73/73 passed / 0 failed**; `npm --prefix FE run build`: **exit 0**
