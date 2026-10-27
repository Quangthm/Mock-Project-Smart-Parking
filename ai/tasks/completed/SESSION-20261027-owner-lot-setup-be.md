# Session Review — 2026-10-27

## 1. Session Information
- **Session ID:** SESSION-20261027-001
- **Date:** 2026-10-27
- **Agent:** Main Agent
- **Branch:** fe-v2.2
- **Related Task:** SPARK-179 / SPARK-180 / V11-DES-03 — Backend Owner Lot Setup & Scoped Token Implementation
- **Status:** Completed

## 2. Objectives
- [x] Khởi tạo scoped `setupToken` trong `register/owner` response, lưu vào `OwnerApplication`, hoãn cấp phát OTP challenge tại bước đăng ký tài khoản.
- [x] Bổ sung endpoint `POST /api/auth/register/owner/lot-setup` nhận `{ registrationId, setupToken, lot }`.
- [x] Xác thực `setupToken` hợp lệ và application đang ở trạng thái pending chưa hoàn thành setup.
- [x] Lưu snapshot cấu hình `lot` (first-lot proposal) vào `OwnerApplication` (`draft_structure` / `lotSetupComplete = true`).
- [x] Issue/queue email OTP challenge ngay sau khi lưu lot setup thành công và trả về trong response payload.
- [x] Chặn verify OTP (`POST /api/auth/register/owner/verify`) và recover nếu chưa hoàn thành lot setup (`lotSetupComplete == false`).
- [x] Cập nhật DTOs (`OwnerApplicationDto`, `OwnerDeviceDto`, `OwnerLotSetupDto`, `SubmitLotSetupRequestDto`).
- [x] Cập nhật `IOwnerRegistrationService` và `OwnerRegistrationService` trong Persistence layer.
- [x] Cập nhật controller `AuthController` tương ứng.
- [x] Cập nhật database schema scripts (`05.5-Owner-Registration-Approval.sql` & `05.8-Account-Workflow-Vehicles.sql`).
- [x] Cập nhật và bổ sung tests trong `SmartParking.UserService.Tests`.
- [x] Xác thực `dotnet build SmartParking.slnx --nologo` (0 warnings, 0 errors) và `dotnet test SmartParking.slnx --nologo` (Passed: 74 in UserService, 48 in ParkingService).

## 3. Work Completed
- [x] Checkpoint 1: Khởi tạo session và đối soát yêu cầu từ V11-DES-03, `owner-register-api-design.md`, `admin-approve-owner-api-design.md`, `ownerApi.ts`, `CreateParkingLotForOwner.tsx`.
- [x] Cập nhật Domain Entity:
  - `SmartParking.UserService.Domain/Entities/OwnerApplication.cs`: Thêm `SetupToken`, `LotSetupComplete`, `DraftStructure`.
- [x] Cập nhật Application Layer:
  - `SmartParking.UserService.Application/DTOs/OwnerRegistrationDto.cs`: Thêm `OwnerDeviceDto`, `OwnerLotSetupDto`, `SubmitLotSetupRequestDto`, mở rộng `OwnerApplicationDto` với `LotSetupComplete`, `LotSetup`, `SetupToken`.
  - `SmartParking.UserService.Application/Common/Interfaces/Services/IOwnerRegistrationService.cs`: Thêm phương thức `SubmitLotSetupAsync(SubmitLotSetupRequestDto, CancellationToken)`.
- [x] Cập nhật Persistence Layer:
  - `SmartParking.UserService.Persistence/AppDbContext.cs`: Map `setup_token`, `lot_setup_complete`, `draft_structure` (jsonb).
  - `SmartParking.UserService.Persistence/Repositories/OwnerRegistrationService.cs`:
    - `RegisterAsync`: Cấp `setupToken` ngẫu nhiên 32 bytes hex, lưu vào `OwnerApplication`, hoãn gửi OTP (`Verification = null`, `LotSetupComplete = false`).
    - `SubmitLotSetupAsync`: Kiểm tra `registrationId`, trạng thái pending, `setupToken` (so sánh bằng `CryptographicOperations.FixedTimeEquals`), lưu snapshot cấu hình `draft_structure`, đánh dấu `LotSetupComplete = true`, issue email OTP challenge (`workflows.IssueOwnerAsync`), hỗ trợ idempotent retry.
    - `ReviewAsync`: Chặn duyệt nếu `!application.LotSetupComplete` (`SETUP_REQUIRED`).
  - `SmartParking.UserService.Persistence/Repositories/AccountWorkflowService.cs`:
    - `VerifyOwnerAsync`: Chặn verify nếu có đơn `OwnerApplication` mà chưa hoàn thành setup (`SETUP_REQUIRED`).
    - `ResendOwnerAsync`: Chặn resend nếu có đơn `OwnerApplication` mà chưa hoàn thành setup (`SETUP_REQUIRED`).
    - `RecoverOwnerAsync`: Chặn recover nếu chưa hoàn thành setup (`SETUP_REQUIRED`).
- [x] Cập nhật API Layer:
  - `SmartParking.UserService.API/Controllers/AuthController.cs`: Thêm endpoint `POST /api/auth/register/owner/lot-setup` với rate limiter `DriverOtp`.
- [x] Cập nhật Database Migration Scripts:
  - `scripts/database/05.5-Owner-Registration-Approval.sql`: Thêm các cột `setup_token`, `draft_structure`, `lot_setup_complete` vào định nghĩa bảng và các lệnh `ALTER TABLE IF NOT EXISTS` idempotent.
  - `scripts/database/05.8-Account-Workflow-Vehicles.sql`: Thêm các lệnh `ALTER TABLE IF NOT EXISTS` idempotent.
