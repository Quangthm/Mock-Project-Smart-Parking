# Architecture refactor handoff — 2026-10-19

## Phạm vi đã thực hiện

- Finding 4: Parking/Reservation API gọi Application service; port/DTO thuộc Application; SQL thuộc Persistence; HTTP clients thuộc Infrastructure.
- Finding 5: User account/MFA/driver/owner/user-management workflows chuyển sang Application, sử dụng IIdentityWorkflowStore. EF/query/row locks giữ trong Persistence.
- Finding 6: JWT signing/key adapter ở Infrastructure, JwtBearer/HTTP ở API, role/session policy và OTP orchestration ở Application.
- Finding 7: owner approval commit intent trước, gọi provisioning ngoài transaction, finalize trong transaction mới; retry cùng application ID và kiểm tra lại quyền/state/payload. Recovery hiện qua explicit retry, chưa có worker riêng.
- Finding 8 (chỉ chatbot): Assistant chuyển từ User sang IoT & Recognition. Đây vẫn là scaffold mặc định tắt; chưa có HTTP host/provider thật. Chỉ FAQ/PARKING_SEARCH, không autonomous financial/security writes.
- Finding 9: architecture dependency/source guardrails và CI. CI hiện chạy unit/architecture suite; để chạy đủ integration suite phải cung cấp PostgreSQL và RabbitMQ như bên dưới.
- Giữ và tích hợp các thay đổi owner onboarding có sẵn; xem OWNER_ONBOARDING_BE_HANDOFF.md cùng thư mục.

Nguồn yêu cầu: docs/01-requirements/srs/smartpark-srs-v1.0.md (RC3), modules/10-ai-assistance.md và docs/02-architecture/service-boundaries/ownership-and-events.md.

## Các commit theo thứ tự phụ thuộc

1. `5cdb554`: migrations User/Facility onboarding và migration runner.
2. `07e7336`: Application ports cho Parking/Reservation, directory persistence và HTTP adapters.
3. `5d8cc41`: User workflows, authentication layering, onboarding và approval coordination/tests.
4. `f6243d4`: chuyển Assistant scaffold/tests sang IoT, cập nhật solution.
5. Commit chứa tài liệu này: architecture guards, CI và bàn giao.

Push cả chuỗi commit. Không cherry-pick riêng commit cuối vì guardrails kiểm tra trạng thái sau refactor. Chưa push từ phiên làm việc này.

## Phần còn mở cho thành viên tiếp theo

1. Checkout cần tách accepted checkout/financial clearance/actual departure; không giải phóng occupancy quá sớm.
2. Walk-in admission cần atomic capacity/claim và test hai xe tranh chỗ cuối.
3. Operations cần authoritative occupancy; Facility giữ config/physical resources. Giải quyết ownership trước khi nối lại các client dormant.

Gate application wrappers hiện giữ nguyên business semantics, chưa giải quyết ba finding này. Incident/support/notification sai owner còn lại trong finding 8 cũng chưa di chuyển. Không coi build/test xanh là toàn bộ SRS đã hoàn thành.

## Kiểm chứng

Hai subagent GPT-6.1 Sol medium review User workflow/approval và Parking/Reservation/Assistant/boundaries. Finding về test bỏ sót HttpClient/concrete-store parameter đã sửa và review lại.

Đã chạy full solution trên Docker PostgreSQL 16 và RabbitMQ 4 bằng SQL/migration có sẵn: **160 pass, 0 fail, 0 skip** (User 122, Parking 37, IoT 1); build 0 warnings/errors. Bao gồm approval concurrency/approve-reject, migration, rollback, MFA/OTP concurrency, outbox → RabbitMQ → inbox/projection → SignalR. Không phải E2E sáu service hoặc live AI/payment provider test. Log local: .cache/postgres-rabbit-full-tests.log.

## Chạy lại đủ suite

Container test độc lập; fixtures tự tạo/xóa database tên ngẫu nhiên và nạp SQL repo. Chỉ dùng tài khoản test trên localhost. Hai tên container bên dưới phải chưa tồn tại.

```powershell
docker run -d --name smartpark-architecture-pg-test-20261019 -e POSTGRES_USER=smartpark_test -e POSTGRES_PASSWORD=local_test_20261019 -e POSTGRES_DB=postgres -p 127.0.0.1:55439:5432 --tmpfs /var/lib/postgresql/data postgres:16-alpine
docker run -d --name smartpark-architecture-rabbit-test-20261019 -e RABBITMQ_DEFAULT_USER=identity_test -e RABBITMQ_DEFAULT_PASS=local-disposable-identity-test -p 127.0.0.1:55679:5672 --tmpfs /var/lib/rabbitmq rabbitmq:4-alpine
# Chờ cả hai readiness checks thành công.
docker exec smartpark-architecture-pg-test-20261019 pg_isready -U smartpark_test -d postgres
docker exec smartpark-architecture-rabbit-test-20261019 rabbitmq-diagnostics -q check_port_connectivity
$env:SMARTPARK_AUTH_TEST_CONNECTION='Host=127.0.0.1;Port=55439;Database=postgres;Username=smartpark_test;Password=local_test_20261019'
$env:SMARTPARK_STRUCTURE_TEST_CONNECTION=$env:SMARTPARK_AUTH_TEST_CONNECTION
$env:SMARTPARK_RABBIT_TEST_PORT='55679'
dotnet restore SmartParking.slnx
dotnet test SmartParking.slnx --no-restore --verbosity normal
# Sau khi test kết thúc, dọn đúng hai container test vừa tạo.
docker rm -f smartpark-architecture-pg-test-20261019 smartpark-architecture-rabbit-test-20261019
```
