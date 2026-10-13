# Driver / Vehicle — implementation, review và QA handoff

Ngày: 12/10/2026. Workspace: thư mục repo đã clone. Nhánh: `develop-fix-application`. Base commit: `304487ad0009be2de9be936dfca88d88658fe4fd`; kết quả áp dụng cho working tree hiện tại. Không sử dụng kết quả test ở bản `GithubNew` làm bằng chứng cho nhánh này.

## Thay đổi và phạm vi

Driver DTO dùng AccountPhoneAttribute để validate contact sau trim; vẫn giữ optional password và contact normalization của service. Có thêm test passwordless email/phone, normalized duplicates, pending/blocked gates, OTP expiry đúng deadline, sai 3 lần và lock 15 phút, resend giữ failure counter/invalidate code cũ, concurrent verify và wrong-code attempts, delivery provider failure/retry, HTTP privilege-field injection và session separation.

Vehicle canonical normalization được tách thành Domain value object dùng lại được, giữ raw input. List/update chỉ áp dụng cho non-deleted CAR/MOTORCYCLE có canonical plate không rỗng, thuộc current active Driver account của actor. Historical nonplated rows không bị xóa hoặc chuyển thành permanent plated registration. Tests kiểm tra persisted values, normalization, image/input validation, own update, foreign404, unsupported rows, current user/account/role states, HTTP authorization/injection và duplicate plates. FR-VEH-02 vẫn OPEN: records không xác lập/chuyển quyền sở hữu; không thêm claim/review/transfer workflow.

Giữ schema microservices baseline + integration overlay +05.8 đang có ở nhánh này. Không thay bằng consolidated schema ở GithubNew. Fixture PostgreSQL dùng database riêng cho từng test; TestClock theo precision microsecond của PostgreSQL để assertion deadline có ý nghĩa. Owner lot_type đã có trong baseline của nhánh này nên không thêm migration thừa.

Root setup.ps1 được bổ sung để script shim scripts/setup-local.ps1 chạy được, không có shared credential hardcoded. Compose schema mounts workflow migrations vào fresh initialization; existing volumes dùng script migration hiện có. .env/user-secrets hiện có được giữ, chỉ bổ sung cấu hình thiếu; runner dùng .env. Windows PowerShell5.1 probe truyền shell script qua stdin để giữ quoting. Setup không xóa/reset volume hoặc đổi password Admin đã có.

Mỗi service dùng DB của mình. Parking/Reservation trao đổi qua HTTP, service key và logical IDs; bản docs API phản ánh các Application/Domain/synchronization/receipt changes đã có của người dùng. Các repository orchestration User hiện hữu không được tuyên bố đã refactor toàn bộ Clean Architecture.

## Verification

- UserService: **78 passed, 0 failed, 0 skipped**. Final TRX: `.cache/correct-repo-redo/test-results/user-final.trx`.
- Parking tests (bao gồm Reservation integration và HTTP contracts): **32 passed, 0 failed, 0 skipped**. TRX: `.cache/correct-repo-redo/test-results/correct-repo_net10.0_20261012101259.trx`.
- Tổng: **110 passed**, build qua các project references; git diff --check passed. Một fixture Vehicle dùng alias OWNER không tồn tại được sửa thành BUSINESS_OWNER của schema hiện tại rồi chạy lại toàn bộ User suite.
- PostgreSQL test server riêng cổng65435, không dùng DB application. Các test databases được dọn; container test đã được xóa sau verification.
- Compose bootstrap độc lập đủ6 database: User/Parking/Reservation/Payment/Notification/IoT; healthy, kiểm tra TCP auth và áp dụng lại05.8–05.12 thành công. Các container/volume validation dùng project smartpark-setup-validation-20261012 riêng; không đụng volume của application.
- Setup syntax, mock -SkipDatabase/-SkipRestore chạy hai lần với .env không đổi; actual PostgreSQL TCP probe chạy trên Windows PowerShell5.1.26100.9168. Không chạy full setup vào .env/user-secrets/DB thật của người dùng.
- Preservation check:14 tracked files có pre-existing Parking/Reservation changes giữ nguyên diff so với snapshot đầu lượt; `.cache/correct-repo-redo/preservation-report.json`. Không reset/stash/stage/commit công việc hiện có.

Reproduce backend verification trên PostgreSQL isolated riêng có quyền CREATE DATABASE (không dùng production):

```powershell
cd <đường-dẫn-tới-repo-đã-clone>
$env:SMARTPARK_SCHEMA_ROOT = (Get-Location).Path
$env:SMARTPARK_AUTH_TEST_CONNECTION = '<isolated-postgres-connection>'
$env:SMARTPARK_STRUCTURE_TEST_CONNECTION = $env:SMARTPARK_AUTH_TEST_CONNECTION
dotnet test SmartParking.slnx --verbosity minimal
```

## Medium agent cross-review

Driver agent review Vehicle; Vehicle agent review Driver: không có finding còn mở. Driver agent review API docs phát hiện sample Reservation INVALID_STRUCTURE thiếu message và sequence status label dùng chung sai; đã sửa và review lại. Vehicle agent review Docker phát hiện Windows PowerShell5.1 stripping native quotes; Docker agent sửa stdin probe, kiểm tra actual PowerShell5.1, Vehicle agent review lại xác nhận resolved. Không dùng review thay thế thiết kế/API approval.

## API deliverables

Theo API Design Template.md:49 endpoint business/internal (33User,13Parking,3Reservation), cộng1 Development OpenAPI. Mỗi endpoint có Overview, API Specification, Request sample, Response sample, Validation, Activity Diagram, Sequence Diagram. Inventory đã đối chiếu actual route declarations;50 tài liệu riêng,100 PlantUML blocks,81 JSON samples parse được, ZIP integrity passed.

[Toàn bộ API](../02-architecture/api/SmartPark-All-API-Design.md) · [Local setup](../../LOCAL-SETUP.md). Bản export ban đầu có bundle và tài liệu từng endpoint; repo giữ bản Markdown tổng hợp để tránh lặp tài liệu.

## Giới hạn trước khi đánh dấu Jira Done

Cần team/API design xác nhận đầy đủ legal/special/historical VN plate formats; code hiện giữ MVP regex theo contract đang dùng. FR-VEH-02 excluded. Cần SMTP/SMS và delivery evidence thật; registration201 nghĩa persisted/queued, không nghĩa provider đã gửi. Cần downstream reallocation/refund adapters và business evidence nếu review Parking operation completion. Cần remote PR/CI/reviewer approval và QA/UAT links; chưa tạo PR hoặc chạy remote CI ở lượt này. Không đổi requirement để hợp thức hóa phần chưa có.
