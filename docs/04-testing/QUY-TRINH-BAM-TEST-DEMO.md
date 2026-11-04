# Quy trình bấm test và demo SmartParking — 16/10/2026

Soạn ngày 15/10/2026 theo component đang được mount trong source hiện tại. Tên nút bên dưới giữ nguyên tiếng Anh trên UI. Đây là checklist để rehearsal; chưa phải biên bản đã chạy qua toàn bộ bằng trình duyệt.

## 0. Chốt phạm vi trước khi demo

| Luồng | Có thể kiểm tra gì ở bản hiện tại? |
|---|---|
| Login/logout | API thật; thử thêm refresh trang và truy cập sau logout |
| Driver/Owner đăng ký | API thật; cần email OTP thực tế |
| Admin duyệt Owner | API thật, cần User và Parking chạy cùng cấu hình internal key |
| Owner tạo site | Có API; UI còn fallback tạo local khi API lỗi, cần reload/Network để xác nhận |
| Owner tạo floor/zone/slot | Editor đang lưu local; demo UI, chưa nghiệm thu persistence backend |
| Owner xem 3D | **Chưa có đường mở viewer trong Owner dashboard đang mount**; không có nút để hướng dẫn bấm thành công |
| Owner tạo Operator/activation | API thật; cần site backend active và SMTP |
| Operator check-in/check-out | Demo UI/local store; chưa có Operations backend cho nghiệp vụ này |
| Slot đổi trạng thái | Có đổi local `occupied`/`available`; chưa chứng minh đồng bộ Owner ↔ Operator qua backend |
| Payment | Booking UI có bước chọn phương thức; chưa nối gateway, chưa thu tiền thật |

Không giới thiệu phần mô phỏng local là end-to-end đã hoàn thành. Nếu cần toàn bộ chuỗi backend thật, các hàng chưa hoàn thành ở trên là blocker cần sửa riêng.

## 1. Chuẩn bị trước buổi demo

- [ ] User/Parking/Reservation chạy ở 5035/5045/5055; FE ở `http://localhost:5173`.
- [ ] Đã áp migrations 05.13 và 05.14 trước khi chạy User API mới.
- [ ] SMTP gửi được vào inbox của bạn; kiểm tra cả Spam. Onboarding origin là `http://localhost:5173`, Development cho phép HTTP loopback.
- [ ] Có tài khoản Admin đang đăng nhập được. Email mặc định là `admin@smartpark.local`; password xem cấu hình riêng của bạn, không ghi vào tài liệu/chụp màn hình. Nếu Admin đã tồn tại thì password không tự đổi theo `.env`.
- [ ] Chuẩn bị ba địa chỉ email khác nhau, nhận được thư: Owner, Driver, Operator. Đừng dùng địa chỉ ví dụ không có inbox thật.
- [ ] Password demo đáp ứng 8–15 ký tự, có chữ hoa/thường/số/ký tự đặc biệt; dùng tài khoản test riêng.
- [ ] Nếu tài khoản bật MFA, có Authenticator hoặc recovery code; không bấm sai liên tục vì có lockout.
- [ ] Đã rehearsal mỗi luồng một lần. Đăng ký dùng email mới ở lần demo; không cố đăng ký lại email đã active.

Điền bảng dữ liệu trước khi bắt đầu:

| Dữ liệu | Giá trị chuẩn bị |
|---|---|
| Email Owner | … |
| Email Driver | … |
| Email Operator | … |
| Business Name | SmartPark Demo 1610 |
| Site backend | Demo Site 1610 |
| Local lot dùng demo gate/3D | … |
| Booking đang Confirmed, thuộc local lot trên | … |
| Biển số và slot của booking đó | … |

