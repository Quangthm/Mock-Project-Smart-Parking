# Bộ test case bàn giao tester — hành trình demo SmartParking

Ngày soạn: 15/10/2026. Baseline Git: `6165024` (`docs(architecture): add consolidated v1.0 project overviews [SPARK-203]`) **cộng working tree chưa commit**, gồm MFA, identity hardening và CQRS/R06 đợt 1.

Tài liệu này thay thế các ví dụ Operator-create có password trong hướng dẫn cũ. Khi tạo Operator, Owner chỉ gửi tên/email/sites/permissions; Operator tự đặt password khi activate.

## 1. Mục tiêu và cách ghi kết quả

Kiểm tra: login/logout → Driver đăng ký → Owner đăng ký → Admin duyệt → Owner tạo site/structure/xem 3D → Owner tạo Operator → Operator check-in/out → slot đổi trạng thái. Payment là phần bổ sung.

**Tất cả case dưới đây chưa được tester thực thi trên môi trường bàn giao.** “Trạng thái source” là kết quả đối chiếu source, không phải kết quả PASS.

| Giá trị kết quả | Cách dùng |
|---|---|
| NOT RUN | Chưa chạy |
| PASS | Đủ tất cả kết quả mong đợi và có bằng chứng phù hợp |
| FAIL | Đã chạy nhưng hành vi sai; tạo bug kèm dữ liệu/timestamp |
| BLOCKED | Không thể chạy do dependency/dữ liệu/tính năng còn thiếu; ghi rõ lý do |
| PASS UI ONLY | Đạt thao tác mô phỏng local, chưa đạt backend/end-to-end; chỉ dùng case được gắn UI local |

Mỗi lần chạy ghi **code commit + định danh working-tree build, migration, URL môi trường, browser, ngày giờ, tester**. Vì code chưa commit, chỉ ghi HEAD không đủ xác định bản test; dev cần bàn giao cùng snapshot/diff có cả file mới hoặc commit sau khi review.

Không dùng “không crash” hoặc toast thành công làm điều kiện PASS persistence. Với dữ liệu server phải kiểm tra GET sau reload hoặc truy cập từ browser độc lập. Có thể dùng Network/Postman và read-only DB theo quyền tester; không sửa DB để vượt điều kiện nghiệp vụ.

## 2. Readiness trước khi chạy

| Phần | Trạng thái source hiện tại | Cách nghiệm thu |
|---|---|---|
| Identity/registration/approval | Có API thật | Chạy UI + kiểm tra HTTP và dữ liệu sau reload |
| Operator create/activate/reissue | Có API thật, create không password | Chạy case OP và regression tenant scope |
| Owner site | Có API; UI còn fallback local khi lỗi | Bắt buộc kiểm tra POST/GET, không dựa toast |
| Owner floor/zone/slot editor | Lưu local trong component đang mount | UI local có case riêng; backend persistence chưa đạt |
| Owner 3D viewer | Chưa có đường mở trong dashboard đang mount | Ghi gap/blocker; Operator viewer không thay thế acceptance Owner |
| Gate check-in/out | Local booking/slot store, chưa có backend Operations tương ứng | Chạy UI fixture riêng; E2E còn blocker |
| Slot realtime Owner ↔ Operator | Chưa có occupancy backend liên thông | Chạy acceptance riêng; local update không đủ PASS |
| Payment | Chưa có gateway kết nối; QR screen nói rõ chưa thu tiền | UI đến pending/method; provider/settlement BLOCKED |

Các mục thiếu vẫn thuộc hành trình người dùng yêu cầu. Tài liệu không đổi chúng thành “đã làm xong” hoặc loại khỏi phạm vi chỉ vì source chưa triển khai.

## 3. Môi trường và dữ liệu test

### 3.1 Điều kiện hệ thống

- User API `http://localhost:5035`, Parking `http://localhost:5045`, Reservation `http://localhost:5055`, FE `http://localhost:5173` (thay bằng URL QA thực tế khi cần).
- Service schema baseline/alignment và migrations User 05.8/05.13/05.14 đã áp dụng. Không chạy migration monolith cũ lên service DB.
- Các API dùng cùng internal service key. Admin approval cần Parking hoạt động.
- SMTP nhận được thư thật vào inbox test. Onboarding origin trỏ đúng FE; HTTP chỉ cho Development loopback khi bật cấu hình rõ ràng.
- Muốn chạy roster realtime: RabbitMQ đã cấu hình, `Messaging:Enabled=true`. Nếu tắt broker, ghi BLOCKED cho case live roster, không đánh FAIL create vì chưa có push.
- Model asset hợp lệ/WebGL và MapTiler key nếu màn hình bản đồ yêu cầu.
- Admin có sẵn; nếu bật MFA phải có Authenticator/recovery code. Credentials chuyển riêng, không đưa password/token vào báo cáo.

