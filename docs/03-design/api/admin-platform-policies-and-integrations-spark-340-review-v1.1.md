# SPARK-340 · Review độc lập vòng 2

**REJECTED — chưa khóa SSOT cho Phase 2–4.** Ngày 12/11/2026. Đối tượng là [API Design v1.1](admin-platform-policies-and-integrations-spark-340-api-design.md), SHA-256 `4D6D7B257E2F2AD2F9056B3E659BF575F2037604379244DF4DC047D0E2692EDD`. Số dòng dưới đây tham chiếu bản này. [Báo cáo vòng 1](admin-platform-policies-and-integrations-spark-340-review.md)

## 1. Executive summary

V1.1 đã sửa quyền ghi của Admin cho cả ba thao tác tạo, sửa và kích hoạt. Catalogue v1 khớp seed, quyền sở hữu ba service rõ, repository port đã xuất hiện trong luồng Operations. Endpoint thứ chín, `PUT /draft/{id}`, đáp ứng phần sửa draft của AC-3. Tất cả 15 khối JSON parse được; catalogue và effective sample đều có 14 key. Đây là kiểm tra tĩnh, chưa chạy API, xUnit, Newman hoặc UAT.

| Chiều đánh giá | Kết quả |
| --- | --- |
| Template | Đủ các phần chính và metadata của 9 route. Còn thiếu schema cho một số response, request mẫu và các mã lỗi nhất quán. |
| Kiến trúc | Đạt về ownership, phân tầng và TOKEN + ADMIN. Quyền Site chỉ đọc đã áp dụng cả khi Admin biết ID của Site draft. |
| Bảo mật | Mask/flags đúng, cấm redirect và đã giới hạn đích. Việc dùng credential khi đổi mode vẫn chặn ký duyệt. |
| Nghiệp vụ, concurrency | Đúng mapping snapshot, bảo vệ key chống lạm dụng và khoảng nửa mở. Thời điểm chọn revision, khóa draft và chọn predecessor chưa nhất quán. |
| Sẵn sàng triển khai | BE chưa có kết quả ghi xác định cho các race. FE chưa thể diễn giải hết health và lỗi. QA còn các tình huống mà cùng một đầu vào có thể dẫn đến hai assertion khác nhau. |

## 2. Detailed findings và trạng thái vòng trước

### Critical

**C1 — Đóng ở mức thiết kế.** Dòng 690–692, 775–778 và 826–829 giới hạn Platform qua cả payload và scope của bản ghi đích. Site draft có ID hợp lệ vẫn phải trả `403 FORBIDDEN_SCOPE_WRITE`. Giữ ca kiểm thử “PUT bằng payload Platform nhưng ID thuộc Site”, kiểm tra dữ liệu Site còn nguyên. [Ma trận quyền](../../01-requirements/srs/permissions.md)

**C2 — Đóng phần URL tùy ý và redirect; còn mở cách ly credential theo mode.** Dòng 113–122 đưa cả HTTP localhost vào allowlist FIXTURE. Dòng 965–969 giữ key đang lưu khi bỏ qua/null và áp dụng cấu hình mới cho lần gọi tiếp theo. Tài liệu chưa quy định FIXTURE phải dừng trước khi gửi credential ra mạng; LIVE và SANDBOX cùng origin, chưa xác định điều gì làm một giao dịch thuộc sandbox. Adapter hiện chọn nhánh qua credential và gắn API key lên HTTP request. Nếu triển khai chỉ bước đổi options như mô tả, chuyển từ LIVE sang FIXTURE khi đang có key có thể gửi key đến đích fixture; chuyển sang SANDBOX có thể tiếp tục đường gọi live. **Điều kiện đóng:** đặc tả hành vi outbound và nguồn credential cho từng mode. Với key live đã lưu, chuyển FIXTURE phải tạo ra 0 request mang key live; SANDBOX phải có bằng chứng không sử dụng merchant live cho giao dịch thử. Đây là rủi ro thiết kế, chưa ghi nhận sự cố thực tế. [PayOsAdapter](../../../src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Adapters/Outbound/PayOsAdapter.cs) · [HTTP client](../../../src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/InfrastructureDependencyInjection.cs)

### Major

