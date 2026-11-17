# SPARK-340 · Review độc lập vòng 5

**Phán quyết: REJECTED — chưa khóa toàn bộ v1.4 làm SSOT.** Ngày 12/11/2026. Đối tượng là [API Design v1.4](admin-platform-policies-and-integrations-spark-340-api-design.md), SHA-256 `227361E92D9845FC66FEB5865D9C0E7303EBFBC801255D44C8EA57810950DEB4`. Các số dòng bên dưới thuộc bản này. [Kết quả vòng 4](admin-platform-policies-and-integrations-spark-340-review-v1.3.md)

## 1. Executive summary

Đã đóng lỗi vòng đời Booking của v1.3: admission trả 202, hold vào PENDING_PAYMENT, Operations quyết định CONFIRMED sau khi xác minh collection. Đã sửa bounds giờ/ngày, bổ sung nhánh UPDATE trúng 0 hàng và đồng nhất health sau PUT với GET. Còn một blocker ở xác thực webhook: sau khi kiểm tra key được gắn với order thất bại, đặc tả cho phép thử các key khác mà chưa định nghĩa giới hạn tin cậy hay kết quả settlement. Phần xử lý tiền đến muộn cũng chưa khớp quyết định của Operations.

| Chiều review | Kết quả của v1.4 |
| --- | --- |
| Template | Đủ các phần chính và 9 endpoint Proposed, gồm PUT bổ sung để sửa draft. 17 khối JSON parse được, effective có 14 key. Hai mẫu PUT payment là SANDBOX/UNTESTED và FIXTURE/HEALTHY. |
| Kiến trúc | Ownership và ADMIN/TOKEN phù hợp. Cần trả quyết định tái cấp chỗ cho Operations khi tiền đến sau hạn hold nhưng trước giờ đỗ. |
| Bảo mật | Scope Platform nằm trong predicate ghi, mask/flags đúng, fixture không gửi HTTP. Nhánh fallback webhook ở dòng 1190 làm yếu ràng buộc order–mode–key. |
| Nghiệp vụ và concurrency | Đúng kết quả cho dải 61–121 và 61–119 phút, cho 0 ngày ở lớp lưu trữ và cho stale write sau pre-check. Còn sai công thức H, mô tả horizon 0 và bảo đảm token đổi giữa hai lần ghi. |
| Sẵn sàng triển khai | Có thể bàn giao phần đọc catalogue/effective, form health và fixture theo schema đã chốt. Chưa ký toàn bộ giao diện Payment/Booking và bộ assertion nghiệm thu. |

## 2. Detailed findings

### Critical · M6 — xác thực callback phải giữ binding của đơn hàng

Dòng 1184–1189 đã giải quyết K0→K1→K2 bằng cách lưu mode và signing key version trên order. Tuy nhiên dòng 1190 cho phép thử current key và các key còn hạn nếu chữ ký không khớp hoặc orderCode lạ. Dòng 1193 chỉ nêu từ chối khi không khớp **bất kỳ** key nào. Không có kết quả riêng cho “chữ ký hợp lệ dưới key khác nhưng không hợp lệ với order này”. BE có thể coi đó là callback thành công, trong khi chữ ký ấy chưa chứng minh binding của order. Đây là lỗi hợp đồng; chưa quan sát sự cố runtime.

**Điều kiện đóng:** với order đã biết, chỉ chấp nhận khoản thu cho order sau khi xác minh đúng binding đã lưu. Nếu cần xử lý callback chưa khớp, mô tả riêng đường đối soát, kết quả HTTP và trạng thái tài chính. Một callback xác minh được bằng key khác hoặc chưa tìm thấy order không được tự làm order thành paid hay phát sự kiện xác nhận booking. QA cần hai ca đối xứng: K0 vẫn hợp lệ cho order K0 sau hai lần rotate; chữ ký chỉ hợp lệ dưới key khác không tạo settlement cho order K0. [Quyết định D11-B và giao diện verified collection](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md)

