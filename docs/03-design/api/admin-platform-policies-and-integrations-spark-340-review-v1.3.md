# SPARK-340 · Review độc lập vòng 4

**Phán quyết: REJECTED — chưa khóa SSOT cho cả ba nhóm.** Review ngày 12/11/2026 trên [API Design v1.3](admin-platform-policies-and-integrations-spark-340-api-design.md), SHA-256 `5754F48CC8F9282C789BBE9BB8267E4BA706EE78EA57814C55DB165CB275DE6B`. Số dòng bên dưới thuộc bản này. [Kết quả vòng 3](admin-platform-policies-and-integrations-spark-340-review-v1.2.md)

## 1. Executive summary

**C1, C2, M1 và lỗi hai lệnh activate cùng draft trong M3 đã đóng ở mức thiết kế.** Predicate sửa draft có cả tenant/site null; giao dịch activate khóa và kiểm tra trạng thái của target trước khi thay lịch. Hold đã commit được giữ snapshot cũ khi Platform đổi revision. Điểm chặn là §6.1 mô tả sai trạng thái Booking sau thu tiền và cho hai kết quả khác nhau với cùng một yêu cầu đã được tiếp nhận. Ngoài ra, hợp đồng bounds đưa thêm trần nghiệp vụ và luồng PayOS chưa bảo toàn mọi checkout đang chờ qua nhiều lần rotate.

| Chiều | Kết luận |
| --- | --- |
| Template | Có metadata, traceability, 9 endpoint Proposed, error catalogue, invariants, 3 sequence, open questions và review checks. Endpoint thứ chín phục vụ sửa draft. Cả 16 khối JSON parse được; catalogue/effective đều có 14 key. |
| Kiến trúc | Đúng service owners, repository port, ADMIN/TOKEN. Phần mô tả dependency Booking lệch quyết định của chủ miền tại §6.1. |
| Bảo mật | Site write bị chặn trước và trong UPDATE. Đã tách credential theo mode, mask ClientId, cấm log raw key, fixture có 0 outbound HTTP. Đây là kết luận về hợp đồng. |
| Nghiệp vụ và concurrency | Ba trạng thái kế thừa/zero/disabled, anti-abuse Platform-only và GiST đã rõ. Còn lỗi bounds, nhánh 0 hàng của optimistic update và lifecycle Booking. |
| Sẵn sàng triển khai | Có thể dùng các ca đã đóng để chuẩn bị code/test. Chưa ký bàn giao toàn bộ v1.3 làm giao diện cố định cho BE, FE, QA. |

## 2. Detailed findings

### Critical · M5 — §6.1 thay đổi sai vòng đời Booking

Dòng 1272–1273 gộp gửi request với commit hold rồi cho sự kiện `PaymentSucceededV1` chuyển reservation sang `COMPLETED`. Quyết định ngày 11/11 nêu rõ `202` chỉ xác nhận admission; hold commit tạo `PENDING_PAYMENT`. Operations xác minh collection cùng binding và các điều kiện xác nhận trước khi commit `CONFIRMED`. `COMPLETED` thuộc cuối phiên đỗ. Quyết định cũng ghi rõ payload `PaymentSucceededV1` hiện tại chưa đủ chứng minh quote/version. Nếu QA dùng §6.1 làm oracle, một booking nhảy thẳng tới trạng thái cuối có thể được đánh dấu đúng. [Quyết định 11/11, D11-A và D11-B](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md) · [Booking API, addendum 11/11](reservation-first-slice-api-design.md)

**Điều kiện đóng:** tham chiếu workflow của Operations, giữ snapshot của hold đã commit và dùng trạng thái `CONFIRMED` sau quyết định hợp lệ. Tách mốc hiệu lực policy khỏi hạn hold và giờ bắt đầu đỗ khi mô tả tiền đến muộn. Dòng 200 và 1504 đã thống nhất rằng policy đổi được phát hiện sau admission dẫn đến receipt `REJECTED / TERMS_CHANGED_OR_EXPIRED`; bỏ lựa chọn HTTP 409 cho chính trường hợp đó ở dòng 1278. QA kiểm riêng response POST, receipt GET và booking GET. [BookingProgressHandler](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/BookingProgressHandler.cs)

