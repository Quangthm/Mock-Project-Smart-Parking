# Review tổng hợp các task tài khoản và cấu trúc bãi — 06/10/2026

> Historical report/guide. See [fix and review, 08/10/2026](cross-task-fix-review-2026-10-08.md) and its current contract links.

## Kết luận

Chưa đủ điều kiện đánh dấu toàn bộ nhóm task là hoàn thành end to end. Backend từng phần đã có và bộ test hiện tại đạt, nhưng luồng Owner đăng ký → Admin duyệt → tạo bãi thật → tạo Operator trên giao diện vẫn bị ngắt. Review này không sửa source, không commit/push và không thay đổi trạng thái task.

Phạm vi lấy từ chat “Chọn task chưa cần API design”: SPARK-173, SPARK-176, SPARK-179/196, SPARK-206, SPARK-188 và SPARK-192. SPARK-166 được nhắc là task review riêng, không thuộc chuỗi triển khai này.

## Findings theo mức ưu tiên

### 1. [P1] Owner được duyệt chưa có membership để dùng Parking Structure hoặc Create Operator

`OwnerRegistrationService.cs:38–40` tạo account BUSINESS_OWNER mà không gán TenantId. `ReviewAsync`, dòng 89–92, chỉ đổi trạng thái user/account. Migration 05.5 cũng không provision tenant. Trong khi đó, `PostgresParkingStructureRepository.Authorize` yêu cầu account thuộc tenant ACTIVE; `OperatorProvisioningService.cs:28–38` JOIN tenants và yêu cầu tenant membership BUSINESS_OWNER.

Do đó Owner mới đăng ký rồi được Admin duyệt có thể đăng nhập, nhưng vẫn nhận FORBIDDEN khi gọi Create Operator với các site hợp lệ; Parking Structure core cũng không chấp nhận scope của tài khoản này. Luồng chỉ hoạt động nếu membership được cấp riêng bằng cách khác. Repo chưa có luồng ứng dụng nối bước đó với onboarding.

Cần xác định và triển khai nơi provision tenant/membership, bảo đảm tính nguyên tử hoặc khả năng retry, rồi thêm acceptance test dùng chính Owner tạo từ RegisterAsync và ReviewAsync. Không dùng Owner có membership được seed sẵn để đại diện cho luồng này.

### 2. [P1] SPARK-192 trên FE vẫn tạo tài khoản local và lưu password dạng rõ

`FE/src/roles/owner/operators/CreateOperatorForm.tsx:32–36` đưa password vào payload, gọi `store.createUser`, báo thành công và đóng form. `FE/src/lib/store.ts` lưu danh sách users bằng JSON trong localStorage. Form không gọi POST /api/users.

Tài khoản vừa được báo tạo thành công không tồn tại trong PostgreSQL và không đăng nhập được bằng auth backend. Mật khẩu do Owner nhập vẫn nằm trong localStorage. Các preset financial/operation/cashier và site ID local cũng chưa ánh xạ sang site UUID cùng permissions của contract backend.

Cần nối form vào API sau khi có danh sách bãi thật và permission mapping được thống nhất; chỉ báo thành công sau response 201, bỏ lưu plaintext password và lấy danh sách staff từ persistence thật. Backend Create Operator hoàn thành riêng không đồng nghĩa giao diện đã hoàn thành task.

### 3. [P1] SPARK-188 chưa có đường truy cập ứng dụng, FE vẫn lưu bãi local

ParkingService hiện có Domain/Application/Persistence, không có API host/controllers hoặc adapter được đăng ký trong UserService. `SmartParking.slnx` xác nhận chỉ ba project core. `FE/src/roles/owner/parking-lots/CreateParkingLotForOwner.tsx:56–69` tạo bãi, slot và đánh dấu onboarding hoàn thành bằng local store; `SiteManagement.tsx:106` cũng gọi store.createLot.

Bãi vừa tạo trên màn hình không xuất hiện trong parking_sites và không thể làm tài nguyên cho API Create Operator. Thay trình duyệt sẽ không thấy bãi này. Core đáp ứng phạm vi đã ghi trong báo cáo SPARK-188, nhưng task tạo/quản lý cấu trúc bãi end to end còn thiếu.

