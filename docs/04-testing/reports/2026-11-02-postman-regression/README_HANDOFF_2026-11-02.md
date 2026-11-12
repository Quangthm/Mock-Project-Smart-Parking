# SmartPark QA Postman 02/11/2026 — HỒ SƠ ĐỐI CHIẾU TRƯỚC KHI NỘP

## Nội dung và trạng thái

- **91 request** của 2 collection theo log trao đổi ngày 02/11: **65 User, 16 Facility, 10 Payment**, được phân loại theo endpoint URL (`userUrl` / `facilityUrl` / `paymentUrl`).
- **Đã thực hiện ít nhất một lần** theo nhật ký QA, **KHÔNG khẳng định 91/91 PASS**; thiếu Runner export được đối chiếu từng request.
- **Master cập nhật, bảo toàn dữ liệu cũ:** User, Facility, Payment; mỗi file thêm đúng 1 sheet `POSTMAN_20261102`. Đã kiểm tra nội dung công thức và các ô có giá trị của sheet cũ không đổi.
- **Master giữ nguyên bản gốc:** Operation, Notification, IoT (0 endpoint tương ứng theo URL trong collection). Việc API của Facility liên quan Operator/Operation không đủ cơ sở để ghi duplicate vào Operation master.
- File Word trong package: báo cáo tổng hợp 11 trang, **2 ảnh minh họa đã có từ bản báo cáo cũ**, kèm phụ lục cập nhật các master. **Không phải bản có 110 ảnh**.
- Toàn bộ 110 ảnh gốc và Word nhúng 110 ảnh **không được đưa vào package GitHub**, vì ảnh có thể chứa token/secret và chưa được duyệt đủ để công khai. Giữ ở máy QA để rà soát và redaction trước khi cung cấp bằng chứng trên Jira/GitHub.

## Jira đã cập nhật trong phiên QA trước

- SPARK-181 — comment `10082`: 6 request Driver/Owner recovery/resend có kết quả báo cáo 200, 3/3.
- SPARK-193 — comment `10081`: Owner/Operator, delivery, cooldown, reissue, retry; retry trên SENT chưa đủ xác nhận FAILED recovery.
- SPARK-314 — comment `10083`: phát hiện Pricing expected 422, actual 503. Cần loại trừ cấu hình auth/noauth và dependency trước khi mở bug backend.
- Facility `Configure category backup` trả HTTP 500 PG `23502 tenant_id null`: phát hiện cần triage, chưa xác định bug Jira trùng/đúng owner; **chưa tạo issue mới**.
- Không tự chuyển Jira status, không merge, không tạo PR bằng tên task implementation.

## Cách hiểu cột Result trong sheet mới

- `Pass (verified)` = có HTTP & Test Results cụ thể do người kiểm thử cung cấp.
- `Pass (reported)` = có số assertion PASS được báo lại; cần đồng bộ evidence/Runner trước nghiệm thu.
- `Group PASS reported` = tổng kết kết quả nhóm, KHÔNG tính PASS cụ thể từng request.
- `Response observed` = có JSON thành công nhưng chưa có HTTP + assertions cuối cùng.
- `Fail (reported)` = expected/actual chênh lệch theo log QA; chưa xác nhận nguyên nhân root cause.
- `Partial` = response thành công với delivery SENT; nhánh retry FAILED chưa được test.
- `Executed, evidence pending` = theo tổng kết đã chạy, thiếu evidence request-level.

## Cấu trúc thư mục dùng trong repo

- `docs/04-testing/test-cases/SmartParking_<Service>_Master_TestCases.xlsx` (6 tên file cố định, đúng một master/service).
- `docs/04-testing/reports/2026-11-02-postman-regression/` chứa Word, 91-request code catalog và manifest ảnh.

## Kiểm tra trước khi commit (bắt buộc)

1. **Không giải nén ghi đè trực tiếp khi chưa có backup và diff**; giữ working tree sạch.
2. Xác nhận source branch/commit SHA đang test, và xác nhận **Jira testing key** cho đợt regression chung hoặc quyết định tách task. Không dùng SPARK-197 hay một task BE làm key cho toàn bộ 91 request.
3. Rà lại từng sheet `POSTMAN_20261102`, đối chiếu evidence cho hai lỗi và các trường hợp thiếu HTTP/Test Results.
4. Không commit secrets, owner/driver credentials, tokens, file Environment thật, 110 ảnh chưa redacted.
5. Chỉ sau khi leader xác nhận scope/key: tạo nhánh QA riêng từ base đúng, commit với key testing, push và tạo **Draft PR** vào base thống nhất; không tự merge.
6. Link Draft PR/commit SHA vào Jira; các comment đã tạo **không phải attachment**, vì upload file lên Jira chưa thực hiện.

## Bảo toàn kiểm tra

- Excel service thay đổi: SmartParking_Facility_Master_TestCases.xlsx, SmartParking_Payment_Master_TestCases.xlsx, SmartParking_User_Master_TestCases.xlsx.
- Excel service không đổi SHA-256: SmartParking_IoT_Master_TestCases.xlsx, SmartParking_Notification_Master_TestCases.xlsx, SmartParking_Operation_Master_TestCases.xlsx.
- Tổng request theo endpoint: {'User': 65, 'Facility': 16, 'Payment': 10}.
- Các sheet lịch sử khác trong master giữ nguyên nội dung ô và công thức. Một số metadata/format nội bộ có thể thay đổi do export XLSX; kiểm tra file thực tế trong Excel trước merge.
