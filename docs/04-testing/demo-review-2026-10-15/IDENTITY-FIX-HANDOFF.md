# R05 / R06 / R08 — implementation và review sau sửa

> Cập nhật CQRS đợt 1: broad `OperatorProvisioningService` đã được thay bằng handler điều phối trực tiếp và helper invitation nhỏ; create request/form không còn password. Password chỉ được nhập khi activation. Xem [bàn giao leader mới nhất](../../05-project-management/handoffs/2026-10-15-changes-vs-6165024.md) và [API contract](../../02-architecture/api/operator-provisioning.md). Các mô tả service/password tương thích bên dưới là baseline trước cập nhật này.

Ngày: 15/10/2026. Phạm vi được người dùng chốt: sửa R05, R06, R08 và đưa các luồng này qua kiến trúc project. Thay đổi nằm trong working tree, giữ nguyên phần MFA đang làm; chưa commit/push và chưa chạy migration lên database demo. Báo cáo REVALIDATION.md là baseline trước sửa, không phải trạng thái mới của ba finding này.

## Những gì đã sửa

| Finding | Hành vi sau sửa | Bằng chứng kiểm thử |
|---|---|---|
| R05: quyền tenant A áp sang B | Quyền và tenant được xét trên cùng membership đang active. `null` dùng role defaults; `[]` từ chối; BACKUP_MARK luôn opt-in. Create/list/reissue/delivery kiểm tra scope hiện tại. | Owner A allow/B deny qua HTTP; default/empty/opt-in qua PostgreSQL |
| R06: Operator active trước xác minh | Create tạo user/account pending; invitation email có proof một lần, hiệu lực 24 giờ, Operator tự chọn password khi activate. Login/assignment chưa được mở trước xác minh. Reissue thu hồi link cũ và cooldown một phút. | Create → pending login denied → expired/reissued link → concurrent activation chỉ một thắng → login/assignment; admin/timed unlock không bypass |
| R08: tuổi thọ session | Access token 15 phút, refresh có hạn tuyệt đối tối đa 7 ngày và hết hiệu lực sau 24 giờ không refresh. Chỉ refresh thành công cập nhật inactivity; GET không kéo dài session. | Boundary 24h/7d, concurrent rotation CAS, logout/refresh race với PostgreSQL |

Frontend refresh theo nhu cầu, single-flight; không refresh định kỳ để giữ session idle. Logout chờ rotation của đúng login. Generation fence chặn request/response của login cũ làm ảnh hưởng login mới, bao gồm MFA và đăng nhập đang pending. Invitation lỗi không xóa session độc lập đang đăng nhập.

## Kiến trúc và call paths để trình bày với leader

- Controllers identity/operator dùng `ISender` → command/query + FluentValidation pipeline → Application handler/service → repository interfaces + Unit of Work → Persistence. Async validators chạy tuần tự để không dùng chung DbContext đồng thời.
- `OperatorProvisioningService` chuyển từ Persistence sang Application. Domain `OperatorActivation` giữ quy tắc pending/verified; `SessionLifetime` giữ giới hạn session. EF và SQL scope/locking ở repository.
- Transaction tạo Operator lưu user/accounts/grants, challenge, encrypted delivery và access event outbox cùng lần commit. SMTP và publish RabbitMQ chạy ngoài database transaction.
- Global exception middleware ánh xạ lỗi nghiệp vụ/validation, trả lỗi 500 đã làm sạch với trace ID; không lộ exception nội bộ. Unique normalized email được ánh xạ thành conflict 409.
- Outbox claim có lease + SKIP LOCKED; RabbitMQ qua MassTransit; inbox deduplicate event ID và projection chỉ nhận version mới hơn. Publish lại sau crash không áp dụng lại version cũ.
- Consumer → SignalR `/hubs/operators` → `OperatorsChanged` không mang dữ liệu tenant → FE reload danh sách từ API có authorization. Hub kiểm tra session và scope hiện tại khi connect/reload/notify; connection hết hạn đóng. Reconnect reload để bắt kịp event bỏ lỡ.

Đây là refactor các luồng trong phạm vi trên. Các workflow legacy khác chưa được chuyển toàn bộ sang CQRS. Projection hiện phục vụ create/activate Operator, không phải nguồn quyết định quyền và chưa phủ toàn bộ lifecycle admin. Realtime hiện hỗ trợ một User API instance; cần backplane/fanout trước khi chạy nhiều instance.

## Chuẩn bị môi trường demo

