# Report đánh giá login/logout và quyết định tích hợp frontend

Ngày: **05/10/2026**. Trạng thái: **WORKING — kết luận tạm thời cho demo; chờ mã nguồn của Văn để chốt so sánh hai bản**.

## 1. Quyết định hiện tại

Dùng **branch demo sau các sửa đổi trong lần làm việc này** làm bản chạy để trình diễn login/logout và tích hợp frontend. Quyết định dựa trên luồng đã chạy được và kiểm tra được, không phải kết luận demo tốt hơn bản của Văn.

**Chưa chọn bên thắng giữa demo và Văn**: người yêu cầu cho biết sẽ gửi bản của Văn sau. Không chấm điểm hoặc suy đoán tính năng của bản chưa đọc. Khi có bản đó, kiểm tra cùng các trường hợp ở mục 6 rồi cập nhật quyết định.

Phạm vi đã làm: nối Sign In với API, bật xác thực JWT, xác nhận phiên qua `me`, logout thu hồi phiên server, xử lý hết phiên và lỗi kết nối, cập nhật Postman và thêm test. Phạm vi chưa làm: OTP, đăng ký backend, refresh rotation, bcrypt, PostgreSQL, gateway và phân quyền toàn hệ thống.

## 2. Nguồn và phạm vi khảo sát

| Nguồn | Bằng chứng | Ý nghĩa |
| --- | --- | --- |
| Repository demo | `D:\DataFPTU\Semester6\ParkingSystemGithub\Mock-Project-Smart-Parking`; HEAD `demo`, commit gốc `f76d3bf` | Có backend UserService và React frontend trong `FE`. Các sửa đổi hiện là working-tree changes trên demo. |
| Repository GitLab đang mở ban đầu | `D:\DataFPTU\Semester6\OJT202_FSoft\smart-parking-system`; `develop`, commit `f1cc203` | Khác repository demo. Bản local có User API tra cứu profile, chưa có auth/frontend. Không chuyển hay ghi đè các thay đổi đang có ở đây. |
| Branch GitLab `Front-End` | `561881c9aaf6df58d38ce1e4b46e8e3f59f8ae9d` | Chỉ có template console .NET; đây không phải frontend đang đánh giá. |
| Bản của Văn | Chưa nhận đường dẫn/branch/commit | Chưa có bằng chứng để đánh giá. |
| SRS v0.8.5 | §3.1.1, §3.1.4, §4.3; FR-AUTH-02; UC-AUTH-01 | OTP 5 phút; khóa 15 phút sau 3 lần sai; session 24 giờ, refresh 7 ngày; RS256. Logout/revocation contract còn cần chốt. |

Không trộn tên project `SmartParking.User.*` ở repository đầu với `SmartParking.UserService.*` ở demo khi chuyển code. Baseline v0.8.5 được đọc ở repository đầu; report không tự sửa SRS.

## 3. Kết quả đọc bản demo trước khi sửa

Backend đã có luồng `AuthController → AutoMapper → MediatR → ValidationBehavior → LoginCommandHandler → IUnitOfWork/IUserRepository → InMemoryDatabase`. Đây là ưu điểm cho việc debug và trình bày tách lớp.

Tuy nhiên, bản demo ban đầu chưa có luồng xác thực hoàn chỉnh:

| Điểm | Hiện trạng trước sửa | Tác động |
| --- | --- | --- |
| Frontend login | `SignIn.tsx` tìm user và so password trong localStorage | Không gọi backend, không chứng minh API xác thực người dùng. |
| Khôi phục phiên | `AppContext.tsx` đọc current-user ID và user local | Sửa dữ liệu local có thể làm giao diện tự nhận user/role. |
| JWT middleware | Có hàm đăng ký JWT nhưng `Program.cs` chưa gọi, chưa có authentication/authorization middleware | Phát token không đồng nghĩa token được kiểm tra khi gọi API. |
| Validate token | `AccessTokenService.ValidateAccessToken` luôn trả true | Hàm này không kiểm tra chữ ký hoặc thời hạn. |
| Logout | Chỉ xóa user frontend | Token đã sao chép chưa bị thu hồi server. |
| Refresh | Sinh GUID rồi trả về, không lưu hoặc đổi token | Có trường refresh nhưng chưa có chức năng refresh thực tế. |
| User role | Backend chưa gán role cho seed/response | Không đủ dữ liệu để frontend điều hướng theo danh tính backend. |
| Mật khẩu | So sánh plaintext ở account seed | Chỉ phù hợp fixture demo, chưa đạt bcrypt trong SRS. |
| Test | Một test scaffold rỗng | Không có bằng chứng kiểm thử auth. |