### 3.2 Dữ liệu

| Ký hiệu | Chuẩn bị |
|---|---|
| ADMIN | Admin active đăng nhập được |
| OWNER_NEW | Email nhận thư chưa đăng ký, contact name, business name, phone hợp lệ, password test đúng policy |
| DRIVER_NEW | Email nhận thư chưa đăng ký; để phone trống khi chỉ test email; password test đúng policy |
| OPERATOR_NEW | Email khác hai user trên, chưa tồn tại |
| OWNER_A / TENANT_A / SITE_A | Owner đã duyệt, membership active có OPERATOR_MANAGE/PARKING_MANAGE; site backend active |
| TENANT_B / SITE_B | Cùng Owner có membership `permissions=[]`, hoặc site của Owner khác; chưa được cấp quyền thao tác |
| SITE_INACTIVE | Site backend inactive phục vụ case âm |
| GATE_LOCAL | Lot local có booking CONFIRMED; ghi chính xác lot ID, booking ID, biển số, slot ID trước test |
| GATE_SECOND | Booking CONFIRMED thứ hai cùng slot hoặc dữ liệu xung đột, chỉ dùng sandbox test concurrency |
| MODEL | Asset 3D hợp lệ; một URL/file lỗi để kiểm tra fallback |

Không hardcode OTP hoặc invitation proof; lấy mã/link mới nhất từ inbox test. Password policy activation: 8–15 ký tự, hoa/thường/số/ký tự đặc biệt.

### 3.3 Cửa sổ trình duyệt

- Identity backend: dùng profile riêng cho Admin/Owner/Operator để tránh đổi nhầm session; login phải có role rõ ràng.
- UI local gate/slot: dùng cùng browser profile và origin, logout/login lần lượt để giữ dữ liệu local. Browser/ẩn danh độc lập không tự nhận cùng localStorage.
- Case đồng bộ backend bắt buộc dùng profile độc lập hoặc máy khác; không chuyển thành local-only chỉ để case pass.

## 4. Login, logout và session

### AUTH-01 — Login password đúng, khôi phục sau reload — P0

**Tiền điều kiện:** user active, verified; dùng lần lượt Driver/Owner/Operator/Admin đã có quyền.

1. Mở FE → **Sign In**.
2. **Sign-in method → Password**; nhập **Email or Phone Number**, **Password**.
3. Bấm **Sign In**; hoàn thành MFA nếu bật.
4. F5, kiểm tra tên user/role và dashboard.

**Mong đợi:** login đúng role; `/api/auth/me` xác nhận cùng user; reload không đổi danh tính. Không lộ password/hash/activation proof trong response. Access session có `expiresIn` tối đa 900 giây, có thể ngắn hơn khi sát hạn tuyệt đối.

### AUTH-02 — Password sai/user chưa có credential — P0

1. Dùng user active, nhập password sai một lần → **Sign In**.
2. Dùng Operator pending vừa tạo ở OP-01, thử password bất kỳ.

**Mong đợi:** không cấp session/dashboard; password sai trả 401. Operator create mới chưa có credential, login password trước activation bị từ chối. Không thử lặp tới lockout trong case happy-path tiếp theo.

### AUTH-03 — Đăng nhập bằng OTP — P1

1. **Sign In → Email / SMS code**; nhập email đã verified → **Send sign-in code**.
2. Nhập OTP mới nhất → **Sign In**; MFA thêm nếu tài khoản bật.

**Mong đợi:** mã đúng mới cấp session; mã sai/hết hạn không cấp session. Dùng email để tránh phụ thuộc SMS nếu chưa có provider.

### AUTH-04 — MFA không cấp session sau password đơn thuần — P1

**Tiền điều kiện:** user test đã bật MFA theo policy được chấp nhận.

1. Login password đúng, quan sát response và màn hình MFA.
2. Nhập TOTP đúng → hoàn tất login.
3. Chạy riêng bằng một recovery code chưa dùng; sau logout thử lại code đó.

**Mong đợi:** password-only response chỉ có MFA challenge, chưa có access/refresh token. TOTP hoặc recovery code hợp lệ hoàn tất login; recovery code đã dùng bị từ chối. Không đổi chính sách recovery trong lượt test này.

### AUTH-05 — Logout thu hồi session — P0

1. Login thành công; giữ cặp token để test bằng Postman ở môi trường test, không đưa vào ảnh.
2. Avatar/menu tài khoản → **Sign Out**.
3. F5, Back; gọi `/api/auth/me` bằng bearer cũ và `/api/auth/refresh` bằng refresh token cũ.

