# SPARK-188 — Luồng sửa cấu trúc bãi và ranh giới microservice

Cập nhật 12/10/2026 trên nhánh develop-fix-application. Tài liệu này mô tả implementation hiện tại, thay cho hướng dẫn core dùng chung DB trước đây.

## Trách nhiệm từng tầng

- **Parking API:** xác thực và lấy scope qua UserService, chuyển DTO sang StructureEdit, gọi Application.
- **Parking Application:** kiểm tra quyền, giữ gate theo bãi, chuẩn bị operation/token, gọi Reservation, gọi Domain, yêu cầu lưu trong transaction và đồng bộ kết quả.
- **Parking Domain:** quy tắc cấu trúc, xóa/move slot, occupancy và capacity.
- **Parking Persistence:** SQL và transaction Parking DB, khóa các dòng, audit, operation và journal đồng bộ. Không gọi HTTP.
- **Parking Infrastructure:** HTTP adapters và hosted worker gọi use case phục hồi.
- **Reservation API:** xác thực service và gọi StructureSafetyService trong Application.
- **Reservation Domain:** xác thực scope/outcome, capacity và backup hợp lệ.
- **Reservation Persistence:** hold, snapshot, projection, capacity và receipt trong Reservation DB.

SQL trigger guard_structure_edit vẫn được giữ để bảo vệ các đường ghi reservation/allocation/session/capacity hiện có. Chưa chuyển toàn bộ quy tắc lifecycle của Reservation từ trigger sang Domain; không được bỏ trigger trước khi mọi đường ghi có bảo vệ tương đương.

## Đọc code theo luồng

1. ParkingStructureService.EditAsync: điều phối use case.
2. IParkingStructureEdit: contract của transaction cục bộ.
3. PostgresParkingStructureRepository.BeginEditAsync và ParkingStructureEdit.CommitAsync: row lock, lưu cấu trúc/audit/kết quả đồng bộ, commit.
4. StructureOutcomeBuilder: chuyển aggregate đã được Domain kiểm tra thành kết quả gửi Reservation.
5. StructureSynchronizationService: gửi lại kết quả và nhận acknowledgement.
6. StructureSafetyService → StructureSafetyStore.ReleaseAsync: kiểm tra Domain, cập nhật projection/capacity/receipt và bỏ hold trong một transaction Reservation.

~~~text
Parking Application: kiểm tra quyền + gate theo bãi
    → phục hồi operation trước đó nếu còn pending
    → lưu token với state preparing trong Parking DB
    → HTTP: Reservation lấy advisory lock, ghi hold và trả snapshot
    → mở transaction Parking, khóa cấu trúc
    → Domain kiểm tra và thực hiện thay đổi
    → lưu cấu trúc + audit + outcome, chuyển journal sang ready
    → commit và dispose transaction Parking
    → HTTP: Reservation cập nhật projection/capacity + receipt + bỏ hold
    → Parking đánh dấu journal và operation hoàn tất
~~~

Không có transaction Parking mở trong lúc chờ HTTP. Gate theo bãi là advisory lock theo **session**, dùng một connection riêng từ pool gate tách biệt với query pool; gate không phải distributed transaction. Hai service vẫn commit độc lập.

Pool gate có giới hạn mặc định 16 connection, chỉnh qua Services:ParkingGateLimit (tối thiểu 2). Admission và việc lấy advisory lock đều không chờ khi đang bận; API trả STRUCTURE_BUSY để caller retry. Operation cần một operation gate và một site gate. Cần tính tổng giới hạn connection của các service khi cấu hình PostgreSQL.

Pool gate nhận connection string gốc từ cấu hình; không lấy lại từ NpgsqlDataSource.ConnectionString vì property này có thể che password.

## Phục hồi và idempotency

Bảng structure_synchronizations thuộc Parking DB:

| Trạng thái | Ý nghĩa |
|---|---|
| preparing | Token đã được lưu, chưa có physical commit được xác nhận trong journal |
| aborting | Phục hồi đã chặn request cũ commit; gửi abort tới Reservation |
| ready | Cấu trúc và exact outcome đã commit atomically trong Parking DB |
| completed | Reservation đã xác nhận; Parking đã lưu acknowledgement |

Worker chạy mỗi 10 giây. Worker và request dùng cùng site gate, nên worker bỏ qua request đang xử lý. Nếu connection giữ gate bị mất, transition preparing → aborting vẫn bảo vệ race với physical commit: transaction sửa cấu trúc chỉ được commit nếu nó chuyển được đúng journal từ preparing → ready.

Khi physical commit thắng race, recovery đọc payload ready và gửi kết quả, không gửi abort. Khi recovery thắng, cập nhật journal của request thất bại và toàn bộ thay đổi vật lý phải rollback.

Bảng structure_release_receipts thuộc Reservation DB lưu token, scope và exact outcome. Gửi lại cùng payload được chấp nhận kể cả khi một hold mới đã tồn tại; payload khác bị từ chối. Token đã release không thể tạo hold mới. Receipt được lưu cùng transaction với projection/capacity và việc bỏ hold.

