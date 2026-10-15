# 2FA — Nội dung gửi leader duyệt

Cập nhật: 15/10/2026 · Nhánh: `Fix2FA`

2FA là bước nhập thêm mã từ Google/Microsoft Authenticator khi đăng nhập. Mã dự phòng dùng thay mã từ ứng dụng khi người dùng không lấy được mã.

## 1. Đã làm

- **Bật 2FA:** nhập lại mật khẩu → quét QR bằng ứng dụng Authenticator → nhập mã 6 số → bật thành công và nhận 8 mã dự phòng để lưu.
- **Đăng nhập:** nhập đúng mật khẩu hoặc mã OTP email/SMS → nếu đã bật 2FA thì nhập thêm mã từ Authenticator hoặc một mã dự phòng → đăng nhập thành công.
- **Mã dự phòng:** mỗi mã chỉ dùng một lần. Dùng xong, hệ thống báo còn bao nhiêu mã.
- **Cài đặt:** đã có tắt 2FA, thay Authenticator và tạo lại bộ mã dự phòng. Các thao tác này yêu cầu xác minh lại.
- **Thay Authenticator:** ứng dụng cũ vẫn dùng được cho đến khi xác minh ứng dụng mới thành công.
- **Tắt/thay Authenticator:** các lần đăng nhập cũ bị hủy, người dùng phải đăng nhập lại.

Phần này đã được kiểm thử và review chéo. Phần khôi phục bên dưới chưa được làm.

## 2. Đề xuất làm thêm

### A. Người dùng vừa dùng mã dự phòng cuối cùng

**Hiện tại:** vẫn đăng nhập được, nhưng nếu mất Authenticator thì không còn mã để xác minh việc tạo lại.

**Đề xuất:** sau khi dùng mã cuối cùng, hệ thống yêu cầu hoàn tất một trong hai cách trước khi vào trang chính:

| Tình trạng | Người dùng cần làm |
|---|---|
| Vẫn dùng được Authenticator | Nhập mã từ ứng dụng → nhận bộ 8 mã dự phòng mới. |
| Đã mất Authenticator | Thêm Authenticator mới → nhập mã từ ứng dụng mới → nhận bộ 8 mã dự phòng mới. |

Người dùng không phải nhập thêm một mã dự phòng khác. Hệ thống ghi nhận mã cuối cùng vừa xác minh để cho phép hoàn tất bước này trong 10 phút (thời gian đề xuất).

Nếu dùng mã dự phòng mà vẫn còn mã khác, người dùng được vào trang chính và được nhắc xử lý sớm. Nếu vẫn lấy được mã từ Authenticator, họ vẫn có thể đăng nhập bằng mã đó dù đã hết mã dự phòng.

### B. Người dùng mất cả Authenticator và toàn bộ mã dự phòng

1. Chọn **Không thể lấy mã xác thực**.
2. Xác minh email/số điện thoại đã đăng ký và gửi yêu cầu khôi phục.
3. Gửi tài liệu chứng minh tài khoản thuộc về mình. **Không bắt buộc CCCD.**
4. Admin đối chiếu tài liệu với thông tin hệ thống, rồi duyệt, từ chối hoặc yêu cầu bổ sung.
5. Nếu được duyệt, người dùng được phép thiết lập Authenticator mới.
6. Xác minh ứng dụng mới thành công → nhận 8 mã dự phòng mới → đăng nhập lại.

Admin chỉ duyệt cho khôi phục, không biết mã bí mật hoặc mã dự phòng của người dùng. Chỉ admin được phân quyền mới được duyệt; không được tự duyệt tài khoản của mình. **Admin duyệt xong chưa có nghĩa người dùng đã khôi phục xong.**

Nếu không truy cập được email/số điện thoại cũ, người dùng vẫn có thể gửi yêu cầu hỗ trợ, nhưng không tự được cấp quyền khôi phục.

Hồ sơ chỉ cho người gửi và admin được phân quyền xem. Đề xuất nhận tối đa 5 file ảnh/PDF, 5 MB/file và xóa file 30 ngày sau khi hồ sơ kết thúc. Các giới hạn này cần leader chốt.

### C. Admin xét hồ sơ như thế nào? — đề xuất chưa triển khai

