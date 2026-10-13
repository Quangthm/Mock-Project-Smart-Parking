# Review report Reservation structure release receipts — 2026-10-13

## Kết luận

Commit `f794d60` chỉ cập nhật `LOCAL-SETUP.md`. Cách khắc phục thiếu bảng trong report khớp với code và migration hiện tại; chưa thấy lỗi chặn riêng commit tài liệu này. Sau khi chuyển authentication test sang PostgreSQL thật và khởi động Docker, 33/33 test Parking/Reservation pass, không skip; upgrade trên volume cũ mô phỏng cũng pass. Có thể mở PR để leader review, nhưng vẫn cần resolve conflict và review phạm vi toàn nhánh trước merge. Các kết quả fail/skip bên dưới là lịch sử trước lần kiểm chứng thực tế cuối cùng.

Phạm vi đối chiếu: report trong Downloads, commit `f794d60`, compose, migration runner, schema receipts, code acquire/release và test liên quan. Đây không phải review đầy đủ tất cả thay đổi của nhánh.

## Đối chiếu report

- `05.12-Reservation-Structure-Receipts.sql` tạo bảng với `token`, `tenant_id`, `site_id`, `outcome`, `completed_at`, khớp các truy vấn của `StructureSafetyStore`.
- Compose mount migration này sau baseline, overlay và `05.10`. Tên mount thực tế là `04-receipts.sql`; report ghi `04-Reservation-Structure-Receipts.sql`, cần sửa chi tiết này.
- `apply-workflow-migrations.ps1 -Service Reservation -SchemaRoot .` kiểm tra các bảng nền tảng, áp `05.10`, rồi `05.12`. `05.10` dùng ADD COLUMN IF NOT EXISTS/CREATE OR REPLACE FUNCTION/drop-create trigger; `05.12` dùng CREATE TABLE IF NOT EXISTS. Không nên mô tả cả hai script chỉ bằng CREATE TABLE IF NOT EXISTS.
- Truy vấn `to_regclass(...) IS NOT NULL` trả `t` chỉ chứng minh bảng tồn tại. Chưa chứng minh schema đúng hoặc thao tác load/edit đã hết HTTP 500. Cần gọi lại request từng lỗi, ghi status và kiểm tra log sau migration.
- Không xác minh độc lập được timestamp volume và kết quả DB cũ của thành viên; đó là bằng chứng do report cung cấp.
- Working tree ban đầu sạch; danh sách file chưa commit trong report không còn phản ánh checkout hiện tại.

## Kiểm tra thực hiện

Lệnh:

```powershell
dotnet test tests/Services/SmartParking.ParkingService.Tests/SmartParking.ParkingService.Tests.csproj --no-restore --verbosity minimal
```

Kết quả: 24 passed, 1 failed, 8 skipped, tổng 33. Build các project Parking/Reservation liên quan thành công.

Test `GatePoolPreservesConfiguredPasswordDuringAuthentication` fail với `EndOfStreamException` tại `GateAuthenticationTests.cs:29`; chạy lại riêng bằng `--no-build --no-restore --filter 'FullyQualifiedName~GateAuthenticationTests'` vẫn fail. Chưa xác định nguyên nhân; không quy lỗi này cho commit tài liệu `f794d60`. Cần điều tra hoặc có kết quả CI/môi trường khác trước khi xác nhận toàn nhánh xanh.

8 test PostgreSQL skip vì chưa cấu hình `SMARTPARK_STRUCTURE_TEST_CONNECTION`. Docker daemon không khả dụng nên không chạy được test migration trên volume cũ hoặc xác minh DB/API thực tế. Repo có `scripts/test-workflow-upgrade.ps1` kiểm tra pre-05.12, chạy migration hai lần, giữ dữ liệu và restart volume; cần chạy script này khi Docker sẵn sàng.

## PR vào develop

- Ref local `origin/develop`: `d39cc7d310c3a29fc7beaa93e2491eab0dd603a1`.
- HEAD/local `origin/develop-fix-application`: `f794d60d9f70e3f2905299a673f08f59141e0b7e`.
- Diff ba chấm: 75 file, 12.406 dòng thêm, 1.079 dòng xóa. PR cả nhánh bao gồm nhiều thay đổi ngoài bản sửa receipts.
- Merge mô phỏng bằng `git merge-tree --write-tree origin/develop HEAD`, dùng object directory riêng trong `.cache`, xác nhận conflict ở `.gitignore` và `FE/src/lib/store.ts`; không sửa branch/index/working tree.
- Không xác minh được ref mới nhất trên GitHub do kết nối mạng thất bại. Kết quả conflict/phạm vi áp dụng cho ref đang có ở local.
- `git diff --check origin/develop...HEAD` báo trailing whitespace trong tài liệu 3D handoff; mức độ thấp.

