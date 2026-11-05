# Báo Cáo Thẩm Định Toàn Diện & Kế Hoạch Khắc Phục Mã Nguồn Notification & Audit Service
## Master Audit Report & Phased Remediation Plan

---

**Mã tài liệu (Document ID)**: `AUDIT-SPARK-NOTIF-001`  
**Nhánh kiểm toán (Target Branch)**: `feat/notification-audit-service`  
**Dịch vụ mục tiêu (Target Service)**: `SmartParking.NotificationAuditService` (`src/Services/NotificationAuditService/`)  
**Ngày phát hành**: 2026-11-05  
**Trạng thái thẩm định**: ĐÃ HOÀN TẤT (SURVEY & AUDIT COMPLETE)  
**Tiêu chuẩn quy chiếu**:
- Quy chuẩn Kiến trúc Mã nguồn: `docs/02-architecture/source-code-architecture-rules.md`
- Đặc tả Yêu cầu Nghiệp vụ (SRS): `docs/01-requirements/srs/modules/08-notifications-reporting.md`
- Thiết kế Hệ thống & API: `docs/02-architecture/api/SmartPark-All-API-Design.md`
- Thiết kế Hợp đồng Hạ nguồn Payment: `docs/02-architecture/api/payment-service/04-downstream-payment-succeeded-consumer-blueprint.md`
- Thiết kế Cơ sở dữ liệu: `docs/02-architecture/database/microservices/05-notification-audit-service-db-docs.md` & `scripts/database/microservices/05-notification-audit-service-db.sql`

---

## Mục Lục Tổng Quan

