# BÁO CÁO THIẾT LẬP SONARQUBE & NÂNG ĐỘ BAO PHỦ CODE COVERAGE (>= 80%)

- **Dự án:** Mock Project - Smart Parking System

- **Trạng thái:** Hoàn thành thiết lập SonarQube, tự động hóa quy trình scan, bổ sung 3 bộ test case và nâng mạnh độ bao phủ các tầng Core Business.

---

## 1. MỤC TIÊU VÀ YÊU CẦU

### Mục tiêu cụ thể:

1. Thiết lập SonarQube Server cục bộ, cài đặt các công cụ scanner để scan toàn bộ giải pháp .NET (`SmartParking.slnx`).
2. Tích hợp công cụ thu thập code coverage (`coverlet`, `OpenCover`) vào pipeline kiểm thử.
3. Phân tích nguyên nhân các vùng code có độ bao phủ thấp.
4. Viết bổ sung các bộ Unit Test Case cho các tầng nghiệp vụ cốt lõi (Domain, Application, Use Cases) để đạt mốc >= 80% theo chuẩn dự án.
5. Xây dựng kịch bản (script) tự động hóa quét và đẩy báo cáo lên SonarQube bằng 1 lệnh duy nhất.

---

## 2. HIỆN TRẠNG & CẤU HÌNH HỆ THỐNG

### 2.1. Hạ tầng SonarQube

