# SPARK-340 · Review độc lập vòng 6

**Phán quyết: REJECTED — chưa khóa toàn bộ v1.5 làm SSOT.** Bản rà soát ngày 12/11/2026, SHA-256 `A2F2CF233107F51F8F60E180AE81D18ACBF078AF13335907C4FF02CABA0072F9`. Số dòng tham chiếu [API Design v1.5](admin-platform-policies-and-integrations-spark-340-api-design.md). [Báo cáo vòng 5](admin-platform-policies-and-integrations-spark-340-review-v1.4.md)

## 1. Executive summary

V1.5 đã sửa cả năm nội dung chính được yêu cầu ở vòng 5 trong các phần đặc tả tương ứng. Hai câu còn lại trong luồng tích hợp cho phép BE và QA hiểu sai thời điểm được ghi nhận thu tiền và xác nhận booking. Cả hai có thể sửa bằng cách dẫn lại hợp đồng của service sở hữu, không cần thêm endpoint Admin.

| Nội dung vòng 5 | Kết quả đối soát |
| --- | --- |
| M6 · Chữ ký sai key của order | Đóng lỗi fallback. Dòng 1199–1209, 1222–1224 chọn đúng mode/key version, chấp nhận K0 cho order K0 sau hai lần rotate, từ chối K1 cho order đó. Đơn lạ không tạo settlement. Điều kiện sau khi chữ ký đúng còn finding F1. |
| M5 · H và recovery | Đóng lỗi công thức và biên H. Dòng 1212–1221, 1371–1381 dùng H = min(hold_created_at + duration, T_start), giữ nhánh reacquire cho H ≤ evaluatedAt < T_start. Điều kiện dùng fast path trước H còn finding F2. |
| M5 · Receipt khi terms stale | Dòng 1395–1410 đã dùng đúng state, code, reservationId và kiểm không có hàng theo request_id; DTO và cột SQL đều tồn tại. Còn sửa envelope ở ghi chú nhỏ bên dưới. |
| M4 · Token draft trùng | Đóng ở SQL chính: dòng 901–910 lấy giá trị lớn hơn giữa giờ DB và token cũ cộng 1 microsecond, kiểm token/scope trong UPDATE và dùng RETURNING. Nhánh 0 hàng phân biệt stale với vừa activate. |
| M2 · Horizon 0 | Đóng. Dòng 823–830 cho lưu 0 ngày và giải thích đúng: khi Start phải sau now, cửa sổ 0 không nhận được lượt đặt tương lai. Giá trị trả về vẫn là số 0. |

## 2. Detailed findings

### Critical · F1 — Chữ ký đúng chưa đủ để ghi nhận thu tiền

**Dòng 1210, 1213–1214 và ví dụ QA 1223** nối chữ ký hợp lệ trực tiếp với ghi nhận thu tiền và phát sự kiện. Một callback ký đúng key có thể mang kết quả giao dịch thất bại, số tiền khác order hoặc là bản gửi lại của cùng một collection. Hợp đồng v1.5 khiến một implementation bỏ qua các kiểm tra này vẫn có vẻ đúng theo ví dụ. Handler Payment hiện tách kiểm chữ ký, kiểm event đã xử lý, so khớp số tiền và kiểm `PaymentStatus == SUCCESS`. D11-B còn phân biệt bản gửi lại của cùng collection với một khoản thu khác thực sự đến trên cùng order. Đây là sai lệch của tài liệu, chưa quan sát thấy settlement sai trong runtime. [Handler Payment](../../../src/Services/PaymentService/SmartParking.PaymentService.Application/Usecase/ProcessWebhook/ProcessPayOsWebhookCommandHandler.cs) · [Quyết định D11-B](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md)

**Điều kiện đóng:** sau xác thực K0, chuyển callback sang luồng kiểm tra collection của Payment. Chỉ phát sự kiện thu tiền thành công khi trạng thái và các giá trị ràng buộc với order hợp lệ, theo quy tắc chống xử lý trùng của Payment. QA giữ ca K0/K1 và thêm ba kết quả: callback ký đúng nhưng trạng thái thất bại không làm order thành PAID; sai số tiền không xác nhận booking; phát lại cùng collection không tạo hiệu ứng lần hai. Bằng chứng về tiền đến sai hoặc đến thêm được xử lý theo luồng đối soát của Payment.

### Major · F2 — Hold còn hạn vẫn có thể mất hiệu lực

**Dòng 1377–1380** lấy `evaluatedAt < H` làm điều kiện vào fast path và chuyển sang CONFIRMED. D11-C chỉ cho bỏ bước phối hợp Facility sau khi chứng minh invariant giữ claim; trước đó vẫn yêu cầu kiểm tra Facility/capacity. D11-D áp dụng cả cho claim bị vô hiệu hoặc không còn chứng minh được quyền giữ chỗ, kể cả trước H. Chẳng hạn, tiền về trước H nhưng claim bị vô hiệu trong lúc chờ thanh toán: điều kiện thời gian vẫn đúng, còn entitlement chưa đủ để xác nhận. [Quyết định D11-C/D](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md) · [Booking API addendum](reservation-first-slice-api-design.md)

