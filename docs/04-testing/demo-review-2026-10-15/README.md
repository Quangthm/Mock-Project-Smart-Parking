# Review và checklist demo SmartParking — 16/10/2026

**Cập nhật sau khi Docker chạy:** xem [runtime-recheck.md](runtime-recheck.md). Cả 6 DB healthy; basic Admin login/logout đã kiểm tra; PostgreSQL tests User **83/83**, Parking **33/33**, không skip. Các số skip/Docker chưa chạy bên dưới là bằng chứng của lượt review ban đầu. Các blocker nghiệp vụ/frontend vẫn còn.

Review ngày 15/10/2026 (Asia/Saigon), nhánh `Fix2FA`, HEAD `6165024c634316d15525fe48bdc5a3ddd8193c1a` **cộng các thay đổi local chưa commit**. Theo lựa chọn của người dùng, lượt này chỉ review, tạo checklist và sắp thứ tự sửa. Không sửa source, migration, dependency, không commit/push, không chạy thanh toán thật.

## Kết luận để chuẩn bị demo

**Chưa đủ bằng chứng để demo toàn bộ chuỗi như một hệ thống end-to-end đã hoàn thành.** Identity đã có API thật; phần Owner structure, Owner 3D và Operator chưa nối thành một luồng dùng cùng dữ liệu backend. Payment chưa có service thực thi. Build thành công không chứng minh các luồng này hoạt động.

| Bước người dùng muốn demo | Hiện trạng source | Điều kiện / điểm chặn |
| --- | --- | --- |
| Login / logout | Có backend và frontend; unit/HTTP tests không cần PostgreSQL đã chạy | Cần chạy thử trên DB thật; logout/refresh race còn tồn tại |
| Driver đăng ký | Có đăng ký, OTP, kích hoạt | Cần SMTP/nhà cung cấp và inbox thực tế; test PostgreSQL đang skip |
| Owner đăng ký | Có đăng ký, xác minh email, chờ duyệt | Không bỏ qua xác minh; dùng contact mới |
| Admin duyệt Owner | Có API và kiểm tra quyền duyệt | FacilityService phải hoạt động để tạo tenant; chưa chạy acceptance thực tế |
| Owner tạo parking structure | Backend có site/unit/slot CRUD; màn hình đang dùng structure local | Onboarding tạo lot local; tab Structure không dùng luồng CRUD backend; form Site có thể che lỗi API |
| Owner xem 3D | Có viewer dùng ở Driver/Operator | Owner chưa có đường gọi viewer; chưa chứng minh model chứa đúng slot đã lưu |
| Owner tạo Operator | Có API thật và form gọi API | Account Active trước xác minh contact; mail onboarding cần public origin HTTPS |
| Operator check-in / check-out | UI đang thay đổi local store | Chưa có backend admission/session/checkout/departure; không chứng minh scope và concurrency |
| Slot đổi trạng thái ngay | UI cục bộ có đổi occupied/available | Chưa có authoritative occupancy write/projection và đồng bộ Owner/Operator |
| Payment (tùy chọn) | Schema DB và UI có sẵn | Chưa có Payment service/payOS order/callback/settlement; không demo là giao dịch thật |

## Những lỗi cần chỉ cho leader trước

1. **Báo tạo site thành công dù API lỗi.** `FE/src/roles/owner/parking-lots/SiteForm.tsx:207` bắt mọi lỗi tạo site, tự sinh `site-${Date.now()}` và gọi `onSaved`. Cả lỗi quyền, trùng mã và mất kết nối đều có thể bị che. Sửa đầu tiên để rehearsal không bị kết quả giả.
2. **Owner structure và Operator dùng hai luồng dữ liệu tách rời.** `OwnerDashboard.tsx:45` vào onboarding local; `ParkingStructure.tsx:2,89` đọc structureStore; `OperatorDashboard.tsx:44` đọc lot local. SiteForm còn tạo local lot với ID khác site backend. Không thể dùng đổi màu trong browser làm bằng chứng hai tài khoản nhìn cùng slot.
3. **Check-in/out còn thiếu vertical slice backend.** Reservation API chỉ có ba route internal bảo vệ structure; không có admission/session/departure. Cần triển khai nghiệp vụ, quyền, transaction, replay và occupancy trước khi nối UI.
4. **Quyền tenant bị ghép sai membership.** Có quyền tại A có thể lấy scope B dù B có permissions rỗng. Ảnh hưởng tạo Operator và thao tác structure. Hai agent độc lập cùng xác nhận source; xem ID-02 / OPS-02.
5. **Operator Active trước xác minh và security baseline chưa đúng.** Access token vẫn 24 giờ; refresh thiếu inactivity 24 giờ. Admin user management bỏ qua `ACCOUNT_ADMIN`. Đây là lý do chức năng có thể chạy nhưng chưa đạt chuẩn.