Mẫu `ValidateAccessTokenCommandHandler` gọi gRPC vẫn là scaffold bị loại khỏi MediatR scanning; luồng đang chạy sử dụng JWT bearer middleware. Không dùng scaffold đó làm bằng chứng xác thực cross-service.

## 4. Kết quả sau sửa

| Hành vi | Triển khai hiện tại |
| --- | --- |
| Login | Frontend `POST /api/auth/login`; backend kiểm tra tài khoản Active và password seed, trả identity/role/token. |
| Kiểm tra JWT | Kiểm tra chữ ký, HS256, issuer, audience và lifetime; clock skew bằng 0. `sub` dùng user ID; `jti` dùng session ID; có claim role. |
| Phiên server | `IAuthSessionStore` là port ở Application; `InMemoryAuthSessionStore` là adapter singleton ở Persistence. |
| Tài khoản bị vô hiệu hóa | Sau khi JWT hợp lệ, middleware kiểm tra session và đọc account hiện tại; account không Active/bị xóa hoặc role đã thay đổi bị từ chối. |
| F5 | Lưu token trong sessionStorage của tab rồi gọi `GET /api/auth/me`; không khôi phục danh tính từ user local. |
| Logout | `POST /api/auth/logout` cần Bearer token; Controller gửi LogoutCommand qua MediatR; server xóa session theo jti. |
| Dùng lại token cũ | JWT còn chữ ký/thời hạn hợp lệ cũng bị 401 nếu session đã thu hồi. |
| Các phiên khác | Logout chỉ thu hồi phiên hiện tại, không tự logout mọi thiết bị. |
| Frontend logout lỗi mạng | Hiển thị lỗi và giữ trạng thái để người dùng thử lại; không thông báo thành công khi server chưa xác nhận. |
| Hết phiên | Frontend xóa phiên/UI khi hết thời hạn hoặc API báo 401; token hết hạn luôn bị backend từ chối. |
| Refresh | Trả `refreshToken: null`; không tạo chuỗi giả khiến frontend tưởng có refresh. |
| CORS | Cho phép frontend local tại localhost/127.0.0.1 port 5173; không bật wildcard origin. |

Luồng login:

```text
Sign In → authApi.login → AuthController
        → MediatR + validation → LoginCommandHandler
        → repository kiểm tra account → JWT + session server
        → frontend nhận user/role → dashboard
```

Luồng logout:

```text
Sign Out → POST logout + Bearer token
         → JWT validation + kiểm tra phiên/account
         → LogoutCommand → thu hồi jti
         → HTTP 204 → frontend xóa token/user
Token cũ → GET me → HTTP 401
```

Đây là JWT kết hợp trạng thái phiên ở server để hỗ trợ thu hồi ngay. Hướng triển khai dùng cơ chế [JWT bearer của ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0) và các tùy chọn [TokenValidationParameters](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters). Session revocation là phần bổ sung của dự án.

## 5. Giới hạn và quyết định kỹ thuật còn mở

