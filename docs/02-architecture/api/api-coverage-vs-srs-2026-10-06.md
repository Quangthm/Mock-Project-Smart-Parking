# Những chức năng còn thiếu sau khi sửa Login/Logout

> Historical contract/review (06/10/2026). See [current workflow contract and mapping, 08/10/2026](workflows-2026-10-08.md) for the implemented changes.

Ngày xem xét: 06/10/2026. Đối chiếu code hiện tại với báo cáo PDF và SRS v0.9.

## 1. Hiện tại đã làm được gì?

Phần đăng nhập và đăng xuất đã được cải thiện. Backend, tức phần xử lý và lưu dữ liệu trên máy chủ, hiện có 4 API:

| Chức năng | Ý nghĩa |
| --- | --- |
| Đăng nhập | Kiểm tra email và mật khẩu, tạo phiên đăng nhập |
| Lấy thông tin người đang đăng nhập | Cho biết đang đăng nhập bằng tài khoản nào và vai trò gì |
| Đăng xuất | Kết thúc phiên đăng nhập |
| Gia hạn phiên | Có API hỗ trợ gia hạn; giao diện chưa tự động sử dụng |

**Như vậy, phần đăng nhập đã tốt hơn, nhưng phần tạo và quản lý tài khoản còn thiếu.**

Báo cáo PDF chỉ kiểm tra Login/Logout ở phiên bản code cũ. Sửa hết các lỗi trong báo cáo đó chưa đồng nghĩa đã làm đủ chức năng trong SRS.

## 2. Vấn đề dễ thấy nhất: tạo tài khoản trên giao diện chưa tạo tài khoản trên máy chủ

Hiện tại, màn hình đăng ký chỉ lưu tài khoản trong trình duyệt đang dùng. Cách lưu này gọi là localStorage.

Trong khi đó, màn hình đăng nhập đã kiểm tra tài khoản trong database trên máy chủ.

Ví dụ:

1. Bạn vào Sign Up và tạo tài khoản mới.
2. Giao diện báo đã tạo và chuyển sang Sign In.
3. Bạn dùng tài khoản đó để đăng nhập.
4. Nếu tài khoản chưa được tạo riêng trong database, máy chủ không tìm thấy và đăng nhập thất bại.

**Hai màn hình này chưa nối thành một quy trình hoàn chỉnh.** Tình trạng tương tự xảy ra với duyệt Owner, khóa tài khoản và tạo Operator: giao diện có thao tác, nhưng thay đổi chưa được lưu lên máy chủ.

## 3. Theo SRS, còn thiếu những gì?

Driver là người dùng đi gửi xe; Owner là chủ bãi; Operator là nhân viên bãi; Admin là quản trị hệ thống.

| Ai sử dụng? | Chức năng cần có | Hiện tại |
| --- | --- | --- |
| Driver | Đăng ký tài khoản thật để có thể đăng nhập | Chưa có API; giao diện chỉ lưu trong trình duyệt |
| Driver | Nhận và xác minh mã OTP khi đăng ký | Chưa có xử lý trên máy chủ |
| Driver | Sửa tên, ảnh đại diện; đổi email, số điện thoại, mật khẩu | Chưa có các API xử lý tương ứng |
| Driver | Thêm, xem và sửa xe của mình | Chưa có API quản lý xe |
| Owner | Gửi đăng ký với tên công ty, email và số điện thoại | Giao diện có, máy chủ chưa xử lý |
| Admin | Xem và duyệt đăng ký Owner | Giao diện có, chưa có API duyệt thật |
| Admin | Xem, tìm kiếm, lọc tài khoản theo vai trò | Chưa có API |
| Admin | Khóa, mở khóa tài khoản và xem lịch sử hoạt động | Giao diện đang thao tác trên dữ liệu trong trình duyệt |
| Owner | Tạo tài khoản Operator và giao bãi làm việc | Giao diện có, chưa có API tạo và giao quyền thật |
| Hệ thống | Kiểm tra mỗi người được phép làm gì, tại bãi nào | Đã có kiểm tra cơ bản; chưa đủ cho các chức năng trên |

SRS còn yêu cầu đăng nhập bằng OTP và hỗ trợ xác thực hai bước bằng ứng dụng tạo mã. Các phần này chưa có. Chúng cần được đưa vào kế hoạch triển khai cùng phần tài khoản.

OTP đăng ký theo SRS có hạn 5 phút; nhập sai 3 lần thì khóa tạm 15 phút. Việc đã làm khóa khi nhập sai **mật khẩu** chưa thay thế được yêu cầu này.

## 4. “Admin check user theo role” nên hiểu thế nào?

Có hai việc khác nhau.

**Việc thứ nhất: Admin xem tài khoản theo từng loại.**

Ví dụ, Admin chọn “Driver” thì thấy danh sách Driver; chọn “Owner” thì thấy danh sách Owner. Có thể tìm theo tên/email hoặc lọc tài khoản đang bị khóa. Phần này cần API lấy danh sách tài khoản từ database.

**Việc thứ hai: hệ thống chặn người không có quyền.**

Ví dụ:

- Driver không được khóa tài khoản của người khác.
- Owner chỉ được quản lý nhân viên và bãi của mình.
- Operator chỉ được thao tác ở bãi được giao.
- Owner chưa được Admin duyệt thì chưa được sử dụng chức năng quản lý bãi.

Máy chủ phải kiểm tra các điều kiện đó mỗi lần thực hiện thao tác. Chỉ ẩn nút hoặc menu trên giao diện chưa đủ.