1. **Nhận hồ sơ:** admin mở mục **Yêu cầu khôi phục 2FA**, chọn hồ sơ và bấm **Nhận xử lý**. Hệ thống ghi nhận admin phụ trách.
2. **Kiểm tra:** xem tài khoản cần khôi phục, lý do, kết quả xác minh email/số điện thoại và tài liệu người dùng gửi. Đối chiếu với dữ liệu có sẵn, ví dụ mã đặt chỗ/giao dịch, thông tin doanh nghiệp/bãi đỗ hoặc thông tin cấp tài khoản nhân viên. Không duyệt chỉ vì giấy tờ đúng tên.
3. **Thiếu thông tin:** bấm **Yêu cầu bổ sung**, ghi rõ cần bổ sung gì. Người dùng nhận thông báo, bổ sung vào cùng hồ sơ; admin kiểm tra lại. Trong lúc chờ, người dùng chưa được vào tài khoản.
4. **Không đủ bằng chứng hoặc có thông tin mâu thuẫn:** bấm **Từ chối** và ghi lý do. Hệ thống thông báo kết quả, không thay đổi 2FA và không cấp quyền đăng nhập. Nếu chưa kết luận được thì giữ hồ sơ chờ xử lý, không tự duyệt khi quá hạn.
5. **Đủ bằng chứng:** admin bấm **Duyệt khôi phục**, ghi lý do và xác minh lại tài khoản admin. Hệ thống kiểm tra quyền duyệt, không cho tự duyệt tài khoản của mình và không cho duyệt lại hồ sơ đã có quyết định.
6. **Sau khi duyệt:** hệ thống hủy Authenticator cũ, mã dự phòng cũ và các lần đăng nhập cũ. Tài khoản chuyển sang **Chờ thiết lập lại 2FA**. Gửi hướng dẫn thiết lập lại qua kênh liên hệ đã được xác minh; admin không tự nhập khóa mới cho người dùng.
7. **Người dùng hoàn tất:** mở hướng dẫn, thêm Authenticator mới, xác minh mã và lưu 8 mã dự phòng mới. Hệ thống mới đánh dấu **Khôi phục hoàn tất** và yêu cầu đăng nhập lại.
8. **Gửi hướng dẫn thất bại hoặc quyền khôi phục hết hạn:** hồ sơ vẫn chưa hoàn tất. Admin thấy lỗi và có thể cấp lại sau khi kiểm tra hồ sơ còn hợp lệ; quyền cũ bị hủy. Không yêu cầu người dùng gửi lại hồ sơ từ đầu nếu thông tin vẫn đủ.

Màn hình admin cần có: thông tin yêu cầu, tài liệu, lịch sử trao đổi, admin phụ trách, trạng thái và các nút **Nhận xử lý / Yêu cầu bổ sung / Từ chối / Duyệt khôi phục**.

Trạng thái dễ hiểu: **Mới gửi → Đang xét → Chờ bổ sung (nếu cần) → Từ chối hoặc Đã duyệt → Chờ thiết lập lại 2FA → Khôi phục hoàn tất**. Hệ thống ghi lại người xử lý, thời gian và lý do ở mỗi bước; quyết định của admin và kết quả khôi phục được lưu riêng.

## 3. Database đã thêm gì, để làm gì?

Đã viết trong file SQL `scripts/database/05.13-Mfa-Lifecycle.sql`; chưa xác nhận đã chạy lên database thực tế của nhóm.

**Bảng `mfa_credentials` — thêm 2 cột:**

| Cột mới | Mục đích |
|---|---|
| `pending_protected_secret` | Giữ khóa bí mật mới trong lúc chờ người dùng xác minh, để chưa ghi đè khóa đang dùng. Khóa được lưu ở dạng bảo vệ. |
| `pending_expires_at` | Ghi thời điểm hết hạn xác minh khóa mới, hiện là 10 phút. |

**Tạo bảng `mfa_recovery_codes` — lưu 8 mã dự phòng:**

| Cột | Mục đích |
|---|---|
| `id` | Phân biệt từng mã. |
| `user_id` | Biết mã thuộc người dùng nào. |
| `code_hash` | Lưu bản băm để kiểm tra mã; không lưu mã gốc có thể đọc lại. |
| `used_at` | Biết mã đã dùng chưa và dùng lúc nào. |

Không thêm cột vào `users`/`accounts` ở phần đã làm. Số mã còn lại được tính từ các mã chưa dùng.

## 4. Database cần thêm cho phần khôi phục — chưa làm

| Thay đổi dự kiến | Mục đích |
|---|---|
| `users.security_version` và `auth_challenges.security_version` | Giúp hủy hiệu lực các yêu cầu xác minh/khôi phục cũ khi bảo mật tài khoản đã thay đổi. |
| `mfa_credentials.recovery_state` | Đánh dấu tài khoản phải thiết lập lại 2FA sau khi admin duyệt; không cho bỏ qua 2FA để vào trang chính. |
| `mfa_credentials.pending_operation_id` | Biết khóa mới đang chờ xác minh thuộc lần khôi phục nào. |
| Bảng `mfa_recovery_grants` | Ghi người dùng nào được khôi phục, được làm gì, đến lúc nào và đã dùng quyền đó chưa. |
| Bảng `account_recovery_requests` | Lưu yêu cầu, lý do, admin xử lý và kết quả duyệt/khôi phục. |
| Bảng `account_recovery_attachments` | Ghi file chứng minh thuộc hồ sơ nào, lưu ở đâu và khi nào phải xóa. |
| Bảng `account_recovery_events` | Ghi lịch sử ai xem hồ sơ, ai duyệt và thời điểm xử lý. |

Tên bảng/cột ở mục 4 là đề xuất, chưa có file SQL để áp dụng.

**Leader cần duyệt:** cách xử lý mã cuối cùng, luồng admin xét hồ sơ, giới hạn file/thời gian lưu và thiết kế database bổ sung. Việc khôi phục hiện chỉ đề xuất cho 2FA, chưa gồm đổi mật khẩu hoặc đổi email/số điện thoại.
