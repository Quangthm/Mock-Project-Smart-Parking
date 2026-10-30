# Quy chuẩn Kiến trúc Mã nguồn & Tiêu chí Review cho Agent (Source Code Architecture Specification)

> **Mục đích:** Tài liệu này là **hướng dẫn thực thi và kiểm toán mã nguồn (Implementation & Code Review Guidelines)** để **AI Agent** và **Developer** đối chiếu, đảm bảo mọi commit / PR trong `src/Services/` tuân thủ nghiêm ngặt các ranh giới kiến trúc đã được phê duyệt của dự án SmartParking.
> **Vị trí tài liệu:** Tài liệu này phục vụ kiểm soát mã nguồn ở mức implementation, **không thay thế hoặc ghi đè** SRS, các quyết định PM đã phê duyệt (`DEC-PAY-ARCH-01`, `DEC-PAY-ARCH-02`) hay các hợp đồng kiến trúc giải pháp hiện hành (`SmartPark Solution Architecture v1.0`).
> **Trọng tâm đặc biệt:** `SmartParking.PaymentService` (đối chiếu chuẩn mực cùng `UserService`, `FacilityService`, `OperationService`).

---

## 1. Bản đồ Kiến trúc Tổng thể (Global Architecture Style)

Hệ thống backend SmartParking áp dụng **Microservices** kết hợp **Clean Architecture / Onion Architecture (Ports & Adapters)** độc lập cho từng service:
1. **Service-Owned Logical Databases & Credentials:** Mỗi service sở hữu một database logic với credentials riêng biệt trên PostgreSQL (môi trường local có thể host nhiều DB logic trên một PostgreSQL server instance). **Tuyệt đối KHÔNG** có cross-database foreign keys, cross-database SQL joins, truy cập chéo bảng, hay tiêm `DbContext` của service này vào service khác.
2. **Giao tiếp liên service (Inter-service Communication):**
   - **Đồng bộ (Synchronous):** REST API / HTTP client qua các interface `Port` đặt tại tầng `Application` và triển khai adapter tại tầng `Infrastructure`.
   - **Bất đồng bộ (Asynchronous):** RabbitMQ + MassTransit sử dụng Outbox/Inbox Pattern (durable events, local transactions).
3. **Mô hình Phân tầng Vòng tròn Đồng tâm (Onion / Clean Architecture):**
   Mọi phụ thuộc bắt buộc tuân thủ nguyên tắc **Inward Dependency Rule**: Mã nguồn ở vòng ngoài phụ thuộc vào vòng trong; vòng trong tuyệt đối không biết gì về vòng ngoài.

   ```mermaid
   graph TD
       subgraph OuterLayer["VÒNG NGOÀI CÙNG (Adapters / Frameworks / Delivery)"]
           Presentation["Presentation (SmartParking.*Service.API)"]
           Infrastructure["Infrastructure (SmartParking.*Service.Infrastructure)"]
           Persistence["Persistence (SmartParking.*Service.Persistence)"]
       end

       subgraph MiddleLayer["VÒNG GIỮA (Use Cases / Ports)"]
           Application["Application (SmartParking.*Service.Application)"]
       end

       subgraph CoreLayer["LÕI TRUNG TÂM (Domain Models / Invariants)"]
           Domain["Domain (SmartParking.*Service.Domain)"]
       end

       Presentation -->|Use Cases / DTOs| Application
       Infrastructure -->|Implements External Ports| Application
       Persistence -->|Implements Repository Ports| Application
       
       Application -->|Business Rules / Aggregates| Domain
   ```

---

## 2. Ma trận Phụ thuộc Dự án (Project Dependency Matrix)

Mọi dự án `.csproj` trong từng service BẮT BUỘC tuân thủ hướng tham chiếu sau:

| Dự án (Project Layer) | Được phép tham chiếu (Allowed References) | Tuyệt đối CẤM tham chiếu (FORBIDDEN) | Ghi chú Ranh giới |
| :--- | :--- | :--- | :--- |
| **Domain** | **KHÔNG CÓ GÌ** (0 Project References, 0 External Provider SDKs / EF Core) | `Application`, `Persistence`, `Infrastructure`, `API`, `EF Core`, `HttpClient`, `payOS SDK`, `System.Text.Json` (cho snapshot) | Lõi nghiệp vụ POCO thuần túy; hoàn toàn độc lập công nghệ và serialization. |
| **Application** | `Domain` | `Persistence`, `Infrastructure`, `API`, `EF Core`, `Npgsql`, `payOS SDK` | Chỉ định nghĩa Ports/Interfaces và DTOs; không phụ thuộc implementation cụ thể. |
| **Persistence** | `Application`, `Domain` | `Infrastructure`, `API` | Implement các Repository / Outbox Ports; cấu hình EF Core mappings. |
| **Infrastructure** | `Application`, `Domain` | `Persistence`, `API` | Implement các External Ports (payOS, HTTP clients, MassTransit adapters). |
| **Presentation (API)** | `Application` *(Runtime Wiring: `Persistence`, `Infrastructure`)* | Logic tầng Domain/Persistence bị gọi trực tiếp từ Controller; 3rd-party provider SDKs | **Phân biệt rõ:** Controllers chỉ phụ thuộc `Application`. Việc tham chiếu `Persistence` & `Infrastructure` chỉ diễn ra tại **Composition Root (`Program.cs`)** để đăng ký DI và cấu hình host. |