Chi tiết có file/dòng, tác động và hướng sửa trong [identity-review.md](identity-review.md), [operations-review.md](operations-review.md), [frontend-review.md](frontend-review.md).

## Thứ tự sửa trước demo

Các mục dưới là backlog đề xuất, **chưa thực hiện trong lượt review**. Không cam kết kịp thời gian khi còn thiếu cả workflow.

| Thứ tự | Công việc | Tiêu chí xong |
| --- | --- | --- |
| 1 | Làm luồng tạo site trung thực: bỏ fake-success fallback; giữ nguyên ID backend; onboarding không tạo bãi chỉ ở local | API lỗi hiện lỗi; API thành công có GET lại cùng ID; tài khoản/browser mới thấy đúng dữ liệu |
| 2 | Nối Owner structure vào API có sẵn; tận dụng logic OwnerWorkspace sau khi review, không thay toàn dashboard mù quáng | Tạo floor/zone/slot, tải lại bằng GET; code và số slot đúng; DB rỗng hiển thị rỗng |
| 3 | Sửa action + tenant scope cùng membership; hoàn thiện Operator xác minh và tải assignments thật | Deny tenant không được cấp quyền, Operator chưa xác minh không có session, chỉ thấy site được giao |
| 4 | Bổ sung Operations entry/session và actual departure với occupancy authoritative; không ghi DB của service khác | Một entry chiếm một slot; trùng thao tác không nhân đôi; hai entry tranh slot chỉ một thắng; sai lot bị từ chối |
| 5 | Nối Operator UI; reload authoritative state sau command, đưa trạng thái đó vào Owner và 3D | Cùng siteId/slotId/sessionId; browser thứ hai nhận dữ liệu mới; không lấy localStorage làm nguồn quyết định |
| 6 | Mở 3D viewer cho Owner từ structure đã lưu và mapping slot ID ổn định | Model hiển thị đúng layout; entry/departure cập nhật đúng màu của đúng slot |
| 7 | Chốt security và regression liên quan: lifetime, idle, Admin permission, phone normalization, logout race | Test đúng yêu cầu; test PostgreSQL chạy trên DB tách biệt, không chỉ unit fake |
| 8 | Payment tùy thời gian: triển khai Payment owner, payOS và callback/reconciliation thật | Provider verification, amount/order binding, idempotency; payment không tự giải phóng occupancy |

Không dành thời gian trước demo cho rename toàn solution hoặc di chuyển folder hàng loạt. Các lệch CQRS/Clean Architecture cần sửa theo từng slice có test; đổi vị trí class không thay thế việc sửa hành vi và phân quyền.

**Quy tắc checkout:** chốt giờ tính phí, thanh toán/clearance, mở cổng và xe thực sự rời bãi là các sự kiện khác nhau. Theo FR-GATE-05, chỉ xác nhận actual departure mới giải phóng occupancy. Nếu nút demo gộp thao tác, UI phải thể hiện rõ xác nhận xe đã rời bãi và backend vẫn giữ các trạng thái riêng. Không âm thầm thay payment bằng cash hoặc fixture.

## Checklist rehearsal

Hiện tất cả checkbox dưới là **chưa xác nhận end-to-end**; chỉ đánh dấu sau khi thực hiện và lưu bằng chứng.

