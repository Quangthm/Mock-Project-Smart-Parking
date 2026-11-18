# Prompt — Kiểm thử đầy đủ SPARK-340 trước khi mở PR

Bạn là tester/QA độc lập cho SPARK-340 và các task con368/369/370. P5 implementation review đã đóng theo quyết định user ngày18/11/2026. Nhiệm vụ hiện tại là kiểm thử lại toàn bộ chức năng trong phạm vi, thu evidence và đưa verdict **đủ/chưa đủ chuẩn bị PR**. Không tự tạo PR, merge, commit/push hoặc đóng parent Jira. Không sửa source để làm test xanh; phát hiện implementation defect thì báo đúng owner và tái hiện.

## 1. Baseline và hướng dẫn

Repo `Quangthm/Mock-Project-Smart-Parking`, branch `feat/SPARK-340-admin-platform-policies`. Fetch/pull --ff-only trong checkout tester đã giữ an toàn local work, ghi actual HEAD và working tree. Source implementation được review tại fb4d2ce4; các commit docs sau đó không thay đổi source. Không hardcode các số test cũ làm kết quả run mới.

Đọc AGENTS.md → ai/AGENTS.md → coding/FE guide áp dụng → session mới nhất → P5 closure ngày18/11 → independent FA–FC review → tester handoff/U01–U20 → F6–F8 rollout guide. Collection hiện **58 request**, gồm4 F8 HTTP negatives. Các report54 request chỉ là lịch sử.

## 2. Môi trường và giới hạn quyền

- Ưu tiên máy/stack tester riêng với secrets riêng và DB có schema đúng. Không dùng credentials thật của PayOS, không gửi HTTP checkout/charge đến provider thật. FIXTURE dùng cho functional test; optional SANDBOX provider credentials thiếu thì ghi SKIP.
- Fresh test DB có init DDL hiện tại. Với DB tester cũ, owner review và áp additive script trước binary mới theo runbook. Công cụ repair chỉ xử lý fixture/legacy evidence do owner xác nhận. Không suy key từ timestamp, không coi signature options hiện tại là fallback.
- Không xóa DB/volume, thay keypair, chạy DDL/import hoặc restart stack user đang UAT khi chưa có target/window được owner xác nhận. Approval schema exception không phải quyền thực thi trên mọi DB. Nếu thiếu target, tiếp tục read-only/unit/temp-DB work và ghi live gates NOT RUN, rồi yêu cầu owner chỉ định môi trường/window.
- Nếu dùng chung máy đang có stack, không dựng thêm stack trên cùng external network/DB aliases hoặc reuse user volumes rồi gọi đó là isolated. Cần cấu hình tách network/volume/port được kiểm tra hoặc dùng máy riêng.
- Full Newman tạo/activate platform revisions và có thể tác động fixture site; restart/recreate Payment ngắt checkout. Chạy trong môi trường/window test riêng, ghi side effects và fixture đã dùng.

## 3. Gate tự động — chạy thật và báo từng kết quả

```powershell
dotnet build SmartParking.slnx --nologo -m:1
dotnet test SmartParking.slnx --no-build --nologo -m:1
npm --prefix FE run build
node --test tests/frontend/*.test.cjs tests/postman/spark340-collection.test.cjs
dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --filter FullyQualifiedName~PlatformPolicyPostgresConcurrencyTests
dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests
```

PG opt-in vars: SMARTPARK_OPERATION_POLICY_TEST_CONNECTION và SMARTPARK_PAYMENT_TEST_CONNECTION, password cung cấp riêng đúng quoting; chỉ fixture-generated temporary DB được CREATE/DROP. Không in connection string. Ghi passed/failed/skipped/total và warning theo lệnh thực tế (build có restore khác --no-restore). Nếu full baseline có lỗi ngoài task, ghi rõ nguồn/ảnh hưởng và chuyển owner, không mở rộng sửa service khác hoặc giấu lỗi.

Các ca Payment quan trọng: callback chỉ tin exact binding; thiếu settings/binding không outbound; DI fake-handler thực sự cô lập; provider code khác order bị chặn trước insert; distinct backend PID tại biên outbound; import dry-run0write, apply/replay/conflict/partial retry, tamper rejection, key-provider/context restart, inventory có unsupported vẫn trả các row hợp lệ. Không dùng current-key/timestamp inference hoặc fake success để vượt gate.

## 4. HTTP/Postman/Newman — full58 trên prepared stack

Import collection và environment example của SPARK-340. Token UserService thật cho Admin/Owner/Driver, resource IDs lấy từ API, fixtures hợp lệ theo đúng ownership. Giữ populated environment/keys ngoài Git. Không chỉnh example trong repo thành file chứa token.

Chứng minh:

