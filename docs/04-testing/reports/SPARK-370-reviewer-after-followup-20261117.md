# Prompt — Independent reviewer SPARK-340/370 after 17 November follow-up

Bạn là reviewer độc lập. Repo `D:\My file\FA26 - OJT FSoft\Mock Project OJT\Mock-Project-Smart-Parking`. Review toàn bộ working tree tại HEAD thực tế, không chỉ diff đã tracked. Không commit/push, reset/clean, xóa DB/volume, đổi keypair hay dùng credential thật với provider. Người dùng đang UAT localhost, tránh sửa policy/restart service trong khi họ test. Nếu cần chạy luồng mutation hoặc recreate service, phối hợp lịch trước, giữ dữ liệu.

Đọc theo thứ tự AGENTS.md → ai/AGENTS.md → coding skill và contract cần thiết → session SPARK-340 mới nhất → `docs/04-testing/reports/SPARK-340-review-followup-20261117.md` → independent report/evidence ngày 17/11 → localhost UAT checklist. Report là đầu mối kiểm tra, không phải nguồn thay thế requirements/code.

## Nhiệm vụ

1. Review BE P1–P4 theo ticket SPARK-340/368/370, API design Admin, coding guide và patterns từng service. Ghi findings có severity, file/line, trigger, expected/actual, ảnh hưởng và cách tái hiện. Phân biệt bug với đề xuất style.
2. Xác minh các ca mới trong `CheckoutBindingOutboundOrderingTests` thực sự bắt lỗi nếu binding bị chuyển xuống sau SendAsync, nếu không await store, hoặc nếu lỗi storage vẫn gửi HTTP. Kiểm tra flag xác nhận ngoài callback để assertion bị adapter catch không thành false PASS.
3. Chạy PostgreSQL suite Payment thật. Ca `CheckoutBinding_IsCommittedAndReadableByIndependentConnectionBeforeOutbound` phải đọc/decrypt được row bằng connection khác ngay tại outbound boundary. Chạy lại fresh-context/fresh-key-provider, conflict/no-overwrite và atomic history allocation. Ghi riêng database tests SKIP nếu không có môi trường. Chỉ dùng database tạm do fixture sinh, không dùng DB ứng dụng làm scratch.
4. Chạy `node --test tests/postman/spark340-collection.test.cjs`. Xác minh terms503 vẫn FAIL một assertion, không dereference termsId sau failure, không lưu accepted terms từ response lỗi. Khi chuẩn bị được fixture và service chạy, recheck request đó qua Newman thật. Booking snapshot vẫn BLOCKED nếu terms không200.
5. Reconcile U04 với đúng stale If-Match/expectedUpdatedAt `RESOURCE_VERSION_CONFLICT`, không dùng overlap409 thay thế. Với U06 phải trích đúng test before/exact/after effectiveFrom và version/value ở mỗi mốc. SCHEDULED listing không đủ chứng minh boundary. Không gán U11/U20 PASS khi chưa chạy.
6. Review blocker U19 theo decision ngày11/11 và implementation alignment, thay vì chỉ đề nghị implement adapter cũ. Kiểm tra combined acceptance+EnsureOrder sau hold commit, external call ngoài transaction, immutable accepted price/coverage, machine authentication, event confirmation, recovery và receipt compatibility. Contract route/auth/coverage/persistence còn OPEN phải ghi rõ. Không thêm fake quote/accept, không thêm accept endpoint riêng, không bỏ acceptance mà chưa hoàn tất protocol thay thế.
7. Run baseline phù hợp và full Newman khi đã có cửa sổ test riêng. Token thật UserService cho Admin/Owner/Driver, fixtures từ API, ghi PASS/FAIL/SKIP/NOT RUN riêng. Full collection có mutation platform/site, phải phối hợp UAT. Không hạ assertion để lấy xanh. Optional SANDBOX credential thiếu vẫn SKIP.

```powershell
dotnet build SmartParking.slnx --nologo -m:1
dotnet test SmartParking.slnx --no-build --nologo -m:1
dotnet test tests/Services/SmartParking.PaymentService.Tests --no-build --filter FullyQualifiedName~PaymentIntegrationPostgresConcurrencyTests
dotnet test tests/Services/SmartParking.OperationService.Tests --no-build --filter FullyQualifiedName~PlatformPolicyPostgresConcurrencyTests
node --test tests/postman/spark340-collection.test.cjs
npm --prefix FE run build
node --test tests/frontend/*.test.cjs
```

Database env là `SMARTPARK_PAYMENT_TEST_CONNECTION` và `SMARTPARK_OPERATION_POLICY_TEST_CONNECTION`. Lấy cấu hình local an toàn, parse `.env` đúng quy tắc, không in secret/connection string. Không dùng tổng test cũ làm kết quả run mới.

## Deliverables

- Report mới và evidence sanitized có timestamp, HEAD, working-tree context, commands, số run/pass/fail/skip, từng request/assertion và lý do NOT RUN. Không raw export có Authorization, token, credential hay key XML trong repo.
- Phân biệt regression tests, HTTP verification và tester-group UAT. Người dùng mới báo hầu hết FE+BE ổn, tester nhóm chưa sign-off. Không tự điền từng UAT PASS.
- Kết luận independent fixes đạt/chưa đạt, blocker nào thuộc SPARK-273, P4 có đủ điều kiện đóng hay không. Khi U19 còn BLOCKED, không kết luận P4 complete.
- Cập nhật session. Giữ report/evidence lịch sử, thêm correction/addendum nếu kết luận cũ quá rộng. Ghi prompt/contract proposal là PROPOSED cho đến khi chủ dự án chấp thuận.
