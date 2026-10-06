# Đối chiếu và phương án sửa Login/Logout

Ngày: 06/10/2026. Nguồn: `API Design vs Code Comparison Report.pdf` (16 trang), code hiện tại, SRS v0.9 §3.1.1/§3.1.4/§4.3. Chưa có bản API Design gốc. Báo cáo kiểm tra commit 2816581; kết quả đó không được coi là kết quả test của code mới.

## So sánh trước khi sửa

| ID trong mục 11 | Code hiện tại | Phương án |
| --- | --- | --- |
| C-01 | `/api/auth` | Giữ duy nhất `/api/auth` theo yêu cầu trực tiếp của người dùng. Không thêm alias versioned. |
| C-02 | So sánh `PasswordHash` với password plaintext; seed plaintext | bcrypt cost 12; không fallback plaintext. Chỉ chuyển seed demo đã biết qua script riêng. |
| C-03/C-04/C-05 | Chỉ kiểm tra Active; exception khóa chưa dùng; không có counter | Lưu counter và thời điểm khóa; khóa sau 3 lần sai, trả 403 ACCOUNT_LOCKED, reset sau login đúng/hết khóa. Serialize login cùng user bằng row lock PostgreSQL. |
| C-06 | 300 giây | Access token 3600 giây theo báo cáo/API Design. |
| C-07/C-08 | Refresh null; phiên RAM; refresh helper ném NotSupportedException | Refresh ngẫu nhiên, lưu SHA-256 hash trong PostgreSQL; 7 ngày theo SRS; xoay token bằng conditional update, chặn reuse token cũ. |
| C-09 | Logout không nhận body | Bắt buộc refreshToken; token phải thuộc đúng user và jti của Bearer token. |
| C-10 | 204 | 200 và thông báo thành công. |
| C-11 | Chỉ message/status | Trả `code`: AUTH_FAILED, ACCOUNT_LOCKED, MISSING_REFRESH_TOKEN, INVALID_TOKEN, REVOKE_SESSION_FAILED. |
| C-12 | HS256/shared key | RS256; RSA private key từ cấu hình ngoài source; khóa tạm chỉ trong Development. |

Các điểm đang đúng và cần giữ: Bearer, dữ liệu user lấy từ backend, validation 400, kiểm tra trạng thái/role hiện tại, thu hồi chỉ phiên logout và chặn access token đã logout.

## Quyết định và giới hạn nguồn

- Contract lỗi dùng envelope sẵn có `{ success, code, message }`; báo cáo chỉ xác định mã lỗi, chưa xác định JSON lỗi đầy đủ. Đây là lựa chọn triển khai cần so lại khi có design gốc.
- Access token 1 giờ là ưu tiên API Design trong tác vụ này, dù SRS còn ghi session 24 giờ. Refresh 7 ngày lấy từ SRS. Rotation không kéo dài hạn tuyệt đối 7 ngày của phiên ban đầu.
- Báo cáo yêu cầu khóa login sau 3 lần sai; SRS nói rõ 3 lần sai OTP và khóa 15 phút. Áp dụng 3 lần/15 phút cho password login như chính sách triển khai từ báo cáo; cấu hình `AuthenticationPolicy` riêng. Không tuyên bố đã triển khai OTP/MFA.
- Thêm `POST /api/auth/refresh` để rotation có luồng thực thi; báo cáo không cung cấp contract endpoint này. Đây là endpoint hỗ trợ được đề xuất, nhận refreshToken và trả cùng cấu trúc session như login.
- Dùng bảng `user_refresh_tokens` đã có trong database script, thêm jti và access-token expiry để cả access/refresh revocation cùng bền vững.
- Frontend chỉ cần giữ refresh token và gửi body logout. Tự động refresh UI không nằm trong phạm vi login/logout này.
- Dữ liệu user cũ dùng plaintext ngoài seed demo cần reset password hoặc chuyển đổi có kiểm soát; không tự coi mọi giá trị password_hash là plaintext để hash lại.

## Thứ tự và kiểm chứng