1. Catalogue14 keys, type/unit/bounds, null/unset so với0/disabled, missing/invalid policy values.
2. Admin allowed, Owner/Driver forbidden và anonymous401 trên GET lẫn writes; Admin không có quyền site/platform ngoài contract, không được cấp thêm lot/gate/device/payment-operation privilege.
3. Draft create/update, stale If-Match/expectedUpdatedAt409 RESOURCE_VERSION_CONFLICT, by-id/history/filter/pagination, invalid ranges và bounds. Overlap409 là ca riêng.
4. Immediate/scheduled activation. Với U06 chụp actual timestamp/version/value **trước/tại/sau effectiveFrom**, dùng supported as-of query để chứng minh đúng boundary và phân biệt với việc chỉ list SCHEDULED.
5. Owner publish trong/ngoài Admin bounds, inheritance, SITE_OVERRIDE thật từ Owner flow; GET effective PLATFORM và SITE có provenance đúng. U11 platform minimum/site inherited maximum khiến activation không hợp lệ phải bị chặn, không làm đổi active policy.
6. Bốn F8 HTTP negatives: create/update × true/false đều400 VALIDATION_FAILED, đúng ArrivalLateToleranceEnabled field. Null/omitted vẫn hợp lệ; so draft/updatedAt trước/sau để bảo đảm invalid request không mutate. Owner enablement vẫn hoạt động.
7. Payment GET/PUT masking và mode isolation, URL allowlist/blank credentials/error contract, fixture zero outbound. Notification registered fixture channels không được diễn giải thành provider live đã gửi thành công.
8. Restart/force-recreate Payment trên test stack được phép: giữ DB/keys volume, cấu hình giải mã được. Nếu chứng minh credential durability, dùng marker synthetic trên fixture settings và chỉ GET/PUT/restart; **không gọi provider**. FIXTURE không lưu credential theo thiết kế nên không dùng FIXTURE để khẳng định raw credential decryption.
9. F6/F7 DDL/inventory/import trên **DB tester được phép**: exact old protected rows migrate, missing historical mapping giữ blocked/reconciliation, không đổi money/status, inventory unsupported là action item. Giữ key ring và dữ liệu tài chính, không prune bằng chứng.

Ghi request-defined/executed/skipped/NOT RUN riêng và assertions passed/failed/skipped, script/prerequest/request failures. Không tính pm.execution.skipRequest() thành PASS. Không giảm expected status để nhận503/400 ngoài contract. Nếu terms vẫn503 OWNER_UNAVAILABLE, ghi đúng failure và các request downstream NOT RUN; không suy booking snapshot từ revision policy.

## 5. Browser UAT và nghiệm thu nhóm

Tester chạy checklist U01–U20 trên FE+BE từ đúng commit. Cần per-case evidence, đặc biệt U06 temporal values, U11 composed invariant và U20 desktop/tablet/mobile (ví dụ360/768/1440px). Kiểm tra loading/error/409 reload, form validation, tab/modal/history, secret clearing/masking, units/zero/disabled và Owner UI. Backend tests không chứng minh responsive UI.

User đã báo phần lớn localhost ổn nhưng chưa có sign-off từng case; không tự điền PASS từ nhận xét đó. Nếu agent không thao tác browser/không có fixture, ghi NOT RUN để tester nhóm tiếp tục, không thay bằng source scan.

U19 hiện BLOCKED bởi SPARK-273. Không yêu cầu task này triển khai commercial producer/event-driven reservation workflow. Snapshot immutability vẫn là AC của SPARK-340/370: giữ dependency và disposition của owner trong report/PR notes, không đánh dấu PASS hoặc tự xóa tiêu chí. Nếu producer chưa có, report rõ phần còn chưa chứng minh end-to-end.

## 6. Deliverables / gate trước PR

- Report mới ngày chạy, actual commit/configuration, fixtures, commands và số thật; UAT matrix PASS/FAIL/SKIP/NOT RUN/BLOCKED có lý do.
- Evidence JSON hợp lệ, response/code/assertions đã sanitize. Không Authorization/Bearer/token/password/connection string/key XML/private manifest, không raw request chứa credential trong repo. Scan output trước khi lưu; chỉ mask prose không phải secret value.
- Findings có severity, file/line, expected/actual và bước tái hiện. P1/P2 mới trong phạm vi thì đề nghị reopen P5/finding; low observations ghi disposition riêng.
- Verdict phân biệt implementation, test-environment validation, owner live rollout và parent acceptance. P5 đã đóng source review không có nghĩa tất cả UAT/live gates PASS.
- Chỉ đề nghị chuẩn bị PR sau khi các gate trong phạm vi có bằng chứng, testcase còn hoãn có owner/disposition rõ và tester nhóm sign-off. Không tự mở PR hoặc đổi Jira. Gắn report vào session, giữ lịch sử và collection SPARK-238.