---

## 3. Quy chuẩn Chi tiết Từng Tầng (Detailed Layer Specifications)

### 3.1. Tầng Domain (`SmartParking.*Service.Domain`)
- **Trách nhiệm:** Chứa các khái niệm nghiệp vụ cốt lõi, Domain Entities, Value Objects, Domain Records, Domain Events, Domain Exceptions và Pure Domain Engines (thuật toán tính toán không trạng thái).
- **Quy tắc bắt buộc:**
  - Phải là C# thuần túy (Plain Old C# Objects - POCO), không chứa logic I/O, không gọi DB, không gọi mạng.
  - Sử dụng C# `record` cho Value Objects / Snapshots để tăng tính toàn vẹn (lưu ý record không tự bảo đảm deep immutability nếu chứa collection mutable, cần dùng `IReadOnlyList`).
  - **Áp dụng DEC-PAY-ARCH-02:** Domain chỉ giữ kết quả tính toán và snapshot dưới dạng **Typed Models / Records**, tuyệt đối **KHÔNG** chứa `System.Text.Json`, không tự gọi serializer hoặc cung cấp phương thức `ToSnapshotJson()`. Logic serialization / mapping JSON snapshot thuộc trách nhiệm của các tầng bên ngoài.
  - Báo lỗi nghiệp vụ dự kiến bằng `DomainException` riêng hoặc Typed Result/Outcome pattern nhất quán; tránh nuốt lỗi hoặc dùng generic `System.Exception`.

### 3.2. Tầng Application (`SmartParking.*Service.Application`)
- **Trách nhiệm:** Điều phối luồng nghiệp vụ (Use Case Orchestration), CQRS, kiểm thực dữ liệu đầu vào (Validation), định nghĩa hợp đồng tích hợp (Ports/Interfaces).
- **Cấu trúc thư mục chuẩn:**
  ```text
  SmartParking.*Service.Application/
  ├── Common/
  │   ├── Behaviors/               # MediatR Pipeline Behaviors (ValidationBehavior, LoggingBehavior)
  │   ├── Interfaces/
  │   │   ├── Ports/               # Outbound ports (e.g. IPaymentGatewayPort, IParkingTariffPort)
  │   │   └── Persistence/         # Repository, UnitOfWork & Outbox interfaces
  ├── DTOs/                        # Request/Response DTOs cho API
  ├── Usecase/                     # Nhóm theo tính năng hoặc Use Case (CQRS)
  │   └── [FeatureName]/
  │       ├── [Feature]Command.cs  hoặc [Feature]Query.cs
  │       ├── [Feature]Handler.cs  (IRequestHandler<...>)
  │       └── [Feature]Validator.cs (AbstractValidator<...>, khi có ràng buộc input)
  └── ApplicationDependencyInjection.cs # Đăng ký MediatR, FluentValidation
  ```
- **Quy tắc bắt buộc:**
  - **CQRS qua MediatR:** Mọi business request từ API đều dispatch qua `IMediator.Send(...)`. Handler có thể gọi các Application Service nội bộ nếu cần tái sử dụng logic phối hợp. Các endpoint thuần transport (Health check, ping) không bắt buộc tạo Command giả.
  - **FluentValidation Pipeline:** Validator được đăng ký qua `ValidationBehavior<TRequest, TResponse>`. Yêu cầu tạo Validator khi use case có ràng buộc input thực tế; không tạo validator rỗng chỉ để ép đủ bộ ba. Các kiểm tra authorization, domain invariants và race condition xử lý ở boundary phù hợp.
  - Tầng Application **chỉ biết Ports/Interfaces**, không biết implementation cụ thể của DB hay External Service.
  - **Quy tắc đặt tên Port Interface:** Interface cho outbound port phải tuân thủ mẫu `I{Concept}Port` (ví dụ: `IPaymentGatewayPort`, `IParkingTariffPort`) và đặt tại `Common/Interfaces/Ports/`. Interface cho persistence phải tuân thủ mẫu `I{Entity}Repository` hoặc `IUnitOfWork` và đặt tại `Common/Interfaces/Persistence/`.