**Cách dùng cửa sổ:** identity thật có thể dùng Chrome cho Owner, Edge cho Admin, cửa sổ ẩn danh cho Operator. Với demo local booking/gate/slot, dùng **cùng browser profile và cùng origin**, logout/login lần lượt để giữ localStorage chung. Cửa sổ ẩn danh/browser khác không tự có cùng dữ liệu local. Hai tab thường chia sẻ localStorage; không dựa vào việc chúng có cùng session đăng nhập.

## 2. Login

1. Mở `http://localhost:5173`.
2. Bấm **Sign In** ở thanh điều hướng.
3. Tại **Sign-in method**, chọn **Password**. Mặc định có thể là **Email / SMS code**.
4. Nhập **Email or Phone Number** và **Password**.
5. Bấm **Sign In**.
6. Nếu xuất hiện bước MFA: nhập mã Authenticator hiện tại hoặc chọn dùng recovery code trên form rồi bấm **Sign In**.

**Mong đợi:** vào dashboard đúng role, không còn form login. Nhấn F5 vẫn nhận diện đúng tài khoản nếu session còn hợp lệ.

**Nhánh OTP nếu muốn trình bày:** chọn **Email / SMS code** → nhập email → **Send sign-in code** → lấy mã mới trong email → điền mã → **Sign In**. Nếu bật MFA vẫn cần bước xác minh MFA.

- [ ] PASS / [ ] FAIL — ghi role và lỗi nếu có: …

## 3. Logout

1. Ở dashboard, bấm avatar/menu tài khoản góc trên bên phải.
2. Bấm **Sign Out**.
3. Nhấn F5; thử quay lại màn hình dashboard bằng Back.

**Mong đợi:** không khôi phục tài khoản đã logout; yêu cầu đăng nhập khi truy cập phần bảo vệ. Nếu hiện lỗi logout, ghi FAIL, không coi việc đóng tab là logout thành công.

- [ ] PASS / [ ] FAIL: …

## 4. Driver đăng ký

1. Khi đang logout, bấm **Sign Up**.
2. Chọn tab **Driver**.
3. Điền **Full Name**, **Email** bằng inbox test. Có thể để Phone Number trống nếu demo qua email.
4. Điền **Password (optional; leave blank to use OTP sign-in)** và **Confirm Password**. Nên đặt password để demo login dễ hơn.
5. Bấm **Create Driver Account**.
6. Ở **Verify your account**, nhập 6 số trong email vào **Verification code**.
7. Bấm **Verify Account**. Nếu chưa nhận mã, đợi countdown rồi bấm **Resend Code**, dùng mã mới nhất.
8. Sau khi về Sign In, chọn Password và đăng nhập Driver vừa tạo.

**Mong đợi:** xác minh thành công, vào Driver dashboard. Driver không phải chờ Admin duyệt.

**Kiểm tra âm khi rehearsal:** mã sai phải báo lỗi; không cấp session trước xác minh. Chỉ thử một lần rồi dùng mã đúng.

- [ ] PASS / [ ] FAIL: …

## 5. Owner đăng ký

1. Logout → **Sign Up** → chọn tab **Business** (không có tab tên Owner).
2. Điền **Contact Person Name**, **Business Name**, **Business Email**, **Business Phone**.
3. Chọn **Parking Lot Type** phù hợp, ví dụ Multi-Storey.
4. Đặt password và confirm password để thử login sau khi duyệt.
5. Bấm **Partner Terms & Conditions**; cuộn nội dung tới cuối để nút đổi từ **Scroll to read all ↓** sang **I Accept**.
6. Bấm **I Accept**; kiểm tra ô đồng ý điều khoản đã được chọn.
7. Bấm **Submit Partnership Application**.
8. Ở **Verify email**, nhập mã email vào **Verification code** → bấm **Verify**. Nếu cần, dùng **Resend code**.

**Mong đợi:** chuyển sang màn hình chờ Admin approval. Xác minh email xong chưa đồng nghĩa Owner đã có quyền vận hành.

**Kiểm tra trước duyệt:** thử login tài khoản này ở cửa sổ khác; phải bị chặn vì pending approval.

