# Prompt giao agent kiểm thử độc lập SPARK-370

> Prompt mới cho follow-up ngày17/11: [SPARK-370-reviewer-after-followup-20261117.md](SPARK-370-reviewer-after-followup-20261117.md). Đọc cùng [báo cáo follow-up](SPARK-340-review-followup-20261117.md), đặc biệt quyết định11/11 gộp acceptance với EnsureOrder sau hold commit. Không dùng yêu cầu adapter trong prompt cũ để tự thêm acceptance endpoint hoặc giữ remote call trong transaction.

## Cập nhật sau sửa môi trường ngày 16/11

Đọc thêm `SPARK-340-UAT-environment-repair-20261116.md` trước khi chạy. Bảng draft đã được đồng bộ additive, local .env có keypair integration hợp lệ, Owner publish và SITE_OVERRIDE đã chạy thật. Không ghi đè keypair hoặc xóa volume. Signer Operation đã sửa lỗi disposed RSA; ba service dùng clock skew30s. URI broker đã dùng password percent-encoded.

Hiện folder04/U19 còn BLOCKED bởi `UnavailableReservationCommercialOwner`, không phải thiếu cờ Facility nữa. Terms thực tế trả503 với thông báo commercial producer chưa tích hợp. Xác nhận dependency này trong report và chuyển cho agent implementation của tích hợp Operation–Payment. Không tính booking snapshot PASS từ các folder khác và không thêm fake quote/accept để làm test xanh.

Bạn phụ trách verification độc lập cho SPARK-340/SPARK-370 sau bản sửa ngày 16/11. Repo `D:\My file\FA26 - OJT FSoft\Mock Project OJT\Mock-Project-Smart-Parking`.

Đọc AGENTS.md → ai/AGENTS.md, coding skill, session SPARK-340 mới nhất, ticket/contract, checklist `docs/04-testing/reports/SPARK-340-localhost-UAT-checklist.md`. Inspect HEAD và toàn bộ working tree chưa commit. Dùng requirements làm tiêu chí, kiểm tra bằng chứng thực tế của từng test.

## Phạm vi

1. Build/test BE toàn solution; build FE và `node --test tests/frontend/*.test.cjs`.
2. Run collection `docs/04-testing/postman/SPARK-340-Admin-Platform-Policies.postman_collection.json` bằng Newman trên Docker với token UserService thật. Dùng environment example làm cấu trúc; giữ token/credential trong bộ nhớ hoặc file tạm ngoài repo.
3. Báo chính xác assertion passed/failed/skipped và request/test-script failures. Collection hiện có 54 request; không hardcode số PASS. Không tính skipped thành passed.
4. Chuẩn bị fixture Owner/site/vehicle/slot hợp lệ qua API và luồng được hỗ trợ; lấy ID từ response. Đọc `snapshot_fixture_ready` và các biến trong collection trước khi cấp fixture. Chứng minh SITE_OVERRIDE thật, reviewed terms → request receipt → booking cùng chuỗi, rồi immutable accepted snapshot sau platform activation. Thiếu fixture phải ghi NOT RUN với lý do cụ thể.
5. Xác minh scheduling trước/tại/sau effectiveFrom, overlap409, If-Match409, Admin200, Owner/Driver403, anonymous401 cả GET và writes, units/null/zero/disabled và bounds thực tế.
6. Run hai suite PostgreSQL thực với env `SMARTPARK_OPERATION_POLICY_TEST_CONNECTION`, `SMARTPARK_PAYMENT_TEST_CONNECTION` trỏ server local dùng CREATE DATABASE. Các fixture chỉ tạo/drop database tạm. Operation yêu cầu password ngay trong connection string; Payment có thể đọc SMARTPARK_DEV_PASSWORD. Không in connection string.
7. Regression Payment bắt buộc: hai Save đồng thời với một append thất bại; reader không thấy candidate; runtime/latest/restart nhất quán. Hydration lỗi không được append default state hoặc vận hành bằng cấu hình chưa biết; retry khôi phục credential cũ.
8. Regression checkout: binding được persist trước outbound; dùng repository/context và DataProtection provider mới để xác minh callback sau rotation/mode switch; cả LIVE/SANDBOX đã có khóa. Binding thiếu fail closed; không thể ghi đè binding cùng order bằng mode/key khác; lỗi storage không được tạo checkout phía provider. Không gửi tiền hoặc credential giả tới provider thật.
9. Kiểm tra restart và `docker compose -f compose.app.yml up -d --no-deps --force-recreate payment-service` giữ config và khóa giải mã. Thông báo người dùng trước khi ngắt service đang UAT. Giữ database/volume. Kiểm tra khóa volume có mount đúng; tuyệt đối không in key XML.
10. Kiểm tra log/response không chứa marker secret của fixture trên initiate/cancel, kể cả rotate trong lúc HTTP đang chạy. Test lỗi storage trả503 có canonical code mà không lộ chi tiết provider.
11. Kiểm tra legacy reallocation chỉ giữ giá trị đã được chấp nhận, không cấp mới 0..5 khi chưa có Admin bounds; kiểm tra ceil division với int.MaxValue không overflow.

## Cách chạy

```powershell
dotnet build SmartParking.slnx --nologo -m:1
dotnet test SmartParking.slnx --no-build --nologo -m:1
npm --prefix FE run build
node --test tests/frontend/*.test.cjs
dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --filter FullyQualifiedName~PlatformPolicyPostgresConcurrencyTests
dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests
```

Dùng Newman programmatic API hoặc sanitize report trước khi ghi evidence. Không lưu raw populated environment, token, Authorization header, request body chứa credential vào repo. Không sửa assertion để nhận lỗi như thành công.

## Bàn giao

- Lệnh và số test thực chạy; tách PASS/FAIL/SKIP/NOT RUN.
- Evidence có timestamp, commit/working-tree context và status từng request/assertion đã sanitize.
- Findings có file/line, trigger, expected/actual, mức độ và acceptance criterion.
- Gắn kết quả UAT của user; kết luận P4 đủ điều kiện đóng hay còn bị chặn.
- Cập nhật session và report mới, giữ báo cáo cũ làm lịch sử.

Không commit/push. Không xóa database/volume ứng dụng. Tự xử lý fixture/test-runner lỗi trong phạm vi kiểm thử; nếu cần sửa implementation hãy ghi rõ nguyên nhân, phạm vi và regression evidence. Không tuyên bố đã chứng minh booking snapshot khi chỉ đọc revision policy.