### 3.3. Tầng Persistence (`SmartParking.*Service.Persistence`)
- **Trách nhiệm:** Tương tác với PostgreSQL, quản lý `DbContext`, Entity Configurations (Fluent API), Migrations, Seeders, Repositories, Unit of Work và Outbox/Inbox tables.
- **Quy tắc bắt buộc:**
  - Implement các interface được khai báo tại `Application/Common/Interfaces/Persistence`.
  - **Ranh giới Query Provider:** Repository **tuyệt đối không trả `IQueryable` ra khỏi tầng Persistence**. Mọi truy vấn phải được đóng gói và trả về Domain Entities hoặc Typed DTOs.
  - Quản lý giao dịch cục bộ (Local Transaction) bảo đảm tính nguyên tử khi lưu trữ thực thể nghiệp vụ và Outbox message.
  - Thực hiện serialize snapshot phục vụ lưu trữ DB (JSONB) tại tầng này theo schema đã được phê duyệt.
  - Đăng ký DI tập trung qua `PersistenceDependencyInjection.cs`.

### 3.4. Tầng Infrastructure (`SmartParking.*Service.Infrastructure`)
- **Trách nhiệm:** Tương tác với các hệ thống bên ngoài: Cổng thanh toán (payOS, VNPay), HTTP clients gọi các microservice khác, Message Broker (RabbitMQ / MassTransit), dịch vụ Mail/SMS, Storage (Cloudinary/S3).
- **Cấu trúc thư mục đề xuất:**
  ```text
  SmartParking.*Service.Infrastructure/
  ├── Adapters/
  │   ├── Outbound/                # HTTP Adapters implement Ports của Application
  │   ├── PaymentGateways/         # payOS adapter implement Payment Ports
  │   └── MessageBroker/           # MassTransit producers & consumers
  └── InfrastructureDependencyInjection.cs
  ```
- **Quy tắc bắt buộc:**
  - **Giới hạn External-provider SDKs:** Các SDK kết nối dịch vụ bên ngoài (như `PayOS` NuGet) CHỈ ĐƯỢC PHÉP cài đặt và sử dụng trong tầng `Infrastructure`. Thư viện kỹ thuật (EF Core, MediatR, FluentValidation) được đặt theo trách nhiệm của từng tầng tương ứng.
  - **Phân biệt Retry Transport vs Mutation Tài chính:** Cấu hình resilience/timeout cho HTTP clients qua `IHttpClientFactory`. **Tuyệt đối không tự động retry mù quáng các mutation thanh toán** (POST tạo giao dịch) khi gặp timeout hoặc lỗi không an toàn (tránh trừ tiền 2 lần).
  - **Quy tắc đặt tên Adapter:** Class implement outbound port phải tuân thủ mẫu `{Concept}Adapter` (ví dụ: `PayOSAdapter`, `ParkingTariffHttpAdapter`) và đặt trong thư mục `Adapters/` tương ứng.
  - **Anti-Corruption Layer (ACL):** Adapter gọi hệ thống bên ngoài **phải map response/DTO của bên ngoài** sang Domain Model hoặc Application DTO trước khi trả kết quả qua Port interface. Tuyệt đối không để model/contract của hệ thống bên ngoài lan vào tầng `Application` hoặc `Domain`. Nếu external API thay đổi contract, chỉ cần sửa Adapter, không ảnh hưởng lõi nghiệp vụ.
  - Đăng ký DI tập trung qua `InfrastructureDependencyInjection.cs`.

### 3.5. Tầng Presentation / API (`SmartParking.*Service.API`)
- **Trách nhiệm:** Cổng giao tiếp HTTP (Controllers hoặc Minimal APIs), Middleware, Authentication / Authorization, Swagger/OpenAPI, và đóng vai trò **Composition Root**.
- **Quy tắc bắt buộc:**
  - **Thin Controllers:** Controller chỉ nhận transport request, map sang Command/Query, gọi `_mediator.Send()`, và trả về HTTP status code phù hợp.
  - **Tuyệt đối KHÔNG:**
    - Không viết business rules / tính toán số tiền trong Controller.
    - Không gọi trực tiếp `DbContext`, Repository hoặc Provider SDK từ Controller.
    - Không nuốt exception bằng `try-catch` rỗng; sử dụng `ExceptionHandlingMiddleware` toàn cục để chuẩn hóa lỗi trả về theo đúng HTTP status, error codes và validation details.
  - **Vai trò Composition Root (`Program.cs`):**
    - Là nơi duy nhất có quyền tham chiếu `Application`, `Persistence`, `Infrastructure` để đăng ký DI container.
    - Cấu hình Middleware pipeline, Authentication/Authorization, CORS, Route mapping và Host configuration. Không nhúng luồng nghiệp vụ vào `Program.cs`.