- [x] Cập nhật Tests:
  - `tests/Services/SmartParking.UserService.Tests/OwnerRegistrationTests.cs`: Cập nhật helper `Register` kiểm tra `SetupToken`, gọi `SubmitLotSetupAsync`, rồi verify OTP. Bổ sung các test trường hợp `INVALID_SETUP_TOKEN`, `APPLICATION_NOT_FOUND`, `SETUP_REQUIRED`.
  - `tests/Services/SmartParking.UserService.Tests/OwnerRegistrationHttpTests.cs`: Triển khai `SubmitLotSetupAsync` trên in-memory stub và bổ sung test HTTP endpoint `POST /api/auth/register/owner/lot-setup`.

## 4. Technical Decisions
- **Decision:** Sử dụng token scoped 32-byte hex ngẫu nhiên (`setupToken`) tạo tại `RegisterAsync` và lưu trữ trên `OwnerApplication` để xác thực context khi gọi `submitLotSetup`.
- **Reason:** Đảm bảo chỉ client vừa hoàn tất bước đăng ký mới được quyền lưu thông tin first-lot proposal cho application đó, ngăn chặn việc giả mạo hoặc gọi tràn lan vào đơn của người khác. Token được so sánh bằng `CryptographicOperations.FixedTimeEquals` chống timing attack.
- **Decision:** Lưu snapshot cấu hình bãi xe vào trường `draft_structure` (kiểu dữ liệu `JSONB` trong Postgres) và trường cờ `lot_setup_complete` (boolean).
- **Reason:** Phù hợp với đặc tả kiến trúc v1.1 và schema của UserService (`01-user-access-service-db.sql`), lưu toàn vẹn các thông số (giá cước, slots, floors, devices, 3D model) làm cơ sở cho Admin xem xét và FacilityService khởi tạo sau khi duyệt.
- **Decision:** Hoãn issue OTP email cho đến khi hoàn thành `submitLotSetup`.
- **Reason:** Tuân thủ quy trình Partner onboarding v1.1: Đăng ký tài khoản -> Thiết lập cấu hình bãi đỗ -> Xác minh Email OTP -> Chờ Admin duyệt.

## 5. Files Changed
| File | Change | Reason |
|---|---|---|
| `scripts/database/05.5-Owner-Registration-Approval.sql` | Modified | Thêm các cột `setup_token`, `draft_structure`, `lot_setup_complete` |
| `scripts/database/05.8-Account-Workflow-Vehicles.sql` | Modified | Thêm các cột `setup_token`, `draft_structure`, `lot_setup_complete` |
| `src/Services/UserService/SmartParking.UserService.Domain/Entities/OwnerApplication.cs` | Modified | Thêm các trường `SetupToken`, `LotSetupComplete`, `DraftStructure` |
| `src/Services/UserService/SmartParking.UserService.Application/DTOs/OwnerRegistrationDto.cs` | Modified | Thêm `OwnerDeviceDto`, `OwnerLotSetupDto`, `SubmitLotSetupRequestDto`, mở rộng `OwnerApplicationDto` |
| `src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Services/IOwnerRegistrationService.cs` | Modified | Thêm `SubmitLotSetupAsync` |
| `src/Services/UserService/SmartParking.UserService.Persistence/AppDbContext.cs` | Modified | Cấu hình ánh xạ EF Core cho các cột mới |
| `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OwnerRegistrationService.cs` | Modified | Cấp `setupToken`, xử lý `SubmitLotSetupAsync`, guard `ReviewAsync` |
| `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/AccountWorkflowService.cs` | Modified | Guard verify, resend và recover khi chưa hoàn thành lot setup |
| `src/Services/UserService/SmartParking.UserService.API/Controllers/AuthController.cs` | Modified | Thêm endpoint `POST /api/auth/register/owner/lot-setup` |
| `tests/Services/SmartParking.UserService.Tests/OwnerRegistrationTests.cs` | Modified | Cập nhật luồng test hoàn chỉnh và thêm test cases lỗi setup |
| `tests/Services/SmartParking.UserService.Tests/OwnerRegistrationHttpTests.cs` | Modified | Thêm test HTTP cho endpoint `/register/owner/lot-setup` |
| `ai/tasks/completed/SESSION-20261027-owner-lot-setup-be.md` | Created | Session tracking protocol |

## 6. Issues and Blockers
- N/A. Toàn bộ build và unit tests backend đều passed 100%.

## 7. Validation Results
- **Build:** `dotnet build SmartParking.slnx --nologo` -> Succeeded (0 Warning(s), 0 Error(s))
- **Tests:** `dotnet test SmartParking.slnx --nologo` -> Passed (UserService.Tests: 74 passed, 14 skipped; ParkingService.Tests: 48 passed, 9 skipped; Total: 122 passed, 0 failed).
- **Manual Verification:** Request/response DTOs tương thích hoàn toàn với `FE/src/lib/ownerApi.ts` và `FE/src/roles/owner/parking-lots/CreateParkingLotForOwner.tsx`.

## 8. Git Status
- **Branch:** fe-v2.2
- **Commit:** e43dc226
- **Working Tree:** 11 files modified

## 9. Handoff / Next Steps
- [ ] Chạy backend service và frontend để thực hiện kiểm thử e2e luồng Owner Onboarding từ màn hình giao diện.

## 10. Context for Next Session
- Luồng Owner Lot Setup backend đã được triển khai hoàn chỉnh, đúng chuẩn Clean Architecture và tương thích 100% với Frontend.