- Giữ email/password và thời hạn 300 giây của **demo có sẵn** để hoàn thành bài tích hợp. Đây không phải thay đổi yêu cầu OTP hoặc thời hạn 24 giờ/7 ngày trong SRS.
- HS256 với Development key, password plaintext và không có OTP/lockout/MFA vẫn là khoảng cách với SRS. Chưa được coi là auth production.
- Không có refresh endpoint/rotation; sau 5 phút phải login lại. Thay đổi refresh-token GUID thành null là sửa contract có chủ đích; Postman đã cập nhật tương ứng.
- Session store chỉ ở bộ nhớ một tiến trình. Restart API làm mọi token cũ mất hiệu lực; nhiều instance cần shared store và chính sách hết hạn/thu hồi thống nhất.
- sessionStorage tránh lưu token vào localStorage lâu dài, nhưng vẫn đọc được bằng JavaScript và không chống XSS. Chốt cơ chế BFF/HttpOnly cookie hoặc token storage cùng frontend/gateway trước khi phát hành.
- Chỉ backend seed `driver@gmail.com` đăng nhập được. Tài khoản admin/owner/operator và tài khoản Sign Up đang nằm trong dữ liệu frontend mock không trở thành tài khoản backend. Registration/provisioning cần công việc riêng.
- Dữ liệu booking, wallet, map và audit log frontend vẫn là mock. Map chưa có MapTiler API key; ảnh kiểm tra hiển thị đúng thông báo thiếu key. Đây không phải lỗi login.
- Có auth và role claim chưa chứng minh đủ RBAC/lot/resource scope cho các API nghiệp vụ chưa triển khai.
- Application hiện còn chứa JWT service và đăng ký ASP.NET authentication. Đây là nợ kiến trúc của scaffold; bản triển khai chính thức nên đặt token adapter ở Infrastructure, wiring/middleware ở API, để Application phụ thuộc port.
- Build báo NU1903 từ `Microsoft.OpenApi 2.0.0`; frontend có cảnh báo bundle lớn. Cần xử lý trước phát hành. Runtime cũng cảnh báo license MediatR/AutoMapper: phải xem điều kiện sử dụng cho production, không suy luận từ việc demo chạy được.

## 6. Bảng so sánh và cách chốt khi nhận bản Văn

| Tiêu chí | Demo ban đầu | Demo sau sửa | Bản Văn |
| --- | --- | --- | --- |
| Login frontend gọi backend | Chưa | Đã kiểm tra trên browser | Chờ đọc code |
| Password/OTP theo SRS | Chưa: plaintext, không OTP | Vẫn chưa; ngoài phạm vi tích hợp demo | Chờ đọc code |
| JWT validation | Chưa bật middleware; helper stub | Có chữ ký/issuer/audience/lifetime/algorithm | Chờ kiểm tra |
| Logout thu hồi server | Chưa | Có; token cũ bị 401 | Chờ kiểm tra |
| Refresh rotation + 7 ngày | Chưa | Chưa; trả null rõ ràng | Chờ kiểm tra |
| Role lấy từ backend | Chưa | Có role Driver; chưa đủ RBAC toàn hệ thống | Chờ kiểm tra |
| Reload/expired/network handling | Local-only | Có restore qua me, hết phiên và lỗi kết nối | Chờ kiểm tra |
| Clean Architecture | Có CQRS/repository; lẫn JWT/ASP.NET trong Application | Giữ pipeline; thêm port session; vẫn còn nợ kiến trúc | Chờ kiểm tra |
| Persistence/multiple instances | In-memory | In-memory; chưa production | Chờ kiểm tra |
| Bằng chứng test | Test scaffold rỗng | Test auth + HTTP/Postman + browser, xem mục 7 | Chờ chạy cùng bộ ca |

Thứ tự ưu tiên để chọn bản chính thức: **tính đúng và an toàn của xác thực/thu hồi phiên → đáp ứng yêu cầu SRS → frontend tích hợp đúng → khả năng kiểm thử và bảo trì → độ phức tạp phù hợp**.

Các điều kiện phải kiểm tra ở cả hai bản: sai password/OTP; account inactive; không có token; token giả/sai chữ ký; expired; revoked sau logout; role từ server; không truy cập tài khoản khác; F5; mất kết nối; refresh replay nếu có refresh. Không chọn dựa vào số lớp, số thư viện hoặc giao diện đẹp.

Nếu Văn đã có password hashing/OTP, token validation, refresh persistence/rotation và logout đúng, ưu tiên giữ phần auth đó rồi dùng hợp đồng frontend đã xây dựng. Nếu Văn chỉ xóa token frontend hoặc nhận danh tính từ localStorage, không coi đó là cải thiện về bảo mật. Đây là tiêu chí quyết định có điều kiện, chưa phải nhận xét về code của Văn.

## 7. Kiểm chứng trong lần làm việc này