1. Dừng User API cũ, backup database theo quy trình nhóm. Với schema checkout đang dùng, chạy `scripts/apply-workflow-migrations.ps1 -Service User -SchemaRoot <schema-checkout> -EnvPath <env-file> -ProjectName <compose-project>`. Script áp dụng workflow 05.8, MFA 05.13 rồi identity 05.14; baseline/overlay phải có trước. Fresh compose cũng mount 05.14. Không dùng migration để tự cấp quyền hoặc giả lập verified contact.
2. Migration rút ngắn session cũ, đưa Operator chưa verify email lẫn phone về pending; giữ Operator đã verify phone. Người dùng có session cũ có thể phải login lại. Owner dùng resend invitation để phục hồi Operator pending. Admin unlock không thay thế xác minh.
3. Trong `.env`, cấu hình SMTP thật và `SMARTPARK_ONBOARDING_PUBLIC_ORIGIN` đúng origin FE. HTTPS bắt buộc ngoài Development loopback. Local có thể dùng `http://localhost:5173` với `SMARTPARK_ONBOARDING_ALLOW_HTTP_LOOPBACK=true`; sửa port theo FE thực tế. Giữ workflow protection key directory ổn định qua restart để đọc được delivery đã queue.
4. Muốn demo live roster: cấu hình broker riêng qua `SMARTPARK_RABBITMQ_HOST/PORT/VIRTUALHOST/USERNAME/PASSWORD`, bật `SMARTPARK_MESSAGING_ENABLED=true`, rồi chạy User bằng `scripts/run-schema-service.ps1 -Service User`. Nếu tắt messaging, intent vẫn nằm trong outbox nhưng không có push. Broker test tạm thời không phải broker demo.
5. Khởi động Parking API cùng internal service key để xác minh sites thật. Chạy FE trỏ User/Parking đúng địa chỉ. Không dùng site ID từ local fixture để cấp assignment backend.

## Checklist rehearsal

1. Owner đã được duyệt có site backend thật → tạo Operator → thấy pending; email không chứa password Owner nhập.
2. Thử login Operator trước activation: bị từ chối. Mở link email, chọn password đúng policy; dùng lại link bị từ chối.
3. Giữ cửa sổ Owner mở: sau activation, danh sách tự reload thành active khi RabbitMQ bật. Reload browser vẫn thấy trạng thái đúng.
4. Operator login → chỉ thấy assignment được cấp. Owner tenant A không tạo/list/resend trên tenant B không có quyền.
5. Logout → token cũ không gọi API được; thử refresh/đổi tài khoản đồng thời không làm mất phiên mới. Kiểm tra expiry bằng test clock, không đổi đồng hồ hệ điều hành để demo.
6. Link hết hạn → Owner resend → link cũ không còn dùng được. Thiếu SMTP phải báo delivery thất bại, không coi create pending là email đã gửi thành công.

## Giới hạn còn lại của demo tổng thể

R01–R04, R07 và payment nằm ngoài phạm vi sửa đã chốt. Báo cáo baseline còn mô tả các khoảng trống structure/viewer, check-in/out backend, occupancy liên thông và payment. SignalR thêm ở đây cập nhật roster Operator, **không phải** slot occupancy. Chưa rehearsal inbox SMTP thật hoặc toàn bộ browser journey trên database demo; không suy ra demo toàn chuỗi sẵn sàng chỉ từ build/test.

## Kết quả kiểm thử và review

Kết quả thực thi cuối ngày 15/10/2026:

- UserService: **99/99 pass, 0 skipped**, `dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-restore --verbosity quiet -p:OutputPath=bin/IdentityFix/ --logger 'trx;LogFileName=identity-fix.trx' --results-directory .cache/identity-fix-tests`. Có `SMARTPARK_AUTH_TEST_CONNECTION` trỏ PostgreSQL local; mỗi fixture tạo/drop database riêng. Có `SMARTPARK_RABBIT_TEST_PORT=5689` trỏ RabbitMQ test riêng. File kết quả `.cache/identity-fix-tests/identity-fix.trx`.
- Frontend: **46/46 pass**, `node --test tests/frontend/auth-api.test.cjs tests/frontend/operator-live-races.test.cjs`. Bao gồm refresh single-flight, login thay thế trong lúc chờ refresh/response, late login, retry cleanup và live/create duplicate.
- `npm run build` trong FE: thành công; còn cảnh báo bundle lớn khoảng 3.26 MB chưa nén. Đây là build Vite, không thay thế kiểm thử browser.
- `git diff --check`: thành công (Git có cảnh báo chuẩn hóa CRLF).
- Khi cài dependency, npm báo 3 vulnerabilities (1 high, 2 critical); chưa phân tích dependency audit trong phạm vi này. Không chạy `audit fix` tự động.

Ba agent đã review identity/authorization, session/messaging và frontend; các lỗi migration phone verification, refresh/logout race, response cũ và live duplicate/retry tìm được đã được sửa kèm regression. Review identity cuối xác nhận không còn finding actionable trong phạm vi đọc. Root đã xác nhận migration/regression chạy thật; kết quả test không được suy từ lời review của agent.

Agent review UI cuối xác nhận reconnect/duplicate fixes không còn blocker trong phạm vi kiểm tra. Container RabbitMQ test tạm đã được dừng và tự xóa sau test; các database/container demo được giữ nguyên.
