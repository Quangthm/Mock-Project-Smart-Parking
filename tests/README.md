# Chạy backend tests trên máy local

`tests/Services/` chứa unit tests, integration tests của SmartPark và helper hỗ trợ.
Giữ `WorkflowTestSupport.cs` và cả ba `SessionOwnershipPersistenceTests`: chúng kiểm tra
workflow thật, FK/trigger sở hữu và refresh với trạng thái DB mới, dùng dữ liệu giả.
Repository guards nằm ở `scripts/architecture/SmartParking.Architecture.Tests/`;
setup/bảo trì nằm ở `scripts/`. Phân loại theo guarantee của assertion, không theo tên file.

## Chạy nhanh, không cần dịch vụ ngoài

Từ thư mục gốc với .NET 10 SDK:

```powershell
dotnet test SmartParking.slnx -c Release --filter 'Category!=Postgres&Category!=PostgresRabbitMQ&Category!=LiveBroker&Category!=Architecture'
# Tương đương, kèm kiểm tra TRX và không cho phép skip:
.\scripts\test-backend.ps1 -Mode Offline
```

Nhóm này dùng fake, EF InMemory, metadata và SMTP loopback; không cần PostgreSQL,
RabbitMQ, Gmail hay credential SMTP thật. Release tránh xung đột DLL Debug trong Visual Studio.

| Trait VSTest | Prerequisite | Guarantee |
|---|---|---|
| Không thuộc bốn category dưới | Không có dịch vụ ngoài | Logic, wiring, contract, doubles |
| `Postgres` | PostgreSQL 16 riêng | FK/trigger, transaction/rollback, row/advisory locks, competing connections, password rejection |
| `PostgresRabbitMQ` | PostgreSQL + RabbitMQ riêng | Hai UserService delivery tests: routing, inbox/SignalR, outbox recovery |
| `LiveBroker` | Cùng broker riêng | Ba Payment tests mới trên HEAD hiện tại, persistence dùng fake |
| `Architecture` | Checkout đầy đủ | 12 case kiểm tra source/project toàn repository |

Các lớp trộn như `PostgresStructureWorkflowTests`, `DirectRolePersistenceTests` và
`ProfileOutboxTests` có trait theo method. Custom Fact có `Skip` không tự tạo category.

## Chuẩn bị fixture riêng an toàn

Dùng PostgreSQL 16 **chỉ dành cho test**, xác thực password; không dùng deployment,
Supabase pooler hoặc database dùng chung. Tài khoản cần `CREATE DATABASE`; gate-auth test
còn tạo/xóa login role và cấp ownership database. Schema cần `uuid-ossp` và `btree_gist`.
Fixture tạo DB tên GUID rồi xóa; nếu kill tiến trình, DB tạm có thể còn lại.

Lưu `SMARTPARK_AUTH_TEST_CONNECTION` và `SMARTPARK_STRUCTURE_TEST_CONNECTION` vào file env
riêng không commit; hai connection có thể trỏ cùng server disposable. Với DB-only:

```powershell
.\scripts\configure-test-settings.ps1 -EnvPath <private-test-env> -DatabaseOnly
.\scripts\test-backend.ps1 -Mode Postgres -Settings smartpark.local.runsettings
```

Generator giữ fallback `SMARTPARK_DEV_PASSWORD`, `SMARTPARK_DB_USER_PORT` /
`SMARTPARK_DB_PARKING_PORT` (5441 / 5442) cho `user-db` / `facility-db`.
Chỉ dùng fallback nếu đã xác nhận đó là server dành riêng cho test.
`-DatabaseOnly` chỉ ghi hai connection và không cần Docker Compose.
`-OutputPath <file>` giúp giữ settings cá nhân hiện có. Mặc định generator thay thế
`smartpark.local.runsettings` (Git ignore); không ghi đè settings cá nhân chưa xác nhận.
XML writer bảo toàn ký tự đặc biệt trong credential. Generator không khởi động container.

### Tạo PostgreSQL disposable nếu chưa có fixture

PowerShell trong checkout test, Docker Desktop đang chạy; chọn port loopback còn trống:

```powershell
if (Test-Path .\smartpark.local.runsettings) { throw 'Giữ settings hiện có hoặc dùng checkout test khác.' }
$pgPort = 55432
$pgPassword = [guid]::NewGuid().ToString('N')
$env:POSTGRES_PASSWORD = $pgPassword
try {
    $pgId = docker run --rm --detach --publish "127.0.0.1:${pgPort}:5432" --env POSTGRES_PASSWORD postgres:16-alpine
    if ($LASTEXITCODE -ne 0) { throw 'Không khởi động được PostgreSQL test.' }
} finally { Remove-Item Env:POSTGRES_PASSWORD }
$ready = $false
for ($i = 0; $i -lt 45; $i++) {
    docker exec $pgId pg_isready -h 127.0.0.1 -U postgres -d postgres *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 1
}
if (!$ready) { throw 'PostgreSQL test chưa sẵn sàng.' }
$dsn = "Host=127.0.0.1;Port=$pgPort;Database=postgres;Username=postgres;Password=$pgPassword"
$testEnv = New-TemporaryFile
@("SMARTPARK_AUTH_TEST_CONNECTION=$dsn", "SMARTPARK_STRUCTURE_TEST_CONNECTION=$dsn", 'SMARTPARK_RABBIT_TEST_PORT=5679') |
    Set-Content -LiteralPath $testEnv.FullName -Encoding UTF8
.\scripts\configure-test-settings.ps1 -EnvPath $testEnv.FullName -DatabaseOnly
```