---

## 4. Đặc tả Chuyên biệt cho PaymentService (`SmartParking.PaymentService`)

Dành cho nhà phát triển và agent triển khai miền Tài chính:

### 4.1. Quyền Sở hữu Biểu phí (Authoritative Tariff Ownership — DEC-PAY-ARCH-01)
> [!IMPORTANT]
> **Quyết định PM:** Payment & Pricing là nguồn có thẩm quyền duy nhất (authoritative owner) đối với versioned tariffs, financial terms và accepted financial snapshots. Các service khác cung cấp dữ kiện thuộc domain của mình qua hợp đồng API/event; không quyết định hoặc ghi trực tiếp tariff của Payment.
- **Xử lý Adapter hiện tại:** Interface `IParkingTariffPort` / `ParkingTariffHttpAdapter` đang gọi sang FacilityService phải được hiểu là **giải pháp chuyển đổi tạm thời (legacy transitional adapter)**, không phải kiến trúc đích. Kế hoạch chuyển đổi ownership và writer phải được thực hiện có kiểm soát, không duy trì 2 nguồn thẩm quyền song song.
- **Bảo toàn Snapshot:** Không sửa đổi hoặc xóa lịch sử snapshot tài chính đã chốt để áp lại giá hiện hành.

### 4.2. Đưa Snapshot Serialization ra ngoài Domain (DEC-PAY-ARCH-02)
> [!IMPORTANT]
> **Quyết định PM:** Domain giữ kết quả tính giá và snapshot dạng typed model, độc lập với JSON serialization. Serialization được thực hiện ngoài Domain.
- Domain `PricingModels.cs` chỉ chứa typed records: `PricingCalculationResult`, `PricingPolicySnapshot`, `PricingSegmentBreakdown`.
- Loại bỏ method `ToSnapshotJson()` và `System.Text.Json` khỏi Domain. Việc serialize ra chuỗi JSONB để lưu trữ cột `invoices.pricing_snapshot` do tầng `Persistence` đảm nhiệm.
- Refactor phải bảo toàn JSON contract hiện hành (camelCase, string enum, thứ tự các trường nếu downstream phụ thuộc) và khả năng đọc snapshot lịch sử; kiểm chứng bằng contract tests.

### 4.3. Quản lý Đơn thanh toán & Lần thử (Orders & Attempts)
- **Domain:** Sở hữu State, Invariants, Enums và Aggregates của `PaymentOrder`, `PaymentAttempt`, `Transaction`, `Refund`.
- **Application:** Chứa các Use Cases (`CreatePaymentOrderCommand`, `ProcessPayOSWebhookCommand`, `QueryPaymentStatusQuery`).
- **Persistence:** Chứa `PaymentDbContext`, migrations, mappings cho các bảng `payment_orders`, `payment_attempts`, `transactions`, `outbox_messages`.

### 4.4. Tích hợp Cổng thanh toán payOS (payOS Integration)
- **Tầng Infrastructure:** Chứa class adapter gọi cổng thanh toán payOS (đề xuất: `PayOSAdapter`), chịu trách nhiệm tạo checkout link và xác thực chữ ký Webhook HMAC SHA256.
- **Tầng API:** Controller webhook (đề xuất: `POST /api/v1/payments/payos/webhook`) nhận request từ payOS, dispatch command sang MediatR.
- **Bảo mật:** Toàn bộ Client ID, API Key, Checksum Key lấy từ `IConfiguration` / biến môi trường; tuyệt đối không commit secret vào git.

### 4.5. Phân định Sự thật Tài chính vs Quyền Vận hành (Financial Fact vs Capacity Right)
- Khi thanh toán thành công, PaymentService lưu trữ attempt thành công và phát sinh sự kiện qua Outbox (nhãn kỹ thuật đề xuất: `PaymentVerifiedEvent`).
- **Ranh giới:** `PaymentVerified` chỉ là **sự thật tài chính (tiền đã về)**, KHÔNG PHẢI là sự cho phép tự động cấp quyền vào/ra. `Parking Operations` tiêu thụ sự kiện này một cách idempotent và tự quyết định chuyển đổi trạng thái (xác nhận đặt chỗ, mở barie) dựa trên sức chứa và quy tắc vận hành.

---

## 5. Bảng Kiểm kê Hiện trạng 4 Services (Snapshot Inventory tại Commit hiện hành)