### Major

| Finding | Bằng chứng và tác động | Điều kiện đóng |
| --- | --- | --- |
| M5 · Deadline và thu tiền sau H | Dòng 1340 đặt H = thời điểm tạo hold + duration. Catalogue quy định H = min(thời điểm tạo hold + duration, giờ bắt đầu đỗ). Với hold 5 phút tạo trước giờ đỗ 2 phút, v1.4 kéo dài hạn thêm 3 phút. Dòng 144/1192 chuyển callback sau H sang đối soát/hoàn tiền, bỏ nhánh D11-D cho Operations tái kiểm tra và giành lại claim trước giờ đỗ. So sánh > H cũng bỏ ngỏ equality. | Dùng công thức có cap. Tại H dừng tạo checkout mới. Với H ≤ thời điểm đánh giá < giờ đỗ, Payment ghi nhận tiền đã xác minh và thông báo cho Operations; Operations có thể tái cấp claim dưới điều kiện D11-D, nếu thất bại thì đối soát. Giữ checkpoint DB trước giờ đỗ; phân biệt thời điểm callback với thời điểm quyết định. [Catalogue](../../01-requirements/business-rules/policy-catalogue.md) · [D11-D/E](../../05-project-management/decision-log/2026-11-11-reservation-event-driven-confirmation.md) |
| M5 / M7 · Assertion dùng sai receipt | Dòng 1360 yêu cầu `status` và `rejectionReason`. Hợp đồng và DTO trả `state`, `code`, `reservationId`. Dòng 1361 yêu cầu GET booking bằng ID trong khi ca bị từ chối không sinh ID. Một 404 với ID tự chọn không chứng minh giao dịch vừa kiểm thử chưa tạo booking. | Sau 202, kiểm GET receipt của chính request: `state=REJECTED`, `code=TERMS_CHANGED_OR_EXPIRED`, `reservationId=null`. Nếu cần chứng minh không có hàng hoặc hold, kiểm theo request trong integration test. Dòng 1340 cũng cần gắn HOLD_CREATED với trạng thái đề xuất trong addendum để QA không hard-code hai token thay thế nhau. [Booking API](reservation-first-slice-api-design.md) · [ReservationReceipt](../../../src/Services/OperationService/SmartParking.OperationService.Application/Reservations/AdmissionContracts.cs) |
| M4 · Token có thể trùng dù UPDATE thành công | Dòng 899–911 đã phân loại đúng race khi UPDATE trả 0. Tuy nhiên dòng 891 dùng CURRENT_TIMESTAMP. Giá trị thời gian của transaction không bảo đảm khác giá trị cũ ở độ chính xác lưu trữ; RETURNING chỉ xác nhận giá trị đã lưu. Nếu trùng, một PUT giữ token cũ có thể tiếp tục ghi thành công. | Bảo đảm mỗi lần ghi đổi token trong chính câu UPDATE, ví dụ lấy giá trị lớn hơn giữa thời gian DB và token cũ cộng một microsecond; trả giá trị từ RETURNING. Test với đồng hồ bằng giá trị cũ, rồi gửi lại token cũ và yêu cầu 409. |
| M2 · Ý nghĩa nghiệp vụ của 0 ngày | Dòng 817 mô tả horizon 0 là “chỉ cho phép đặt trong ngày”. Catalogue đo khoảng cách từ thời điểm quyết định đến giờ bắt đầu; domain yêu cầu đồng thời `Start > now` và `Start - now <= advanceHorizon`. Với horizon 0, không có giờ bắt đầu tương lai nào thỏa mãn, kể cả sau một phút cùng ngày. | Giữ [0,0] là bound biểu diễn được; sửa diễn giải cho FE và QA theo hợp đồng domain. Nếu muốn nhận mọi lượt trong cùng ngày lịch, cần quyết định nghiệp vụ riêng. [ReservationInterval](../../../src/Services/OperationService/SmartParking.OperationService.Domain/Reservations/ReservationInterval.cs) |