- `dotnet test SmartParking.slnx`: **16/16 đạt**, trong đó **15 ca auth mới** và 1 test scaffold rỗng có sẵn. Các ca mới kiểm tra credential sai, identity/role, token malformed/unsigned/tampered, sai key/issuer/audience, expired, session không tồn tại/sai user và logout chỉ thu hồi phiên hiện tại.
- Build UserService API: thành công, 0 lỗi; có cảnh báo OpenAPI nêu trên.
- `npm run build`: thành công; `tsc --noEmit`: thành công.
- HTTP thực: anonymous me 401; login 200; me 200; sai password 401; email không hợp lệ 400; logout 204; dùng token đã logout 401; token giả 401; CORS preflight origin local 204.
- Browser thực: sai password hiển thị lỗi; login hiển thị Demo Driver; F5 vẫn đúng account; Sign Out về trang public; F5 sau logout không trở lại dashboard. Khi tắt API, Sign Out hiển thị lỗi kết nối và không báo thành công.
- Newman chạy collection hiện tại: **10/10 request và 23/23 assertion đạt**, gồm anonymous me, login/validation, me, logout và revoked token. Không lấy kết quả ngày 02/10 làm kết quả của lần sửa mới.

Ảnh minh chứng: [account Demo Driver và menu Sign Out](../../04-testing/evidence/auth-demo-login-2026-10-05.png). Cách chạy: [hướng dẫn tích hợp](../../04-testing/login-logout-frontend-guide.md).

Chưa kiểm chứng: OTP/MFA, khóa sau 3 lần sai, đăng ký backend, multi-instance, refresh rotation, phân quyền nghiệp vụ toàn hệ thống và so sánh với bản Văn.

## 8. Câu trả lời chuẩn bị khi thầy hỏi

**Vì sao chọn demo?** Hiện em chọn demo để chạy phần tích hợp vì đã kiểm tra được toàn luồng frontend–API–logout. Em chưa kết luận demo tốt hơn bản Văn vì chưa có mã nguồn bản đó. Quyết định cuối dựa trên cùng tiêu chí và bộ test.

**Frontend có tự xác thực không?** Không. Nó gửi email/password lên API; account, ID và role do server trả. Dữ liệu local chỉ còn dùng cho UI mock.

**JWT là gì, có mã hóa password không?** JWT mang claims và được ký để phát hiện thay đổi. Payload thông thường không được mã hóa; em không đưa password vào token. Production cần HTTPS.

**Xóa token frontend đã đủ logout chưa?** Chưa đủ để ngăn token đã sao chép. Bản này thu hồi jti ở server; kiểm tra token cũ qua me trả 401.

**Tại sao JWT lại cần session store?** Để thu hồi trước hạn. JWT có thể xác thực chữ ký độc lập, nhưng yêu cầu logout ngay làm phát sinh kiểm tra trạng thái thu hồi.

**F5 thì đăng nhập lại bằng cách nào?** Token của tab còn trong sessionStorage; frontend gọi me để server xác nhận. Nó không tự tin user ID/role trong localStorage.

**Tại sao token 5 phút, trong SRS 24 giờ?** 5 phút là contract demo cũ em giữ cho bài tích hợp. Chưa nghiệm thu theo SRS; session 24 giờ và refresh 7 ngày phải hoàn thiện ở bản chính thức.

**Refresh token đâu?** Demo ban đầu trả GUID nhưng không dùng được. Em trả null để thể hiện chưa hỗ trợ, tránh frontend gọi một chức năng không tồn tại.

**Password đã hash chưa? OTP đâu?** Account demo vẫn dùng fixture plaintext. Em ghi rõ chưa đạt bcrypt/OTP theo SRS, không trình bày đây là auth production.

**Các lớp làm gì?** Controller nhận HTTP và gửi request; MediatR/validation chạy use case; Application đặt contract; Domain chứa user/role/status; Persistence đọc account và giữ phiên demo. Token/wiring hiện còn lẫn trong Application là nợ cần tách tiếp.

**Test nào chứng minh logout?** Login lấy token, me trả 200, logout trả 204, dùng lại chính token đó gọi me trả 401. Test riêng cũng chứng minh logout một phiên không làm mất phiên khác.

**Restart hoặc nhiều server thì sao?** Demo giữ phiên ở memory nên restart mất phiên; chạy nhiều instance cần shared session store. Em chưa tuyên bố bản này giải quyết triển khai phân tán.

**Chức năng nào chưa hoàn thành?** So sánh và chọn giữa hai bản còn chờ code Văn. OTP, registration backend, refresh và hardening production chưa nằm trong kết quả tích hợp demo này.