- [ ] PASS / [ ] FAIL: …

## 6. Admin duyệt Owner

1. Mở cửa sổ dành cho Admin → **Sign In** → chọn **Password** → đăng nhập Admin.
2. Trong sidebar bấm **Applications**.
3. Tại **Pending Review**, tìm đúng **Business Name/email Owner** vừa đăng ký.
4. Kiểm tra dòng **Contacts: Verified**. Nếu **Awaiting verification**, quay lại hoàn thành OTP Owner; nút Approve bị disabled là đúng.
5. Có thể nhập **Review Note (optional)**, ví dụ `Approved for demo`.
6. Bấm **Approve** một lần, chờ kết quả. Source hiện gọi review trực tiếp từ nút, không có bước confirm thứ hai bắt buộc.
7. Kiểm tra hồ sơ ra khỏi Pending Review và có trạng thái duyệt trong **Previously Reviewed**.
8. Quay lại cửa sổ Owner → đăng nhập lại.

**Mong đợi:** Owner truy cập được dashboard hoặc onboarding wizard. Nếu Approve lỗi, kiểm tra Parking API và internal service key; không tự sửa status trong DB để đánh dấu pass.

- [ ] PASS / [ ] FAIL: …

## 7. Owner setup lần đầu và tạo site

### 7A. Nếu login lần đầu mở onboarding wizard

1. Chọn loại bãi, ví dụ **Multi-Storey** → **Continue**.
2. Điền **Lot Name**, **Address**, **Total Parking Slots** = 12, **Number of Floors / Levels** = 1 → **Continue**.
3. Chọn thiết bị nếu cần → **Continue**.
4. Điền các trường pricing đang hiển thị; hoàn thành lựa chọn/đồng ý bắt buộc của bước này → **Continue to Review**.
5. Kiểm tra thông tin → **Activate My Parking Lot**.

**Giới hạn quan trọng:** wizard hiện tạo lot trong local store và đánh dấu onboarding local. Đây chưa phải tạo site backend cho Operator assignment. Vẫn phải làm bước 7B.

### 7B. Tạo site dùng cho Operator thật

1. Sidebar Owner → **Sites**.
2. Bấm **Add New Site**.
3. Chọn **Parking Structure Type**, ví dụ **Multi-Storey**.
4. Điền **Site Name** = `Demo Site 1610`.
5. Chọn **Province / City**, **District**, nhập **Street Address / Complex**.
6. Điền số slot/tầng và giá theo form; ví dụ 12 slot, 1 tầng. Hoàn thành các trường có dấu `*`.
7. Bấm **Create Parking Site**.
8. Nhấn F5 → vào lại **Sites**, kiểm tra site vẫn xuất hiện. Tiếp tục vào **Operators** xem site có trong danh sách chọn.

**Điều kiện PASS backend:** POST tạo site thành công và GET sau reload trả lại đúng site. Khi rehearsal, mở F12 → Network để kiểm tra request. UI hiện có fallback local khi POST lỗi, vì vậy toast thành công đơn lẻ không đủ chứng minh.

- [ ] PASS backend / [ ] Chỉ thấy local / [ ] FAIL: …

## 8. Owner tạo parking structure — demo editor local

1. Sidebar → **Parking Structure**.
2. Ở dropdown **Site:**, chọn lot phù hợp đang có trong editor. Ghi lại tên; không mặc định rằng đây là cùng ID với site backend ở 7B.
3. Bấm **Add Floor**.
4. Điền mã tầng `F2`, tên `Floor Demo 2`, giữ trạng thái active → **Create Floor**.
5. Bấm tab tầng vừa tạo.
6. Bấm **+ Add Zone**.
7. Nhập tên zone `Zone Demo A`, chọn **Car**; tại **Initial Parking Spaces to Generate**, nhập `6` → **Create Zone**.
8. Kiểm tra zone mới có sáu slot; bấm một slot để mở thông tin.
9. Thử lọc **Zone:** và trạng thái **Available**; sau đó bỏ bộ lọc.
10. Nhấn F5, vào lại editor cùng browser profile để kiểm tra dữ liệu local còn tồn tại.