## Trước khi merge

1. Điều tra test authentication fail và lấy kết quả test/CI phù hợp.
2. Chạy upgrade test trên DB cô lập; gọi lại API từng lỗi để bổ sung bằng chứng sau migration.
3. Resolve conflict theo develop mới nhất, giữ các tính năng FE hiện có rồi kiểm tra lại.
4. Mô tả đúng phạm vi toàn nhánh trong PR; sửa các chi tiết nhỏ và giới hạn kết luận trong report.

Có thể mở draft PR ngay để leader review. Chưa nên đánh dấu toàn nhánh đã kiểm chứng xong để merge.

## Cập nhật: chuyển authentication test sang PostgreSQL thật

Theo yêu cầu, `GateAuthenticationTests` đã bỏ protocol peer giả và dùng server PostgreSQL trong `SMARTPARK_STRUCTURE_TEST_CONNECTION`. Test tạo database và login có tên UUID riêng, kiểm tra password sai bị từ chối với SQLSTATE `28P01`, password đúng mở được gate pool, xác nhận user/database/application_name, lấy và nhả advisory lock, rồi mở lại lease. Cuối test dọn database/login được tạo, không sửa tài khoản hay dữ liệu ứng dụng.

Connection phải dùng TCP với password authentication (SCRAM hoặc MD5), tài khoản chạy fixture cần quyền CREATE DATABASE và CREATE ROLE cùng quyền gán ownership cho role test (superuser local có đủ quyền). Cấu hình trust không đủ để kiểm chứng password và sẽ làm test fail ở nhánh password sai.

```powershell
# Đặt connection string của PostgreSQL local/test trong biến môi trường,
# theo host/port và credential trên máy; không commit hoặc in password.
$env:SMARTPARK_STRUCTURE_TEST_CONNECTION = '<local-postgres-connection>'
dotnet test tests/Services/SmartParking.ParkingService.Tests/SmartParking.ParkingService.Tests.csproj --filter 'FullyQualifiedName~GateAuthenticationTests' --verbosity minimal
```

Sau thay đổi: build thành công, 24 passed, 0 failed, 9 skipped. Test PostgreSQL mới bị skip vì chưa có connection string. Đây không phải kết quả xác thực DB thành công và không thay thế kết quả fail lịch sử ở trên. Docker daemon chưa chạy, checkout chưa có `.env` và không phát hiện listener trên các port PostgreSQL local thông dụng; chưa chạy được test với DB thật ở môi trường hiện tại.

## Kết quả thực tế cuối cùng — 2026-10-13

Đã khởi động Docker Desktop và chạy PostgreSQL 16.15 từ image `postgres:16-alpine`, cùng dòng phiên bản compose dự án. Dùng container PostgreSQL thật với SCRAM, port localhost tự cấp và database/login UUID riêng. Các container ứng dụng local hiện có đang stopped, không thay đổi dữ liệu của chúng.

- Bộ test Parking/Reservation: **33 passed, 0 failed, 0 skipped**; build 0 warning, 0 error.
- Authentication test mới pass: password sai bị từ chối với `28P01`, password đúng kết nối được, xác nhận identity/application_name, lấy/nhả advisory lock và mở lại lease.
- Cả 8 test PostgreSQL workflow đã thực sự chạy và pass, bao gồm receipt replay, rollback đồng bộ receipts/projections/hold và phục hồi sau mất response.
- `scripts/test-workflow-upgrade.ps1` pass: baseline thiếu `05.11/05.12`, apply migration hai lần, restart volume và xác nhận bảng/dữ liệu sentinel vẫn tồn tại.
- Kiểm tra trước khi xóa container: không còn database test hoặc login test; container/volume test đã được dọn.
- Chưa thực hiện smoke test HTTP qua frontend/API đang chạy; kết quả này kiểm chứng integration với PostgreSQL thật, không phải toàn bộ hành trình UI.

Bằng chứng local: `.cache/review-results/parking-real-postgres.trx` và `.cache/review-results/workflow-upgrade-real-postgres.log`. Runner kiểm chứng: `.cache/run-real-postgres-review.ps1`. Thay đổi test và report chưa commit/push.
