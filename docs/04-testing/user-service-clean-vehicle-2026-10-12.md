# User Service cleanup — Vehicle Registration (12/10/2026)

## Repo và phạm vi

Repo đúng: `D:\DataFPTU\Semester6\Mock-Project-Smart-Parking`, branch `develop-fix-application`, baseline commit `70348b1`. Trước sửa chỉ có thay đổi ở `docs/04-testing/tester-api-handoff-2026-10-08.md`; thay đổi đó được giữ nguyên. Refactor lần đầu nhầm repo GithubNew đã được hoàn tác riêng, giữ nguyên các thay đổi có từ trước tại repo đó.

Đợt này chỉ clean luồng Vehicle Registration làm mẫu. Các luồng User Management, Driver registration, Owner/Operator và OTP/MFA/outbox vẫn là phần tiếp theo.

## Thay đổi

- Use case VehicleRegistrationService chuyển sang Application, không phụ thuộc EF, SQL hoặc Persistence.
- IVehicleRegistration tách thành file riêng, giữ namespace/chữ ký.
- IVehicleRepository là Application port; VehicleRepository trong Persistence thực hiện truy vấn, row locks và tracking.
- Application xác định transaction qua IUnitOfWork; UnitOfWork/repository dùng chung scoped AppDbContext.
- Giữ nguyên validation, error codes/status, raw/canonical plate, account FK, timestamp, HTTPS image và API/DB schema.
- Giữ điều kiện riêng của repo đúng: chỉ CAR/MOTORCYCLE có canonical plate không rỗng được list/update. Giữ eligibility FOR SHARE và ownership FOR UPDATE. Không thay chính sách duplicate plate OPEN.
- Vehicle tests giữ toàn bộ assertions, gồm CurrentDriverAccountScopeExcludesDeletedAndNonPlatedVehicles; chỉ đổi cách khởi tạo service.
- Bốn mock HTTP fixture bổ sung throw-only UnsupportedVehicleRepository để DI validation tiếp tục hoạt động. Full-host vehicle tests vẫn dùng PostgreSQL thật.

## Kiểm chứng

Baseline và sau refactor: UserService 77 passed, 0 failed, 0 skipped; ParkingService 24 passed, 0 failed, 8 skipped. Các test integration Parking skip do thiếu cấu hình riêng, không được tính là pass. Build qua dotnet test và git diff --check thành công.

PostgreSQL container test riêng: smartpark-user-clean-correct-20261012, loopback 65435, không mount volume dự án. Mỗi UserService integration test tạo/drop DB UUID riêng. Không áp schema hoặc migration lên DB deployment.

TRX: `.cache/review-results/user-clean-correct-baseline/` và `.cache/review-results/user-clean-correct-final/`. Original source trước sửa tại `.cache/user-clean-before-correct/` theo repository-relative path.

```powershell
# Cần PostgreSQL test tạm sẵn sàng ở port này.
$env:SMARTPARK_AUTH_TEST_CONNECTION = 'Host=127.0.0.1;Port=65435;Database=postgres;Username=review;Password=review-local-test'
$env:SMARTPARK_SCHEMA_ROOT = (Get-Location).Path
dotnet test SmartParking.slnx --no-restore --logger trx --results-directory .cache/review-results/user-clean-correct-final
git diff --check
```

Chưa commit/push/merge. Đây là một đợt refactor, chưa hoàn tất toàn bộ User Service.

## Review chéo

Hai subagent reasoning medium (review_layers_retry và review_transactions) đã đọc trực tiếp repo đúng, so sánh original backup, review độc lập và kiểm tra chéo lẫn nhau. Không có finding actionable trong phạm vi Vehicle. Đặc biệt đã kiểm tra chéo bộ lọc CAR/MOTORCYCLE và plate không rỗng ở cả list/update, scoped DbContext chung, transaction/lock/disposal và test account scope. Kết quả review của repo GithubNew trước đó không dùng làm bằng chứng cho repo này. Review local không thay thế approval của team. Container PostgreSQL test tạm đã được xóa.
