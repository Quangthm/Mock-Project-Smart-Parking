# Vòng đời 2FA

Tài khoản mặc định chưa bật 2FA. TOTP dùng mã 6 số, chu kỳ 30 giây,
chấp nhận lệch một bước thời gian và không cho dùng lại bước đã xác minh.
Mã dự phòng gồm 8 mã ngẫu nhiên, mỗi mã có 128 bit ngẫu nhiên và dùng một lần.
Database chỉ lưu hash SHA-256 của mã dự phòng; secret Authenticator được bảo vệ
bằng ASP.NET Core Data Protection. Cần giữ bền vững `Workflow:KeyDirectory`.

Phạm vi Fix2FA được người dùng duyệt ngày 14/10/2026 cho phép đăng nhập đầy đủ
bằng primary proof và một mã dự phòng, rồi tùy chọn thay Authenticator hoặc để
sau. Điều này thay thế phương án restricted replacement bằng mã dự phòng trong
`docs/01-requirements/srs/authentication-recovery.md` cho đợt triển khai này.
Khôi phục qua admin và tích hợp audit service thuộc đợt tiếp theo.

## Thiết lập và quản lý

- `GET /api/auth/mfa/status`: trạng thái bật và số mã dự phòng chưa dùng.
- `POST /api/auth/mfa/setup`: xác minh mật khẩu; nếu đang bật 2FA, yêu cầu thêm
  `totp` hoặc `recoveryCode`. Trả `secret`, `uri`, `expiresAt` để hiển thị QR.
  Secret mới ở trạng thái chờ trong 10 phút; secret đang dùng vẫn hoạt động.
- `POST /api/auth/mfa/enable` với `{ code }`: xác minh secret đang chờ,
  kích hoạt và trả `{ recoveryCodes, signInRequired }`. Frontend chỉ giữ mã
  trong bộ nhớ cho đến khi người dùng lưu. Khi thay Authenticator,
  thu hồi tất cả session và yêu cầu đăng nhập lại sau khi lưu mã.
- `POST /api/auth/mfa/disable`: yêu cầu mật khẩu và TOTP hoặc mã dự phòng,
  xóa secret/mã dự phòng và thu hồi tất cả session.
- `POST /api/auth/mfa/recovery-codes`: yêu cầu mật khẩu và TOTP hoặc mã dự phòng,
  tạo bộ 8 mã mới, hủy bộ cũ, giữ nguyên Authenticator.

Các API quản lý yêu cầu Bearer token. Khi bật, tắt, thay Authenticator hoặc
tạo lại mã dự phòng, thông báo được đưa vào hàng đợi email nếu tài khoản có
email đã xác minh. SMTP cần được cấu hình để gửi thực tế.

## Đăng nhập

`POST /api/auth/login` kiểm tra mật khẩu trước. Nếu chưa bật 2FA, trả session
như trước. Nếu đã bật, trả `mfaRequired: true` và `data` gồm `challengeId`,
`challengeToken`, `expiresAt`; không trả access/refresh token. Challenge có hạn
5 phút và token ngẫu nhiên 256 bit, database chỉ lưu hash token.

`POST /api/auth/mfa/login` nhận `challengeId`, `challengeToken` và một trong
hai trường `totp`/`recoveryCode`. Thành công mới tạo session và đóng challenge.
Đăng nhập OTP qua `/otp/login` cũng yêu cầu TOTP hoặc mã dự phòng nếu bật 2FA.
Khi dùng mã dự phòng, response session có `recoveryCodesRemaining`; frontend
hiện gợi ý thiết lập lại Authenticator. Không lưu mã hay challenge token
vào localStorage/sessionStorage.

Sai mã 3 lần khóa xác minh 15 phút, chung cho TOTP và mã dự phòng. Tạo challenge
mới không xóa bộ đếm. Request không nhập mã không tính là nhập sai. User và
credential được khóa trong transaction để mã dự phòng không thể dùng hai lần
đồng thời. Thay đổi cấu hình bảo mật đóng các challenge đăng nhập còn mở.

## Migration và kiểm tra

Áp dụng `scripts/database/05.13-Mfa-Lifecycle.sql` sau `05.8`. Database mới
chạy migration qua compose; database có volume cũ chạy:

```powershell
.\scripts\apply-workflow-migrations.ps1 -Service User -SchemaRoot .
```

Script yêu cầu `.env` đã cấu hình theo `LOCAL-SETUP.md`. Migration có thể chạy
lặp lại và không thay secret đang bật của người dùng hiện có.

```powershell
npm.cmd --prefix FE run build
node --test tests/frontend/auth-api.test.cjs tests/frontend/sign-in.test.cjs tests/frontend/mfa-security.test.cjs
dotnet test tests/Services/SmartParking.UserService.Tests --filter FullyQualifiedName~MfaLifecycleTests
```

Test PostgreSQL yêu cầu `SMARTPARK_AUTH_TEST_CONNECTION` trỏ tới server test
riêng có quyền tạo database. Fixture tạo/xóa database riêng từng test;
không chạy bằng connection production. Khi dùng clock giả, mốc ban đầu ở quá
khứ để JWT phát hành sau khi tăng clock vẫn có `notBefore` hợp lệ ở TestServer.

Chưa triển khai khôi phục mất toàn bộ mã qua hồ sơ giấy tờ/admin.