**Mong đợi UI:** floor/zone/slot được tạo và hiển thị. Không đánh dấu PASS persistence backend hoặc đồng bộ thiết bị/Operator từ các bước này.

- [ ] PASS UI local / [ ] FAIL: …

## 9. Owner xem 3D model — đang bị chặn

**Không có nút mở 3D viewer trong Owner dashboard hiện tại.** Không đặt bước “Owner bấm View 3D” trong bài demo như một tính năng đã hoàn thành. Chọn model ở onboarding cũng không chứng minh đã có màn hình Owner viewer.

Nếu muốn giới thiệu renderer 3D đã có, thực hiện mục 12 bằng **Operator**, nói rõ role đang dùng. Không đổi tên demo đó thành Owner view 3D.

- [ ] BLOCKED — cần nối viewer vào Owner dashboard nếu bắt buộc tiêu chí này.

## 10. Owner tạo Operator và activation

1. Owner → sidebar **Operators** → **Create Operator**.
2. Điền **Full Name**, **Email** bằng inbox Operator test. Form tạo Operator không còn ô password; Operator tự đặt password khi activate.
3. Dropdown **Site** → chọn site backend active ở 7B. Nếu hiện “No active sites…”, quay lại Sites; không chọn lot local để thay thế.
4. Bấm **Create Operator** trong modal.
5. Kiểm tra Operator ở trạng thái pending và thông báo invitation queued.
6. Ở cửa sổ Operator, thử login bằng một password test bất kỳ → phải bị từ chối vì chưa có credential trước activation. Không có password do Owner cấp.
7. Mở email invitation → bấm link → thấy **Activate your Operator account**.
8. Điền **New password** riêng của Operator → **Verify email and activate**.
9. Thấy `Email verified and account activated...` → bấm **Sign in**, vào form Sign In nếu đang ở landing, chọn Password → đăng nhập bằng password mới.
10. Quay lại Owner: nếu RabbitMQ bật, roster tự reload; nếu chưa bật, F5 để đọc lại trạng thái active.

**Resend khi rehearsal:** ở dòng Operator pending, bấm **Resend invitation** sau cooldown một phút. Dùng link mới nhất; link cũ phải bị từ chối. Không cần làm nhánh resend trong demo chính nếu email đầu tiên đến bình thường.

**Mong đợi:** chưa verify thì không login được; sau verify thì login đúng role Operator. Email không chứa password Owner nhập. Link activation dùng lại bị từ chối.

- [ ] PASS / [ ] FAIL: …

## 11. Operator check-in — demo local có điều kiện

**Chuẩn bị bắt buộc:** có booking `Confirmed` trong đúng local lot đang chọn, biết biển số và slot. Booking pending payment không đủ điều kiện. Operator mới tạo trên backend có thể chưa có local lot/booking tương ứng; nếu không thấy đúng dữ liệu thì ghi BLOCKED, không xem fallback sang bãi khác là phân quyền đã pass.

1. Đăng nhập Operator trong browser profile có dữ liệu demo local.
2. Sidebar → **Gate Check-In / Out**.
3. Kiểm tra tên bãi đang hiển thị đúng bãi rehearsal; nếu cần quay **Dashboard Overview** để chọn bãi rồi trở lại.
4. Trong bảng booking, chọn một dòng **Confirmed**, ghi biển số và slot.
5. Tại **Gate Terminal / Fast Lookup**, nhập chính biển số hoặc booking ID đó → **Search Gate**.
6. Kiểm tra kết quả tìm kiếm đúng xe/slot → **Confirm Gate Check-In**.
7. Trong dialog **Confirm Gate Check-In**, bấm **Confirm Check-In**.
8. Chờ thông báo thành công; booking đổi thành **Parked / In-Use**.
9. Chuyển sang **Lot Status & 3D Map** → kiểm tra đúng slot thành **Occupied**.