- [ ] Mở Docker Desktop; kiểm tra các DB/service theo `LOCAL-SETUP.md`. Không reset/xóa volume để chữa lỗi. Setup/schema đúng branch và migration MFA local hiện tại.
- [ ] Chạy User `5035`, Parking `5045`, Reservation `5055`, frontend `5173`; xác nhận URL frontend trỏ đúng service.
- [ ] Chuẩn bị Admin có quyền duyệt, email Owner/Driver mới, SMTP nhận thư thật. Kiểm tra delivery thực tế, không chỉ HTTP queued. Mail Operator cần HTTPS `Onboarding:PublicOrigin` cấu hình hợp lệ.
- [ ] Driver đăng ký → nhập OTP → đăng nhập → F5 → logout → gọi lại `/me` bằng session cũ bị từ chối. Sai/expired OTP không kích hoạt account.
- [ ] Owner đăng ký → xác minh email → PendingApproval; trước duyệt không truy cập Owner operation. Admin duyệt đúng application; đăng nhập lại Owner đã được gán tenant.
- [ ] Owner tạo site → units → slots. Ghi lại siteId/slotId và GET sau reload; kiểm tra không tự hiện fixture khi danh sách trống. API lỗi không báo thành công.
- [ ] Owner mở model của chính site đó; so code/số slot với GET backend. Browser mới phải thấy cùng layout.
- [ ] Owner tạo Operator gắn site → recipient xác minh → Operator đăng nhập → assignments chỉ có site được cấp. Wrong-site/revoked grant phải bị backend từ chối.
- [ ] Actual entry → có session ID → đúng slot occupied → Owner và Operator thấy cùng dữ liệu. Refresh trang/browser khác không tạo khác biệt. Gửi lại command không tạo session thứ hai.
- [ ] Checkout chốt phí; nếu chưa có financial clearance thật thì ghi rõ giới hạn. Xác nhận actual departure → session kết thúc → đúng slot available; replay không release hai lần.
- [ ] Nếu demo payment: tạo order đúng amount/site/session, hoàn tất qua adapter đã triển khai, callback lặp không ghi nhận hai lần; trước actual departure slot vẫn occupied. Nếu chưa đạt, loại khỏi demo chính và ghi là chưa triển khai.
- [ ] Quay một lần toàn luồng từ browser sạch với dữ liệu riêng cho rehearsal. LocalStorage tồn tại sau F5 không phải bằng chứng persistence backend.

Nếu giữ nguyên source hiện tại, chỉ nên trình bày các bước identity **sau khi rehearsal thật thành công**; structure/check-in/out/3D/payment phải mô tả đúng là phần chưa nối xong hoặc UI fixture. Không có luồng click nào hiện được xác nhận thỏa toàn bộ yêu cầu demo.

## Kiểm tra đã chạy

| Lệnh | Kết quả | Giới hạn |
| --- | --- | --- |
| `dotnet test tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj --no-restore --verbosity minimal` | 65 passed, 18 skipped, 0 failed / 83 | Có compile ba API; các test PostgreSQL skip |
| `dotnet test tests/Services/SmartParking.FacilityService.Tests/SmartParking.FacilityService.Tests.csproj --no-restore --verbosity minimal` | 24 passed, 9 skipped, 0 failed / 33 | PostgreSQL/locking/trigger chưa được xác minh |
| `node --test tests/frontend/*.test.cjs` | 66 passed, 22 failed / 88 | 22 lỗi VM loader thiếu stub import; không kết luận 22 lỗi runtime từ kết quả này |
| `npm run build` tại FE | PASS | Không chứng minh typecheck, e2e, payment hoặc dữ liệu thật; có chunk-size warning |
| `docker version --format '{{.Server.Version}}'` | Không kết nối được Docker engine | Không chạy API + PostgreSQL end-to-end trong lượt này |

Không cộng các lần agent chạy lại/scoped test thành số test độc lập. Kết quả xanh có thể chứa expectation cũ, ví dụ 86.400 giây access token. Không có claim production readiness hoặc concurrency acceptance.

## Cách dùng tài liệu được gửi

Đã đọc guide `Downloads/AGENTS.md`, README, review 01, playbook 02, companion guide 03; CSV 04 dùng để xác định commit/phạm vi nguồn. Review gốc ở commit 6165024 không tự động mô tả các thay đổi local hiện tại. Hướng dẫn cài đặt AGENTS/companion là gợi ý trong tài liệu, không phải yêu cầu thay instruction repo. Không cài hay thay AGENTS trong lượt này.

MFA password continuation và enrollment/recovery đã có thêm trong working tree, nên không lặp kết luận cũ rằng hoàn toàn chưa có. Chưa vì vậy mà xác nhận toàn bộ MFA/recovery đạt yêu cầu; cần review/test riêng các contract còn lại. Ba agent review độc lập các phạm vi identity, operations và frontend theo yêu cầu người dùng; báo cáo là bằng chứng static + các kiểm tra nêu rõ ở trên.
