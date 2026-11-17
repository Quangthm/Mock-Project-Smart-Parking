# SPARK-340 · Review độc lập vòng 3

**Phán quyết: REJECTED — chưa khóa hợp đồng v1.2.** Ngày 12/11/2026. Đối tượng là [API Design v1.2](admin-platform-policies-and-integrations-spark-340-api-design.md), SHA-256 `CE693D67D0F3CF469B236BB52B32269096861605F6DFFA4E03689E070A11ECFE`. Các số dòng dưới đây thuộc bản này. [Báo cáo vòng 2](admin-platform-policies-and-integrations-spark-340-review-v1.1.md)

## 1. Executive summary

Đã đóng finding Critical về chuyển credential PayOS giữa các mode ở **mức thiết kế**. LIVE và SANDBOX dùng bộ key riêng; FIXTURE quy định 0 cuộc gọi HTTP outbound. Ví dụ catalogue cũng đã khớp seed. Cách chọn predecessor/successor giờ xử lý được trường hợp chèn một revision vào giữa hai lịch liền kề.

V1.2 mở lại vấn đề kiểm tra quyền trên đường ghi khi đưa SQL cụ thể vào hợp đồng. Thuật toán kích hoạt còn một race trên chính draft đích. Phần snapshot bảo vệ đúng booking đã xác nhận nhưng đưa ra kết quả HTTP không thống nhất với workflow Booking đang sở hữu thao tác.

| Chiều | Kết quả |
| --- | --- |
| Template & cấu trúc | Đủ các phần chính, 9 endpoint Proposed, đủ metadata và 3 sequence. 16 khối JSON parse thành công. GET payment và GET notifications hiện chỉ còn mẫu response, thiếu bảng trường và nullability/enum mà FE cần. |
| Ranh giới & kiến trúc | Đúng ba service, repository port, ADMIN/TOKEN và Site read-only ở mức phân quyền. Điều kiện ở câu ghi thực tế chưa bảo đảm toàn bộ cam kết scope. |
| Bảo mật | Đã chốt mask, cấm log raw key, mode isolation và redirect. Cần đóng C1 dưới đây trước khi ký. Chưa có bằng chứng runtime về rò rỉ hay ghi trái quyền. |
| Nghiệp vụ & concurrency | Catalogue đúng 14 key, có phân biệt zero/disabled; DDL GiST khớp sentinel toàn số 0. Chưa khóa đủ outcome cho cùng draft bị activate hai lần và cho stale terms sau admission. |
| Sẵn sàng BE/FE/QA | Đủ căn cứ chuẩn bị fixture và form catalogue. Chưa đủ để ba nhóm triển khai độc lập toàn bộ hợp đồng. |

## 2. Findings và trạng thái so với vòng 2

### Critical · C1 mở lại cho PUT draft

**Quyền Platform được mô tả đúng nhưng predicate thực hiện lệnh ghi bỏ qua scope.** Dòng 839 và sequence 1227–1234 chỉ kiểm ID, draft và timestamp. Dòng 842 kiểm Site sau khi UPDATE trả về 0 hàng. Với Site draft có timestamp khớp, truy vấn chọn đúng hàng đó để cập nhật. Với payload Platform như ví dụ, CHECK cấm các cột anti-abuse/bounds ở Site có thể làm câu lệnh lỗi ngay, trước nhánh trả 403. Đây là sai lệch có thể xác định từ SQL, chưa phải bằng chứng Site đã bị sửa thành công.

**Điều kiện đóng:** đặt cả hai điều kiện scope Platform vào phép ghi nguyên tử, đồng bộ sơ đồ và bảng lỗi. Dùng một Site draft có thật, gửi payload Platform hợp lệ cùng timestamp khớp. Kết quả phải là `403 FORBIDDEN_SCOPE_WRITE`, không thay đổi dữ liệu hay updatedAt. Lặp lại với timestamp cũ để xác nhận ưu tiên lỗi quyền. [Ma trận quyền](../../01-requirements/srs/permissions.md) · [CHECK theo scope](../../../scripts/database/microservices/03b-reservation-operations-service-db.sql)

### Major