**Mong đợi UI local:** booking `confirmed → checked-in`, slot `occupied`. Nếu bảng không có booking Confirmed, dừng bước này và chuẩn bị dữ liệu trước demo; không có walk-in backend trong luồng này.

- [ ] PASS UI local / [ ] BLOCKED thiếu dữ liệu / [ ] FAIL: …

## 12. Xem model 3D bằng Operator

1. Sidebar → **Lot Status & 3D Map**.
2. Bấm **3D Digital Twin** nếu chưa được chọn.
3. Chờ model tải. Thử thao tác camera và bấm đúng slot đã check-in để xem thông tin/highlight.
4. Kiểm tra nhãn/trạng thái Occupied thay vì chỉ dựa vào màu.
5. Nếu hiện `3D model asset could not be initialized...`, ghi lỗi model; chỉ tiếp tục demo grid fallback và nói rõ 3D chưa tải thành công.

**Mong đợi:** viewer hiển thị lot local đang chọn. Không chứng minh model Owner mới tạo ở mục 8 đã đồng bộ sang đây.

- [ ] PASS 3D UI / [ ] Chỉ chạy grid fallback / [ ] FAIL: …

## 13. Operator check-out và slot trở về trống

1. Trở về **Gate Check-In / Out**.
2. Nhập lại biển số ở mục 11 → **Search Gate**.
3. Booking đang checked-in phải cho thao tác **Confirm Gate Check-Out**.
4. Bấm **Confirm Gate Check-Out** → trong dialog bấm **Confirm Check-Out**.
5. Kiểm tra booking thành **Completed**.
6. Vào **Lot Status & 3D Map**, kiểm tra đúng slot trở về **Available**.
7. Thử tìm lại booking Completed: không được tiếp tục check-out như một booking active.

**Mong đợi UI local:** booking `checked-in → completed`, slot `occupied → available`. Luồng hiện tại chưa có backend gate/admission và chưa liên kết settlement payment; không trình bày đây là xác nhận đã thu tiền hay xe rời bãi do thiết bị thật ghi nhận.

- [ ] PASS UI local / [ ] FAIL: …

## 14. Kiểm tra yêu cầu “slot thay đổi ngay”

| Điểm cần quan sát | Kỳ vọng để nghiệm thu đầy đủ | Bản hiện tại |
|---|---|---|
| Sau check-in trong UI Operator | Slot tương ứng Occupied | Có thao tác cập nhật local |
| Sau check-out trong UI Operator | Slot tương ứng Available | Có thao tác cập nhật local |
| Owner mở cùng slot ở browser khác | Tự cập nhật từ backend | Chưa hoàn thành/chưa được chứng minh |
| Reload hoặc mở máy khác | Trạng thái từ server vẫn đúng | Không được suy từ localStorage |

SignalR vừa thêm ở R05/R06 phục vụ danh sách Operator; không dùng nó làm bằng chứng slot occupancy đã realtime.

## 15. Payment flow — chỉ demo tới giới hạn đang có

1. Login Driver trong browser profile có lot local để booking.
2. Vào **Dashboard**, mở màn hình tìm bãi nếu đang ở Current booking; chọn bãi trên danh sách/bản đồ → **Book this lot**.
3. Chọn slot Available → **Continue with Slot [mã slot]**.
4. Chọn xe đã đăng ký; nếu chưa có xe, thêm xe bằng form mà màn hình yêu cầu. Nhập thời gian và hoàn thành các trường bắt buộc → **Continue**.
5. Kiểm tra booking hiển thị **Payment Pending**, số tiền và thời hạn giữ chỗ.
6. Chọn phương thức thanh toán → **Continue**.
7. Nếu đến màn hình **QR payment**, đọc thông báo `No gateway is connected yet, so this booking has not been charged.` Đây là điểm dừng đúng của bản hiện tại.
8. Có thể bấm **Back to payment methods** hoặc **Cancel hold**.