> *Ghi chú:* Đây là kiểm kê thực tế mã nguồn tại commit hiện hành (`6165024c`), mục tiêu dài hạn của toàn hệ thống vẫn là 6 domain-owned services (gồm cả IoT & Recognition, Notification & Audit).

| Service | Tầng Domain | Tầng Application | Tầng Persistence | Tầng Infrastructure | Tầng Presentation (API) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`PaymentService`** | `Domain/Pricing` (Cần refactor bỏ `ToSnapshotJson`) | MediatR CQRS + FluentValidation + Ports | Skeleton DI (chuẩn bị `PaymentDbContext`) | HTTP Outbound Adapters (Tariff legacy, Reservation) | `PricingController`, `ExceptionHandlingMiddleware` |
| **`UserService`** | `Domain/Entities`, `ValueObjects`, `BaseEntity` | MediatR CQRS + FluentValidation + Services | EF Core `AppDbContext` + Repositories + UnitOfWork | Email / Token Services | MVC Controllers |
| **`FacilityService`** | `Domain/ParkingStructure.cs` | `ParkingStructureService` + Handlers | PostgreSQL Direct Stores (`Npgsql`) | HTTP Service Clients + Workers | Minimal APIs (`Program.cs`) |
| **`OperationService`**| `Domain/StructureEditPolicy.cs` | `StructureSafetyService` | PostgreSQL Safety Store riêng (`Npgsql`) | *(Tích hợp trong Persistence)* | Minimal APIs (`Program.cs`) |

---

## 6. Bảng Tiêu chí Review Mã nguồn dành cho Agent (Agent Code Review Checklist)

Khi một AI Agent hoặc lập trình viên review code PR / task mới, phải tuần tự kiểm tra theo checklist sau:

> **Chú giải mức độ nghiêm trọng:**
> - 🔴 **BLOCKER** — Vi phạm kiến trúc nghiêm trọng, phải sửa trước khi merge.
> - 🟡 **WARNING** — Vi phạm cần sửa, có thể tạo tech-debt ticket nếu chưa sửa ngay được.
> - 🟢 **BEST PRACTICE** — Khuyến nghị cải thiện chất lượng code.

### [ ] Nhóm 1: Kiểm tra Hướng Phụ thuộc & Phân tầng (Dependency & Layering)
- [ ] 🔴 File mới hoặc sửa đổi có nằm đúng project layer theo mô hình Onion không?
- [ ] 🔴 Có `using` nào từ `Domain` trỏ sang `Application`, `Persistence`, `Infrastructure` hoặc `API` không? (Nếu có → **FAIL NGAY LẬP TỨC**).
- [ ] 🔴 Có `using` nào từ `Application` trỏ sang `Persistence` hoặc `Infrastructure` không? (Nếu có → **FAIL NGAY LẬP TỨC**).
- [ ] 🔴 Tầng `Domain` có bị dính `System.Text.Json`, `Newtonsoft.Json`, EF Core hay 3rd-party SDK không? (DEC-PAY-ARCH-02 → Nếu có **FAIL**).
- [ ] 🔴 Repository trong `Persistence` có trả `IQueryable` ra ngoài không? (Nếu có → **FAIL**).
- [ ] 🟡 Thư viện External-provider SDK (như payOS) có bị cài sai ngoài `Infrastructure` không?
- [ ] 🟡 Port interface có đúng quy tắc đặt tên `I{Concept}Port` tại `Application/Common/Interfaces/Ports/` không? Adapter class có đúng quy tắc `{Concept}Adapter` tại `Infrastructure/Adapters/` không?

### [ ] Nhóm 2: Kiểm tra Tầng API & Composition Root
- [ ] 🔴 Controller có bị béo (Fat Controller) chứa logic tính toán hoặc gọi DB/SDK trực tiếp không? (Nếu có → **FAIL**).
- [ ] 🔴 Controller có gọi trực tiếp `DbContext` hay Repository không? (Nếu có → **FAIL**).
- [ ] 🔴 Mọi business endpoint có được dispatch qua `IMediator.Send(...)` không?
- [ ] 🟡 `Program.cs` có chỉ đóng vai trò Composition Root (DI, Middleware, Routing) mà không chứa business logic không?
- [ ] 🟡 Lỗi trả về có qua `ExceptionHandlingMiddleware` và giữ đúng mã HTTP, error code không?