**Mong đợi:** UI về trạng thái logout; session cũ không dùng được, hai request cũ trả 401. Không coi đóng tab là logout.

### AUTH-06 — Refresh theo nhu cầu và giới hạn session — P1/API + test clock

1. Dùng test clock/fixture được dev cung cấp, để access hết hạn nhưng refresh còn hiệu lực; thực hiện thao tác protected trên FE.
2. Gửi hai protected requests đồng thời.
3. Chạy các mốc inactivity <24h, =24h; sau các refresh hợp lệ kiểm tra mốc tuyệt đối 7 ngày.

**Mong đợi:** FE chỉ refresh một lần cho requests cùng lúc, dùng bearer mới; token cũ không refresh lại. Refresh trước 24h inactivity có thể hợp lệ, đúng 24h không hợp lệ; rotation không kéo dài hạn 7 ngày. GET không cập nhật mốc inactivity. Không đổi đồng hồ Windows để test.

### AUTH-07 — Đổi login trong khi refresh/logout/request đang chờ — P1

1. Dùng proxy/test harness trì hoãn refresh hoặc response của login A.
2. Bắt đầu logout/mutation/read của A, sau đó login B hoặc clear session.
3. Thả response A về.

**Mong đợi:** không revoke/xóa/ghi đè B; mutation chưa gửi không chuyển sang dùng bearer B; response A không cập nhật UI B. Nếu không có công cụ trì hoãn, ghi BLOCKED và dẫn regression tự động, không suy từ click nhanh bằng tay.

## 5. Driver đăng ký

### DRIVER-01 — Đăng ký email + verify + login — P0

1. Logout → **Sign Up → Driver**.
2. Điền **Full Name**, **Email**, Password và Confirm Password; để Phone trống nếu chỉ test email.
3. **Create Driver Account**.
4. Tại **Verify your account**, nhập email OTP vào **Verification code → Verify Account**.
5. **Sign In → Password**, đăng nhập Driver mới.

**Mong đợi:** chỉ sau verify mới có quyền active/login; vào Driver dashboard, không cần Admin duyệt. Xác nhận identity qua `/api/auth/me` và reload.

### DRIVER-02 — Validation, contact trùng — P1

1. Thử form trống, email sai, cả email và phone trống, confirm password khác, password không đạt policy.
2. Thử email đã đăng ký active, gồm khác chữ hoa/thường.

**Mong đợi:** validation rõ ràng, không cấp session hoặc tạo identity trùng. Nếu UI phục hồi registration pending cũ, chỉ tiếp tục verify đúng registration; không biến user active trùng thành registration mới.

### DRIVER-03 — OTP sai, resend và trước verify — P1

1. Tạo registration mới, thử login trước verify.
2. Nhập OTP sai một lần, sau đó đợi countdown và bấm **Resend Code**.
3. Dùng OTP mới nhất → **Verify Account**.

**Mong đợi:** chưa verify không login được; OTP sai không activate; resend tuân cooldown và mã mới verify đúng. Không để email delivery lỗi bị hiểu là verify thành công.

## 6. Owner đăng ký và Admin duyệt

### OWNER-01 — Nộp hồ sơ và xác minh email — P0

1. Logout → **Sign Up → Business**.
2. Điền **Contact Person Name**, **Business Name**, **Business Email**, **Business Phone**, **Parking Lot Type**, password và confirm.
3. **Partner Terms & Conditions** → cuộn tới cuối → **I Accept**.
4. **Submit Partnership Application**.
5. **Verify email → Verification code → Verify** bằng OTP inbox.

**Mong đợi:** lưu hồ sơ đúng thông tin, email verified; Owner vẫn pending approval. Không cấp quyền Owner vận hành chỉ vì verify email thành công.

### OWNER-02 — Điều khoản, validation và email OTP lỗi — P1

1. Chưa đồng ý terms, thử submit; thử thiếu/sai email/phone/password confirmation.
2. Sau khi có registration, nhập OTP sai; dùng **Resend code** theo cooldown.

**Mong đợi:** dữ liệu sai không tạo hồ sơ hợp lệ; OTP sai không đánh verified. Thư chưa gửi/SMTP lỗi phải hiển thị lỗi hoặc trạng thái chờ phù hợp, không tự approve.

### APPROVAL-01 — Admin duyệt Owner verified — P0

1. Login ADMIN → sidebar **Applications**.
2. Trong **Pending Review** tìm đúng email/business OWNER_NEW.
3. Xác nhận **Contacts: Verified**; điền **Review Note (optional)**.
4. **Approve**, chờ request hoàn tất.
5. Kiểm tra hồ sơ ở **Previously Reviewed**; reload.
6. Login Owner ở cửa sổ khác.