**Điều kiện đóng:** tại dòng 1214 và 1379, dẫn đúng luồng xác nhận guarded của Operations. Giữ Facility coordination cho tới khi bằng chứng D11-C được duyệt. QA tách ca claim còn được bảo vệ với ca claim bị vô hiệu trước H; ca thứ hai phải tái thẩm định/reacquire trước T_start hoặc chuyển đối soát nếu thất bại. Snapshot v1 đã nhận tiếp tục bất biến trong cả hai trường hợp.

### Minor · Đồng bộ ví dụ với phần đặc tả chính

- Dòng 1497 của Sequence 1 vẫn gán `updated_at = CURRENT_TIMESTAMP`; sửa thành cùng biểu thức tăng token như dòng 901 hoặc chỉ mô tả kết quả của repository. Nếu sao chép SQL từ sequence, lỗi v1.4 sẽ xuất hiện trở lại.
- Dòng 210 còn ghi `status: REJECTED`. Dòng 1397–1405 là JSON của DTO. Controller GET thực tế bọc DTO trong `{success, data}`. Ghi rõ đây là nội dung của `data`, để Newman dùng `data.state`, `data.code`, `data.reservationId`. [Controller](../../../src/Services/OperationService/SmartParking.OperationService.API/Controllers/ReservationRequestsController.cs) · [DTO](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/AdmissionContracts.cs)
- Dòng 142 và 1198 còn dùng “tối thiểu 24 giờ hoặc cho đến khi …”. Nếu cả hai là điều kiện bảo vệ, ghi rõ thời điểm sớm nhất được xóa key và đường đối soát khi key đã hết hạn. Dòng 1202 có thể bỏ câu về thử key cho order lạ để nhất quán với nhánh trả ngay 404.

## 3. Traceability & AC verification matrix

| AC | Kết luận ở cổng thiết kế | Căn cứ |
| --- | --- | --- |
| AC-1 · Catalogue và effective | Đạt | 14 key, nguồn kế thừa/override, zero và flag disabled. |
| AC-2 · Resolution và bounds | Đạt | Biên đọc theo revision, phân biệt khả năng override với bound hiện hành; horizon 0 khớp domain. |
| AC-3 · Draft, sửa, lịch và history | Đạt ở đặc tả chính | Scope, token tăng, conditional update, target lock và GiST. Đồng bộ Sequence 1. |
| AC-4 · Snapshot | Đạt về tính bất biến; luồng xác nhận còn F2 | Hold nhận v1 giữ snapshot khi policy đổi sang v2. Confirm vẫn phụ thuộc claim tại checkpoint. |
| AC-5 · Integration | Một phần | Health GET/PUT theo mode, bốn delivery fixture read-only và rotation đã rõ; sửa F1. |
| AC-6 · Secret và giới hạn quyền | Đạt về scope/redaction | Admin chỉ ghi Platform, key theo mode, không fallback cho order đã biết, fixture không gửi HTTP. |
| AC-7 · TOKEN/ADMIN | Đạt về thiết kế | 9 route đều khai báo TOKEN và ADMIN. |
| AC-8 · Automation/UAT | Chưa xác minh runtime | Đã parse 18 khối JSON, đối chiếu DTO/schema. Chưa chạy xUnit, Newman hay UAT. |
| AC-9 · Tái sử dụng hợp đồng | Một phần | Đã dùng resolver, schema và Receipt của Operations. Cần dẫn đúng điều kiện settlement/confirmation của Payment và Operations. |

## 4. Sign-off & next steps

Trong năm chiều review, cấu trúc và dữ liệu mẫu đáp ứng phần chính của template; phân quyền Admin và che giấu secret đạt ở mức thiết kế. Ngữ nghĩa giữa hai service và các assertion cho settlement/confirmation là phần chặn bàn giao. Đặc tả vẫn gồm chín endpoint Proposed, trong đó PUT sửa draft phục vụ AC-3.

| Nhóm | Quyết định |
| --- | --- |
| SPARK-368 · Backend | Có thể triển khai API quản trị policy, optimistic update và cấu hình/masking theo mode. Chưa ký luồng xử lý tài chính và xác nhận booking cho tới khi đóng F1/F2. |
| SPARK-369 · Frontend | Đủ để bàn giao các view catalogue/effective/history, form draft và dashboard health/fixture trong phạm vi Admin. Khi đọc receipt Booking, dùng envelope của controller. |
| SPARK-370 · QA | Đủ để soạn fixture cho chín route. Thêm ca chữ ký đúng nhưng giao dịch thất bại, callback lặp và claim bị vô hiệu trước H vào ma trận trước khi khóa expected results liên dịch vụ. |

Phạm vi vòng sửa tiếp theo là hai điều kiện quyết định settlement/confirmation và các ví dụ đang diễn đạt khác đặc tả chính. Không cần mở lại C1, C2, M1 hoặc thuật toán schedule đã đối soát.