### Major

| ID | Bằng chứng và tác động | Điều kiện đóng |
| --- | --- | --- |
| M2 · Bounds chưa được phê duyệt | Dòng 804 yêu cầu range chứa ít nhất một giá trị lưu được. Dòng 806–810 lại đặt trần 1.440 phút, 168 giờ và 30 ngày, loại 0 ngày cho advance booking, đồng thời buộc cả hai đầu range chia hết cho 60/1440. Ví dụ interval 61–121 phút chứa đúng một lựa chọn 2 giờ, nhưng validator mô tả sẽ loại bỏ. Catalogue v1.1 giao Admin quyền định bounds và cấm biến giới hạn biểu diễn thành trần nghiệp vụ; SQL cho phép advance booking ≥ 0. | Kiểm tra có giá trị nguyên trong miền cột, tính dương/không âm theo từng key và min ≤ max. Mọi trần nghiệp vụ bổ sung cần nguồn quyết định. Test range 61–121 phút, 61–119 phút cho cột giờ và 0 ngày cho advance booking. Giữ phần đã đúng về legacy equal-default và late tolerance 15–30. [Catalogue](../../01-requirements/business-rules/policy-catalogue.md) · [Schema](../../../scripts/database/microservices/03b-reservation-operations-service-db.sql) |
| M4 · UPDATE trả 0 hàng sau pre-check | Dòng 851–884 kiểm metadata trước conditional UPDATE; sequence 1363–1369 chỉ xử lý thành công. Hai PUT có thể cùng vượt qua pre-check, sau đó một UPDATE trả 0 hàng. Nếu activate thắng trong khoảng giữa đọc và ghi thì cùng kết quả 0 hàng nhưng client cần một mã lỗi khác. Việc trả thẳng giờ ứng dụng 7 chữ số cũng chưa bảo đảm khớp timestamp đã lưu và token thay đổi ở mỗi lần ghi. | Chỉ trả 200 nếu sửa đúng một hàng; khi 0 hàng phân loại trạng thái hiện hành thành active/stale/not found như bảng lỗi. Trả token thực đã lưu, bảo đảm round-trip và đổi sau mỗi sửa thành công. QA đồng bộ hai request tại điểm sau pre-check, không chỉ gửi lần lượt như ví dụ 888–893. |
| M6 · Rotation chưa bao phủ checkout còn chờ | Dòng 1153 đã pin mode đúng. Dòng 141 và 1152 giữ đúng một previous key trong một giờ. Checkout sinh dưới K0; nếu rotate K0→K1→K2 trước khi callback K0 đến, cả hai key mà thuật toán thử đều không khớp, kể cả sau vài phút. Callback muộn hơn cửa sổ cũng chưa có kết quả đối soát. | Chốt cách bảo vệ order đang mở theo phiên bản signing key hoặc quy tắc ngăn rotation làm mất phiên bản còn cần. Xác định nhánh đối soát an toàn khi đã quá hạn. QA kiểm hai lần rotate trong cửa sổ và callback muộn; chỉ ghi nhận thu tiền sau xác minh có thẩm quyền. [Adapter hiện tại](../../../src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Adapters/Outbound/PayOsAdapter.cs) |
| M6 · PUT và GET payment khác trạng thái | Dòng 1071–1079 quy định FIXTURE luôn HEALTHY, LIVE/SANDBOX thiếu key là MISCONFIGURED. Dòng 1150 lại quy định PUT luôn trả UNTESTED. Cùng lần đổi sang FIXTURE, badge có thể chuyển UNTESTED→HEALTHY chỉ vì refresh trang dù không có probe. | Cho GET và PUT cùng dùng bảng trạng thái của version vừa lưu, hoặc đặc tả rõ ý nghĩa khác nhau của hai response. Kiểm ba ca FIXTURE, SANDBOX thiếu key và SANDBOX đủ key mới rotate. |

### Minor / các finding đã đóng