**Mong đợi:** review state approved bền vững, Owner login được dashboard/onboarding; tenant/membership cần cho nghiệp vụ được tạo hợp lệ. Không có hồ sơ/tenant trùng khi reload hoặc request lặp.

### APPROVAL-02 — Chặn duyệt khi chưa verify và sai role — P0

1. Dùng hồ sơ chưa xác minh email: kiểm tra **Approve** disabled; gửi request review trực tiếp nếu cần.
2. Dùng Driver/Operator/Owner thử API `/api/owner-applications/{id}/review`.
3. Thử login Owner trước Admin approve sau khi email đã verified.

**Mong đợi:** không approve hồ sơ unverified; non-admin không review được; pending Owner không vào quyền vận hành.

### APPROVAL-03 — Reject và Parking dependency — P1

1. Dùng hồ sơ phụ, Admin **Reject** với note; kiểm tra sau reload và login Owner bị chặn.
2. Trên sandbox riêng, mô phỏng Parking unavailable trong khi approve hồ sơ khác.
3. Khôi phục dependency và retry theo response.

**Mong đợi:** reject bền vững; dependency lỗi không báo approve thành công giả hoặc cấp quyền một phần; retry không tạo tài nguyên trùng. Dùng tài khoản/phụ để không phá dữ liệu happy path.

## 7. Owner tạo parking site và structure

### SITE-01 — Onboarding lần đầu — P1/UI local

1. Owner mới login, nếu wizard xuất hiện chọn **Multi-Storey → Continue**.
2. **Lot Name**, **Address**, **Total Parking Slots = 12**, **Number of Floors / Levels = 1 → Continue**.
3. Hoàn thành bước thiết bị → **Continue**; giá và lựa chọn bắt buộc → **Continue to Review**.
4. **Activate My Parking Lot**.

**Mong đợi UI:** hoàn tất wizard và vào Owner dashboard. **Không dùng case này xác nhận site backend:** wizard hiện tạo local lot; làm SITE-02 để chuẩn bị Operator thật.

### SITE-02 — Tạo site backend, kiểm tra sau reload — P0

1. Owner → **Sites → Add New Site**.
2. Chọn **Parking Structure Type**, điền **Site Name**, **Province / City**, **District**, **Street Address / Complex**, số slot/tầng và các trường `*`.
3. **Create Parking Site**, ghi site ID từ response Network.
4. F5 → **Sites**; xác minh GET API trả đúng ID/thông tin.
5. Vào **Operators → Create Operator**, kiểm tra site xuất hiện trong **Site** dropdown.

**Mong đợi:** POST thành công thật; reload/GET cùng ID; không sinh ID chỉ tồn tại local. Source có fallback local khi POST lỗi, vì vậy toast đơn lẻ không đủ PASS.

### SITE-03 — Tạo site bị API từ chối — P0

1. Dùng sandbox, làm POST create site trả 403/validation error/network failure qua proxy hoặc user mất permission.
2. Bấm **Create Parking Site**.
3. Quan sát toast, list và reload/GET.

**Mong đợi nghiệm thu:** UI báo lỗi, không thêm site giả và không báo thành công. **Rủi ro source đã biết:** UI còn local fallback; nếu tái hiện ghi FAIL và dẫn bug, không đổi expected result để hợp thức hóa fallback.

### STRUCTURE-01 — Thêm floor/zone/slot trong editor — P0/UI local

1. Owner sidebar **Parking Structure** → dropdown **Site:** chọn lot đang test.
2. **Add Floor**, mã `F2`, tên `Floor Demo 2` → **Create Floor**.
3. Chọn tab tầng F2 → **+ Add Zone**, tên `Zone Demo A`, chọn **Car**, **Initial Parking Spaces to Generate = 6 → Create Zone**.
4. Kiểm tra sáu slot; bấm một slot; lọc **Zone:** và **Available**, bỏ bộ lọc; F5 quay lại editor.

**Mong đợi UI:** đủ floor/zone/slot và local persistence trong profile hiện tại. Ghi **PASS UI ONLY** nếu đạt; source hiện ghi qua structureStore local.

### STRUCTURE-02 — Persistence và identity chung giữa site/structure — P0/E2E

1. Dùng SITE-02 tạo site thật, ghi ID S.
2. Tạo floor/zone/slot bằng editor cho S.
3. GET structure API của S; dùng profile độc lập login Owner và mở cùng S.
4. Kiểm tra unit/slot IDs đúng server; chọn site này trong assignment Operator.