| Finding | Đã sửa ở v1.2 | Phần còn mở và điều kiện đóng |
| --- | --- | --- |
| M3 · Kích hoạt | Dòng 923–932 xử lý trùng mốc và chèn C giữa A/B đúng cho một request trên lịch liên tục. | Dòng 917–922 kiểm DRAFT trước transaction, chỉ khóa hàng đã active. Hai lệnh activate cùng draft ở T1, T2 có thể cùng qua precondition. Dòng 933 và sequence 1257 không yêu cầu target vẫn là draft. GiST chỉ xét overlap; một lần đổi lịch của cùng hàng có thể để lại gap. Phải kiểm/khóa target trong transaction hoặc dùng conditional update, rollback toàn bộ nếu thua. Chốt kết quả khi thiếu predecessor (kể cả platform chưa có active row), và dùng một giá trị serverNow cho kích hoạt ngay. QA kiểm một lần chuyển trạng thái, không có thay đổi phụ khi request thứ hai thất bại. |
| M5 · Mốc snapshot và giao diện Booking | Dòng 1170–1171 đúng cho booking đã xác nhận trước T và review mới sau T. Mapping các cột JSONB đúng. | Dòng 1166 đồng nhất giờ review với hold_created_at trong khi review chạy trước hold. Dòng 184/1172 hứa HTTP 409 `STALE_TERMS_CONFIRMATION_REQUIRED` khi xác nhận; hợp đồng Booking phân biệt lỗi trước admission với receipt REJECTED sau 202, handler hiện ghi `TERMS_CHANGED_OR_EXPIRED`. Quyết định 11/11 còn tách commit hold khỏi xác nhận qua sự kiện thu tiền. Tham chiếu hợp đồng sở hữu, chốt outcome riêng cho review cũ chưa có hold và hold v1 hợp lệ đã commit trước T nhưng tiền đến sau T; giữ binding bất biến của hold khi xử lý collection. [Booking API](reservation-first-slice-api-design.md) · [Quyết định 11/11](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md) · [BookingProgressHandler](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/BookingProgressHandler.cs) |
| M2 · Bounds và effective source | Đã chốt allowlist 10 key, đơn vị phút, late tolerance 15–30, cặp tenant/site, site không có revision tại thời điểm đọc. | Dòng 782 cho phép range 0–0 cho hold/min/max duration mặc dù các giá trị đó bắt buộc dương. Max duration lưu số giờ nguyên, advance booking lưu số ngày nguyên; một range phút có thể không chứa giá trị nào lưu được. Chọn kết quả validation cho range không có giá trị khả dụng. Dòng 440–442 còn thiếu nhánh explicit bằng default khi bound null; checklist 1362 lại gắn SITE_OVERRIDE theo sự hiện diện. Resolver bỏ qua so sánh bound khi hai giá trị bằng nhau. Chốt một kết quả cho dữ liệu cũ và bổ sung fixture. [Resolver](../../../src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationPolicies.cs) · [Schema](../../../scripts/database/microservices/03b-reservation-operations-service-db.sql) |
| M4 · Sửa draft | expectedUpdatedAt và điều kiện is_active trong UPDATE giải quyết stale edit/activate-trước-edit cho Platform. GET history có tie-breaker id. | Ngoài C1, dòng 841–845 phân biệt active với stale nhưng sequence 1229–1233 cùng trả RESOURCE_VERSION_CONFLICT. Chốt outcome khi thiếu cả hai token, header/body khác nhau, độ chính xác timestamp khi round-trip, và bảo đảm token thay đổi sau mỗi lần ghi. QA cần hai PUT xuất phát từ cùng phiên bản và lỗi riêng khi draft đã active. |
| M6 · Payment health và giao dịch đang xử lý | Có DEGRADED, audit scope/version và UNTESTED sau rotate. C2 về key live ở fixture đã đóng trong hợp đồng (dòng 35–46, 109–114). | Dòng 112 quy định SANDBOX thiếu key là UNTESTED, dòng 998 quy định MISCONFIGURED; chưa rõ flags/health cho FIXTURE không cần key. Chốt một bảng kết quả GET sau PUT theo mode, đủ/thiếu key và lần probe theo configVersion. Nêu cách xử lý checkout LIVE đang chờ callback khi đổi mode/rotate key: adapter hiện verify bằng checksum của options. Phải xác định binding cấu hình của giao dịch đã phát hành hoặc điều kiện ngăn thay đổi gây gián đoạn, và kiểm tra callback hợp lệ không bị mất. [PayOsAdapter](../../../src/Services/PaymentService/SmartParking.PaymentService.Infrastructure/Adapters/Outbound/PayOsAdapter.cs) |
| M7 · Mã lỗi | Mẫu lỗi đã dùng dữ liệu cụ thể; bảng có mã riêng cho scope, URL và stale edit. | Dòng 45/128/1051 vẫn ghi hai tên mã URL trong khi bảng 174 chỉ có INVALID_PAYMENT_BASE_URL. Dòng 1193 mô tả mapping 23P01 như đã có; handler hiện trả RESOURCE_CONFLICT cho exclusion và INVALID_REQUEST cho FluentValidation. Ghi rõ mapping đích Proposed theo endpoint, chỉ đổi đúng constraint; đồng bộ với M4/M5. [Handler hiện tại](../../../src/Services/OperationService/SmartParking.OperationService.API/Middleware/ReservationExceptionHandler.cs) |