Giữ ba bảng structure_edit_holds, structure_site_state, structure_removed_resources. Các bảng này không tạo join hoặc foreign key xuyên DB.

Các hold cũ không có journal phải phục hồi bằng scripts/recover-structure-hold.ps1 sau khi dừng toàn bộ Parking instances. Script ưu tiên exact payload nếu đã có journal; chỉ dựng authoritative snapshot cho legacy hold.

## Migration và rollout

Dừng các phiên bản service cũ trước khi rollout để chúng không bỏ qua gate/journal mới. Áp dụng baseline/overlay của đúng service, rồi chạy:

~~~powershell
./scripts/apply-workflow-migrations.ps1 -Service Parking -SchemaRoot <schema-checkout>
./scripts/apply-workflow-migrations.ps1 -Service Reservation -SchemaRoot <schema-checkout>
~~~

Script chạy migration cũ trước, rồi migration mới tương ứng:

- Parking DB: 05.11-Parking-Structure-Synchronization.sql.
- Reservation DB: 05.12-Reservation-Structure-Receipts.sql.

Mỗi file chỉ chạy trên DB của service được ghi trong tên file. Chạy service mới sau khi cả hai migration đã được áp dụng. Task refactor không tự sửa DB đang sử dụng.

## Kiểm thử và giới hạn

~~~powershell
dotnet test SmartParking.slnx
~~~

ApplicationWorkflowTests kiểm tra authorization, thứ tự HTTP/transaction, Domain rejection, lost response, recovery sau restart, gate đang được giữ và commit thắng race với recovery. Các test Domain cũ vẫn giữ nguyên. Test mới kiểm tra Operator bị thu hồi quyền trong lúc lấy hold không được commit. GateAuthenticationTests dùng một protocol peer local với password giả để kiểm chứng forwarding credential mà không cần PostgreSQL cài trên máy.

StructureHttpContractTests dùng API TestServer và HTTP adapter thật, chỉ thay boundary Persistence bằng fake. Các case kiểm tra JSON/token/outcome, idempotent release, 400 khi outcome không hợp lệ, 409 khi hold xung đột và 401 khi thiếu service key.

PostgresStructureWorkflowTests tạo hai DB test riêng, áp dụng SQL của repository rồi kiểm tra:

- Physical commit + lost acknowledgement + replay từ instance mới.
- Trigger chặn ghi capacity khi có hold.
- Retry receipt không giải phóng hold mới, token đã abort không được dùng lại.
- Physical commit thắng race với recovery.
- Recovery thắng race khiến physical writes rollback.
- Capacity/quota/tombstone/receipt được đồng bộ khi replay.
- Projection thất bại rollback đồng thời receipt, trạng thái, tombstone và việc bỏ hold.
- Giữ hai gate không chiếm connector của query pool có giới hạn 2.
- Foreground cập nhật lỗi muộn trả operation completed thay vì chuyển trạng thái lùi.

Case race chờ pg_stat_activity xác nhận recovery đang bị row lock chặn trước khi commit. PostgreSQL test connection cần MaxPoolSize tối thiểu 3 cho transaction, recovery và connection quan sát; mẫu bên dưới dùng giới hạn mặc định.

Chạy trên PostgreSQL test riêng, với quyền CREATE DATABASE:

~~~powershell
$env:SMARTPARK_STRUCTURE_TEST_CONNECTION = 'Host=127.0.0.1;Port=<port>;Database=postgres;Username=<test-user>;Password=<test-password>'
dotnet test tests/Services/SmartParking.ParkingService.Tests/SmartParking.ParkingService.Tests.csproj
~~~

Không có biến môi trường này thì các test PostgreSQL được báo **skipped**, không phải passed. Kiểm thử bằng fake không thay thế kiểm thử khóa/transaction thực tế.

Phạm vi còn giữ nguyên: chưa triển khai automatic reallocation/refund khi giảm cấu trúc đang có cam kết; Domain tiếp tục chặn thay đổi đó. Chưa thêm Message Broker và chưa chuyển toàn bộ reservation writers sang Application. Journal/receipt được giữ để replay; cần chính sách lưu trữ/dọn lịch sử khi đưa lên môi trường vận hành lâu dài.

## Review chéo sau refactor

Ba agent ở reasoning medium review độc lập theo các mảng kiến trúc/quyền, transaction/phục hồi và migration/API/kiểm thử. Các lỗi đã được sửa và review lại:

- Khôi phục kiểm tra quyền Operator sau HTTP acquire, trước transaction sửa cấu trúc.
- Tách pool session gates khỏi query pool và giới hạn admission để tránh starvation.
- Conditional update gặp operation đã completed trả đúng trạng thái cuối cùng, vẫn giữ tenant/actor scope.
- Giữ password của pool gate bằng connection string gốc.
- Tăng coverage capacity/rollback/lock wait/HTTP và credential forwarding.

Review source và các test chạy được không thay thế xác nhận PostgreSQL integration trong môi trường thật.