**Mong đợi đầy đủ:** layout bền vững ở server, cùng site/unit/slot IDs giữa các màn hình và profile, không tự sinh local ID thay thế. **Source hiện còn gap:** editor local chưa nối đầy đủ; nếu không thực hiện được ghi BLOCKED/FAIL tương ứng. Không lấy STRUCTURE-01 thay case này.

## 8. Owner xem 3D

### OWNER3D-01 — Owner mở viewer của site vừa tạo — P0/E2E

1. Login Owner, chọn site S đã có structure/model.
2. Tìm thao tác mở viewer 3D trong dashboard của Owner.
3. Khi có viewer: mở model của S, chọn tầng, thao tác camera, chọn một slot và đối chiếu mã slot/trạng thái.
4. Reload, mở lại đúng site/model.

**Mong đợi:** Owner xem được 3D đúng site, slot liên kết cùng ID backend và trạng thái hiện tại. **Source hiện tại:** không có đường mở viewer ở Owner dashboard đang mount; case BLOCKED vì thiếu chức năng. Việc Operator xem được 3D không đủ PASS case Owner.

### VIEW3D-02 — Viewer Operator và fallback asset — P1/UI

1. Login Operator có lot/model fixture phù hợp → **Lot Status & 3D Map → 3D Digital Twin**.
2. Chờ tải, thao tác camera, bấm slot; kiểm tra thông tin slot.
3. Với asset lỗi ở môi trường test, thử lại và kiểm tra thông báo/fallback grid.

**Mong đợi:** asset hợp lệ render đúng; lỗi asset được báo, UI vẫn dùng được fallback, không coi fallback là 3D đã PASS. Phân biệt lot local và site backend khi ghi kết quả.

## 9. Owner tạo Operator, activation và authorization

### OP-01 — Create không password, pending và response không lộ secret — P0

1. Owner A → **Operators → Create Operator**.
2. Xác nhận form **không có Password/Re-Password**.
3. Điền **Full Name**, **Email**, **Site = SITE_A → Create Operator**.
4. Quan sát POST `/api/users`; body chỉ có `fullName`, `email`, `siteIds`, `permissions`.
5. Kiểm tra response 201, status `pending_verification`; reload roster.

**Mong đợi:** Operator pending có đúng grants/site; delivery queued không đồng nghĩa email đã gửi. Response không có password/hash/activation token; Owner giữ session. DB/test adapter xác nhận pending credential chưa có password sử dụng được.

### OP-02 — Contract cũ và validation — P0/API + UI

1. Gửi POST `/api/users` có thêm `password` bằng Postman.
2. Thử thiếu name/email/site, email sai, site ID rỗng/trùng, permissions rỗng/trùng/ngoài danh sách.
3. Thử email Operator đã tồn tại; frontend nhận conflict rồi sửa email khác và submit lại.

**Mong đợi:** request cũ có password trả 400, không âm thầm bỏ qua; invalid fields không tạo user/grants/delivery/outbox. Email trùng trả 409; form giữ thông tin, hết busy và retry được; không submit trùng khi double click.

### OP-03 — Activation email, chọn password và login — P0

1. Operator pending từ OP-01: thử password login và OTP login trước activation.
2. Mở invitation email → **Activate your Operator account**.
3. Nhập **New password → Verify email and activate**.
4. **Sign in**, chọn Password, nhập email + password mới → **Sign In**.
5. Kiểm tra `/api/operators/me` và roster Owner.

**Mong đợi:** trước verify không có Operator session; sau proof hợp lệ email verified, user/accounts active, assignment chỉ đúng SITE_A. Password do Operator đặt hoạt động. Owner create không tạo credential cho bước trước activation.

### OP-04 — Proof sai/hết hạn/replay và password yếu — P0

1. Dùng invitation phụ, thử token sửa một ký tự, password yếu; sau đó link đúng và password hợp lệ.
2. Dùng lại link đã activate; trên test clock thử link quá 24 giờ.
3. Nếu có thể, gửi hai activation request đồng thời cùng proof.

**Mong đợi:** proof sai/hết hạn/đã consume bị từ chối; password yếu trả validation; không ghi state một phần. Concurrent activation chỉ một thành công. Activation 401 không xóa một session độc lập đang login ở browser đó.

### OP-05 — Resend thu hồi link cũ, cooldown — P0

1. Owner → dòng Operator pending → **Resend invitation**.
2. Gửi lại ngay trong một phút, kiểm tra thông báo/cooldown.
3. Sau khi reissue hợp lệ, thử link cũ rồi link mới nhất.

**Mong đợi:** cooldown bị chặn 429; link cũ không dùng được; chỉ link mới activate. Delivery cũ chưa gửi được hủy, không gửi password. Operator locked/verified không được biến lại pending chỉ bằng reissue.