**Không có bước thanh toán gateway thật thành công để demo hiện tại.** Không quét/chuyển tiền thật, không đánh dấu booking confirmed chỉ vì đã qua màn hình chọn phương thức. Nếu có Payment History/Retry hoặc POS hiển thị success trong local UI, không dùng làm bằng chứng provider callback/settlement thật.

Booking pending payment tạo ở mục này **không thể dùng ngay cho mục 11**, vì fast lookup chỉ nhận confirmed/checked-in. Cần booking fixture Confirmed đã rehearsal riêng cho demo gate.

- [ ] PASS màn hình chọn/giữ chỗ / [ ] BLOCKED thanh toán thật / [ ] FAIL UI: …

## 16. Thứ tự trình bày đề nghị

1. Login/logout bằng tài khoản chuẩn bị sẵn.
2. Driver đăng ký + OTP + login.
3. Owner đăng ký + OTP → Admin Approve → Owner login.
4. Owner onboarding nếu xuất hiện → Sites tạo site backend → Parking Structure demo editor local.
5. Owner tạo Operator → pending login bị chặn → activation → login thành công.
6. Chuyển sang dữ liệu fixture đã rehearsal để giới thiệu Operator 3D, check-in, check-out và đổi slot local.
7. Payment để cuối: giới thiệu booking/pending/method selection và nêu rõ gateway chưa tích hợp.

Khi được hỏi Owner 3D: báo đúng là chưa có đường mở viewer trong Owner dashboard; viewer hiện demo ở Operator. Nếu yêu cầu toàn bộ chuỗi backend thật là tiêu chí chấm, cần báo các blocker này với leader trước buổi demo.

## 17. Ghi nhận kết quả rehearsal

| Mục | PASS / FAIL / BLOCKED / UI local | Ghi chú hoặc ảnh |
|---|---|---|
| Login/logout | | |
| Driver registration + OTP | | |
| Owner registration + OTP | | |
| Admin approval | | |
| Site backend tồn tại sau reload | | |
| Floor/zone/slot editor | | |
| Owner 3D | BLOCKED ở source hiện tại | |
| Operator create + activation | | |
| Operator 3D | | |
| Check-in + Occupied | | |
| Check-out + Available | | |
| Occupancy đồng bộ Owner/browser khác | Chưa hoàn thành | |
| Payment provider thật | BLOCKED | |

Khi lỗi: chụp màn hình có thông báo, ghi role/nút vừa bấm và thời điểm; với API dùng Network ghi status + response đã che token/password. Không đưa OTP, invitation token hoặc recovery code vào ảnh chia sẻ.

## Nguồn đối chiếu tên nút và giới hạn

- `FE/src/App.tsx`, các Dashboard Owner/Admin/Operator/Driver: component đang mount và sidebar.
- `FE/src/roles/authentications/Sign In/SignIn.tsx`, `Sign Up/SignUp.tsx`, `DriverOtpForm.tsx`, `OwnerVerification.tsx`, `BootstrapPassword.tsx`.
- `FE/src/roles/admin/applications/Applications.tsx`.
- `FE/src/roles/owner/parking-lots/CreateParkingLotForOwner.tsx`, `SiteManagement.tsx`, `SiteForm.tsx`.
- `FE/src/roles/owner/structure/ParkingStructure.tsx`, `FloorModal.tsx`, `ZoneModal.tsx`.
- `FE/src/roles/owner/operators/OperatorManagement.tsx`, `CreateOperatorForm.tsx`.
- `FE/src/roles/operator/check-in-out/CheckInOut.tsx`, `parking-lot/LotStatus.tsx`.
- `FE/src/roles/driver/parking/FindParking.tsx`, `booking/BookingFlow.tsx`.