### Minor / phần đã đóng

- **M1 đóng.** JSON catalogue v1 chỉ có bound cho ARRIVAL_LATE_TOLERANCE; tiêu đề effective và revision IDs/versions đã khớp. [Seed v1](../../../scripts/database/seeds/03b-reservation-policy-defaults.sql)
- Dòng 59 dùng T ≤ effective_until, còn thuật toán chính dòng 925 dùng T < effective_until. Đồng nhất theo khoảng nửa mở. Danh sách revisions đã có thứ tự xác định khi trùng version và createdAt.
- Bổ sung schema nullable của payment và notifications, enum channel status và mẫu kênh unavailable. Dòng 1120–1158 chỉ minh họa ACTIVE cho cả bốn kênh. Giữ phạm vi read-only theo WBS và ghi rõ khi nghiệm thu AC-5. [API template](api-design-template.md)

## 3. Traceability & AC verification matrix

| AC | Trạng thái hợp đồng | Kiểm chứng quyết định |
| --- | --- | --- |
| AC-1 · Catalogue & effective | Catalogue đạt; effective một phần | Seed v1, zero/disabled và nguồn explicit bằng default khi thiếu bound. |
| AC-2 · Override trong bounds | Một phần | Biên inclusive, range không biểu diễn được, ngoài biên và key cấm. |
| AC-3 · Draft và lịch sử | Một phần | Hai PUT, sửa Site draft, activate cùng một draft ở hai thời điểm, chèn giữa lịch, vắng predecessor. |
| AC-4 · Snapshot bất biến | Invariant đạt cho booking đã xác nhận; phần giao tiếp Booking còn mở | Tách review, admission, hold commit, xác nhận; kiểm version/value trước và sau T. |
| AC-5 · Integration | Một phần | Xác định health theo mode, callback của checkout đang chờ và bốn fixture read-only. |
| AC-6 · Bí mật/giới hạn quyền | C2 đạt về thiết kế; C1 chưa đóng | LIVE→FIXTURE không có outbound HTTP, SANDBOX không lấy key LIVE, Site PUT luôn 403. |
| AC-7 · Freshness | Đạt ở mức phân loại | 9 route TOKEN/ADMIN; 401, 403 role và 403 scope là ba tình huống riêng. |
| AC-8 · Kiểm thử tự động | Chưa xác minh runtime | Vòng này kiểm tra tài liệu và JSON mẫu, chưa chạy xUnit, Newman hoặc UAT. |
| AC-9 · Tái sử dụng | Một phần | Đúng ownership/row/resolver; phải thống nhất stale terms với Booking trước khi khóa mã lỗi mới. |

## 4. Sign-off

| Bàn giao | Quyết định | Điều kiện |
| --- | --- | --- |
| Phase 2 · SPARK-368 | Chưa ký | Sửa predicate scope, đảm bảo cùng draft chỉ được publish một lần, thống nhất binding Booking/Payment khi đổi cấu hình. |
| Phase 3 · SPARK-369 | Chưa ký | Khóa bảng health, nguồn effective, schema fixture và mã lỗi mà UI cần xử lý. |
| Phase 4 · SPARK-370 | Chưa ký | Chốt expected outcome trong ma trận; có thể chuẩn bị ngay các fixture theo seed và ca biên về quyền, lịch và snapshot. |

Ưu tiên vòng sửa tiếp theo là đồng bộ câu lệnh ghi với scope của nó và thống nhất kết quả với hai chủ luồng Booking/Payment. Các phần C2, M1 và phân loại TOKEN đã đủ rõ để giữ nguyên làm căn cứ thiết kế.