Schema mặc định lấy từ checkout hiện tại; chỉ đặt `SMARTPARK_SCHEMA_ROOT` khi cần override
có chủ đích. Registration migration lấy qua `ServiceSchema.ScriptPath("test/05.4-Driver-Registration-OTP.sql")`,
vẫn chạy hai lần để kiểm tra idempotence.

### Thêm một RabbitMQ cho tất cả delivery tests

`compose.tests.yml` là nguồn duy nhất cho giá trị broker test. Generator đọc cấu hình Compose
đã render vào bộ nhớ, không in credential. Cần Docker Compose CLI để sinh settings broker;
không cần daemon chỉ để render. Dùng cùng env file/shell cho Compose và generator.
Port test mặc định 5679 bind loopback, độc lập application broker 5672.

```powershell
$brokerProject = 'spark330-' + [guid]::NewGuid().ToString('N').Substring(0,8)
docker compose -p $brokerProject --env-file $testEnv.FullName -f compose.tests.yml up -d --wait rabbitmq
if ($LASTEXITCODE -ne 0) { throw 'RabbitMQ test chưa sẵn sàng.' }
.\scripts\configure-test-settings.ps1 -EnvPath $testEnv.FullName
.\scripts\test-backend.ps1 -Mode PostgresRabbitMQ -Settings smartpark.local.runsettings
.\scripts\test-backend.ps1 -Mode LiveBroker -Settings smartpark.local.runsettings
```

Override bằng `SMARTPARK_RABBIT_TEST_PORT`; generator ghi cùng giá trị vào
`SMARTPARK_PROFILE_RABBITMQ_PORT`. Hai delivery tests phải cùng port.
Override **chỉ dành cho broker test**: `SMARTPARK_TEST_RABBITMQ_USER`,
`SMARTPARK_TEST_RABBITMQ_PASSWORD`, `SMARTPARK_TEST_RABBITMQ_VHOST`.
Compose mặc định dùng tài khoản disposable `identity_test`, vhost `/`; generator ánh xạ
chính xác sang `SMARTPARK_RABBITMQ_USER/PASSWORD/VHOST` mà test đọc.
Không lấy credential application. Biến shell được Compose ưu tiên hơn env file.

HTTP tests chỉ cần DB giữ production outbox capture nhưng không khởi động broker connection
hay workflow delivery timer. Delivery tests chủ động bật topology/consumer và outbox worker thật.
Capture-only/rollback vẫn kiểm tra commit nguyên tử và không có event sau rollback.

## Lệnh trực tiếp và cách đọc kết quả

```powershell
dotnet test SmartParking.slnx -c Release --settings smartpark.local.runsettings --filter 'Category=Postgres'
dotnet test SmartParking.slnx -c Release --settings smartpark.local.runsettings --filter 'Category=PostgresRabbitMQ'
dotnet test SmartParking.slnx -c Release --settings smartpark.local.runsettings --filter 'Category=LiveBroker'
dotnet test scripts/architecture/SmartParking.Architecture.Tests/SmartParking.Architecture.Tests.csproj -c Release
# Architecture vẫn nằm trong solution nên CI hiện tại chạy đúng một lần:
dotnet test SmartParking.slnx -c Release --filter 'Category=Architecture'
# Full run giữ skip có điều kiện khi thiếu fixture:
dotnet test SmartParking.slnx -c Release --settings smartpark.local.runsettings
```

Khi **yêu cầu integration run**, dùng `test-backend.ps1`: kiểm tra prerequisite,
exit code và TRX; báo lỗi nếu không có test hoặc còn skip. Lệnh `dotnet test` trực tiếp
vẫn cho phép skip; exit 0 không chứng minh integration đã chạy.
Server được cấu hình nhưng unreachable, sai password/port hoặc assertion sai phải là failure.
Không probe TCP để biến server lỗi thành skip. `-NoBuild` chỉ dùng sau build Release mới nhất.
TRX lưu dưới `TestResults/backend-*`.

Focused filter `FullyQualifiedName~SessionOwnershipPersistenceTests` phải chạy đủ ba case;
`FullyQualifiedName~GateAuthenticationTests` phải chạy một case; `PostgresRabbitMQ` có hai case.
Phân biệt assertion failure, skipped prerequisite và warning theo mã/logger.
Bốn Facility fixture assertions đã lỗi trước SPARK-330; cần Facility/FE owner xác nhận contract
trước khi sửa fixture/expectation; input redaction phải còn trường nhạy cảm để kiểm tra masking.

## Visual Studio / Test Explorer

`tests/Directory.Build.props` nạp `smartpark.local.runsettings` khi file tồn tại.
Reload solution hoặc chọn **Test > Configure Run Settings > Select Solution Wide runsettings File**.
File tùy chọn qua `-OutputPath` cần chọn thủ công. `.env` không được Test Explorer tự đọc.
Group theo **Traits** hoặc lọc Category; không chọn cả class nếu trộn offline/integration.
Vàng là **Skipped**, không phải compiler warning; đọc lý do trong Output/Test.
Regenerate settings khi đổi port/credential. Settings mới đủ cho cả hai UserService delivery tests
và Payment LiveBroker, không cần mở Visual Studio từ shell chứa credential.

## Dọn fixture do phiên này tạo

Chỉ dùng ID/project do ví dụ tạo; không dừng service hoặc xóa volume của project khác:

```powershell
# Chỉ khi đã tạo broker:
docker compose -p $brokerProject --env-file $testEnv.FullName -f compose.tests.yml down -v
docker stop $pgId
Remove-Item -LiteralPath $testEnv.FullName
# Chỉ xóa settings do phiên disposable này tạo:
Remove-Item -LiteralPath .\smartpark.local.runsettings
```

Đóng shell sau khi kiểm tra. Không commit settings, env riêng, credential hoặc raw log chứa secret.