1. [Tóm Tắt Điều Hành & Bảng Điểm Kiến Trúc Toàn Diện (Executive Summary & Scorecard)](#tom-tat-dieu-hanh--bang-diem-kien-truc)
2. [Phần 1: Thẩm Định Tính Sẵn Sàng Vận Hành Production (Production Readiness Audit - R1)](#phan-1-tham-dinh-tinh-san-sang-van-hanh-production)
3. [Phần 2: Thẩm Định Liên Kết & Tích Hợp Sự Kiện (Event Integration Audit - R2)](#phan-2-tham-dinh-lien-ket--tich-hop-su-kien)
4. [Phần 3: Thẩm Định Định Hướng Kiến Trúc & Tuân Thủ SRS (Architectural Alignment & SRS Audit - R3)](#phan-3-tham-dinh-dinh-huong-kien-truc--tuan-thu-srs)
5. [Phần 4: Sổ Đăng Ký Rủi Ro & Phân Loại Mức Độ Nghiêm Trọng (Risk Register)](#phan-4-so-dang-ky-rui-ro--phan-loai-muc-do-nghiem-trong)
6. [Phần 5: Kế Hoạch Hành Động Khắc Phục Phân Pha (Phased Remediation Plan)](#phan-5-ke-hoach-hanh-dong-khac-phuc-phan-pha)
7. [Phần 6: Phương Pháp & Lệnh Kiểm Chứng Độc Lập (Verification Commands)](#phan-6-phuong-phap--lenh-kiem-chung-doc-lap)

---

<a name="tom-tat-dieu-hanh--bang-diem-kien-truc"></a>
## Tóm Tắt Điều Hành & Bảng Điểm Kiến Trúc Toàn Diện (Executive Summary & Scorecard)

### Bối cảnh thẩm định
Đợt kiểm toán toàn diện được thực hiện trên nhánh `feat/notification-audit-service` nhằm đánh giá mức độ sẵn sàng bàn giao của **Notification & Audit Service** trước khi sáp nhập vào nhánh tích hợp chung (`develop`). Dịch vụ này đóng vai trò sống còn trong hệ sinh thái SmartParking:
1. Tiếp nhận các sự kiện xác thực thanh toán, xác nhận đặt chỗ, cảnh báo quá giờ và chuyển phát thông báo đa kênh (In-App, Email, SMS, Mobile Push) tới tài xế và đối tác vận hành.
2. Lưu vết bền vững và khử trùng lặp (Idempotent Delivery Tracking) để bảo đảm thông báo không bị thất lạc hoặc gửi lặp vô tận.
3. Thu nạp các sự kiện chiếu kiểm toán tập trung (Central Audit Fact Projections) từ tất cả các microservices phục vụ mục đích thanh tra và tuân thủ pháp lý theo chuẩn 7 năm.

### Kết quả đánh giá sơ bộ
Về mặt tổ chức mã nguồn, dự án thể hiện kỷ luật cao trong việc phân ranh giới Clean Architecture: tầng Domain hoàn toàn là POCO thuần túy (.NET 10.0, 0 NuGet dependencies), quy tắc phụ thuộc hướng tâm (Inward Dependency Rule) đạt mức tuyệt đối, 0 rò rỉ `IQueryable` qua tầng Persistence, và toàn bộ 35 unit/integration test nội bộ hiện có đều vượt qua (35/35 Tests PASS).

Tuy nhiên, **dịch vụ CHƯA THỂ VẬN HÀNH TRÊN MÔI TRƯỜNG PRODUCTION VÀ CHƯA THỂ TÍCH HỢP VỚI HỆ THỐNG THỰC TẾ** do tồn tại **4 Rủi ro Chặn (Blockers)** và **4 Rủi ro Nghiêm trọng (Critical Risks)**:
- **Lỗ hổng bảo mật nghiêm trọng**: Thiếu hoàn toàn middleware xác thực JWT Bearer trong `Program.cs`, controller không có `[Authorize]`, và tồn tại cửa sau `CurrentUserService` nhận diện người dùng qua HTTP Header giả mạo `X-User-Id` / `X-User-Role` không kiểm duyệt.
- **Đứt gãy luồng thông điệp với Payment Service**: Lệch hoàn toàn hợp đồng sự kiện (`PaymentVerifiedNotificationEvent` 8 trường vs `PaymentSucceededV1` 18 trường của Payment Service), sai lệch MassTransit exchange/queue topology dẫn đến việc 100% tin nhắn thanh toán bị RabbitMQ drop hoặc đưa vào `_skipped`.
- **Cô lập Broker trong Docker Compose**: File `compose.app.yml` bị hardcode cố định `RabbitMQ__UseInMemory: "true"`, khiến dịch vụ chạy tách biệt hoàn toàn khỏi message bus chung của hệ sinh thái SmartParking.
- **Gãy vỡ vòng đời thông báo UI**: Entity `DurableNotification` thiếu hoàn toàn các thuộc tính trạng thái đã đọc (`is_read`, `read_at`) và tiêu đề/nội dung hiển thị (`title`, `message`), thiếu endpoint `PATCH /api/v1/notifications/{id}/read`, làm tê liệt giao diện chuông thông báo `NotificationBell.tsx` trên Frontend.
- **Lỗi ngụy tạo sức khỏe (False-Positive Health Check)**: Endpoint `/health` luôn trả về HTTP 200 OK `Healthy` ngay cả khi kết nối cơ sở dữ liệu PostgreSQL bị ngắt kết nối hoàn toàn.

---

### Bảng Điểm Đánh Giá Kiến Trúc (Architecture Scorecard)

| Trục Đánh Giá (Audit Dimension) | Trọng Số | Điểm Đạt Được | Trạng Thái | Nhận Định Cốt Lõi |
| :--- | :---: | :---: | :---: | :--- |
| **1. Tính Thuần Khiết Kiến Trúc (Architecture Purity)** | 25% | **9.2 / 10** | **ĐẠT XUẤT SẮC** | Domain POCO 100% thuần túy, 0 NuGet, Inward Dependency tuyệt đối, 0 IQueryable leaks, Ports & Adapters rõ ràng, phân lập DB chuẩn port 5445. |
| **2. Tính Sẵn Sàng Vận Hành (Production Readiness)** | 25% | **4.0 / 10** | **KHÔNG ĐẠT (BLOCKER)** | Thiếu xác thực JWT Bearer, cửa sau giả mạo header, health check false-positive, hardcode password, Npgsql thiếu transient retry. |
| **3. Tích Hợp Sự Kiện & Hàng Đợi (Event Integration)** | 25% | **3.0 / 10** | **KHÔNG ĐẠT (BLOCKER)** | Lệch chuẩn hợp đồng với Payment Service, MassTransit FQTN routing drop message, compose hardcode In-Memory broker, kênh PUSH gây crash runtime. |
| **4. Tuân Thủ SRS & Khớp Hợp Đồng UI (SRS/UI Alignment)** | 25% | **5.5 / 10** | **CẢNH BÁO (CRITICAL)** | Thiếu `is_read`, `read_at`, `title`, `message`, thiếu API đọc thông báo cho UI, thiếu 3/6 bảng baseline (support tickets, push tokens), thiếu Admin Audit Query. |
| **ĐIỂM TỔNG HỢP TOÀN DIỆN (WEIGHTED COMPOSITE)** | **100%** | **5.42 / 10** | **CẦN KHẮC PHỤC TRƯỚC KHI RELEASE** | **Cần thực thi triệt để Kế hoạch Khắc phục 5 Pha trước khi đưa vào vận hành.** |

---

<a name="phan-1-tham-dinh-tinh-san-sang-van-hanh-production"></a>
## Phần 1: Thẩm Định Tính Sẵn Sàng Vận Hành Production (Production Readiness Audit - R1)

### 1.1 Cấu Hình Containerization: `Dockerfile` & `.dockerignore`
- **Tập tin kiểm tra**: `docker/Dockerfile.backend` và `docker/Dockerfile.backend.dockerignore`.
- **Cơ chế đóng gói**:
  - Tận dụng quy trình build đa tầng (multi-stage build) dựa trên Microsoft SDK `mcr.microsoft.com/dotnet/sdk:10.0` cho khâu biên dịch và `mcr.microsoft.com/dotnet/aspnet:10.0` cho môi trường runtime.
  - Tối ưu hóa bộ nhớ đệm (cache layer) bằng cách sao chép và restore các file `.csproj` trước khi copy toàn bộ mã nguồn.
  - Biến môi trường và cổng mặc định được thiết lập: `ENV ASPNETCORE_URLS=http://+:8080`, `EXPOSE 8080`.
  - Tệp `.dockerignore` hoạt động hiệu quả, loại bỏ chính xác các thư mục nhị phân `bin/`, `obj/`, thư mục git, `node_modules`, và mã nguồn frontend.
- **Lỗ hổng bảo mật Container (Security Finding)**:
  - Dockerfile **không khai báo chỉ thị `USER app`** hoặc chuyển sang người dùng không có đặc quyền (unprivileged non-root user).
  - Kết quả: Khi container chạy trong Docker hoặc Kubernetes, tiến trình .NET API thực thi dưới quyền tối cao `root` (UID 0). Đây là vi phạm nghiêm trọng tiêu chuẩn bảo mật container (CIS Benchmark, Principle of Least Privilege), tạo nguy cơ bị khai thác chiếm quyền điều khiển máy chủ chủ (container breakout).

### 1.2 Cấu Hình `compose.app.yml` & Môi Trường Chạy
- **Tập tin kiểm tra**: `compose.app.yml` (dòng 113–131):
  ```yaml
  notification-service:
    build:
      context: .
      dockerfile: docker/Dockerfile.backend
      args:
        SERVICE: NotificationAuditService
    image: smartpark/notification-service:local
    environment:
      <<: *backend-env
      ConnectionStrings__Notification: "Host=notification-db;Port=5432;Database=smartpark_notification;Username=smartpark_notification;Password=${SMARTPARK_DEV_PASSWORD:?Set SMARTPARK_DEV_PASSWORD}"
      ConnectionStrings__NotificationAudit: "Host=notification-db;Port=5432;Database=smartpark_notification;Username=smartpark_notification;Password=${SMARTPARK_DEV_PASSWORD:?Set SMARTPARK_DEV_PASSWORD}"
      # Fallback to MassTransit InMemory when RabbitMQ broker is not present
      RabbitMQ__UseInMemory: "true"
    ports:
      - "127.0.0.1:5068:8080"
    networks:
      - smartpark
    healthcheck: *healthcheck
    restart: "no"
  ```
- **Các khiếm khuyết hạ tầng nghiêm trọng**:
  1. **Hardcode In-Memory Broker**: Thuộc tính `RabbitMQ__UseInMemory: "true"` bị gán cứng trong file compose chính. Khi triển khai container bằng Docker Compose, MassTransit khởi tạo một bus ảo trong RAM cục bộ. Dịch vụ hoàn toàn bị cô lập khỏi RabbitMQ broker dùng chung và không thể nhận bất kỳ sự kiện nào từ các service khác.
  2. **Thiếu biến môi trường RabbitMQ**: Không có khai báo các biến `RabbitMQ__Host`, `RabbitMQ__Port`, `RabbitMQ__VirtualHost`, `RabbitMQ__Username`, `RabbitMQ__Password`. Nếu gỡ bỏ `RabbitMQ__UseInMemory: "true"`, ứng dụng sẽ fallback về `localhost:5672` bên trong mạng nội bộ của container và lập tức ném ngoại lệ mất kết nối broker.
  3. **Thiếu định danh `container_name`**: Không khai báo `container_name: notification-service`, làm phát sinh tên container ngẫu nhiên do Docker gán, gây khó khăn cho việc cấu hình DNS nội bộ và logging.
  4. **Thiếu quan hệ phụ thuộc `depends_on`**: Không khai báo `depends_on: [notification-db]`, khiến container notification có thể khởi động trước khi cơ sở dữ liệu PostgreSQL sẵn sàng chấp nhận kết nối.
  5. **Chính sách khởi động lại không an toàn**: Khai báo `restart: "no"`. Trong môi trường staging/production, nếu container gặp sự cố tắt đột ngột (OOM hoặc transient failure), Docker engine sẽ không tự động phục hồi container.

### 1.3 Quản Lý Cấu Hình & Bí Mật (Secrets Management)
- **Tập tin kiểm tra**: `appsettings.json`, `PersistenceDependencyInjection.cs`, `RabbitMqOptions.cs`.
- **Rò rỉ thông tin xác thực (Hardcoded Credentials)**:
  - File `appsettings.json` (dòng 3–4) chứa mật khẩu cơ sở dữ liệu dạng plaintext: `"Username=postgres;Password=postgres"`.
  - File `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Persistence/PersistenceDependencyInjection.cs` (dòng 26–27) chứa fallback chuỗi kết nối hardcode trong mã C#:
    ```csharp
    connectionString = "Host=localhost;Port=5445;Database=smartpark_notification;Username=postgres;Password=postgres";
    ```
  - File `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Infrastructure/Options/RabbitMqOptions.cs` (dòng 10–11) hardcode thông tin xác thực mặc định của broker:
    ```csharp
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    ```
- **Thiếu hụt các phân vùng cấu hình quan trọng**:
  - **Không có cấu hình `Jwt`**: Khác với `UserService` và `PaymentService`, file `appsettings.json` không có schema `Jwt` (Issuer, Audience, SecretKey hoặc PublicKey).
  - **Không có cấu hình nhà cung cấp kênh chuyển phát**: Hoàn toàn thiếu các section cấu hình cho Email (SMTP Host, Port, Credentials, FromAddress), SMS (Provider API Key, Sender ID), hoặc Push (Firebase Server Key / FCM Credentials).
  - **Lệch pha trong POCO Options**: Class `RabbitMqOptions` không khai báo thuộc tính `public bool UseInMemory { get; set; }`, buộc tầng `InfrastructureDependencyInjection.cs` phải gọi đọc untyped `configuration.GetValue<bool>("RabbitMQ:UseInMemory")`.

### 1.4 Xác Thực & Phân Quyền (Authentication & Authorization)
- **Tập tin kiểm tra**: `Program.cs`, `NotificationsController.cs`, `CurrentUserService.cs`.
- **Thiếu hoàn toàn Middleware xác thực trong `Program.cs`**:
  - Không gọi `builder.Services.AddAuthentication(...)`.
  - Không gọi `builder.Services.AddAuthorization()`.
  - Pipeline HTTP không kích hoạt `app.UseAuthentication()` và `app.UseAuthorization()`.
- **Thiếu thuộc tính bảo vệ trên API Controller**:
  - File `NotificationsController.cs` không gắn bất kỳ thuộc tính `[Authorize]` nào ở mức Class lẫn mức Action. Mọi endpoint đều mở công khai (Anonymous Access).
- **Lỗ hổng Cửa sau Giả mạo Danh tính (Insecure Identity Spoofing)**:
  - File `CurrentUserService.cs` (dòng 32–71):
    ```csharp
    // Dev / testing fallback header
    var headerUserId = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(headerUserId) && Guid.TryParse(headerUserId, out var fallbackParsed))
    {
        return fallbackParsed;
    }
    ...
    var headerRole = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Role"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(headerRole))
    {
        return headerRole;
    }
    ...
    public bool IsAuthenticated => UserId.HasValue;
    ```
  - **Phân tích tác động an ninh**:
    1. Bất kỳ đối tượng bên ngoài nào cũng có thể vượt qua toàn bộ cơ chế bảo mật bằng cách gửi header `X-User-Id: <GUID-nạn-nhân>` và `X-User-Role: Admin`. Hệ thống chấp nhận đây là danh tính đã được xác thực hợp lệ (`IsAuthenticated = true`).
    2. Logic kiểm tra này **không được bọc bởi điều kiện môi trường `Environment.IsDevelopment()`**, dẫn tới việc cửa sau này tồn tại trên cả môi trường Production.
    3. Token JWT Bearer thật (`Authorization: Bearer <token>`) do UserService cấp phát hoàn toàn bị bỏ qua và không được phân giải.

### 1.5 Xử Lý Lỗi Toàn Cục & Ghi Nhật Ký (Global Error Handling & Logging)
- **Tập tin kiểm tra**: `ExceptionHandlingMiddleware.cs`, `LoggingBehavior.cs`.
- **Đánh giá Middleware xử lý ngoại lệ**:
  - Middleware được kích hoạt đúng vị trí tại đầu pipeline (`app.UseMiddleware<ExceptionHandlingMiddleware>()`).
  - Ánh xạ đúng các mã lỗi nghiệp vụ: `ValidationException` (400), `NotificationDomainException` (400), `NotificationForbiddenException` (403), `KeyNotFoundException` (404), `UnauthorizedAccessException` (401), và lỗi mặc định (500).
  - **Điểm yếu chuẩn hóa**: Phản hồi lỗi sử dụng cấu trúc JSON tự tạo (`{ success, errorCode, message, traceId, errors, timestamp }`) thay vì tuân thủ định dạng chuẩn công nghiệp **RFC 7807 `ProblemDetails`** (`type`, `title`, `status`, `detail`, `instance`).
  - **Thiếu Status Code Pages**: Không cấu hình `app.UseStatusCodePages()`. Các yêu cầu gửi tới endpoint không tồn tại (404) hoặc sai phương thức HTTP (405) sẽ bỏ qua middleware và trả về trang trắng hoặc HTML mặc định của ASP.NET.
- **Hệ thống Ghi nhật ký (Logging)**:
  - Chưa cài đặt Serilog: Gói `SmartParking.NotificationAuditService.API.csproj` không có Serilog, chỉ sử dụng Console Logger mặc định của .NET.
  - MediatR Pipeline Behavior (`LoggingBehavior.cs`) chỉ log chuỗi cơ bản: `Handling request {RequestName}`.
  - Thiếu toàn bộ các bộ làm giàu ngữ cảnh (Context Enrichers): không ghi nhận `CorrelationId`, `RequestPath`, `HttpMethod`, `ClientIp`, hay `UserId` trong log context, cản trở nghiêm trọng việc truy vết phân tán (Distributed Tracing).

### 1.6 Khả Năng Phục Hồi (Resilience) & Lỗi Ngụy Tạo Sức Khỏe (Health Check)
- **Tập tin kiểm tra**: `Program.cs` (dòng 57–80), `PersistenceDependencyInjection.cs`, `InfrastructureDependencyInjection.cs`.
- **Lỗi ngụy tạo sức khỏe nghiêm trọng (False-Positive Health Check)**:
  ```csharp
  app.MapGet("/health", async (NotificationAuditDbContext dbContext) =>
  {
      var dbConnected = false;
      try { dbConnected = await dbContext.Database.CanConnectAsync(); }
      catch { dbConnected = false; }

      return Results.Ok(new
      {
          status = "Healthy",
          service = "SmartParking.NotificationAuditService",
          timestamp = DateTimeOffset.UtcNow,
          dependencies = new
          {
              database = dbConnected ? "Connected" : "Disconnected",
              messaging = "MassTransit.RabbitMQ Configured"
          }
      });
  });
  ```
  - **Hậu quả vận hành**: Khi cơ sở dữ liệu chết hoặc không thể kết nối, biến `dbConnected` bằng `false`, nhưng endpoint **vẫn trả về HTTP 200 OK (`Results.Ok`) với `status = "Healthy"`**!
  - Docker Compose healthcheck (`curl -fsS http://127.0.0.1:8080/health || exit 1`) và Kubernetes Liveness/Readiness Probes đánh giá sức khỏe dựa trên mã HTTP status code. Vì luôn nhận được HTTP 200, hệ thống điều phối container sẽ nhận định sai rằng container hoàn toàn khỏe mạnh và tiếp tục điều hướng traffic vào một service đang bị liệt database!
  - Thuộc tính `messaging` bị gán cứng `"MassTransit.RabbitMQ Configured"` mà không hề kiểm tra trạng thái thực tế của Bus (kể cả khi bus chạy In-Memory hoặc mất kết nối broker).
- **Khả năng tự phục hồi kết nối (Connection Resilience)**:
  - Tầng Persistence (`PersistenceDependencyInjection.cs:29`): Sử dụng `options.UseNpgsql(connectionString)` nhưng **hoàn toàn không bật `EnableRetryOnFailure()`**. Mọi sự cố gián đoạn mạng tạm thời (transient connection blip) trong quá trình khởi động PostgreSQL sẽ làm ứng dụng sập ngay lập tức thay vì tự động thử lại.
  - Cấu hình thử lại MassTransit (`InfrastructureDependencyInjection.cs:55`): Sử dụng `cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)))`. Đây là thử lại tĩnh cố định (3 lần @ 1s), thiếu cơ chế giãn cách lũy thừa (**Exponential Backoff**), thiếu jitter chống bão yêu cầu (thundering herd), và không có Circuit Breaker.

---

<a name="phan-2-tham-dinh-lien-ket--tich-hop-su-kien"></a>
## Phần 2: Thẩm Định Liên Kết & Tích Hợp Sự Kiện (Event Integration Audit - R2)

### 2.1 Bảng Ma Trận Đối Soát Sự Kiện Toàn Diện (Event Reconciliation Matrix)

Bảng dưới đây đối chiếu chi tiết giữa sự kiện thực tế do Payment Service phát tán trên nhánh `feat/payment-service` với hiện trạng triển khai consumer trong Notification Service:

| Tiêu Chí Kỹ Thuật | Phía Phát Tán (Publishing Service): Payment Service | Phía Tiêu Thụ Hiện Tại (Consumer): Notification Service | Đánh Giá Tương Thích & Hệ Quả |
| :--- | :--- | :--- | :--- |
| **Tên Sự Kiện (Event Name)** | `PaymentSucceededV1` | `PaymentVerifiedNotificationEvent` | **BẤT TƯƠNG THÍCH (MISMATCH)**: Tên sự kiện sai lệch hoàn toàn. |
| **Namespace Khai Báo** | `SmartParking.PaymentService.Domain.Events` | `SmartParking.NotificationAuditService.Application.Contracts.Notifications` | **LỆCH FQTN**: MassTransit tự động tạo exchange theo Fully Qualified Type Name, dẫn tới 2 exchange riêng biệt. |
| **Exchange & Định Tuyến Broker** | Topic Exchange: `payment.payment-succeeded` hoặc exchange mang tên namespace của Payment Service | Queue `payment-notification` chỉ bind tới exchange `...Application.Contracts.Notifications:PaymentVerifiedNotificationEvent` | **MẤT TIN NHẮN (MESSAGE DROP)**: RabbitMQ không tìm thấy binding giữa topic của Payment Service với queue của Notification Service. |
| **Số Lượng Trường Dữ Liệu (Payload Schema)** | **18 trường nghiệp vụ hoàn chỉnh**: `EventId`, `OccurredAt`, `CorrelationId`, `PaymentOrderId`, `PaymentTransactionId`, `PaymentMethod`, `GatewayReference`, `TenantId`, `SiteId`, `ReferenceType`, `ReferenceId`, `PlateNumber`, `AccountId`, `Amount`, `ParkingFee`, `PenaltyFee`, `PenaltyReason`, `Currency`. | **8 trường cơ bản**: `EventId`, `PaymentOrderId`, `DriverId`, `Amount`, `Currency`, `PaymentMethod`, `TransactionId`, `OccurredAt`. | **THIẾU TRƯỜNG & LỆCH KIỂU**: Lệch `AccountId?` (Guid nullable) vs `DriverId` (Guid required); lệch `PaymentTransactionId` (Guid) vs `TransactionId` (string); thiếu `PlateNumber`, `PenaltyFee`, `TenantId`, `SiteId`. |
| **Định Danh Truy Vết (Correlation ID)** | Có trường `CorrelationId` chuẩn chuỗi. | Thiếu thuộc tính `CorrelationId` trong hợp đồng DTO. | Đứt gãy khả năng liên kết nhật ký phân tán khi điều tra sự cố. |
| **Khử Trùng Lặp (Idempotency Key)** | Quản lý bởi Transactional Outbox tại Payment Service. | Khóa tạo động: `PAY_{EventId}_{DriverId}` tra cứu qua `GetByIdempotencyKeyAsync`. | Lỗ hổng tương tranh: Dễ phát sinh `DbUpdateException` khi có 2 message trùng lặp đến đồng thời. |
| **Chính Sách Thử Lại (Retry Policy)** | Outbox Dispatcher có chính sách backoff. | Thử lại tĩnh: 3 lần @ 1s (`cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)))`). | Thiếu Exponential Backoff (2s, 5s, 10s) theo chuẩn blueprint của hệ thống. |
| **Dead-Letter Queue (DLQ) & Cảnh Báo** | Tích hợp fault queue. | Tự động chuyển vào queue `payment-notification_error`. | Không có `Fault<T>` Consumer để ghi log lỗi mức CRITICAL hoặc cảnh báo quản trị viên. |

---

### 2.2 Phân Tích Đứt Gãy Luồng Thông Điệp Thanh Toán
Tại sao thông báo thanh toán thành công không bao giờ đến được tay người dùng khi tích hợp thực tế?

```
[Payment Service] 
   │  Phát tán Outbox: SmartParking.PaymentService.Domain.Events.PaymentSucceededV1
   ▼
[RabbitMQ Exchange: SmartParking.PaymentService.Domain.Events:PaymentSucceededV1] 
hoặc [Exchange: payment.payment-succeeded]
   │
   ├── (X) KHÔNG CÓ BINDING NÀO TỚI QUEUE 'payment-notification'
   │        (Vì Notification Service chỉ khai báo Consumer<PaymentVerifiedNotificationEvent>
   │         nên MassTransit chỉ bind queue với Exchange mang FQTN của Notification Service)
   ▼
[RabbitMQ Drop Message] ➔ Tin nhắn bị vứt bỏ hoàn toàn trong broker!
   │
   ▼ (Kịch bản giả định nếu ép liên kết thủ công)
[Header MT-MessageType không khớp] ➔ MassTransit đẩy thẳng vào queue 'payment-notification_skipped'
   │
   ▼ (Kịch bản giả định nếu dùng Raw JSON Deserializer)
[Lệch schema: AccountId (nullable) vs DriverId (required)] ➔ Deserialization Exception ➔ Queue '_error'
```

**Kết luận thực tế**: Toàn bộ chuỗi thông điệp bị tê liệt 100%. Tài xế thanh toán tiền đỗ xe qua payOS hoặc quầy thu ngân sẽ không bao giờ nhận được biên lai thanh toán số hay thông báo xác nhận thành công.

---

### 2.3 Hiện Trạng Các Consumer Mồ Côi & Các Sự Kiện SRS Còn Thiếu
1. **Các Consumer Mồ Côi (Orphaned Consumers)**:
   - `ReservationNotificationConsumer`: Đang lắng nghe `ReservationConfirmedNotificationEvent`. Tuy nhiên, trong toàn bộ mã nguồn hiện tại của dự án, `ReservationService` hay `OperationService` chưa hề được tích hợp MassTransit Publisher để phát tán sự kiện này.
   - `AuditProjectionConsumer`: Đang lắng nghe `AuditProjectionEvent`. Ngoại trừ unit test nội bộ của Notification Service, không có microservice nào khác (`UserService`, `FacilityService`, `PaymentService`) phát tán sự kiện theo cấu trúc này.
2. **Các sự kiện nghiệp vụ theo SRS Module 08 còn thiếu hoàn toàn Consumer**:
   - `ReservationCancelled` (Hủy đặt chỗ): Chưa có consumer xử lý.
   - `ReservationExpired` (Hết hạn giữ chỗ): Chưa có consumer xử lý.
   - `VehicleEntered` / `VehicleDeparted` (Xe vào/ra bãi): Chưa có consumer xử lý gửi thông báo xác nhận vào/ra.
   - `OverstayDetected` (Cảnh báo đỗ quá giờ): Chưa có consumer tính toán cảnh báo phạt và nhắc nhở tài xế.
   - `RefundCompleted` (Hoàn tiền hoàn tất): Chưa có consumer gửi biên lai hoàn tiền.
   - `MonthlyPassExpiring` (Vé tháng sắp hết hạn): Chưa có consumer gửi thông báo nhắc hạn trước 7 ngày và 1 ngày theo yêu cầu FR-BAS-06.

---

### 2.4 Khử Trùng Lặp (Idempotency), Transactional Inbox & Nguy Cơ Race Condition
- **Hiện trạng xử lý**:
  - Tại `ProcessPaymentNotificationCommandHandler.cs` (dòng 36–48), hệ thống kiểm tra khóa lũy thừa:
    `idempotencyKey = $"PAY_{request.EventId}_{request.DriverId}"`.
  - Kiểm tra tồn tại qua repository: `await _notificationRepository.GetByIdempotencyKeyAsync(idempotencyKey)`. Nếu đã có, trả về `NotificationDeliveryResult.Duplicate(existing.Id)`.
  - Trên bảng cơ sở dữ liệu `durable_notifications`, cột `idempotency_key` có ràng buộc Unique Index (`DurableNotificationConfiguration.cs:92-93`).
- **Lỗ hổng tương tranh (Concurrency Race Condition)**:
  - Nếu broker RabbitMQ redeliver 2 bản tin trùng lặp song song cùng mili-giây (do timeout mạng hoặc parallel consumers), cả hai luồng đồng thời kiểm tra `GetByIdempotencyKeyAsync` và đều nhận kết quả `null`.
  - Cả hai luồng cùng gọi `DurableNotification.Create(...)` và nạp vào DbContext.
  - Luồng hoàn tất sau khi gọi `_unitOfWork.SaveChangesAsync` sẽ bị PostgreSQL chặn lại và ném ra ngoại lệ **`DbUpdateException`** (Mã lỗi Postgres 23505 - Unique Violation).
  - Khối code hiện tại **không bọc `try-catch (DbUpdateException)`** để chuyển đổi êm thuận thành kết quả Duplicate, khiến exception bung ra ngoài, kích hoạt MassTransit retry liên tục 3 lần rồi ném message vào Dead-Letter Queue một cách không cần thiết.
- **Thiếu Transactional Inbox Pattern**:
  - Service chưa kích hoạt `x.AddEntityFrameworkOutbox<NotificationAuditDbContext>()`.
  - Việc nhận tin nhắn và xử lý lưu vết chưa được đồng bộ nguyên tử ở tầng transport của MassTransit, làm tăng nguy cơ xử lý lại (at-least-once delivery duplicates) khi ứng dụng restart giữa chừng.

---

### 2.5 Các Kênh Chuyển Phát Hạ Nguồn (Delivery Channels) & Lỗi Crash Kênh PUSH
- **Định nghĩa kênh**: Enum `NotificationChannel` bao gồm 4 giá trị: `IN_APP`, `EMAIL`, `SMS`, `PUSH`.
- **Hiện trạng triển khai**:
  - `InAppDeliveryAdapter`: Chỉ là stub console log dòng `[IN_APP_FIXTURE] In-app notification...`. Chưa có SignalR Hub hay WebSocket endpoint kết nối Frontend.
  - `LocalEmailCaptureAdapter`: Chỉ là stub console log dòng `[LOCAL_EMAIL_CAPTURE_FIXTURE]...`. Chưa có SMTP client hay SendGrid SDK.
  - `LocalSmsFixtureAdapter`: Chỉ là stub console log dòng `[LOCAL_SMS_FIXTURE]...`. Chưa tích hợp SMS Gateway (Twilio/eSMS).
  - **Lỗi Crash Kênh `PUSH` (Runtime Crash)**:
    - Trong `InfrastructureDependencyInjection.cs` (dòng 20–23), hệ thống đăng ký các adapter vào DI container:
      ```csharp
      services.AddScoped<INotificationChannelAdapter, InAppDeliveryAdapter>();
      services.AddScoped<INotificationChannelAdapter, LocalEmailCaptureAdapter>();
      services.AddScoped<INotificationChannelAdapter, LocalSmsFixtureAdapter>();
      ```
    - **Hoàn toàn không có Adapter nào cho kênh `PUSH`!**
    - Khi một thông báo yêu cầu chuyển phát qua `PUSH`, class điều phối `NotificationDispatcher.cs` (dòng 42–56) tìm kiếm adapter tương ứng trong danh sách `IEnumerable<INotificationChannelAdapter>`. Khi không tìm thấy, hệ thống trả về lỗi nghiêm trọng: `"No registered delivery adapter found for channel PUSH"`.
    - Điều này khiến toàn bộ luồng gửi thông báo PUSH tới ứng dụng di động của tài xế bị đánh dấu thất bại (`FAILED` / `DEAD_LETTER`).

---

<a name="phan-3-tham-dinh-dinh-huong-kien-truc--tuan-thu-srs"></a>
## Phần 3: Thẩm Định Định Hướng Kiến Trúc & Tuân Thủ SRS (Architectural Alignment & SRS Audit - R3)

### 3.1 Đánh Giá Clean Architecture & Quy Chuẩn Mã Nguồn
Đối chiếu mã nguồn với tài liệu quy chuẩn `docs/02-architecture/source-code-architecture-rules.md`:

```
SmartParking.NotificationAuditService.API (Composition Root, Thin Controllers)
  ├── ⬇ depends on
SmartParking.NotificationAuditService.Application (CQRS Commands, Queries, Ports, Validators)
  ├── ⬇ depends on
SmartParking.NotificationAuditService.Domain (Pure POCO Entities, Value Objects, Domain Exceptions)
  ⬆ implements ports (Application Interfaces)
SmartParking.NotificationAuditService.Persistence (EF Core DbContext, Repositories, Configurations)
  ⬆ implements ports (Application Interfaces)
SmartParking.NotificationAuditService.Infrastructure (MassTransit Consumers, Delivery Adapters)
```

1. **Tính thuần túy của tầng Domain (Domain POCO Purity)**:
   - File `SmartParking.NotificationAuditService.Domain.csproj` chứa **0 `<PackageReference>`** và **0 `<ProjectReference>`**. Hoàn toàn không phụ thuộc vào EF Core, MassTransit hay thư viện JSON của bên thứ ba.
   - Các Entity (`DurableNotification`, `CentralAuditProjection`, `NotificationTemplate`) tuân thủ tính đóng gói cao: private setters, hàm tạo không tham số protected cho EF Core, và khởi tạo thông qua Factory Method tĩnh `Create(...)` có kiểm tra bất biến nghiệp vụ.
   - Điểm đánh giá: **9.5 / 10**.
2. **Quy tắc phụ thuộc hướng tâm (Inward Dependency Rule)**:
   - Phân tích tĩnh cho thấy các tầng phụ thuộc hoàn toàn hướng vào trong. Không có bất kỳ câu lệnh `using` trái phép nào (không có Persistence/Infrastructure trong Domain/Application).
   - Điểm đánh giá: **10 / 10**.
3. **Mô hình Cổng & Bộ Thích Ứng (Ports & Adapters)**:
   - Cổng giao tiếp (Ports) được đặt chuẩn xác tại `Application/Common/Interfaces/Ports/` (`INotificationChannelAdapter`, `INotificationDispatcher`) và `Persistence/` (`INotificationRepository`, `IUnitOfWork`).
   - Bộ thích ứng (Adapters) nằm tại Persistence và Infrastructure.
   - Điểm đánh giá: **9.0 / 10** (Trừ 1.0 điểm do thiếu PUSH adapter).
4. **Kiểm soát rò rỉ nhà cung cấp truy vấn (Zero IQueryable Leaks)**:
   - Quét toàn bộ repository interfaces và implementations: **Tuyệt đối không có phương thức nào trả về `IQueryable<T>`**.
   - Toàn bộ kết quả truy vấn đều được đóng gói thành `Task<T?>` hoặc `Task<IReadOnlyList<T>>` đã được hiện thực hóa dữ liệu (materialized data) trong tầng Persistence.
   - Điểm đánh giá: **10 / 10**.
5. **Tính độc lập của Controller (Thin Controllers)**:
   - `NotificationsController.cs` hoàn toàn đóng vai trò mỏng: chỉ inject `IMediator` và `ICurrentUserService`, chuyển tiếp lệnh tới CQRS handlers, không chứa logic nghiệp vụ hay truy cập DbContext trực tiếp.
   - Điểm đánh giá: **7.5 / 10** (Trừ 2.5 điểm do lỗ hổng bảo mật và thiếu thuộc tính phân quyền).
6. **Ranh giới Microservice & Độc lập Cơ sở Dữ liệu**:
   - Dịch vụ sở hữu cơ sở dữ liệu logic riêng biệt `smartpark_notification` trên cổng 5445.
   - Tuyệt đối không có truy vấn chéo DB (cross-database join) hoặc khóa ngoại (foreign key) sang các cơ sở dữ liệu `UserDb`, `FacilityDb`, `OperationsDb`, hay `PaymentDb`. Các tham chiếu người dùng/tài nguyên đều là UUID thuần túy.
   - Điểm đánh giá: **9.5 / 10**.

---

### 3.2 Ma Trận Đáp Ứng Yêu Cầu Chức Năng SRS Module 08

| Mã Yêu Cầu SRS | Mô Tả Yêu Cầu Theo Tài Liệu SRS | Hiện Trạng Triển Khai Trong Mã Nguồn | Mức Độ Tuân Thủ | Sai Lệch & Khiếm Khuyết Chi Tiết |
| :--- | :--- | :--- | :---: | :--- |
| **FR-RPT-01** | Chuyển phát thông báo theo các sự kiện nghiệp vụ thẩm quyền (thanh toán, đặt chỗ, hoàn tiền, vi phạm, đánh giá). Thất bại chuyển phát không được làm rollback sự kiện gốc. | `ProcessPaymentNotificationCommandHandler.cs:81-98`, `ProcessReservationNotificationCommandHandler.cs:82-99` | **ĐẠT MỘT PHẦN (PARTIAL)** | Đạt: Cơ chế bọc try-catch không làm ném lỗi rollback. Chưa đạt: Thiếu toàn bộ các sự kiện quá giờ, hủy chỗ, hoàn tiền, khóa tài khoản. |
| **FR-BAS-06** | Hỗ trợ đa kênh chuyển phát (IN_APP, EMAIL, SMS, PUSH). Chế độ kiểm thử có bộ thu Email cục bộ và SMS fixture. Khử trùng lặp qua idempotency. | `InAppDeliveryAdapter.cs`, `LocalEmailCaptureAdapter.cs`, `LocalSmsFixtureAdapter.cs`, `NotificationDispatcher.cs:42-56` | **ĐẠT MỘT PHẦN (PARTIAL)** | Đạt: Đã có fixture cho Email, SMS, In-App; có khóa idempotency. Chưa đạt: Kênh PUSH không có adapter gây văng lỗi runtime khi dispatch. |
| **FR-RPT-03** | Tiếp nhận chiếu dữ liệu kiểm toán tập trung từ các microservices. Ghi bất biến (append-only), lưu trữ tuân thủ quy chuẩn 7 năm. | `RecordAuditProjectionCommandHandler.cs:27-65`, `CentralAuditProjectionConfiguration.cs:11-75` | **ĐẠT (MET)** | Entity `CentralAuditProjection` không có phương thức update, lưu trữ append-only, đảm bảo tính toàn vẹn dữ liệu kiểm toán. |
| **FR-RPT-04** | Cung cấp giao diện/API tra cứu nhật ký kiểm toán cho Quản trị viên (Admin Audit Search) lọc theo tác nhân, thời gian, bãi đỗ, hành động. | *Chưa hiện thực trong Application và API* | **CHƯA ĐẠT (NOT MET)** | Hoàn toàn chưa có Query handler hay API endpoint nào cho phép Admin tìm kiếm dữ liệu từ bảng `central_audit_projections`. |
| **FR-RPT-05 / SEC-02** | Mô-đun tiếp nhận yêu cầu hỗ trợ chung và khôi phục tài khoản (`support_tickets`, `support_ticket_messages`). | *Chưa hiện thực trong Domain, Persistence và API* | **CHƯA ĐẠT (NOT MET)** | Chưa triển khai thực thể và API xử lý ticket hỗ trợ dù đã có trong đặc tả kiến trúc database baseline. |
| **FR-BAS-07** | Tương tác chuông thông báo trong ứng dụng: xem danh sách thông báo, đếm số lượng chưa đọc, đánh dấu đã đọc, đẩy thông báo thời gian thực. | `NotificationsController.cs:25-49` (`GetNotifications`) | **ĐẠT MỘT PHẦN (PARTIAL)** | Đạt: Có API xem danh sách. Chưa đạt: Không có trường `is_read`, không có API đánh dấu đã đọc, không có push SignalR thời gian thực. |

---

### 3.3 Thiếu Sót Mô Hình Dữ Liệu & EF Core Migrations
- **Đối chiếu với DDL chuẩn (`scripts/database/microservices/05-notification-audit-service-db.sql`)**:
  - DDL chuẩn quy định 6 bảng:
    1. `notification_templates`: Đã triển khai trong `NotificationAuditDbContext`.
    2. `durable_notifications`: Đã triển khai trong `NotificationAuditDbContext`.
    3. `central_audit_projections`: Đã triển khai trong `NotificationAuditDbContext`.
    4. `push_device_tokens`: **CHƯA TRIỂN KHAI** (Bảng lưu token thiết bị FCM/APNS của người dùng).
    5. `support_tickets`: **CHƯA TRIỂN KHAI** (Bảng lưu khiếu nại và yêu cầu khôi phục tài khoản).
    6. `support_ticket_messages`: **CHƯA TRIỂN KHAI** (Bảng lưu trao đổi tin nhắn giữa tài xế và CSKH).
  - Tỷ lệ hoàn thành mô hình dữ liệu: **3 / 6 bảng (50%)**.
- **Thiếu thư mục EF Core Migrations**:
  - Thư mục `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Persistence/` **hoàn toàn không có thư mục `Migrations/`**.
  - Dịch vụ không thể tự động khởi tạo hoặc nâng cấp lược đồ cơ sở dữ liệu khi triển khai môi trường mới qua CI/CD, phụ thuộc hoàn toàn vào script SQL thủ công bên ngoài.

---

### 3.4 Lệch Pha Nghiêm Trọng Với Hợp Đồng Frontend UI (`NotificationBell.tsx`)
Đối chiếu mã nguồn frontend `FE/src/components/navigation/NotificationBell.tsx`, `FE/src/lib/types.ts` và `FE/src/lib/operatorApi.ts`:

1. **Hợp đồng giao diện Frontend yêu cầu**:
   ```typescript
   export interface AppNotification {
     id: string;
     recipientId: string;
     recipientRole: Role;
     type: NotificationType;
     title: string;        // BẮT BUỘC: Tiêu đề hiển thị cho người dùng
     message: string;      // BẮT BUỘC: Nội dung thông báo hiển thị
     timestamp: string;
     isRead: boolean;      // BẮT BUỘC: Trạng thái đã đọc hay chưa
     relatedEntityId?: string;
   }
   ```
   Các API gọi từ Frontend:
   - `GET /api/v1/notifications` ➔ Lấy danh sách thông báo kèm cờ `isRead`.
   - `PATCH /api/v1/notifications/{id}/read` ➔ Đánh dấu một thông báo đã đọc.
   - `POST /api/v1/notifications/mark-all-read` ➔ Đánh dấu tất cả thông báo đã đọc để tắt chấm đỏ badge.

2. **Thực tế Backend trả về (`NotificationDto.cs`)**:
   - Backend chỉ trả về: `id`, `originatingEvent`, `recipientId`, `channel`, `templateCode`, `payload` (chuỗi JSON thô dạng `{"amount": 50000, ...}`), `status`, `attempts`, `maxAttempts`, `lastError`, `scheduledAt`, `deliveredAt`, `createdAt`, `updatedAt`.
   - **Các điểm gãy vỡ hợp đồng**:
     * **Thiếu `title` và `message`**: Người dùng mở chuông thông báo sẽ không thấy câu chữ rõ ràng mà nhận payload JSON thô hoặc trống chữ.
     * **Thiếu `is_read` và `read_at`**: Entity `DurableNotification` chỉ theo dõi trạng thái chuyển phát của hệ thống (`PENDING`, `DELIVERED`, `FAILED`), không hề có trạng thái đọc của người dùng.
     * **Thiếu toàn bộ các Endpoint tương tác**: Hoàn toàn không có API `PATCH /api/v1/notifications/{id}/read` hay `POST /api/v1/notifications/mark-all-read`.
     * **Sai tiền tố Route**: Controller khai báo `[Route("api/notifications")]` thay vì chuẩn RESTful `[Route("api/v1/notifications")]`.
   - **Hệ quả**: Giao diện chuông thông báo trên Web/Mobile luôn hiển thị số lượng badge chưa đọc sai lệch và tài xế bấm vào thông báo không thể làm mất chấm đỏ.

---

<a name="phan-4-so-dang-ky-rui-ro--phan-loai-muc-do-nghiem-trong"></a>
## Phần 4: Sổ Đăng Ký Rủi Ro & Phân Loại Mức Độ Nghiêm Trọng (Risk Register)

Tất cả các phát hiện trong đợt thẩm định được tổng hợp, phân loại theo thang mức độ nghiêm trọng chuẩn quốc tế (BLOCKER, CRITICAL, MAJOR, MINOR) kèm theo dẫn chứng tệp mã nguồn và tác động vận hành:

| Mã Rủi Ro | Mức Độ | Phân Loại | Mô Tả Chi Tiết Khiếm Khuyết | Tác Động Nghiệp Vụ & Kỹ Thuật | Dẫn Chứng Mã Nguồn (File Citation) |
| :--- | :---: | :---: | :--- | :--- | :--- |
| **RSK-BLK-01** | **BLOCKER** | Bảo mật / Xác thực | Thiếu hoàn toàn middleware xác thực JWT Bearer trong `Program.cs`, thiếu `[Authorize]` trên controller, và cửa sau `CurrentUserService` chấp nhận header giả mạo `X-User-Id` / `X-User-Role` trong mọi môi trường. | Người dùng bên ngoài có thể mạo danh bất kỳ tài khoản hoặc Admin nào; các token JWT thật từ UserService bị loại bỏ; từ chối truy cập trên production. | `Program.cs:11-87`<br>`CurrentUserService.cs:32-71`<br>`NotificationsController.cs:11-101` |
| **RSK-BLK-02** | **BLOCKER** | Tích hợp Sự kiện | Lệch hoàn toàn hợp đồng sự kiện thanh toán giữa Payment Service (`PaymentSucceededV1`) và Notification Service (`PaymentVerifiedNotificationEvent`) về tên, namespace, và cấu trúc payload (18 fields vs 8 fields). | 100% tin nhắn thanh toán thành công bị RabbitMQ loại bỏ hoặc chuyển vào `_skipped`; tài xế không nhận được biên lai thanh toán. | `PaymentNotificationConsumer.cs:9`<br>`PaymentVerifiedNotificationEvent.cs:1-13`<br>`PaymentSucceededV1.cs:16-43` |
| **RSK-BLK-03** | **BLOCKER** | Hạ tầng / Container | Cấu hình `compose.app.yml` bị hardcode cố định `RabbitMQ__UseInMemory: "true"` và thiếu các biến môi trường kết nối broker thực tế. | Notification Service chạy trên bus RAM ảo, hoàn toàn bị cô lập khỏi hệ sinh thái microservices trong Docker Compose. | `compose.app.yml:124-125`<br>`RabbitMqOptions.cs:1-13`<br>`InfrastructureDependencyInjection.cs:42-60` |
| **RSK-BLK-04** | **BLOCKER** | Cấu hình Broker | Sai lệch MassTransit Topology so với bản thiết kế hạ nguồn (Downstream Blueprint): queue `payment-notification` không bind vào exchange `payment.payment-succeeded` hoặc exchange FQTN của Payment Service. | Hàng đợi không nhận được dữ liệu từ Payment Service dù có chung broker; cấu hình retry tĩnh 1s vi phạm quy chuẩn. | `InfrastructureDependencyInjection.cs:27-60`<br>`04-downstream-payment-succeeded-consumer-blueprint.md` |
| **RSK-CRT-01** | **CRITICAL** | Giám sát / Sức khỏe | Endpoint `/health` trả về HTTP 200 OK `Healthy` ngay cả khi kết nối PostgreSQL thất bại (`dbConnected == false`); phụ thuộc messaging bị hardcode text. | Orchestrator (Docker/K8s) điều hướng traffic vào container đang bị liệt database do đánh giá sai tình trạng sức khỏe. | `Program.cs:57-80`<br>`compose.app.yml:75` |
| **RSK-CRT-02** | **CRITICAL** | Mô hình Dữ liệu / UI | `DurableNotification` thiếu thuộc tính `is_read`, `read_at`, `title`, `message`; thiếu các API đánh dấu đã đọc (`PATCH .../read`, `POST .../mark-all-read`). | Làm tê liệt giao diện chuông thông báo `NotificationBell.tsx`; không thể xóa chấm đỏ thông báo; hiển thị payload JSON thô cho người dùng. | `DurableNotification.cs:7-24`<br>`NotificationBell.tsx:25-70`<br>`NotificationsController.cs:25-49` |
| **RSK-CRT-03** | **CRITICAL** | Quản lý Bí mật | Hardcode mật khẩu cơ sở dữ liệu (`postgres:postgres`) và broker (`guest:guest`) dạng plaintext trong `appsettings.json` và mã nguồn C#. | Nguy cơ lộ lọt thông tin xác thực; vi phạm chính sách bảo mật cấu hình môi trường dự án. | `appsettings.json:3-4, 10-11`<br>`PersistenceDependencyInjection.cs:26-27`<br>`RabbitMqOptions.cs:10-11` |
| **RSK-CRT-04** | **CRITICAL** | Chuyển phát Kênh | Enum `NotificationChannel` có định nghĩa `PUSH` nhưng DI container không đăng ký bất kỳ Adapter nào cho kênh này. | Dispatcher ném lỗi thất bại runtime ngay khi xử lý thông báo PUSH tới ứng dụng di động của tài xế. | `NotificationDispatcher.cs:42-56`<br>`InfrastructureDependencyInjection.cs:20-23` |
| **RSK-MAJ-01** | **MAJOR** | Khả năng Phục hồi | Cấu hình EF Core Npgsql thiếu `EnableRetryOnFailure`; MassTransit thiếu chính sách thử lại lũy thừa (Exponential Backoff) và Circuit Breaker. | Ứng dụng sập ngay lập tức khi mạng gặp sự cố tạm thời (transient connection drops); dễ bị bão tin nhắn khi broker khởi động lại. | `PersistenceDependencyInjection.cs:29`<br>`InfrastructureDependencyInjection.cs:55` |
| **RSK-MAJ-02** | **MAJOR** | Tương tranh / Outbox | Thiếu MassTransit Transactional Inbox (`AddEntityFrameworkOutbox`) và nguy cơ bung lỗi `DbUpdateException` (Postgres 23505) khi có 2 tin nhắn trùng đến đồng thời. | Tin nhắn trùng lặp bị ném lỗi bung ra ngoài thay vì xử lý êm thuận thành kết quả Duplicate; tăng tải không cần thiết lên hàng đợi lỗi. | `ProcessPaymentNotificationCommandHandler.cs:36-48`<br>`DurableNotificationConfiguration.cs:92-93` |
| **RSK-MAJ-03** | **MAJOR** | Mô hình Dữ liệu | Chưa hiện thực 3 trong 6 bảng cơ sở dữ liệu baseline (`push_device_tokens`, `support_tickets`, `support_ticket_messages`) và hoàn toàn thiếu thư mục EF Core Migrations. | Không hỗ trợ lưu token thiết bị mobile; thiếu hoàn toàn mô-đun xử lý khiếu nại tài xế; không thể tự động migrate DB qua CI/CD. | `NotificationAuditDbContext.cs:12-14`<br>`05-notification-audit-service-db.sql:26-95` |
| **RSK-MAJ-04** | **MAJOR** | Tuân thủ SRS | Thiếu API tra cứu nhật ký kiểm toán tập trung cho Quản trị viên (FR-RPT-04) và tồn tại các Consumer mồ côi (`ReservationNotificationConsumer`, `AuditProjectionConsumer`) thiếu publisher. | Quản trị viên không thể tra cứu dữ liệu kiểm toán; các luồng thông báo đặt chỗ và kiểm toán chưa hoạt động thực tế. | `AuditProjectionConsumer.cs:9`<br>`ReservationNotificationConsumer.cs:9` |
| **RSK-MAJ-05** | **MAJOR** | Containerization | Dockerfile chạy dưới quyền `root` (thiếu `USER app`), compose thiếu `container_name`, thiếu `depends_on: [notification-db]`, và cấu hình `restart: "no"`. | Vi phạm quy chuẩn bảo mật container (CIS Benchmark); container không tự khởi động lại khi crash; không đảm bảo thứ tự khởi động DB. | `Dockerfile.backend:1-40`<br>`compose.app.yml:113-131` |
| **RSK-MIN-01** | **MINOR** | Giám sát & Logging | Chưa cấu hình Serilog tập trung, thiếu Context Enrichers (CorrelationId, RequestPath, ClientIp) và phản hồi lỗi chưa theo chuẩn RFC 7807 `ProblemDetails`. | Khó khăn trong việc truy vết log phân tán (Distributed Tracing) giữa các microservices; phản hồi lỗi HTTP chưa chuẩn hóa quốc tế. | `Program.cs:11-20`<br>`ExceptionHandlingMiddleware.cs:55-80` |
| **RSK-MIN-02** | **MINOR** | Hiệu năng Truy vấn | Lọc trạng thái và phân trang thông báo của tài xế được thực hiện trên bộ nhớ C# RAM (In-Memory Filtering) thay vì pushdown xuống cơ sở dữ liệu SQL. | Gây lãng phí bộ nhớ và nghẽn băng thông database khi tài xế có lịch sử hàng ngàn thông báo tích lũy. | `GetDriverNotificationsQueryHandler.cs:32-42`<br>`NotificationRepository.cs:25-30` |
| **RSK-MIN-03** | **MINOR** | Chuẩn hóa API | Đường dẫn route của controller thiếu tiền tố phiên bản `/v1/` (`api/notifications` thay vì `api/v1/notifications`). | Không đồng bộ với quy ước đặt tên API chung của toàn bộ dự án SmartParking. | `NotificationsController.cs:12` |

---

<a name="phan-5-ke-hoach-hanh-dong-khac-phuc-phan-pha"></a>
## Phần 5: Kế Hoạch Hành Động Khắc Phục Phân Pha (Phased Remediation Plan)

Để giải quyết triệt để 16 rủi ro đã xác định, đưa Notification & Audit Service về trạng thái sẵn sàng vận hành thực tế (Production-Ready), kế hoạch khắc phục được chia thành **5 Phase thực thi tuần tự** có kiểm soát nghiệm thu:

```
[Phase 1: Bảo Mật & Xác Thực JWT] (P0 - Blocker)
   ↓
[Phase 2: Khắc Phục Hạ Tầng Broker & Đồng Bộ Hợp Đồng MassTransit] (P0 - Blocker)
   ↓
[Phase 3: Chuẩn Hóa Vòng Đời Thông Báo & Đồng Bộ Hợp Đồng Frontend] (P1 - Critical)
   ↓
[Phase 4: Nâng Cao Khả Năng Phục Hồi, Health Check & Transactional Inbox] (P1 - Critical)
   ↓
[Phase 5: Kênh Push, Mô-đun Hỗ Trợ & Truy Vấn Kiểm Toán Admin] (P2 - Major)
```

---

### Phase 1: Chuẩn Hóa Bảo Mật & Xác Thực JWT Bearer (P0 - Blocker)

#### 1. Mục tiêu
- Loại bỏ hoàn toàn cửa sau giả mạo header `X-User-Id` / `X-User-Role`.
- Tích hợp chuẩn xác thực JWT Bearer tương thích với UserService.
- Thiết lập phân quyền trên controller và chuẩn hóa route prefix sang `/api/v1/notifications`.

#### 2. Phạm vi thay đổi (Files to Modify/Create)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/SmartParking.NotificationAuditService.API.csproj`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Program.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/appsettings.json`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Services/CurrentUserService.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Controllers/NotificationsController.cs`

#### 3. Hướng dẫn kỹ thuật chi tiết kèm mã nguồn mẫu

**Bước 1.1**: Bổ sung gói xác thực JWT trong `SmartParking.NotificationAuditService.API.csproj`:
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
```

**Bước 1.2**: Cập nhật cấu hình JWT trong `appsettings.json`:
```json
{
  "Jwt": {
    "Issuer": "SmartParking.UserService",
    "Audience": "SmartParking",
    "SecretKey": "SmartParkingDevSecretKeyForLocalJwtTokenVerificationOnlyDoNotUseInProd123456!"
  }
}
```

**Bước 1.3**: Cấu hình Middleware Xác thực trong `Program.cs`:
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký xác thực JWT Bearer
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"] ?? "SmartParkingDevSecretKeyForLocalJwtTokenVerificationOnlyDoNotUseInProd123456!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? "SmartParking.UserService",
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? "SmartParking",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// ...
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

**Bước 1.4**: Chuẩn hóa `CurrentUserService.cs` loại bỏ cửa sau header trên production:
```csharp
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var subClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? user?.FindFirst("sub")?.Value;

            if (!string.IsNullOrWhiteSpace(subClaim) && Guid.TryParse(subClaim, out var parsed))
            {
                return parsed;
            }

            // CHỈ cho phép header fallback khi chạy trên môi trường Development cục bộ
            if (_environment.IsDevelopment())
            {
                var headerUserId = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(headerUserId) && Guid.TryParse(headerUserId, out var fallbackParsed))
                {
                    return fallbackParsed;
                }
            }

            return null;
        }
    }

    public string? Role =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? (_environment.IsDevelopment() 
            ? _httpContextAccessor.HttpContext?.Request.Headers["X-User-Role"].FirstOrDefault() 
            : null);

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true
                                   || (_environment.IsDevelopment() && UserId.HasValue);
}
```

**Bước 1.5**: Bổ sung `[Authorize]` và chuẩn hóa Route trên `NotificationsController.cs`:
```csharp
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    // ...
}
```

#### 4. Tiêu chí kiểm chứng độc lập (Verification Criteria)
- Gửi yêu cầu HTTP không có header `Authorization` tới `GET /api/v1/notifications` ➔ Nhận mã `401 Unauthorized`.
- Gửi yêu cầu kèm token JWT giả mạo hoặc hết hạn ➔ Nhận mã `401 Unauthorized`.
- Gửi yêu cầu kèm header `X-User-Id` trên môi trường Production (`ASPNETCORE_ENVIRONMENT=Production`) ➔ Nhận mã `401 Unauthorized` (Cửa sau đã bị vô hiệu hóa).
- Gửi yêu cầu kèm token JWT hợp lệ do UserService cấp ➔ Nhận mã `200 OK` với đúng dữ liệu của user trong claim `sub`.

---

### Phase 2: Khắc Phục Hạ Tầng Broker & Đồng Bộ Hợp Đồng MassTransit (P0 - Blocker)

#### 1. Mục tiêu
- Đồng bộ hóa hợp đồng sự kiện với Payment Service theo chuẩn `PaymentSucceededV1`.
- Cấu hình tường minh MassTransit Topology (Receive Endpoint & Topic Binding) theo đúng blueprint hạ nguồn `04-downstream-payment-succeeded-consumer-blueprint.md`.
- Gỡ bỏ `RabbitMQ__UseInMemory: "true"` khỏi `compose.app.yml` và bổ sung đầy đủ biến môi trường kết nối broker.

#### 2. Phạm vi thay đổi (Files to Modify/Create)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Application/Contracts/Events/PaymentSucceededV1.cs` (Tạo mới)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Infrastructure/Consumers/PaymentSucceededNotificationConsumer.cs` (Tạo mới thay thế `PaymentNotificationConsumer.cs`)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Infrastructure/InfrastructureDependencyInjection.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Infrastructure/Options/RabbitMqOptions.cs`
- `compose.app.yml`

#### 3. Hướng dẫn kỹ thuật chi tiết kèm mã nguồn mẫu

**Bước 2.1**: Định nghĩa chuẩn hợp đồng `PaymentSucceededV1.cs` khớp 100% 18 trường của Payment Service:
```csharp
namespace SmartParking.PaymentService.Domain.Events;

public sealed record PaymentSucceededV1
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;

    public Guid PaymentOrderId { get; init; }
    public Guid PaymentTransactionId { get; init; }
    public string PaymentMethod { get; init; } = "PAYOS";
    public string? GatewayReference { get; init; }

    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public string ReferenceType { get; init; } = "PARKING_SESSION";
    public Guid ReferenceId { get; init; }
    public string? PlateNumber { get; init; }
    public Guid? AccountId { get; init; }

    public decimal Amount { get; init; }
    public decimal ParkingFee { get; init; }
    public decimal? PenaltyFee { get; init; }
    public string? PenaltyReason { get; init; }
    public string Currency { get; init; } = "VND";
}
```

**Bước 2.2**: Hiện thực `PaymentSucceededNotificationConsumer.cs`:
```csharp
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Domain.Events;
using SmartParking.NotificationAuditService.Application.Features.Notifications.Commands.ProcessNotification;

namespace SmartParking.NotificationAuditService.Infrastructure.Consumers;

public class PaymentSucceededNotificationConsumer : IConsumer<PaymentSucceededV1>
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentSucceededNotificationConsumer> _logger;

    public PaymentSucceededNotificationConsumer(IMediator mediator, ILogger<PaymentSucceededNotificationConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentSucceededV1> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Processing PaymentSucceededV1 for Order {OrderId}, Account {AccountId}, Amount {Amount}",
            msg.PaymentOrderId, msg.AccountId, msg.Amount);

        // Ánh xạ sang command nội bộ của Notification Service
        var command = new ProcessPaymentNotificationCommand(
            EventId: msg.EventId,
            PaymentOrderId: msg.PaymentOrderId,
            DriverId: msg.AccountId ?? Guid.Empty,
            Amount: msg.Amount,
            Currency: msg.Currency,
            PaymentMethod: msg.PaymentMethod,
            TransactionId: msg.PaymentTransactionId.ToString(),
            PlateNumber: msg.PlateNumber,
            OccurredAt: msg.OccurredAt
        );

        await _mediator.Send(command, context.CancellationToken);
    }
}
```

**Bước 2.3**: Cấu hình Topology tường minh trong `InfrastructureDependencyInjection.cs`:
```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentSucceededNotificationConsumer>();
    x.AddConsumer<ReservationNotificationConsumer>();
    x.AddConsumer<AuditProjectionConsumer>();

    if (useInMemory)
    {
        x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    }
    else
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                          ?? new RabbitMqOptions();

            cfg.Host(options.Host, (ushort)options.Port, options.VirtualHost, h =>
            {
                h.Username(options.Username);
                h.Password(options.Password);
            });

            // Cấu hình tường minh endpoint theo Blueprint
            cfg.ReceiveEndpoint("notification-audit-service.payment-succeeded", e =>
            {
                e.ConfigureConsumeTopology = false; // Tắt auto-bind FQTN
                e.Bind("payment.payment-succeeded"); // Bind vào Topic Exchange của PaymentService
                e.Bind<PaymentSucceededV1>();        // Bind trực tiếp exchange theo contract type
                
                // Exponential Backoff Retry Policy
                e.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(2), 
                    TimeSpan.FromSeconds(5), 
                    TimeSpan.FromSeconds(10)));
                
                e.ConfigureConsumer<PaymentSucceededNotificationConsumer>(context);
            });

            cfg.ConfigureEndpoints(context);
        });
    }
});
```

**Bước 2.4**: Sửa đổi `compose.app.yml`:
```yaml
  notification-service:
    build:
      context: .
      dockerfile: docker/Dockerfile.backend
      args:
        SERVICE: NotificationAuditService
    image: smartpark/notification-service:local
    container_name: notification-service
    environment:
      <<: *backend-env
      ConnectionStrings__Notification: "Host=notification-db;Port=5432;Database=smartpark_notification;Username=smartpark_notification;Password=${SMARTPARK_DEV_PASSWORD:?Set SMARTPARK_DEV_PASSWORD}"
      ConnectionStrings__NotificationAudit: "Host=notification-db;Port=5432;Database=smartpark_notification;Username=smartpark_notification;Password=${SMARTPARK_DEV_PASSWORD:?Set SMARTPARK_DEV_PASSWORD}"
      # Kết nối RabbitMQ thật trong mạng docker
      RabbitMQ__UseInMemory: "false"
      RabbitMQ__Host: rabbitmq
      RabbitMQ__Port: "5672"
      RabbitMQ__VirtualHost: "/"
      RabbitMQ__Username: ${SMARTPARK_RABBITMQ_USER:-smartpark}
      RabbitMQ__Password: ${SMARTPARK_RABBITMQ_PASSWORD:?Set SMARTPARK_RABBITMQ_PASSWORD}
    ports:
      - "127.0.0.1:5068:8080"
    networks:
      - smartpark
    depends_on:
      - notification-db
      - rabbitmq
    healthcheck: *healthcheck
    restart: unless-stopped
```

#### 4. Tiêu chí kiểm chứng độc lập (Verification Criteria)
- Chạy `docker compose -f compose.app.yml config` không có lỗi cú pháp.
- Chạy container và kiểm tra giao diện RabbitMQ Management UI (`http://localhost:15672`):
  * Tồn tại queue `notification-audit-service.payment-succeeded`.
  * Queue được bind thành công với exchange `payment.payment-succeeded`.
- Dùng script phát tán thông điệp mẫu `PaymentSucceededV1` vào exchange ➔ Consumer kích hoạt, log ghi nhận xử lý thành công, bản ghi mới xuất hiện trong bảng `durable_notifications`.

---

### Phase 3: Chuẩn Hóa Vòng Đời Thông Báo, Trạng Thái Đã Đọc & Đồng Bộ Hợp Đồng Frontend (P1 - Critical)

#### 1. Mục tiêu
- Bổ sung các trường `is_read`, `read_at`, `title`, `message` vào Entity `DurableNotification` và database.
- Cung cấp phương thức nghiệp vụ `MarkAsRead()` trong Domain.
- Hiện thực các endpoint `PATCH /api/v1/notifications/{id}/read` và `POST /api/v1/notifications/mark-all-read`.
- Khớp hoàn toàn với hợp đồng Frontend UI `NotificationBell.tsx`.

#### 2. Phạm vi thay đổi (Files to Modify/Create)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Domain/Entities/DurableNotification.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Persistence/Configurations/DurableNotificationConfiguration.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Application/Features/Notifications/DTOs/NotificationDto.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Application/Features/Notifications/Commands/MarkAsRead/` (Tạo mới)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Controllers/NotificationsController.cs`

#### 3. Hướng dẫn kỹ thuật chi tiết kèm mã nguồn mẫu

**Bước 3.1**: Cập nhật Domain Entity `DurableNotification.cs`:
```csharp
public class DurableNotification : BaseEntity
{
    // ... các trường hiện có
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsRead { get; private set; } = false;
    public DateTimeOffset? ReadAt { get; private set; }

    // Bổ sung Title và Message vào Factory Method Create
    public static DurableNotification Create(
        string originatingEvent,
        string idempotencyKey,
        Guid recipientId,
        NotificationChannel channel,
        string templateCode,
        string title,
        string message,
        string payload,
        DateTimeOffset? scheduledAt = null)
    {
        // Kiểm tra invariants...
        return new DurableNotification
        {
            Id = Guid.NewGuid(),
            OriginatingEvent = originatingEvent,
            IdempotencyKey = idempotencyKey,
            RecipientId = recipientId,
            Channel = channel,
            TemplateCode = templateCode,
            Title = title,
            Message = message,
            Payload = payload,
            Status = NotificationStatus.PENDING,
            Attempts = 0,
            MaxAttempts = 3,
            ScheduledAt = scheduledAt ?? DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsRead = false
        };
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = DateTimeOffset.UtcNow;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
```

**Bước 3.2**: Cập nhật ánh xạ EF Core trong `DurableNotificationConfiguration.cs`:
```csharp
builder.Property(n => n.Title)
    .HasColumnName("title")
    .HasMaxLength(255)
    .IsRequired();

builder.Property(n => n.Message)
    .HasColumnName("message")
    .IsRequired();

builder.Property(n => n.IsRead)
    .HasColumnName("is_read")
    .IsRequired()
    .HasDefaultValue(false);

builder.Property(n => n.ReadAt)
    .HasColumnName("read_at");

// Thêm chỉ mục tối ưu truy vấn danh sách thông báo chưa đọc của tài xế
builder.HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAt });
```

**Bước 3.3**: Cập nhật `NotificationDto.cs` đáp ứng chuẩn `AppNotification` của Frontend:
```csharp
public record NotificationDto(
    Guid Id,
    Guid RecipientId,
    string OriginatingEvent,
    string Channel,
    string TemplateCode,
    string Title,
    string Message,
    string Payload,
    bool IsRead,
    DateTimeOffset? ReadAt,
    string Status,
    int Attempts,
    DateTimeOffset CreatedAt
);
```

**Bước 3.4**: Thêm endpoint trong `NotificationsController.cs`:
```csharp
[HttpPatch("{id:guid}/read")]
public async Task<IActionResult> MarkAsRead([FromRoute] Guid id, CancellationToken ct)
{
    var currentUserId = _currentUserService.UserId;
    if (!currentUserId.HasValue) return Unauthorized();

    var result = await _mediator.Send(new MarkNotificationAsReadCommand(id, currentUserId.Value), ct);
    return result ? Ok(new { success = true, message = "Notification marked as read" }) 
                  : NotFound(new { success = false, message = "Notification not found" });
}

[HttpPost("mark-all-read")]
public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
{
    var currentUserId = _currentUserService.UserId;
    if (!currentUserId.HasValue) return Unauthorized();

    var count = await _mediator.Send(new MarkAllNotificationsAsReadCommand(currentUserId.Value), ct);
    return Ok(new { success = true, markedCount = count });
}
```

#### 4. Tiêu chí kiểm chứng độc lập (Verification Criteria)
- Gọi API `GET /api/v1/notifications` ➔ Phản hồi JSON chứa đầy đủ `title`, `message`, `isRead: false`.
- Gọi API `PATCH /api/v1/notifications/{id}/read` ➔ Trả về HTTP 200 OK.
- Gọi lại `GET /api/v1/notifications` ➔ Bản ghi có `isRead: true` và `readAt` chứa timestamp hợp lệ.
- Kiểm tra tích hợp với `NotificationBell.tsx` trên Frontend: Chấm đỏ unread badge giảm về 0 sau khi bấm "Mark all as read".

---

### Phase 4: Nâng Cao Khả Năng Phục Hồi, Sửa Lỗi Health Check & Transactional Inbox (P1 - Critical)

#### 1. Mục tiêu
- Khắc phục triệt để lỗi false-positive của `/health`: trả về HTTP 503 khi cơ sở dữ liệu ngắt kết nối.
- Cấu hình chuẩn ASP.NET Core Health Checks phân tách `/health/ready` và `/health/live`.
- Bổ sung `EnableRetryOnFailure` cho kết nối Npgsql.
- Xử lý êm thuận ngoại lệ tương tranh `DbUpdateException` (Postgres Unique Constraint 23505).

#### 2. Phạm vi thay đổi (Files to Modify/Create)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Program.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Persistence/PersistenceDependencyInjection.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Application/Features/Notifications/Commands/ProcessNotification/ProcessPaymentNotificationCommandHandler.cs`

#### 3. Hướng dẫn kỹ thuật chi tiết kèm mã nguồn mẫu

**Bước 4.1**: Sửa đổi toàn diện Endpoint `/health` trong `Program.cs`:
```csharp
app.MapGet("/health", async (NotificationAuditDbContext dbContext, IBusRegistrationContext busContext) =>
{
    var dbConnected = false;
    try
    {
        dbConnected = await dbContext.Database.CanConnectAsync();
    }
    catch
    {
        dbConnected = false;
    }

    // Nếu cơ sở dữ liệu không kết nối được, trả về HTTP 503 Service Unavailable
    if (!dbConnected)
    {
        return Results.Json(new
        {
            status = "Unhealthy",
            service = "SmartParking.NotificationAuditService",
            timestamp = DateTimeOffset.UtcNow,
            dependencies = new
            {
                database = "Disconnected",
                messaging = "MassTransit Running"
            }
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "Healthy",
        service = "SmartParking.NotificationAuditService",
        timestamp = DateTimeOffset.UtcNow,
        dependencies = new
        {
            database = "Connected",
            messaging = "MassTransit Running"
        }
    });
});
```

**Bước 4.2**: Bật `EnableRetryOnFailure` trong `PersistenceDependencyInjection.cs`:
```csharp
services.AddDbContext<NotificationAuditDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
        npgsqlOptions.MigrationsAssembly(typeof(NotificationAuditDbContext).Assembly.FullName);
    });
});
```

**Bước 4.3**: Xử lý ngoại lệ tương tranh `DbUpdateException` trong Command Handler:
```csharp
try
{
    await _unitOfWork.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
{
    // Bắt lỗi vi phạm Unique Constraint trên idempotency_key do 2 luồng đến đồng thời
    _logger.LogWarning("Concurrent duplicate notification detected for key {Key}. Handling gracefully.", idempotencyKey);
    
    var existingDuplicate = await _notificationRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
    if (existingDuplicate != null)
    {
        return NotificationDeliveryResult.Duplicate(existingDuplicate.Id);
    }
    throw;
}
```

#### 4. Tiêu chí kiểm chứng độc lập (Verification Criteria)
- Tạm dừng container `notification-db` (`docker stop notification-db`), gọi `curl -i http://localhost:5068/health` ➔ Trả về đúng **`HTTP/1.1 503 Service Unavailable`** với JSON `"status": "Unhealthy"`.
- Bật lại DB (`docker start notification-db`), gọi lại ➔ Trả về **`HTTP/1.1 200 OK`** với `"status": "Healthy"`.
- Giả lập 2 yêu cầu xử lý cùng một sự kiện `EventId` đồng thời ➔ Không ném ngoại lệ 500/Crash, cả hai luồng đều trả về kết quả thành công mà chỉ có 1 bản ghi được tạo trong DB.

---

### Phase 5: Bổ Sung Kênh Push, Module Hỗ Trợ (Support Tickets) & Truy Vấn Kiểm Toán Admin (P2 - Major)

#### 1. Mục tiêu
- Triển khai `PushNotificationDeliveryAdapter` ngăn chặn crash runtime khi xử lý kênh PUSH.
- Bổ sung 3 thực thể còn thiếu (`PushDeviceToken`, `SupportTicket`, `SupportTicketMessage`) vào Domain và Persistence.
- Hiện thực API tra cứu nhật ký kiểm toán tập trung cho Quản trị viên (`GET /api/v1/audit/projections` - SRS FR-RPT-04).
- Tạo bộ EF Core Migration ban đầu (`InitialCreate`).

#### 2. Phạm vi thay đổi (Files to Modify/Create)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Infrastructure/Adapters/Delivery/LocalPushFixtureAdapter.cs` (Tạo mới)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Domain/Entities/SupportTicket.cs` (Tạo mới)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Domain/Entities/PushDeviceToken.cs` (Tạo mới)
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Persistence/NotificationAuditDbContext.cs`
- `src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Controllers/AuditProjectionsController.cs` (Tạo mới)

#### 3. Hướng dẫn kỹ thuật chi tiết kèm mã nguồn mẫu

**Bước 5.1**: Triển khai `LocalPushFixtureAdapter.cs`:
```csharp
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;

public class LocalPushFixtureAdapter : INotificationChannelAdapter
{
    private readonly ILogger<LocalPushFixtureAdapter> _logger;

    public LocalPushFixtureAdapter(ILogger<LocalPushFixtureAdapter> logger)
    {
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.PUSH;

    public Task<ChannelDeliveryResult> DeliverAsync(DurableNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[LOCAL_PUSH_FIXTURE] Pushed to Device for Recipient {RecipientId}: Title: {Title}, Message: {Message}",
            notification.RecipientId, notification.Title, notification.Message);

        return Task.FromResult(ChannelDeliveryResult.Success($"PUSH_MOCK_{Guid.NewGuid():N}"));
    }
}
```
Đăng ký vào `InfrastructureDependencyInjection.cs`:
```csharp
services.AddScoped<INotificationChannelAdapter, LocalPushFixtureAdapter>();
```

**Bước 5.2**: Hiện thực API Tra cứu Kiểm toán dành cho Quản trị viên (`AuditProjectionsController.cs`):
```csharp
[ApiController]
[Route("api/v1/audit/projections")]
[Authorize(Roles = "ADMIN,AUDITOR")]
public class AuditProjectionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditProjectionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> SearchAuditProjections(
        [FromQuery] string? originatingService,
        [FromQuery] Guid? actorId,
        [FromQuery] string? action,
        [FromQuery] string? resourceType,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new SearchCentralAuditProjectionsQuery(
            originatingService, actorId, action, resourceType, fromDate, toDate, page, pageSize);

        var result = await _mediator.Send(query, ct);
        return Ok(new { success = true, data = result });
    }
}
```

#### 4. Tiêu chí kiểm chứng độc lập (Verification Criteria)
- Gửi thông báo với kênh `PUSH` ➔ Dispatcher gọi `LocalPushFixtureAdapter` thành công, không ném exception, status chuyển thành `DELIVERED`.
- Đăng nhập với quyền `ADMIN`, gọi `GET /api/v1/audit/projections` ➔ Trả về danh sách nhật ký kiểm toán phân trang kèm thông tin lọc chính xác.
- Đăng nhập với quyền `DRIVER`, gọi `GET /api/v1/audit/projections` ➔ Nhận mã `403 Forbidden`.

---

<a name="phan-6-phuong-phap--lenh-kiem-chung-doc-lap"></a>
## Phần 6: Phương Pháp & Lệnh Kiểm Chứng Độc Lập (Verification Commands)

Nhằm đảm bảo tính minh bạch, khách quan và phục vụ công tác tái thẩm định của Kiểm toán viên Độc lập (Forensic Auditor), dưới đây là danh mục các lệnh kiểm chứng có thể sao chép và thực thi trực tiếp trên terminal:

### 6.1 Kiểm chứng Biên dịch & Bộ Kiểm Thử Tự Động Hiện Hữu
```powershell
# 1. Kiểm tra trạng thái Git và commit
git status
git log -n 4 --oneline feat/notification-audit-service

# 2. Biên dịch toàn bộ Solution của dịch vụ
dotnet build src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/SmartParking.NotificationAuditService.API.csproj -c Release

# 3. Chạy toàn bộ 35 unit/integration test nội bộ
dotnet test tests/Services/SmartParking.NotificationAuditService.Tests/SmartParking.NotificationAuditService.Tests.csproj --logger "console;verbosity=normal"
```

### 6.2 Kiểm chứng Lỗ Hổng Bảo Mật & Xác Thực (RSK-BLK-01)
```powershell
# 1. Quét tìm cấu hình JWT Bearer trong Program.cs (Kỳ vọng: KHÔNG TÌM THẤY KẾT QUẢ)
Select-String -Path src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Program.cs -Pattern "AddAuthentication|UseAuthentication|AddAuthorization|UseAuthorization"

# 2. Quét tìm thuộc tính [Authorize] trong Controller (Kỳ vọng: KHÔNG TÌM THẤY KẾT QUẢ)
Select-String -Path src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Controllers/NotificationsController.cs -Pattern "Authorize"

# 3. Trích xuất cửa sau giả mạo header trong CurrentUserService.cs
Get-Content src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Services/CurrentUserService.cs | Select-String -Pattern "X-User-Id|X-User-Role" -Context 3,3
```

### 6.3 Kiểm chứng Bất Tương Thích Hợp Đồng Sự Kiện & Broker (RSK-BLK-02, RSK-BLK-03)
```powershell
# 1. Kiểm tra sự sai khác tên và namespace giữa Consumer và Hợp đồng Payment
Select-String -Path src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Infrastructure/Consumers/PaymentNotificationConsumer.cs -Pattern "IConsumer<"
Select-String -Path src/Services/PaymentService/SmartParking.PaymentService.Domain/Events/PaymentSucceededV1.cs -Pattern "PaymentSucceededV1"

# 2. Kiểm tra gán cứng In-Memory broker trong Docker Compose
Get-Content compose.app.yml | Select-String -Pattern "RabbitMQ__UseInMemory" -Context 3,3

# 3. Kiểm tra tính hợp lệ cú pháp của Docker Compose
docker compose -f compose.app.yml config
```

### 6.4 Kiểm chứng Tính Thuần Khiết Kiến Trúc & Rò Rỉ IQueryable (R3)
```powershell
# 1. Xác nhận Domain không chứa NuGet dependencies bên ngoài (Kỳ vọng: 0 kết quả)
Select-String -Path "src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Domain/*.csproj" -Pattern "<PackageReference"

# 2. Xác nhận Domain không tham chiếu project khác (Kỳ vọng: 0 kết quả)
Select-String -Path "src/Services/NotificationAuditService/SmartParking.NotificationAuditService.Domain/*.csproj" -Pattern "<ProjectReference"

# 3. Quét kiểm tra rò rỉ IQueryable trong toàn bộ dịch vụ (Kỳ vọng: 0 kết quả)
git grep -n "IQueryable" src/Services/NotificationAuditService/
```

### 6.5 Kiểm chứng Lỗi Ngụy Tạo Sức Khỏe Health Check (RSK-CRT-01)
```powershell
# Xem khối lệnh trả về HTTP 200 OK bất kể kết nối database
Get-Content src/Services/NotificationAuditService/SmartParking.NotificationAuditService.API/Program.cs | Select-String -Pattern "app.MapGet\(""/health""" -Context 0,22
```

---

## Lời Kết & Cam Kết Chất Lượng

Báo cáo Thẩm định Toàn diện trên được lập dựa trên việc rà soát thực tế từng dòng mã nguồn, đối chiếu nghiêm ngặt với các tiêu chuẩn thiết kế hệ thống và kiểm chứng thực nghiệm bằng công cụ tự động. Khi hoàn thành toàn bộ 5 Phase trong Kế hoạch Khắc phục, `SmartParking.NotificationAuditService` sẽ đạt mức độ hoàn thiện kiến trúc tối đa, sẵn sàng sáp nhập vào nhánh chính và vận hành an toàn trên môi trường Production của hệ thống SmartParking.

**Người lập báo cáo**: Ban Kiểm toán Kỹ thuật Độc lập (Worker 1 - Audit Report Author)  
**Tình trạng tài liệu**: BẢN CHÍNH THỨC (FINAL MASTER AUDIT REPORT)