### OP-06 — Tenant A không cấp quyền sang B — P0/API

1. Dùng OWNER_A với permission A allow, B explicit `[]`.
2. Create Operator với SITE_B hoặc trộn SITE_A + SITE_B.
3. Đọc owner scope và thử list/reissue/delivery của Operator ngoài scope.
4. Thu hồi permission A ở sandbox rồi thử lại thao tác với A.

**Mong đợi:** B bị từ chối; mixed create không tạo một phần A; scope chỉ trả tenant được phép; quyền thu hồi có hiệu lực ở request tiếp theo. `null` role defaults khác `[]` deny; BACKUP_MARK luôn cần grant explicit. Ghi mã response thực tế, không yêu cầu response tiết lộ dữ liệu foreign tenant.

### OP-07 — Assignment và site inactive — P0/API

1. Login Operator active có grant SITE_A.
2. Gọi `/api/operators/me`; slot-scope đúng SITE_A khi có SLOT_OVERRIDE.
3. Thử SITE_B/ungranted permission; làm SITE_A inactive trong sandbox và đọc lại.

**Mong đợi:** chỉ assignments hiện tại/site active được phép; role JWT đơn lẻ không mở site khác. UI fallback hiện bãi fixture không phải bằng chứng quyền backend đã đúng. Thêm/thu hồi assignment riêng chưa có API trong CQRS đợt 1.

### OP-08 — Roster live qua RabbitMQ/SignalR — P1

1. Bật RabbitMQ/messaging; để Owner mở **Operators**.
2. Ở profile khác, activate Operator pending trong scope; quan sát roster tự reload thành active.
3. Ngắt/kết nối lại network Owner, thay đổi roster khi disconnected rồi reconnect.
4. Test Owner không có tenant scope và session đã logout/revoked nếu có harness.

**Mong đợi:** live hint gây GET có authorization, reconnect reload; không lộ roster tenant khác; không trùng dòng khi POST create trả muộn; retry không restart connection Connected. Đây là realtime roster, không phải slot occupancy. Bản hiện tại hỗ trợ một API instance.

## 10. Operator check-in/check-out và slot

### GATE-01 — Check-in booking Confirmed — P0/UI local

**Tiền điều kiện:** GATE_LOCAL có booking Confirmed đúng lot/slot, ghi IDs và trạng thái trước test.

1. Login Operator trong profile có dữ liệu fixture; chọn đúng lot (qua **Dashboard Overview** nếu cần).
2. **Gate Check-In / Out**; kiểm tra bảng có booking Confirmed.
3. **Gate Terminal / Fast Lookup**, nhập biển số hoặc booking ID → **Search Gate**.
4. Kiểm tra xe/slot → **Confirm Gate Check-In → Confirm Check-In** trong dialog.
5. Xem booking → **Lot Status & 3D Map**, chọn cùng slot.

**Mong đợi UI:** booking `confirmed → checked-in`/Parked/In-Use, slot `occupied`, thông tin xe đúng. Chỉ PASS UI ONLY: source dùng timer/local store, chưa có backend gate.

### GATE-02 — Check-out booking đang đỗ — P0/UI local

1. Sau GATE-01, nhập lại cùng biển số → **Search Gate**.
2. **Confirm Gate Check-Out → Confirm Check-Out**.
3. Kiểm tra booking Completed; **Lot Status & 3D Map**, cùng slot Available.

**Mong đợi UI:** booking `checked-in → completed`, slot `occupied → available`. Case này không chứng minh thanh toán/thiết bị đã xác nhận xe rời bãi.

### GATE-03 — Booking sai trạng thái/không tìm thấy — P1/UI local

1. Search biển số không tồn tại, input trống, booking Pending Payment/Cancelled/Completed.
2. Search lại booking Completed sau GATE-02.

**Mong đợi:** không có gate action hợp lệ; báo lỗi; không thay trạng thái slot. Fast lookup chỉ nhận Confirmed/Checked-in. Không dùng booking payment pending để demo check-in thành công.

### GATE-04 — E2E cùng site/slot, persistence, concurrency — P0/E2E

1. Dùng SITE_A/slot S backend từ STRUCTURE-02, Operator được cấp đúng A, booking thật đã đủ điều kiện entry.
2. Thực hiện check-in, ghi request/response nghiệp vụ; reload và đọc từ profile/máy độc lập.
3. Lặp request check-in cùng operation ID; hai xe tranh cùng S trên sandbox.
4. Check-out/departure đúng workflow được chốt; lặp request và đọc lại từ server.