Hiện tại, code có kiểm tra phiên đăng nhập, trạng thái tài khoản và vai trò hiện tại. Tuy nhiên, chưa có đầy đủ phần quản lý quyền và kiểm tra bãi được phép thao tác.

## 5. Tạo tài khoản cần chia thành những luồng nào?

Không nên coi tất cả tài khoản đều đăng ký giống nhau. Theo SRS, có ba luồng chính:

| Loại tài khoản | Quy trình cần hoàn thiện |
| --- | --- |
| Driver | Tự đăng ký → xác minh OTP → sử dụng tài khoản |
| Owner | Gửi đăng ký doanh nghiệp → Admin duyệt → được quản lý bãi |
| Operator | Owner tạo tài khoản → giao bãi và quyền → nhân viên làm việc trong phạm vi được giao |

Không có luồng đăng ký công khai để người dùng tự chọn làm Admin. Người dùng cũng không được tự sửa vai trò hoặc trạng thái tài khoản của mình.

Ví dụ với Owner: nút “Approve” cần thay đổi trạng thái trong database. Chỉ đổi một giá trị trong trình duyệt của Admin chưa làm tài khoản Owner được duyệt thật.

## 6. Nên làm phần nào trước?

Tôi đề xuất chia thành các đợt nhỏ, mỗi đợt làm xong một quy trình có thể sử dụng được.

1. **Đăng ký Driver và OTP.** Tạo tài khoản trong database, xác minh, rồi đăng nhập được. Đồng thời kiểm tra quyền ở máy chủ cho những thao tác mới.
2. **Admin quản lý tài khoản và duyệt Owner.** Xem danh sách, tìm kiếm/lọc theo vai trò, khóa/mở khóa và duyệt đăng ký Owner. Mọi thay đổi phải lưu trên máy chủ và có lịch sử.
3. **Owner tạo và quản lý Operator.** Tạo nhân viên, giao bãi làm việc, cấp và thu hồi quyền.
4. **Thông tin cá nhân, mật khẩu và xe.** Người dùng sửa dữ liệu của mình; đổi email/số điện thoại cần xác minh lại.
5. **Các nghiệp vụ đỗ xe.** Tiếp tục phần tìm bãi, đặt chỗ, vào/ra bãi, thanh toán, hoàn tiền và vé tháng.

Dấu hiệu một đợt đã hoàn thành là: dữ liệu lưu trên máy chủ, dùng được từ trình duyệt khác, người không có quyền bị chặn và giao diện hiển thị đúng kết quả.

Ví dụ, sau đợt 1, tài khoản đăng ký trên máy A phải có thể đăng nhập trên máy B. Sau đợt 2, Admin khóa tài khoản thì tài khoản đó phải bị chặn bởi máy chủ, kể cả người dùng vẫn đang mở giao diện.

## 7. Ngoài tài khoản, hệ thống còn thiếu gì?

Trong code backend đang xem, tôi chưa thấy API nghiệp vụ cho các nhóm sau:

- Tạo và quản lý bãi, xem tình trạng chỗ trống, tìm bãi.
- Đặt/hủy chỗ và xem lịch sử đặt chỗ.
- Ghi nhận xe vào/ra bãi.
- Tính phí, thanh toán, hóa đơn và hoàn tiền.
- Quản lý vé tháng.
- Ghi nhận sự cố, khiếu nại, thông báo và báo cáo.
- Nhận diện biển số và chatbot kết nối với dữ liệu thật.

Danh sách này phản ánh code trong thư mục hiện tại. Có thể thành viên khác đang làm ở nhánh hoặc repository khác.

Một số nội dung trong SRS vẫn đang đề xuất hoặc để giai đoạn sau, như tích hợp thiết bị thật và một số tính năng AI nâng cao. Không nên đưa tất cả vào danh sách bắt buộc phải làm ngay.

## 8. Những điểm cần thống nhất khi triển khai

- Thời hạn đăng nhập: code dùng access token 1 giờ theo báo cáo, còn SRS ghi phiên 24 giờ. Cần làm rõ hai khái niệm và thống nhất tài liệu.
- Driver được đăng ký bằng email hoặc số điện thoại theo SRS; cấu trúc database hiện tại vẫn bắt buộc có số điện thoại. Cần xử lý trước khi cho đăng ký chỉ bằng email.
- Chi tiết từ chối đăng ký Owner, cấp mật khẩu cho Operator và đổi vai trò cần được thống nhất khi thiết kế API.

Các điểm này không thay đổi kết luận rằng chức năng đang thiếu; chúng giúp tránh phải sửa lại sau khi triển khai.

## Nguồn đối chiếu

- Báo cáo `API Design vs Code Comparison Report.pdf`: phạm vi Login/Logout, kiểm tra commit cũ 2816581.
- [SRS v0.9](../../01-requirements/srs/smartpark-srs-v0.9.md): phần tài khoản §3.1, quản trị §3.7.4–5 và bảng quyền §2.3.1.
- [Use Cases v0.9](../../01-requirements/use-cases/smartpark-use-cases-v0.9.md): ba quy trình tài khoản UC-AUTH-01/02/03.
- [Báo cáo sửa Login/Logout trước](auth-design-alignment-2026-10-06.md).
- Code chính: AuthController, SignUp, DriverAccounts, Applications, CreateOperatorForm và store.ts.

Đây là kết quả đọc code và tài liệu; chưa chạy lại kiểm thử trong lần nghiên cứu này. Các API mới cần được thiết kế cụ thể trước khi triển khai. Bản báo cáo này chỉ được viết lại cho dễ đọc, chưa thay đổi code nghiệp vụ.