### [ ] Nhóm 3: Kiểm tra Tầng Application & CQRS
- [ ] 🔴 Use Case có phân tách Command / Query rõ ràng không?
- [ ] 🔴 Handler có tương tác với các hệ thống ngoài (DB, HTTP, Broker) thông qua `Interface/Port` không?
- [ ] 🟡 Validator có chỉ được tạo khi có input constraints thực tế không (không tạo validator rỗng)?
- [ ] 🟡 Mỗi Port interface ở `Application` có ít nhất 1 Adapter implement và được đăng ký đúng trong DI container (`PersistenceDependencyInjection.cs` hoặc `InfrastructureDependencyInjection.cs`) không?

### [ ] Nhóm 4: Ranh giới Dữ liệu Microservice & Anti-Corruption
- [ ] 🔴 Service có tự ý đọc/ghi vào database của service khác không? (Nếu có → **FAIL VI PHẠM BOUNDARY**).
- [ ] 🟡 Các tham chiếu ID giữa các service có được xử lý dạng logical reference (Guid, string, hoặc long tùy loại ID của provider) thay vì Foreign Key DB không?
- [ ] 🟡 Adapter gọi service bên ngoài có map external response về Domain model / Application DTO trước khi trả qua Port không? (Không để DTO/response model của hệ thống bên ngoài lan vào `Application` hoặc `Domain`).

### [ ] Nhóm 5: 11 Nghĩa vụ Kỹ thuật Bắt buộc cho Payment (Payment Reliability Checklist)
- [ ] 🔴 **1. Idempotency bền vững:** Có phân biệt logical order, attempt, client idempotency key, correlation ID không? Có unique constraint / lock trong DB để chống tạo 2 giao dịch cho cùng 1 yêu cầu không?
- [ ] 🔴 **2. Trạng thái UNKNOWN:** Đã persist attempt và provider order correlation trước khi gọi ra cổng ngoài chưa? Khi timeout sau create, có đánh dấu UNKNOWN và query lại cùng orderCode thay vì tự tạo order mới hoặc chuyển thu tiền mặt bừa bãi không?
- [ ] 🟡 **3. Ngân sách Retry tối đa 4 lần:** Giới hạn 4 attempts có chỉ áp dụng cho definitive retryable failure không? (Broker redelivery và HTTP rate limiting tuyệt đối không tính là failure tiêu tốn attempt).
- [ ] 🔴 **4. Xác thực Chữ ký & Payload:** Callback / Webhook / Query có xác thực chữ ký HMAC SHA256 và kiểm tra khớp orderCode, amount, currency, payee không? (Tuyệt đối không tin screenshot, browser return url hay sample callback).
- [ ] 🔴 **5. Atomicity & Outbox/Inbox:** Cập nhật trạng thái tiền và ghi Outbox message có nằm chung trong một Local Transaction không? Consumer có cơ chế Inbox deduplication chống xử lý lặp không?
- [ ] 🟡 **6. Race giữa Tiền và Capacity:** Tiền đến sau khi hold expiry (hết hạn giữ chỗ) có được xử lý an toàn (thử reacquire capacity, nếu hết chỗ thì đưa vào hàng đợi hoàn tiền/reconciliation, không tự ý cướp chỗ người khác) không?
- [ ] 🟡 **7. Quy trình Hoàn tiền (Refunds):** Số dư khả dụng hoàn tiền (eligible refundable balance) có được trừ tạm ứng (reserve) nguyên tử, tính cả pending/unknown refund không? (Duyệt refund không đồng nghĩa payout thành công; surplus refund chỉ tạo 1 nghĩa vụ).
- [ ] 🟡 **8. Tách biệt Checkout & Departure:** Đã cover đủ tiền thì cho phép hoàn tất checkout thương mại, nhưng capacity chỉ được giải phóng khi có sự kiện xe rời bãi thực tế (actual departure) từ Operations.
- [ ] 🔴 **9. Tính giá & Làm tròn:** Dùng kiểu `decimal` trong toàn bộ quá trình tính; chỉ làm tròn `half-up` 1 lần duy nhất ở kết quả cuối cùng tới 1.000 VND; không tính phí 2 lần cho khoảng thời gian đã thanh toán.
- [ ] 🟢 **10. Định danh Provider:** payOS được dán nhãn đúng là payOS; các phương thức native VNPay/MoMo/ZaloPay dán nhãn riêng biệt, không mạo danh ví điện tử nếu đi qua cổng payOS QR.
- [ ] 🔴 **11. Bảo mật & Bí mật (Secrets):** API keys, Client Secret lấy từ cấu hình an toàn; không log, không in ra màn hình, không commit vào repository.


## 7. Ví dụ Minh họa Vi phạm vs Đúng chuẩn (Negative / Positive Code Examples)

> Mục này cung cấp code snippets cụ thể để Agent và Developer nhanh chóng nhận diện vi phạm kiến trúc.