**Mong đợi đầy đủ:** session/slot authoritative cùng IDs, không nhận hai xe cùng slot, retry không tạo hai sessions, state bền vững và site authorization thực thi ở backend. Không giải phóng slot trước actual departure khi workflow phân biệt checkout tài chính và rời bãi. **Hiện chưa có gate backend tương ứng: BLOCKED**, chưa có API contract để tự đặt tên endpoint/status code. Không dùng local timer thay bước này.

### SLOT-01 — Slot đổi ngay trên màn hình Operator — P0/UI local

1. Ghi mã S, trạng thái trước GATE-01.
2. Sau check-in kiểm tra S Occupied, sau check-out kiểm tra S Available.
3. Kiểm tra cả nhãn/chi tiết và tổng đếm, không chỉ màu; không nhầm slot khác cùng số ở lot khác.

**Mong đợi:** local UI nhất quán với local booking. Nếu phải chuyển tab để đọc lại thì ghi rõ độ trễ/cách reload; không gọi là push realtime backend.

### SLOT-02 — Owner và Operator thấy cùng occupancy — P0/E2E

1. Owner mở slot S thuộc SITE_A; Operator mở cùng S ở profile độc lập, không chung localStorage.
2. Operator check-in → đo thời gian Owner thấy Occupied **không F5**.
3. Operator actual check-out/departure → Owner thấy Available; ngắt/kết nối lại viewer, reload và kiểm tra cùng state từ backend.

**Mong đợi:** hai role cùng authoritative slot ID và state, không leak site khác, reconnect bắt kịp dữ liệu. Ghi thời gian quan sát thực tế; mục tiêu độ trễ nghiệm thu cần team chốt, không tự đặt số giây. **Hiện chưa có occupancy integration; Owner viewer cũng còn thiếu: BLOCKED**. Chia sẻ localStorage không đủ PASS.

## 11. Payment — phần bổ sung

### PAY-01 — Booking, hold và chọn phương thức — P1/UI local

1. Login Driver, chọn lot fixture trong **Dashboard**/màn tìm bãi → **Book this lot**.
2. Chọn slot Available → **Continue with Slot [mã slot]**.
3. Chọn xe/nhập thông tin còn thiếu và thời gian → **Continue**.
4. Kiểm tra Payment Pending, giá/hold; chọn phương thức → **Continue**.
5. Nếu đến **QR payment**, quan sát thông báo gateway chưa kết nối; thử **Back to payment methods** và **Cancel hold** trên booking phụ.

**Mong đợi hiện tại:** không tuyên bố đã thu tiền hoặc tự confirm booking; hold/cancel UI đúng. Không chuyển khoản thật vào QR demo. Payment pending ở case này không đáp ứng GATE-01.

### PAY-02 — Payment provider thành công và settlement — P1/E2E

**Tiền điều kiện:** provider sandbox, payment backend, callback contract và booking integration đã được triển khai/cấu hình.

1. Tạo booking/amount từ server; thanh toán sandbox.
2. Trước callback hợp lệ, kiểm tra chưa chuyển sang paid chỉ do browser redirect.
3. Gửi callback hợp lệ, rồi replay; kiểm tra payment/booking và lịch sử từ profile khác.

**Mong đợi đầy đủ:** đúng amount/currency/booking; callback được xác thực; chỉ cập nhật một lần; không charge thật ngoài sandbox; trạng thái financial và occupancy theo workflow riêng. **Hiện chưa có provider/backend: BLOCKED**. UI “success” local hoặc POS notice không đủ PASS.

### PAY-03 — Fail/cancel/timeout và callback giả — P1/E2E

1. Khi PAY-02 có dependency, thử cancel, provider failure, timeout.
2. Callback sai signature, amount/booking khác, callback trùng hoặc đến muộn.

**Mong đợi:** không ghi paid sai; retry/reconciliation theo contract đã duyệt, không giải phóng slot đang có xe chỉ vì payment lỗi. **Hiện BLOCKED**, không tự đặt retry/refund policy khi contract chưa có.

## 12. Case xuyên suốt để nghiệm thu chuỗi

### JOURNEY-01 — Toàn hành trình bằng dữ liệu backend liên thông — P0

1. DRIVER-01 → OWNER-01 → APPROVAL-01.
2. SITE-02 → STRUCTURE-02 → OWNER3D-01.
3. OP-01 → OP-03 → OP-07.
4. Tạo booking đủ điều kiện thực tế; nếu payment là điều kiện, cần PAY-02.
5. GATE-04 → SLOT-02 → AUTH-05 cho các role.

**Mong đợi:** cùng site/unit/slot/booking IDs xuyên suốt, dữ liệu bền vững, đúng role/site permissions, state transitions nhất quán. Nếu một mắt xích chưa triển khai ghi BLOCKED và dẫn case, không nối dữ liệu fixture khác để đánh PASS E2E.