| Finding vòng 1 | Kết quả đối soát v1.1 | Điều kiện đóng còn lại |
| --- | --- | --- |
| M1 · Catalogue | **Đóng phần bounds v1.** Dòng 202–209 và JSON phản ánh đúng một bound late tolerance. Tiêu đề dòng 432 ghi effective v2 nhưng payload 442–448 là v1. | Sửa nhãn ví dụ hoặc cung cấp v2 đã active đúng thời điểm đánh giá. [Seed](../../../scripts/database/seeds/03b-reservation-policy-defaults.sql) |
| M2 · DTO và bounds | **Một phần.** Dòng 725–740 bổ sung đủ trường draft và dòng 416 chốt flag Platform là null. Tuy nhiên validator cho bounds chỉ kiểm min ≤ max và cấm key chống lạm dụng. Chưa có kết quả cho key lạ, range late tolerance vượt 15–30 hoặc đơn vị bounds của key lưu bằng giờ/ngày. Dòng 422–423 cũng bỏ trống Site lưu giá trị bằng default khi chưa có bound; resolver hiện chỉ kiểm bounds nếu giá trị thay đổi. | Chốt valid keys, kiểu/đơn vị và giới hạn bắt buộc của từng bound; kiểm tra POST/PUT cùng kết quả. Định nghĩa source/inherited hoặc lỗi cho dữ liệu cũ bằng default. [Catalogue](../../01-requirements/business-rules/policy-catalogue.md) · [Resolver](../../../src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicies.cs) |
| M3 · Kích hoạt | **Mở.** Dòng 833–839 chọn predecessor chỉ bằng thời điểm kết thúc. Với A=[01/11,01/12), B=[01/12,∞), cả A và B thỏa predicate khi chèn C tại 20/11. Chọn B sẽ từ chối một lần chèn hợp lệ. Khi T bằng đầu của B, phép tìm successor ≥ T còn có thể sinh [T,T), vi phạm CHECK của DB. Ví dụ lỗi 873–879 lại nêu predecessor mở vốn có thể đóng thành công. | Chọn duy nhất hàng đang phủ T, xử lý riêng trường hợp trùng đầu interval và gap/không có predecessor. Sau thành công phải thu được A,C,B liền kề; khi trùng đầu trả 409 và lịch còn nguyên. Dùng cùng một giờ server cho default/kiểm tra backdate. DDL dòng 1112–1113 phải khớp sentinel UUID toàn số 0 của schema. [Schema Operations](../../../scripts/database/microservices/03b-reservation-operations-service-db.sql) |
| M4 · Edit draft | **Một phần.** Đã có PUT, đủ payload trong history và 4 trạng thái. Dòng 771–782 không có precondition từ client; version giữ nguyên sau sửa. Sequence 1161–1166 tách kiểm tra DRAFT khỏi UPDATE. Nếu hai tab sửa cùng draft, bản lưu cũ có thể đè bản mới. Nếu activate xen giữa kiểm tra và ghi, nội dung một revision đã publish có thể bị đổi mà GiST không phát hiện. | Chốt precondition chống mất cập nhật, kiểm tra lại trạng thái của target nguyên tử khi sửa/kích hoạt và xác định mã lỗi cho thao tác thua. QA cần hai ca PUT/PUT và PUT/activate. Audit xác định actor/scope của lần ghi. |
| M5 · Snapshot | **Đóng mapping, còn mở mốc chọn revision.** Dòng 1089 dùng thời điểm review, 1104 dùng `reservation_start >= effective_from`, 1105 buộc giữ snapshot của booking đã xác nhận. Một booking đã nhận trước T nhưng đỗ sau T thỏa cả hai điều kiện. Handler hiện resolve với giờ quyết định của backend. | Chốt theo thời điểm xét/nhận terms và tái xác nhận khi terms đổi. Ghi kỳ vọng cho booking xác nhận trước T, bắt đầu sau T (giữ v1); booking nhận terms mới sau T (v2); review trước T nhưng xác nhận sau T (kiểm tra lại terms). [Booking review](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/BookingReviewHandlers.cs) · [FR-POL-05/06](../../01-requirements/srs/modules/04-reservations-allocation.md) |
| M6 · PayOS health/audit | **Một phần.** Đã chốt lưu bền, hiệu lực cho lần gọi sau và trạng thái UNTESTED. Dòng 905–909 thiếu kết quả khi đủ key nhưng lần kiểm tra cuối thất bại. Mẫu PUT 984–999 báo HEALTHY và timestamp mới dù sequence chỉ lưu cấu hình. Audit 127–136 thiếu version/scope theo UC-POL-02. | Ràng buộc health với đúng phiên bản cấu hình, nêu trạng thái sau rotate và sau provider failure, chốt webhook status nếu trong phạm vi. Audit phải xác định được cấu hình nào thay đổi; dùng credential giả để kiểm tra response và log trên đường lỗi. [UC-POL-02](../../01-requirements/use-cases/uc-pol-02.md) |
| M7 · Mã lỗi | **Một phần.** Bổ sung mã scope và base URL. Dòng 121/967 mô tả hai tên mã cho URL sai; bảng 165 và sequence 1265 chọn `INVALID_PAYMENT_BASE_URL`. Handler Operations hiện trả `INVALID_REQUEST` cho FluentValidation và `RESOURCE_CONFLICT` cho exclusion. Dòng 844/1126 mô tả mapping đích như hành vi đã có. | Chọn đúng một giá trị wire `code` mỗi lỗi. Ghi rõ thay đổi Proposed theo service; chỉ map đúng constraint policy thành overlap. Chốt kết quả retry sau timeout và hình dạng field errors tùy chọn. [Exception handler](../../../src/Services/OperationService/SmartParking.OperationService.API/Middleware/ReservationExceptionHandler.cs) |