### 7.1. Vi phạm Hướng Phụ thuộc (Dependency Direction Violation)

**❌ SAI — Domain phụ thuộc vào Infrastructure / tầng ngoài:**
```csharp
// File: SmartParking.PaymentService.Domain/Entities/PaymentOrder.cs
using SmartParking.PaymentService.Application.DTOs;     // ❌ Domain → Application
using SmartParking.PaymentService.Persistence;           // ❌ Domain → Persistence
using System.Text.Json;                                  // ❌ Domain dính serialization (DEC-PAY-ARCH-02)

public class PaymentOrder
{
    public string ToSnapshotJson()  // ❌ Serialization trong Domain
        => JsonSerializer.Serialize(this);
}
```

**✅ ĐÚNG — Domain thuần POCO, không biết tầng ngoài:**
```csharp
// File: SmartParking.PaymentService.Domain/Entities/PaymentOrder.cs
namespace SmartParking.PaymentService.Domain.Entities;

public class PaymentOrder
{
    public Guid Id { get; private set; }
    public decimal TotalAmount { get; private set; }
    public PaymentStatus Status { get; private set; }

    // Business rules thuần túy, không I/O
    public void MarkAsPaid(decimal confirmedAmount)
    {
        if (confirmedAmount != TotalAmount)
            throw new PaymentAmountMismatchException(TotalAmount, confirmedAmount);
        Status = PaymentStatus.Paid;
    }
}
```

---

### 7.2. Fat Controller vs Thin Controller

**❌ SAI — Controller chứa business logic và gọi trực tiếp DB:**
```csharp
// File: SmartParking.PaymentService.API/Controllers/PaymentController.cs
[HttpPost("create")]
public async Task<IActionResult> CreatePayment(CreatePaymentRequest request)
{
    // ❌ Tính toán nghiệp vụ trong Controller
    var tariff = await _dbContext.Tariffs.FindAsync(request.TariffId);
    var amount = tariff.PricePerHour * request.Hours;
    if (amount < 10000) amount = 10000; // ❌ Business rule trong Controller

    // ❌ Gọi trực tiếp DbContext
    var order = new PaymentOrder { Amount = amount };
    _dbContext.PaymentOrders.Add(order);
    await _dbContext.SaveChangesAsync();

    // ❌ Gọi trực tiếp SDK bên ngoài
    var payOS = new PayOS(_clientId, _apiKey, _checksumKey);
    var link = await payOS.createPaymentLink(...);

    return Ok(link);
}
```

**✅ ĐÚNG — Controller chỉ dispatch qua MediatR:**
```csharp
// File: SmartParking.PaymentService.API/Controllers/PaymentController.cs
[HttpPost("create")]
public async Task<IActionResult> CreatePayment(
    [FromBody] CreatePaymentOrderRequest request)
{
    var command = new CreatePaymentOrderCommand(
        request.ReservationId, request.VehiclePlate);

    var result = await _mediator.Send(command);

    return result.IsSuccess
        ? Ok(result.Value)
        : BadRequest(result.Error);
}
```

---

### 7.3. Handler gọi thẳng Implementation vs qua Port

**❌ SAI — Handler phụ thuộc trực tiếp vào implementation:**
```csharp
// File: SmartParking.PaymentService.Application/Usecase/CreatePayment/CreatePaymentHandler.cs
using SmartParking.PaymentService.Persistence;        // ❌ Application → Persistence
using SmartParking.PaymentService.Infrastructure;     // ❌ Application → Infrastructure

public class CreatePaymentHandler : IRequestHandler<CreatePaymentOrderCommand, Result>
{
    private readonly PaymentDbContext _dbContext;              // ❌ Concrete DbContext
    private readonly PayOSAdapter _payOSAdapter;              // ❌ Concrete Adapter

    public async Task<Result> Handle(...)
    {
        _dbContext.PaymentOrders.Add(order);                   // ❌ Trực tiếp DB
        var link = await _payOSAdapter.CreateLink(order);     // ❌ Trực tiếp Adapter
    }
}
```

**✅ ĐÚNG — Handler chỉ biết Port Interface:**
```csharp
// File: SmartParking.PaymentService.Application/Usecase/CreatePayment/CreatePaymentHandler.cs
namespace SmartParking.PaymentService.Application.Usecase.CreatePayment;

public class CreatePaymentHandler : IRequestHandler<CreatePaymentOrderCommand, Result>
{
    private readonly IPaymentOrderRepository _orderRepo;      // ✅ Interface từ Application
    private readonly IPaymentGatewayPort _paymentGateway;     // ✅ Port Interface
    private readonly IUnitOfWork _unitOfWork;                  // ✅ Interface

    public async Task<Result> Handle(
        CreatePaymentOrderCommand command, CancellationToken ct)
    {
        var order = PaymentOrder.Create(command.ReservationId, ...);
        await _orderRepo.AddAsync(order, ct);
        var checkoutLink = await _paymentGateway.CreateCheckoutLinkAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(checkoutLink);
    }
}
```