### Minor / phần đã đóng

- **C1, C2, M1, M3 giữ kết quả vòng 4** đối với scope Admin, credential outbound theo mode, seed v1 và hai lệnh activate cùng một draft. Blocker M6 ở trên thuộc đường webhook inbound.
- **M2 đã đóng phần trần nghiệp vụ.** [61,121] phút chứa 2 giờ; [61,119] không chứa giờ nguyên; [0,0] là dải ngày biểu diễn được. Catalogue v1 chỉ có bound cho ARRIVAL_LATE_TOLERANCE. [Seed](../../../scripts/database/seeds/03b-reservation-policy-defaults.sql)
- **M6 đã đóng phần health và cách giữ nhiều phiên bản key.** GET/PUT đồng nhất theo mode và credential. Dòng 142/1186 nên diễn đạt rõ thời điểm được xóa key khi thời hạn tối thiểu và vòng đời order khác nhau; giữ phương án đối soát cho callback đến sau thời hạn lưu.
- **M7 đã thống nhất mã URL** thành INVALID_PAYMENT_BASE_URL và phân biệt mapping GiST đề xuất với handler hiện tại. Vấn đề còn lại về tên trường receipt thuộc hợp đồng Booking.

## 3. Traceability & AC verification matrix

| AC | Trạng thái thiết kế | Bằng chứng hoặc ca quyết định |
| --- | --- | --- |
| AC-1 · Catalogue/effective | Đạt về schema | 14 key, nguồn explicit/inherited, zero/disabled. Sửa mô tả horizon 0 trước khi dùng làm UI copy. |
| AC-2 · Bounds | Đạt về validation giờ/ngày; còn diễn giải | Các dải 61–121, 61–119 và 0 ngày có kết quả xác định. |
| AC-3 · Draft và lịch | Một phần | Scope, GiST, target lock, outcome khi mất race đã rõ; cần bảo đảm token mới thật sự đổi. |
| AC-4 · Snapshot | Đạt phần giữ v1 của hold hợp lệ đã commit; còn deadline | Booking sau đổi policy giữ đúng snapshot. Chốt hạn H và hai nhánh phục hồi trước giờ đỗ. |
| AC-5 · Integration | Một phần | Đạt health và fixture read-only; chưa khóa callback khi key không khớp order. |
| AC-6 · Bí mật và quyền vận hành | Đạt phần redaction/scope | Cần chứng minh thêm không settlement sai binding khi dùng retired key. |
| AC-7 · Freshness | Đạt về thiết kế | 9 route ADMIN/TOKEN. |
| AC-8 · Automation/UAT | Chưa xác minh runtime | Đã kiểm tra JSON tĩnh. Chưa chạy xUnit, Newman hoặc UAT. |
| AC-9 · Tái sử dụng | Một phần | Đã dùng lại resolver và phân tầng; sửa tên trường receipt và nhánh D11-D theo chủ miền. |

## 4. Sign-off

| Nhóm | Quyết định | Việc còn lại để ký |
| --- | --- | --- |
| SPARK-368 · Backend | Chưa ký toàn bộ | Khóa điều kiện ghi nhận collection, deadline/recovery và token draft. |
| SPARK-369 · Frontend | Đủ để triển khai các view catalogue/health/notification đã rà; chưa ký toàn bộ | Dùng đúng field receipt và thông điệp cho horizon 0. |
| SPARK-370 · QA | Có thể chuẩn bị fixture; chưa ký nghiệm thu | Thêm ca đúng/sai key theo order, H bằng giờ hết hạn, recovery trước giờ đỗ, receipt không có reservationId và hai PUT trùng timestamp. |

Lần review tiếp theo có thể tập trung vào bốn vị trí quyết định: xác thực webhook, công thức H và recovery, DTO receipt, phép sinh token draft. Phần bounds chỉ còn sửa câu mô tả 0 ngày.