### Minor và hoàn thiện template

- Dòng 148–149 vẫn có hai placeholder dù checklist dòng 1297 ghi đã loại hết. Dòng 427 còn tham chiếu file resolver riêng. Link planning ở dòng 1024 trỏ vào thư mục không tồn tại trong repo.
- GET effective cần nêu kết quả khi chỉ truyền tenantId và khi site có history nhưng không có revision hiệu lực tại `at`. GET revisions với scope ALL cần tie-breaker duy nhất sau version và createdAt để phân trang ổn định giữa các site. Bổ sung schema nullable của response effective/history và request mẫu cho các GET theo template.
- Ghi nhận chín endpoint trong baseline bàn giao: tám endpoint gốc và PUT sửa draft. Notification GET read-only phù hợp WBS; xác nhận cách hiểu này với chủ AC-5 vì câu AC dùng “cấu hình” cho cả delivery adapters. [Template](api-design-template.md)

## 3. Traceability & AC verification matrix

| AC | Kết luận vòng 2 | Bằng chứng cần cho sign-off |
| --- | --- | --- |
| AC-1 · Catalogue và effective view | Đạt phần catalogue; effective còn một ca nguồn giá trị chưa chốt | Fixture v1, Site explicit bằng default, zero buffer và flag disabled. |
| AC-2 · Resolution và bounds | Một phần | Hai biên inclusive, vượt biên, thiếu bound, sai đơn vị và key cấm. Kiểm tra luồng Owner/domain phù hợp quyền. |
| AC-3 · Draft/edit/schedule/history | Một phần | 9 route đã bao gồm sửa draft. Chốt stale edit, PUT/activate, chèn giữa hai lịch, trùng đầu lịch và retry. |
| AC-4 · Snapshot | Một phần | Mapping đúng cột; chốt ba ca cắt qua mốc T ở M5 cho cả policy/price. |
| AC-5 · Integration | Một phần | Chốt hiệu ứng từng mode, health thất bại và xác nhận phạm vi read-only của bốn fixture. |
| AC-6 · Secret và phân quyền | Chưa đạt | Scope Admin và mask đã đúng. C2 cần bằng chứng thiết kế cho chuyển mode có key cũ và test redaction đường lỗi. |
| AC-7 · Freshness | Đạt ở mức thiết kế | Cả 9 route TOKEN/ADMIN; giữ ca 401, 403 role và 403 scope. |
| AC-8 · Test evidence | Chưa xác minh runtime | Chưa chạy xUnit, frontend test, Newman, UAT trong vòng review tài liệu này. |
| AC-9 · Tái sử dụng | Đạt định hướng thiết kế | Tái sử dụng row/resolver, repository port, service-owned adapters. Kiểm tra implementation khi có code. |

## 4. Sign-off và bàn giao

| Nhóm | Quyết định | Điều kiện ký |
| --- | --- | --- |
| Backend · SPARK-368 | Chưa ký | Chốt cách ly mode, thao tác nguyên tử trên draft và interval, thời điểm chọn revision, bounds và error mapping. |
| Frontend · SPARK-369 | Chưa ký | Khóa kết quả stale edit, source/inherited, health sau rotate/test thất bại và schema response. |
| QA · SPARK-370 | Chưa ký | Có thể chuẩn bị fixture ngay. Khóa assertion cho các trường hợp C2, M2–M7 rồi chạy suite khi có implementation. |

Ưu tiên vòng sửa tiếp theo là ca chuyển LIVE → FIXTURE khi còn key, chèn một revision giữa hai lịch đã có và xác nhận booking có giờ bắt đầu nằm sau mốc thay chính sách. Mỗi ca cần một kết quả quan sát được thống nhất giữa đặc tả, sequence và ví dụ.