1. Bổ sung domain/ports, bcrypt và cấu hình RSA.
2. Bổ sung PostgreSQL session store, counter/lock và script schema.
3. Cập nhật use cases/controller/error mapping; cập nhật body logout frontend.
4. Test credential/lock/reset, RS256/expiry/tamper, refresh rotation/replay/expiry, logout đúng/sai phiên, HTTP status/error/body và persistence khi có PostgreSQL.

Kết quả kiểm chứng sẽ được cập nhật sau khi sửa; không dùng tỷ lệ PASS của báo cáo cũ làm bằng chứng.

## Kết quả sau sửa

- Đã triển khai C-02 đến C-12 theo các quyết định ở trên; C-01 dùng `/api/auth` theo yêu cầu người dùng.
- Đã sửa seed, fixture unit test, caller logout frontend và Postman. Role/status mapping tương thích schema uppercase; không cấp token cho account bị xóa/không có role.
- `dotnet test SmartParking.slnx --no-restore`: 52 test unit/HTTP đạt. Trong đó một test scaffold có sẵn; không gọi đó là 52 test auth.
- API build và frontend `npm run build` thành công. Build vẫn báo cảnh báo có sẵn NU1903 ở Microsoft.OpenApi 2.0.0 và frontend bundle lớn.
- Test PostgreSQL thực đã chạy ngày 06/10/2026 bằng Docker `postgres:16-alpine`, cổng 127.0.0.1:65433: **1/1 đạt, 0 skip**. Đã kiểm tra migration chạy lặp, seed bcrypt qua pgcrypto, ba login sai đồng thời, rotation đồng thời chỉ một request thắng, giữ hạn refresh tuyệt đối, revoke và đọc trạng thái qua DbContext mới. Database/container test biệt lập đã được dọn; không thay đổi database dự án. Khi không đặt `SMARTPARK_AUTH_TEST_CONNECTION`, test vẫn chủ động skip để bộ unit/HTTP chạy độc lập.
- Chưa chỉnh database hiện có. Trước khi chạy bản mới trên database cũ, chạy `05.2-Auth-Design-Alignment.sql`; seed plaintext demo có script `05.3-Demo-Password-Hash.sql` riêng. Không tự reset các user khác.
- Khóa RSA tạm chỉ cho Development. Muốn phiên sống qua restart/nhiều instance, cấu hình cùng `Jwt:PrivateKeyPem` và issuer/audience ngoài source.

Tham khảo kỹ thuật: [BCrypt.Net chính thức](https://github.com/BcryptNet/bcrypt.net), [RSA.ImportFromPem](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsa.importfrompem), [EF Core conditional updates](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).

## Review và sửa bổ sung trước khi push

- Truy vấn email thông thường và truy vấn login có row lock đều loại user có `deleted_at`. Đã tái hiện lỗi trước sửa và bổ sung test PostgreSQL: xóa mềm user cũ, tạo user mới cùng email, login chọn đúng user mới; khi tất cả đã xóa thì trả AUTH_FAILED.
- Frontend không tự xóa token hoặc coi logout 401 là thành công. Nó kiểm tra bearer qua `/me`; bearer còn hợp lệ thì giữ phiên và báo lỗi, bearer không hợp lệ thì phát sự kiện hết phiên. Lỗi mạng/server khi kiểm tra không xóa token. Response logout thành công cũng không xóa token của phiên mới được thay trong lúc chờ.
- Pin Microsoft.OpenApi 2.7.5, bản vá cho cảnh báo NU1903 đã thấy ở 2.0.0. Restore/build/test không còn cảnh báo này; endpoint OpenAPI thật trả 200.
- Kiểm chứng sau sửa ngày 06/10/2026: **54/54 backend đạt, 0 skip** (bao gồm 2 test PostgreSQL thật), **9/9 test frontend đạt**, frontend build đạt, `git diff --check` đạt. API Release chạy ở cổng riêng đã kiểm tra login/me/logout và revocation; API tạm đã dừng sau kiểm tra. Cảnh báo bundle frontend lớn vẫn còn.
- Các kết quả ở mục trước là lịch sử lần triển khai đầu. PostgreSQL dự án đã được khởi động phục vụ demo ở cổng 5433; PostgreSQL test ở 65433. Test mới chỉ tạo/xóa database tên ngẫu nhiên riêng và không thay đổi dữ liệu user dự án.
