# Debug Login/Logout qua Clean Architecture

Cập nhật 06/10/2026: xem [hướng dẫn hiện tại](../login-logout-frontend-guide.md) để migration PostgreSQL và cấu hình RSA. Các kết quả ngày 02/10 ở cuối chỉ là lịch sử.

## Chạy API

Từ thư mục gốc repository, dùng .NET SDK 10:

```powershell
dotnet restore SmartParking.slnx
dotnet build SmartParking.slnx --no-restore
dotnet run --project src/Services/UserService/SmartParking.UserService.API --no-build --launch-profile http
```

API chạy tại `http://localhost:5035`. Profile `http` đặt môi trường `Development`, nơi có cấu hình JWT demo. Cần PostgreSQL và schema auth mới; không cần Kafka/gRPC cho login/logout.

## Kiểm tra bằng Postman

1. Import `docs/04-testing/postman/UserService.postman_collection.json` vào Postman.
2. Biến collection `baseUrl` mặc định là `http://localhost:5035`.
3. Chọn request **Login successful**, nhấn **Send** hoặc chạy cả collection trong Collection Runner.

```http
POST http://localhost:5035/api/auth/login
Content-Type: application/json
```

```json
{
  "email": "driver@gmail.com",
  "password": "Password@123"
}
```

Kết quả thành công: HTTP `200`, `success: true`, `data.accessToken`, refresh token thực, `data.expiresIn: 3600` và `data.user` có tên `Demo Driver`, ID là một `UUID` hợp lệ, role `driver`. Response không trả password/hash. Từ 06/10/2026 phát refresh token thực và lưu hash PostgreSQL; collection kiểm tra thêm `me`, `logout` và từ chối token đã thu hồi. Hướng dẫn frontend: [login/logout](../login-logout-frontend-guide.md).

| Request | HTTP mong đợi | Đi đến Persistence? |
| --- | --- | --- |
| Login successful | 200 | Có |
| Wrong password | 401 | Có |
| Unknown email | 401 | Có |
| Missing email | 400 | Không, validation chặn |
| Invalid email | 400 | Không, validation chặn |
| Missing password | 400 | Không, validation chặn |

Chạy tự động bằng Newman (trình chạy collection Postman), trong khi API đang chạy:

```powershell
npx.cmd --yes --cache .cache/npm --package newman newman run docs/04-testing/postman/UserService.postman_collection.json
```

## Debug từng lớp trong Visual Studio

1. Mở `SmartParking.slnx`, chọn `SmartParking.UserService.API` làm Startup Project và profile `http`.
2. Dừng API đang chạy trong terminal bằng Ctrl+C để giải phóng port 5035, rồi nhấn F5 trong Visual Studio.
3. Đặt breakpoint theo bảng dưới. Gửi request **Login successful** từ Postman.
4. Dùng F11 (Step Into) tại lời gọi phương thức. MediatR/AutoMapper là thư viện nên có thể không bước trực tiếp vào handler; dùng F5 để dừng tại breakpoint kế tiếp. Dùng F10 để chạy qua lệnh LINQ.
5. Trong cửa sổ Locals/Watch, xem `loginDto`, `loginCommand`, `request`, `user`, `result` và Call Stack. Không ghi password/token vào log hoặc ảnh báo cáo công khai.

Đường dẫn trong bảng tính từ `src/Services/UserService/`:

| Lớp | File / phương thức | Đặt breakpoint tại |
| --- | --- | --- |
| API | `SmartParking.UserService.API/Controllers/AuthController.cs` — `Login` | `Mediator.Send(Mapper.Map<LoginCommand>(loginDto), ...)` |
| Application | `SmartParking.UserService.Application/Common/Behaviors/ValidationBehavior.cs` — `Handle` | `var context = new ValidationContext...` |
| Application | `SmartParking.UserService.Application/Usecase/Login/LoginCommandHandler.cs` — `Handle` | `BeginTransactionAsync` và `GetByEmailForLoginAsync` |
| Persistence | `SmartParking.UserService.Persistence/Repositories/UnitOfWork.cs` — getter `UserRepository` | `this.userRepository` |
| Persistence | `SmartParking.UserService.Persistence/Repositories/UserRepository.cs` — `GetByEmailForLoginAsync` | `FromSqlInterpolated` |
| Application | `SmartParking.UserService.Application/Services/AccessTokenService.cs` — `GenerateAccessToken` | `new JwtSecurityToken(...)` |
| API | `SmartParking.UserService.API/Controllers/AuthController.cs` — `Login` | `return Ok(...)` |

`UserRepository.GetByEmailForLoginAsync` đọc user/accounts/roles từ PostgreSQL và khóa row trong transaction để tránh mất counter khi nhiều request login sai đồng thời. Password verification dùng `Infrastructure/Services/BcryptPasswordService`. `AuthSessionService` tạo token và `PostgresAuthSessionStore` lưu phiên. Domain chứa User/Account/Role/AuthSession, không phải service gọi trực tiếp.

Controller → MediatR/validation → LoginCommandHandler → IUnitOfWork/IUserRepository → PostgreSQL → bcrypt/token/session → Controller response. Logout gửi LogoutCommand rồi conditional update thu hồi đúng phiên. Các điểm breakpoint nên theo tên phương thức hiện tại thay vì dòng code lịch sử.

## Kết quả kiểm tra ngày 02/10/2026

- Build solution: thành công, không có lỗi biên dịch.
- Postman collection chạy bằng Newman 6.2.2 trên API thật: 6/6 request, 18/18 assertion đạt.
- Kết quả: thành công 200; sai mật khẩu/email không tồn tại 401; thiếu email/email sai định dạng/thiếu mật khẩu 400.
- Chưa thực hiện Step Into bằng debugger IDE trong phiên tự động; làm theo các breakpoint ở trên để trình bày trực tiếp.
- Restore/build hiện có cảnh báo NU1903 từ dependency `Microsoft.OpenApi` 2.0.0 của scaffold sẵn có.

Đây là bài demo gọi API đến Persistence: mật khẩu seed so sánh plaintext, JWT HS256 dùng key demo trong cấu hình Development, thời hạn 5 phút; refresh chưa triển khai và trả null. Login/logout đã được nối với frontend, JWT được kiểm tra và phiên bị thu hồi khi logout. Session store chỉ ở bộ nhớ một tiến trình; khởi động lại API làm mọi phiên cũ không còn hợp lệ. Không dùng luồng này làm xác thực production.