- **C1 đóng:** dòng 851–882 kiểm scope của target và ràng buộc ngay tại UPDATE. Site draft với timestamp khớp hay stale đều phải trả `403 FORBIDDEN_SCOPE_WRITE`.
- **C2 và M1 đóng:** allowlist, tắt redirect, bộ key riêng, zero outbound ở fixture; chỉ ARRIVAL_LATE_TOLERANCE có bound trong seed v1. [Seed](../../../scripts/database/seeds/03b-reservation-policy-defaults.sql)
- **M3 đóng cho các ca vòng 3:** dòng 966–1007 lấy một serverNow, khóa target, kiểm DRAFT trong transaction, chọn predecessor và successor theo cùng scope, chấp nhận bootstrap không có predecessor. Hai lần activate cùng draft với mốc hợp lệ có một thành công và một `422 CANNOT_ACTIVATE_NON_DRAFT`. Cần xác minh bằng PostgreSQL khi triển khai.
- **M7 đóng phần mapping Operations:** §6.3 phân biệt mapping hiện tại và thay đổi Proposed, chỉ map đúng constraint. Phần Payment vẫn ghi cả INVALID_REQUEST và INVALID_PAYMENT_BASE_URL ở dòng 135/1145; thống nhất theo bảng lỗi dòng 186. [Exception handler](../../../src/Services/OperationService/SmartParking.OperationService.API/Middleware/ReservationExceptionHandler.cs)
- GET notifications đã có bảng trường và mẫu suy giảm. Dòng 455 còn dẫn `ReservationPolicyResolver.cs`; lớp thực nằm trong [ReservationPolicies.cs](../../../src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicies.cs).

## 3. Traceability & AC verification matrix

| AC | Kết luận thiết kế | Căn cứ hoặc ca kiểm quyết định |
| --- | --- | --- |
| AC-1 · Catalogue/effective | Đạt | 14 key, seed v1, nguồn explicit bằng default, null kế thừa, zero và disabled. |
| AC-2 · Bounds | Chưa đạt | Khôi phục đúng miền được Admin cấu hình, kiểm giá trị thực sự biểu diễn được (M2). |
| AC-3 · Draft/edit/schedule/history | Một phần | Race activate cùng draft đã rõ; PUT cần outcome khi mất race sau pre-check. |
| AC-4 · Snapshot | Một phần | Snapshot v1 của hold trước mốc đổi policy được bảo vệ. Cần sửa vòng đời và cách quan sát kết quả xác nhận (M5). |
| AC-5 · Integration | Một phần | Đúng ownership và phạm vi đọc của bốn delivery fixtures; health sau PUT và vòng đời signing key còn mở. |
| AC-6 · Secret và quyền vận hành | Đạt về thiết kế redaction/scope | Không echo raw key; test logs, HTTP outbound và Site write khi triển khai. |
| AC-7 · Freshness | Đạt về thiết kế | 9 route ADMIN/TOKEN, không thêm Saga cho cấu hình. |
| AC-8 · Automation/UAT | Chưa xác minh runtime | Review tĩnh; chưa chạy xUnit, Newman hoặc UAT. |
| AC-9 · Tái sử dụng | Một phần | Dùng lại resolver/schema/adapter; dẫn đúng dependency Booking thay vì định nghĩa thêm chuyển trạng thái. |

## 4. Sign-off & next steps

| Bàn giao | Quyết định | Điều kiện ký |
| --- | --- | --- |
| SPARK-368 · Backend | Chưa ký toàn bộ | Sửa M5 với chủ Booking, M2 theo catalogue, M4 ở nhánh 0 hàng và M6 với chủ Payment. |
| SPARK-369 · Frontend | Chưa khóa contract | Có thể dùng catalogue, effective và notification schemas; khóa badge sau PUT và mã conflict của draft trước khi hoàn tất client. |
| SPARK-370 · QA | Có thể chuẩn bị fixture; chưa ký nghiệm thu | Dùng các kết quả quan sát được tại M2/M4/M5/M6 để khóa assertion. Lưu bằng chứng chạy ở phase triển khai. |

Vòng tiếp theo chỉ cần đối chiếu các mục còn mở và kiểm tra các thay đổi liên quan có giữ được C1/C2/M1/M3 hay không.
