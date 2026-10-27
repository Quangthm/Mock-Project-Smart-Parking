# SmartParking frontend — Design brief

**Phạm vi:** prototype thiết kế lại trong `van/`, tham chiếu frontend nguồn trong `FE/`.  
**Trải nghiệm ưu tiên:** Driver — tìm bãi, so sánh thông tin, chọn chỗ và hiểu rõ bước xác nhận.  
**Visual direction:** Organic Tech Bento, theo chuẩn chi tiết tại [`design-system.md`](design-system.md).

## Mục tiêu sản phẩm

Giúp tài xế tìm bãi đỗ phù hợp, hiểu tình trạng chỗ và giá, đặt chỗ với thông tin minh bạch, đồng thời quản lý xe và booking. Giao diện cần khiến người dùng biết mình đang ở đâu, trạng thái nào đã được xác nhận và cần làm gì tiếp theo.

## Người dùng và công việc

| Vai trò | Công việc chính | Ưu tiên thiết kế |
|---|---|---|
| Driver | Khám phá bãi, xem giá/chỗ, đặt chỗ, quản lý xe và lịch sử | Wayfinding rõ, giá và trạng thái trung thực, thao tác ít bước |
| Owner | Thiết lập bãi, cấu trúc, giá và thông tin kinh doanh | Hướng dẫn theo bước, lỗi dễ sửa, cấu hình rõ |
| Operator | Theo dõi chỗ/booking và hỗ trợ hoạt động tại bãi | Quét nhanh, trạng thái phân biệt, thao tác phục hồi |
| Admin | Quản lý quyền và các trường hợp nghiệp vụ | Quyền hạn rõ, tác vụ ảnh hưởng lớn có xác nhận |

Không coi prototype Driver hiện tại là kiến trúc cuối cho mọi vai trò. Chỉ thiết kế role, route và tính năng đã xác nhận trong `FE/` và đặc tả liên quan.

## Visual direction

Phong cách **Organic Tech Bento** dùng lưới Bento có chủ đích, typography Britti Sans + Manrope và palette Deep Forest, Impact Green, Soft Ash, Carbon Grey. Trang nền sáng trung tính là mặc định; màu nhấn dùng tiết chế. Bản đồ, sơ đồ bãi, zone và slot tạo nên đặc thù sản phẩm.

“Bento” không có nghĩa là bọc mọi thứ thành card. Chỉ tạo card khi ranh giới đó giúp hiểu nhóm thông tin hoặc thao tác. Ưu tiên khoảng thở và phân tầng nội dung hơn việc lấp đầy màn hình.

## Nguyên tắc trải nghiệm

1. **Định hướng trước trang trí:** địa điểm, tầng, zone, slot và đường đi phải hỗ trợ tác vụ.
2. **Một bước chính mỗi ngữ cảnh:** chỉ một CTA có mức nhấn cao nhất; hành động phụ đứng sau.
3. **Tách bạch trạng thái:** booking, pass/ticket, payment và tình trạng vật lý của slot là các trạng thái riêng.
4. **Nói rõ điều đã biết:** phân biệt dữ liệu thật, fixture, pending, unknown và kết quả đã xác nhận.
5. **Minh bạch trước khi xác nhận:** giá, thời lượng, khoản giữ chỗ, điều kiện và hậu quả hủy phải hiện đúng lúc.
6. **Thiết kế theo thiết bị:** desktop có thể dùng canvas nhiều vùng; mobile cần thứ tự nội dung và controls phù hợp, không thu nhỏ desktop.

## Quy trình trước khi thiết kế

- Xem màn hình hiện có trong `FE/` và yêu cầu/contract nghiệp vụ liên quan.
- Viết ngắn gọn user, tác vụ, thông tin quyết định, CTA chính và trạng thái cần hỗ trợ.
- Phác thứ bậc và bố cục toàn màn hình trước khi dựng component.
- Xác định hành vi responsive và trạng thái loading/empty/error/pending/success phù hợp.
- Thực thi theo [`design-system.md`](design-system.md) và [`ai-ui-rules.md`](ai-ui-rules.md).

## Nội dung và tính trung thực

- Dùng tiếng Việt giản dị, thống nhất cách gọi bãi, ô đỗ, vé, đặt chỗ và thanh toán.
- Không bịa số liệu, đánh giá, bãi hoạt động, giá hoặc tình trạng chỗ. Nếu cần fixture, ghi nhãn dữ liệu mẫu.
- Không tuyên bố đã đặt/thanh toán thành công trước khi có trạng thái authoritative.
- Làm rõ yêu cầu quyền, điều kiện sử dụng và lý do khi hành động không khả dụng.

## Phạm vi và nguồn chuẩn

Brief định hướng sản phẩm; không thay thế yêu cầu nghiệp vụ, API/schema, quyền truy cập hoặc policy trong repo. Khi tài liệu mâu thuẫn, dùng thứ tự ưu tiên trong [`AGENTS.md`](AGENTS.md) và cập nhật nguồn lỗi.