**Dự kiến theo source hiện tại:** chain BLOCKED tại structure persistence/Owner viewer/gate backend/occupancy; payment thêm blocker nếu dùng. Các case identity riêng vẫn chạy độc lập.

## 13. Phiếu thực thi và bằng chứng

Sao chép bảng này vào ticket/test run hoặc điền trực tiếp trong bản riêng của tester. Định nghĩa case nằm ở các mục trên; khởi tạo tất cả NOT RUN, chỉ đổi kết quả sau khi thực thi:

| Case ID | Build/snapshot | Data IDs | Kết quả | Actual result | Evidence/Bug ID | Tester/thời gian |
|---|---|---|---|---|---|---|
| AUTH-01 | | | NOT RUN | | | |
| AUTH-02 | | | NOT RUN | | | |
| AUTH-03 | | | NOT RUN | | | |
| AUTH-04 | | | NOT RUN | | | |
| AUTH-05 | | | NOT RUN | | | |
| AUTH-06 | | | NOT RUN | | | |
| AUTH-07 | | | NOT RUN | | | |
| DRIVER-01 | | | NOT RUN | | | |
| DRIVER-02 | | | NOT RUN | | | |
| DRIVER-03 | | | NOT RUN | | | |
| OWNER-01 | | | NOT RUN | | | |
| OWNER-02 | | | NOT RUN | | | |
| APPROVAL-01 | | | NOT RUN | | | |
| APPROVAL-02 | | | NOT RUN | | | |
| APPROVAL-03 | | | NOT RUN | | | |
| SITE-01 | | | NOT RUN | | | |
| SITE-02 | | | NOT RUN | | | |
| SITE-03 | | | NOT RUN | | | |
| STRUCTURE-01 | | | NOT RUN | | | |
| STRUCTURE-02 | | | NOT RUN | | | |
| OWNER3D-01 | | | NOT RUN | | | |
| VIEW3D-02 | | | NOT RUN | | | |
| OP-01 | | | NOT RUN | | | |
| OP-02 | | | NOT RUN | | | |
| OP-03 | | | NOT RUN | | | |
| OP-04 | | | NOT RUN | | | |
| OP-05 | | | NOT RUN | | | |
| OP-06 | | | NOT RUN | | | |
| OP-07 | | | NOT RUN | | | |
| OP-08 | | | NOT RUN | | | |
| GATE-01 | | | NOT RUN | | | |
| GATE-02 | | | NOT RUN | | | |
| GATE-03 | | | NOT RUN | | | |
| GATE-04 | | | NOT RUN | | | |
| SLOT-01 | | | NOT RUN | | | |
| SLOT-02 | | | NOT RUN | | | |
| PAY-01 | | | NOT RUN | | | |
| PAY-02 | | | NOT RUN | | | |
| PAY-03 | | | NOT RUN | | | |
| JOURNEY-01 | | | NOT RUN | | | |

**Evidence tối thiểu:** screen + bước vừa bấm + user role + resource IDs + Network status/response khi liên quan server + timestamp. Che password/access-refresh tokens/OTP/invitation proofs/recovery codes. Bằng chứng persistence dùng GET sau reload/profile độc lập; không cần copy thông tin nhạy cảm từ DB.

Mẫu bug: `[Case ID] [Role] thao tác — expected / actual`, môi trường/build, dữ liệu tiền điều kiện, bước tái hiện, HTTP status/error code, phạm vi ảnh hưởng và evidence.

## 14. Tài liệu liên quan và kiểm thử tự động đã có

- [Bản bàn giao thay đổi so với commit](../../05-project-management/handoffs/2026-10-15-changes-vs-6165024.md).
- [Quy trình bấm demo chi tiết](../demo-review-2026-10-15/QUY-TRINH-BAM-TEST-DEMO.md).
- [Operator API contract hiện tại](../../02-architecture/api/operator-provisioning.md).
- [Postman workflows](../postman/assigned-workflows-2026-10-08.postman_collection.json): chạy từng request và kiểm tra contract mới; không chạy Runner cả collection vì OTP/MFA/invitation cần tương tác. Ví dụ/status của phần cũ phải đối chiếu current API contract.

Lượt code verification trước khi soạn tài liệu đã đạt **99 backend tests (0 skipped), 9 frontend Operator tests, 46 frontend auth/live tests**, FE build thành công. Đây là regression evidence của dev, **không điền thay kết quả tester** và không chứng nhận các case E2E còn thiếu. Full frontend suite chưa được xác nhận pass; còn fixture lỗi cũ ngoài phạm vi.
