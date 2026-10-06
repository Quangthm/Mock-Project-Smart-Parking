# Demo API Login qua Clean Architecture (không gọi DB)

## Chạy API

Từ thư mục gốc repository, dùng .NET SDK 10:

```powershell
dotnet restore SmartParking.slnx
dotnet build SmartParking.slnx --no-restore
dotnet run --project src/Services/UserService/SmartParking.UserService.API --no-build --launch-profile http
```

API chạy tại `http://localhost:5035`. Profile `http` đặt môi trường `Development`, nơi có cấu hình JWT demo. Không cần connection string, PostgreSQL, EF migrations, Kafka hay gRPC.

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

Kết quả thành công: HTTP `200`, `success: true`, `data.accessToken`, `data.refreshToken: null`, `data.expiresIn: 300` và `data.user` có tên `Demo Driver`, ID là một `UUID` hợp lệ, role `driver`. Response không trả password/hash. Từ 05/10/2026 không phát refresh token giả; collection kiểm tra thêm `me`, `logout` và từ chối token đã thu hồi. Hướng dẫn frontend: [login/logout](../login-logout-frontend-guide.md).

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
| API | `SmartParking.UserService.API/Controllers/AuthController.cs` — `Login` | `var loginCommand = this.Mapper.Map...` rồi `this.Mediator.Send(...)` |
| Application | `SmartParking.UserService.Application/Common/Behaviors/ValidationBehavior.cs` — `Handle` | `var context = new ValidationContext...` |
| Application | `SmartParking.UserService.Application/Usecase/Login/LoginCommandHandler.cs` — `Handle` | `var user = await this.unitOfWork.UserRepository...` |
| Persistence | `SmartParking.UserService.Persistence/Repositories/UnitOfWork.cs` — getter `UserRepository` | `this.userRepository` |
| Persistence | `SmartParking.UserService.Persistence/Repositories/UserRepository.cs` — `GetByEmailAsync` | `cancellationToken.ThrowIfCancellationRequested()` rồi `var user = this.entities.FirstOrDefault...` |
| Application | `SmartParking.UserService.Application/Services/AccessTokenService.cs` — `GenerateAccessToken` | `var jwtOptions = ...` |
| API | `SmartParking.UserService.API/Controllers/AuthController.cs` — `Login` | `return Ok(...)` |

`UserRepository` lấy danh sách entity `User` của Domain từ `Persistence/FakeDatabase/InMemoryDatabase.cs`. Dữ liệu này chỉ nằm trong bộ nhớ tiến trình. Domain chứa entity và enum, không phải một lớp service bắt buộc phải gọi thêm. `Infrastructure` chưa có tác vụ bên ngoài cần dùng trong demo này.

```text
Postman
  -> AuthController.Login (API)
  -> Mapper -> MediatR.Send
  -> ValidationBehavior -> LoginCommandValidator (Application)
  -> LoginCommandHandler.Handle (Application)
  -> IUnitOfWork.UserRepository / IUserRepository.GetByEmailAsync (Application contracts)
  -> UnitOfWork -> UserRepository.GetByEmailAsync (Persistence)
  -> InMemoryDatabase.Users / User entity (Domain)
  <- User -> tạo session -> HTTP 200
```

Interface không có thân hàm để Step Into; debugger sẽ đi vào implementation do DI đăng ký. Handler chỉ phụ thuộc interface của Application; Controller không gọi trực tiếp repository. API tham chiếu Persistence để đăng ký DI tại composition root. Chiều phụ thuộc project: `Application -> Domain`, `Persistence -> Application + Domain`, `API -> Application + Persistence`.

Thử **Wrong password** để quan sát repository vẫn chạy rồi handler trả `LoginResult` thất bại; Controller chuyển kết quả này thành HTTP 401. Email không tồn tại và tài khoản không Active cũng trả kết quả thất bại, không ném exception, nên debugger không dừng do exception khi test các trường hợp này. Thử **Missing email** để quan sát validation dừng luồng trước handler; validation vẫn sử dụng `ValidationException`. Mẫu gRPC `ValidateAccessTokenCommandHandler` chưa có implementation `IUnitOfGrpc`, nên được loại khỏi MediatR scanning để API demo khởi động; không tắt kiểm tra DI.

## Kết quả kiểm tra ngày 02/10/2026

- Build solution: thành công, không có lỗi biên dịch.
- Postman collection chạy bằng Newman 6.2.2 trên API thật: 6/6 request, 18/18 assertion đạt.
- Kết quả: thành công 200; sai mật khẩu/email không tồn tại 401; thiếu email/email sai định dạng/thiếu mật khẩu 400.
- Chưa thực hiện Step Into bằng debugger IDE trong phiên tự động; làm theo các breakpoint ở trên để trình bày trực tiếp.
- Restore/build hiện có cảnh báo NU1903 từ dependency `Microsoft.OpenApi` 2.0.0 của scaffold sẵn có.

Đây là bài demo gọi API đến Persistence: mật khẩu seed so sánh plaintext, JWT HS256 dùng key demo trong cấu hình Development, thời hạn 5 phút; refresh chưa triển khai và trả null. Login/logout đã được nối với frontend, JWT được kiểm tra và phiên bị thu hồi khi logout. Session store chỉ ở bộ nhớ một tiến trình; khởi động lại API làm mọi phiên cũ không còn hợp lệ. Không dùng luồng này làm xác thực production.