Cần chốt contract HTTP, thêm host/auth adapter và nối FE với persistence thật. Acceptance phải kiểm tra tạo bãi bằng Owner đã duyệt, đọc lại bằng phiên/trình duyệt khác và dùng UUID bãi đó để tạo Operator.

### 4. [P2] SPARK-173 còn khác biệt với authentication trong SRS

SRS §3.1.4 yêu cầu OTP authentication, MFA TOTP tùy chọn và session token 24 giờ. Login hiện nhận email/password; `AccessTokenService.cs:13` đặt lifetime 3600 giây. FE không gọi refresh mà phát sự kiện hết phiên khi access token hết hạn (`AppContext.tsx:85–90`). OTP đăng ký Driver không thay thế OTP login.

Đây là khác biệt đã được tài liệu alignment và review SPARK-173 ghi nhận. Luồng password Login/Logout hiện tại có test đạt; chưa thể kết luận toàn bộ FR-AUTH-02 đạt SRS. Cần xác nhận thay đổi yêu cầu hoặc triển khai policy/luồng còn thiếu trước khi nghiệm thu theo SRS.

## Trạng thái từng task

| Task | Phần hiện có | Phần cần hoàn tất/kiểm chứng |
| --- | --- | --- |
| SPARK-173 | Password login, /me, refresh backend, logout và kiểm tra phiên/quyền hiện tại | Khác biệt SRS tại finding 4; browser acceptance |
| SPARK-176 | API + FE đăng ký Driver/OTP, persistence, expiry/lock/resend | Gửi email/SMS thật; browser acceptance; recovery nếu mất challenge ID chưa có |
| SPARK-179/196 | API + FE đăng ký/duyệt Owner, trạng thái và review evidence | Membership/onboarding xuyên task tại finding 1; browser acceptance |
| SPARK-206 | API + FE tìm kiếm/phân trang/chi tiết/khóa-mở khóa; thu hồi phiên | Browser acceptance; activity hiện chỉ là 50 login sessions, không phải toàn bộ hoạt động nghiệp vụ |
| SPARK-188 | Domain/Application/PostgreSQL core và migration 05.6 | HTTP host/contract/auth adapter, FE và dữ liệu bãi thật tại finding 3 |
| SPARK-192 | POST /api/users, persistence/grants và permission check | Nối FE, membership/bãi thật tại findings 1–2; operational endpoints phải sử dụng permission check |

## Kiểm chứng thực hiện trong lần review này

- `dotnet test SmartParking.slnx --no-restore --verbosity minimal`, với SMARTPARK_AUTH_TEST_CONNECTION trỏ tới PostgreSQL test hiện có tại 127.0.0.1:65433: **73 passed, 0 failed, 0 skipped** (62 UserService + 11 ParkingService).
- Các integration test tự tạo/xóa database UUID riêng. Không migrate hoặc thay dữ liệu database dự án ở cổng 5433; không tạo/dừng container.
- `npm run build --prefix FE`: **pass**, còn warning chunk lớn (bundle JS khoảng 2.23 MB trước gzip).
- `git diff --check`: **pass**, có cảnh báo chuyển LF/CRLF ở một số file hiện có.
- Lần test đầu chưa đặt connection test: 66 passed, 7 skipped; kết quả dùng để kết luận là lần chạy đầy đủ ở trên.
- Chưa chạy browser walkthrough, SMTP/SMS thật hoặc acceptance xuyên các task. Bộ test hiện có kiểm chứng core/service và HTTP TestHost; test Owner/Operator/Parking riêng không chứng minh toàn bộ onboarding liên thông.

## Thứ tự xử lý

1. Nối Owner approval/onboarding với tenant membership.
2. Hoàn thiện API Parking Structure và nối FE để có site UUID thật.
3. Nối Create Operator FE vào API, bỏ credential local và ánh xạ permission.
4. Chạy acceptance Owner đăng ký → duyệt → tạo bãi → tạo Operator → Operator đăng nhập, kiểm tra quyền bãi khác và thu hồi quyền.
5. Chốt khác biệt SRS/authentication, kiểm chứng OTP delivery thật và browser acceptance cho các luồng tài khoản.