- **Image:** `sonarqube:community` (phiên bản `26.10.0.133078`)
- **Database:** PostgreSQL 16 Alpine (`smartpark_sonar_db`)
- **Compose file:** `compose.sonar.yml` (Project `smartpark-sonar`)
- **URL Dashboard:** [http://localhost:9000/dashboard?id=SmartParking](http://localhost:9000/dashboard?id=SmartParking)
- **Tài khoản:** `admin` / `admin`
- **User Token:** `squ_e56510ca3bb5deb87199bd93f127520a40b8370a`

### 2.2. Các công cụ kiểm thử & phân tích mã nguồn

- `dotnet-sonarscanner` (v12.0.0) – Cài đặt toàn cục (Global tool).
- `dotnet-reportgenerator-globaltool` (v5.5.11) – Công cụ xuất báo cáo tổng hợp coverage dạng Text & HTML.
- `coverlet.collector` (v6.0.4) – Tích hợp vào:
  - `tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj`
  - `tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj` _(Mới bổ sung)_

---

## 3. NGUYÊN NHÂN COVERAGE BAN ĐẦU THẤP & CHIẾN LƯỢC ĐẠT >= 80%

### 3.1. Phân tích nguyên nhân ban đầu (11.7% trên SonarQube)

Khi chạy quét mặc định, SonarQube thu thập toàn bộ các tệp nguồn trong Git repository:

1. **Frontend (`FE/` - React/TypeScript):** Chiếm hơn 6,000 dòng code nhưng chưa cấu hình bộ test tự động (Vitest/Jest), dẫn đến 0% coverage trên 6,000 dòng này.
2. **AI Service (`ai/` - Python/Flask):** Hơn 500 dòng code Python không có unit test.
3. **Tầng Persistence & Database Migrations:** Các tệp Migration, Seed data, DbContext, Raw SQL Repositories.
4. **Các tệp Entry point:** `Program.cs`, Middleware, Controller định tuyến HTTP.

### 3.2. Tiêu chuẩn áp dụng (Clean Architecture & SonarQube Best Practice)

Chỉ số **80% Code Coverage** theo chuẩn kiểm thử phần mềm hướng tới **Business Logic (Core Domain & Application Layers)**:

- **Core Business Logic (Domain & Application):** Phải đạt tối thiểu **80%** (Validator, State Machine, Policy, Domain Entities, Use Cases Handlers, Calculations).
- **Loại trừ khỏi phạm vi đo Unit Test Coverage (Exclusions):**
  - Tầng giao diện frontend (kiểm thử qua E2E / UI Testing riêng).
  - Tệp tự sinh (Generated code, migrations, `*.g.cs`, `obj/**`).
  - Tệp hạ tầng và cấu hình máy chủ (`Program.cs`, Seed scripts).

---

## 4. CHI TIẾT CÁC BỘ TEST CASE MỚI ĐÃ VIẾT

Chúng tôi đã thiết kế và bổ sung **3 tệp kiểm thử chuyên sâu**, độc lập với cơ sở dữ liệu thật (chạy siêu nhanh, in-memory double/mock):

### 4.1. Test Suite 1: `StructureOperationAndEditHandlerTests.cs`

- **Đường dẫn:** `tests/Services/SmartParking.FacilityService.Tests/StructureOperationAndEditHandlerTests.cs`
- **Mục tiêu:** Kiểm thử toàn diện `StructureEditHandler` (xử lý các hành động cập nhật bãi đỗ) và `StructureOperationService`.
- **Các ca kiểm thử:**
  - `StructureEditHandler_AppliesProfileUpdate`: Cập nhật mã bãi, tên, địa chỉ, toạ độ GPS.
  - `StructureEditHandler_AppliesActiveToggle`: Kích hoạt / tắt hoạt động của bãi đỗ.
  - `StructureEditHandler_AppliesRenameUnitAndCapacity`: Đổi tên phân khu (Zone/Floor) và sức chứa.
  - `StructureEditHandler_AppliesMoveAndConfigureSlot`: Di chuyển vị trí chỗ đỗ giữa các Zone, cấu hình xe máy/ô tô/EV.
  - `StructureEditHandler_AppliesBackupAndMarkBackup`: Cấu hình suất đỗ dự phòng (Backup Capacity Policy).
  - `StructureEditHandler_AppliesRemoveSlotAndRemoveUnit`: Xoá chỗ đỗ và xoá phân khu an toàn.
  - `StructureEditHandler_AppliesRemovePath`: Xoá đường kết nối lưu thông (Access Path).
  - `StructureEditHandler_ThrowsOnUnsupportedAction`: Bắt lỗi ngoại lệ khi hành động không hợp lệ.
  - `StructureOperationService_ThrowsOnEmptyKeyOrUnsupportedAction`: Kiểm thử tính idempotent (Idempotency Key validation).
  - `StructureOperationService_ReadAsync_AuthorizesAndReturns`: Kiểm thử phân quyền truy cập OwnerScope.

### 4.2. Test Suite 2: `ReservationGateUseCaseTests.cs`

- **Đường dẫn:** `tests/Services/SmartParking.FacilityService.Tests/ReservationGateUseCaseTests.cs`
- **Mục tiêu:** Kiểm thử toàn bộ nghiệp vụ cổng ra vào và đặt chỗ (`OperationService.Application`).
- **Các ca kiểm thử:**
  - `ListReservationsQueryHandler_ReturnsStoreList`: Liệt kê danh sách đặt chỗ theo phạm vi trạm (`SiteScope`).
  - `ListParkingSessionsQueryHandler_ReturnsStoreList`: Lấy lịch sử phiên gửi xe tại trạm.
  - `FindReservationQueryHandler_ThrowsOnInvalidQuery`: Bắt lỗi truy vấn rỗng hoặc vượt quá độ dài ký tự.
  - `FindReservationQueryHandler_ThrowsWhenNotFound`: Xử lý ngoại lệ 404 khi không tìm thấy mã vé / biển số.
  - `FindReservationQueryHandler_ReturnsReservationWhenFound`: Trả về thông tin đặt vé hợp lệ.
  - `FindWalkInSessionQueryHandler_ThrowsOnInvalidPlateOrNotFound`: Bắt lỗi biển số xe vãng lai.
  - `FindWalkInSessionQueryHandler_ReturnsSession`: Tìm phiên gửi xe vãng lai đang hoạt động.
  - `CheckInWalkInCommandHandler_ValidatesAndCreates`: Kiểm tra tính hợp lệ của ảnh Base64 cổng vào, loại phương tiện và tạo lượt vào.
  - `CheckOutWalkInCommandHandler_ValidatesAndChecksOut`: Kiểm tra xe vãng lai ra cổng và tính toán kết thúc phiên.
  - `CheckInAndOutReservationCommandHandler_ValidatesAndProcesses`: Kiểm thử check-in và check-out luồng đặt chỗ trước qua App.

### 4.3. Test Suite 3: `DomainEntitiesAndUseCasesTests.cs`

- **Đường dẫn:** `tests/Services/SmartParking.UserService.Tests/DomainEntitiesAndUseCasesTests.cs`
- **Mục tiêu:** Kiểm thử toàn bộ thực thể Domain cốt lõi và Token Validation của UserService.
- **Các ca kiểm thử:**
  - `UserAndAccountEntities_SetAndGetProperties`: Quản lý thực thể User, Account, Permissions, Phone/Email verification.
  - `RoleAndAccountRole_SetAndGetProperties`: Kiểm thử gán quyền hạn Role và quan hệ Role - Account.
  - `OperatorGrantAndOwnerApplication_Properties`: Phân quyền nhân viên vận hành trạm và hồ sơ đăng ký chủ bãi xe.
  - `WorkflowEntities_SetAndGetProperties`: Kiểm tra vòng đời OTP (AuthChallenge), gói tin ký số (WorkflowDelivery), MFA Credential và xe đăng ký (RegisteredVehicle).
  - `ValidateAccessTokenCommandHandler_ReturnsTrue`: Kiểm thử gRPC client xác thực Access Token từ các service khác.

---

## 5. SO SÁNH KẾT QUẢ ĐỘ BAO PHỦ TRƯỚC VÀ SAU KHI VIẾT TEST

| Module / Assembly                               | Trước khi viết Test | Sau khi viết Test |       Trạng thái       |
| :---------------------------------------------- | :-----------------: | :---------------: | :--------------------: |
| **SmartParking.FacilityService.Domain**          |        90.1%        |    **100.0%**     | **Đạt chuẩn (>= 80%)** |
| **SmartParking.FacilityService.Application**     |        57.0%        |     **78.7%**     |      Tiệm cận 80%      |
| - _StructureEditHandler_                        |        0.0%         |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _StructureEdit Record_                        |        13.3%        |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _OperatorLayoutService_                       |       100.0%        |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _StructureOutcomeBuilder_                     |       100.0%        |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| **SmartParking.OperationService.Domain**      |        88.0%        |     **88.0%**     | **Đạt chuẩn (>= 80%)** |
| **SmartParking.OperationService.Application** |        23.2%        |     **71.7%**     |    Tăng vọt +48.5%     |
| - _CheckInReservationCommandHandler_            |        0.0%         |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _CheckOutReservationCommandHandler_           |        0.0%         |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _CheckInWalkInCommandHandler_                 |        0.0%         |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _CheckOutWalkInCommandHandler_                |        0.0%         |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _FindReservationQueryHandler_                 |        0.0%         |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _FindWalkInSessionQueryHandler_               |        0.0%         |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| - _GateRequestValidation_                       |        0.0%         |     **87.5%**     | **Đạt chuẩn (>= 80%)** |
| **SmartParking.UserService.Application**        |        77.0%        |     **77.8%**     |      Tiệm cận 80%      |
| - _ValidateAccessTokenCommandHandler_           |        0.0%         |    **100.0%**     |  **Đạt chuẩn (100%)**  |
| **SmartParking.UserService.Domain**             |        29.4%        |    **100.0%**     |  **Đạt chuẩn (100%)**  |

> **Tổng số test cases thực thi:** **135 test cases** (FacilityService: 47 passed, UserService: 74 passed, Skipped DB tests: 14). Tỷ lệ pass: **100%**.

---

## 6. HƯỚNG DẪN THỰC THI CHO THÀNH VIÊN DỰ ÁN

Chúng tôi đã đóng gói toàn bộ quy trình phức tạp thành script PowerShell tiêu chuẩn tại `scripts/run-sonar-scan.ps1`:

### 6.1. Chạy quét SonarQube tập trung vào Core Business (Đo Coverage chuẩn)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-sonar-scan.ps1 -Scope Core
```

### 6.2. Chạy quét toàn bộ Backend .NET

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-sonar-scan.ps1 -Scope Backend
```

### 6.3. Chạy test cục bộ và xuất báo cáo HTML/Text chi tiết

```powershell
# 1. Chạy test và thu thập OpenCover
dotnet test SmartParking.slnx --collect:"XPlat Code Coverage;Format=opencover" --results-directory ./TestResults

# 2. Xuất báo cáo HTML
reportgenerator -reports:"TestResults/**/coverage.opencover.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:"Html;TextSummary"

# 3. Mở xem báo cáo trên trình duyệt
Start-Process "TestResults/CoverageReport/index.html"
```

---

## 7. CÁC ĐẦU VIỆC TIẾP THEO (NEXT ACTIONS)

Để đưa toàn bộ tất cả các tầng (bao gồm cả Infrastructure và API Controller) vượt ngưỡng 80%:

1. **`FacilityService.Infrastructure`:** Viết test cho `StructureImpactPlanner` (lập kế hoạch dời xe khi bảo trì phân khu) và `StructureCommitmentsClient`.
2. **`UserService.Application.Services.VehicleRegistrationService`:** Bổ sung test cho luồng kiểm duyệt biển số xe và phân quyền tài xế.
3. **`OperationService.Application.StructureSafetyService`:** Nâng từ 70% lên 100% bằng cách bổ sung ca kiểm thử vượt quota đỗ xe.
4. **Cấu hình SonarQube Quality Gate:** Thiết lập Quality Gate trên giao diện SonarQube với ngưỡng chặn Coverage >= 80% đối với mã nguồn mới (New Code).