---

### 7.4. Vi phạm Anti-Corruption Layer

**❌ SAI — DTO của hệ thống bên ngoài lan vào Application:**
```csharp
// File: SmartParking.PaymentService.Application/Common/Interfaces/Ports/IParkingTariffPort.cs
using FacilityService.Contracts.V1;  // ❌ Import contract của service khác vào Application

public interface IParkingTariffPort
{
    // ❌ Trả về DTO thuộc về FacilityService — khi FacilityService đổi contract, Application bị vỡ
    Task<ParkingTariffResponseDto> GetTariffAsync(Guid structureId);
}
```

**✅ ĐÚNG — Port trả về model thuộc sở hữu của Application / Domain:**
```csharp
// File: SmartParking.PaymentService.Application/Common/Interfaces/Ports/IParkingTariffPort.cs
namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public interface IParkingTariffPort
{
    // ✅ Trả về model thuộc Application — Adapter ở Infrastructure lo việc map
    Task<ParkingTariffInfo> GetTariffAsync(Guid structureId, CancellationToken ct);
}

// File: SmartParking.PaymentService.Application/DTOs/ParkingTariffInfo.cs
public record ParkingTariffInfo(
    Guid StructureId,
    decimal PricePerHourVnd,
    string CurrencyCode,
    DateTime EffectiveFrom);
```

```csharp
// File: SmartParking.PaymentService.Infrastructure/Adapters/Outbound/ParkingTariffHttpAdapter.cs
namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

public class ParkingTariffHttpAdapter : IParkingTariffPort
{
    public async Task<ParkingTariffInfo> GetTariffAsync(
        Guid structureId, CancellationToken ct)
    {
        // Gọi FacilityService API
        var externalDto = await _httpClient.GetFromJsonAsync<ExternalTariffResponse>(
            $"/api/v1/structures/{structureId}/tariff", ct);

        // ✅ Map tại Adapter — contract bên ngoài đổi thì chỉ sửa ở đây
        return new ParkingTariffInfo(
            externalDto.Id,
            externalDto.RatePerHour,
            externalDto.Currency,
            externalDto.ValidFrom);
    }
}
```

---

### 7.5. IQueryable rò rỉ ra ngoài Persistence

**❌ SAI — Repository trả `IQueryable` để tầng trên tự query:**
```csharp
// File: SmartParking.PaymentService.Persistence/Repositories/PaymentOrderRepository.cs
public IQueryable<PaymentOrder> GetAll()  // ❌ IQueryable rò rỉ
    => _dbContext.PaymentOrders.AsQueryable();
```

**✅ ĐÚNG — Repository đóng gói truy vấn, trả kết quả cụ thể:**
```csharp
// File: SmartParking.PaymentService.Persistence/Repositories/PaymentOrderRepository.cs
public async Task<PaymentOrder?> GetByIdAsync(Guid id, CancellationToken ct)
    => await _dbContext.PaymentOrders
        .FirstOrDefaultAsync(o => o.Id == id, ct);

public async Task<IReadOnlyList<PaymentOrder>> GetPendingOrdersAsync(CancellationToken ct)
    => await _dbContext.PaymentOrders
        .Where(o => o.Status == PaymentStatus.Pending)
        .ToListAsync(ct);
```

---

### 7.6. DI Registration thiếu nhất quán

**❌ SAI — Port được khai báo nhưng không có Adapter đăng ký:**
```csharp
// Application khai báo interface
public interface INotificationPort { Task SendAsync(string message); }

// ❌ Infrastructure KHÔNG có class implement INotificationPort
// ❌ InfrastructureDependencyInjection.cs KHÔNG đăng ký — Runtime crash: Unable to resolve INotificationPort
```

**✅ ĐÚNG — Đầy đủ: Interface → Adapter → DI Registration:**
```csharp
// 1. Application: khai báo Port
public interface INotificationPort { Task SendAsync(string message, CancellationToken ct); }

// 2. Infrastructure: implement Adapter
public class EmailNotificationAdapter : INotificationPort
{
    public async Task SendAsync(string message, CancellationToken ct) { /* ... */ }
}

// 3. Infrastructure: đăng ký DI
// File: InfrastructureDependencyInjection.cs
services.AddScoped<INotificationPort, EmailNotificationAdapter>();
```
