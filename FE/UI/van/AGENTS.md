# Hướng dẫn làm việc trong `van/`

## Mục tiêu

`van/` là prototype để thử nghiệm thiết kế lại frontend SmartParking dựa trên ứng dụng gốc trong `FE/`. Mục tiêu là tạo giao diện dễ hiểu, thoải mái và nhất quán; không phải thêm hiệu ứng hoặc component chỉ để làm màn hình trông “hiện đại”.

## Thứ tự ưu tiên tài liệu

1. [`design-brief.md`](design-brief.md): người dùng, mục tiêu và phạm vi trải nghiệm.
2. [`design-system.md`](design-system.md): nguồn chuẩn duy nhất cho visual identity, tokens, typography, spacing, layout và states.
3. [`ai-ui-rules.md`](ai-ui-rules.md): nguyên tắc thực thi UI và kiến trúc code.
4. Đặc tả nghiệp vụ trong [`specs/`](specs/) và hợp đồng/requirements trong repo gốc: nguồn chuẩn cho hành vi nghiệp vụ.

Nếu thấy mâu thuẫn, ưu tiên theo thứ tự trên, ghi nhận mâu thuẫn và sửa tài liệu sai. Không tự tạo thêm màu, font, spacing hay pattern để giải quyết mâu thuẫn.

## Quy trình bắt buộc trước khi dựng hoặc sửa màn hình

1. Đọc brief và đặc tả của luồng cần làm. Xác định vai trò người dùng, việc họ cần hoàn tất, dữ liệu cần thấy, hành động chính và trạng thái bất thường.
2. Xem màn hình/luồng tương ứng trong `FE/` và ghi lại hành vi hoặc nội dung cần giữ. Không tự suy diễn nghiệp vụ từ mockup.
3. Lập bố cục ở cấp màn hình trước khi code: vùng điều hướng, vùng nội dung, thứ bậc tiêu đề, nhóm thông tin, CTA chính, responsive behavior. Chỉ thêm component khi nó phục vụ bố cục hoặc một hành động có thật.
4. Áp dụng tokens và component hiện có. Tránh màu, font, khoảng cách và hiệu ứng hard-code nếu đã có token hoặc utility phù hợp.
5. Hoàn tất đầy đủ các trạng thái cần thiết: loading, empty, error, success/pending và disabled khi phù hợp. Dữ liệu prototype phải được nhận diện là dữ liệu mẫu.
6. Kiểm tra lại toàn màn hình ở desktop và mobile: thứ bậc có rõ không, nội dung có bị chật không, các vùng bấm có đủ lớn không, trạng thái có dễ phân biệt không.

## Các nguyên tắc không được vi phạm

- Tuân theo duy nhất visual identity trong `design-system.md`. Không thay font hoặc palette theo sở thích cá nhân.
- Không nhồi mọi chức năng vào một toolbar, dock hoặc hàng card. Chỉ đưa hành động quan trọng với ngữ cảnh hiện tại lên màn hình.
- Không tạo số liệu, trạng thái “live”, tính năng, chính sách hoặc kết quả thanh toán giả như dữ liệu thật. Gắn nhãn rõ cho fixture/mock data.
- Không ưu tiên trang trí hơn khả năng đọc, điều hướng, phân cấp và khoảng thở.
- Không thêm hiệu ứng glow/neon, gradient trang trí, bóng đổ nặng hoặc nhiều lớp kính mờ. Chỉ dùng khi có lý do chức năng và phù hợp design system.
- Không dùng inline style hoặc giá trị khoảng cách/màu/font tùy tiện khi có thể dùng token, CSS class hoặc biến thể component chung. Inline style chỉ được chấp nhận cho giá trị runtime không thể biểu đạt bằng class tĩnh, ví dụ tọa độ marker trên bản đồ.
- Giữ cấu trúc feature-based và public API `index.ts` cho feature khi phù hợp với cấu trúc hiện tại. Không tạo lớp trừu tượng chỉ để tuân thủ hình thức.

## Cổng tự rà soát trước khi bàn giao

Màn hình chỉ sẵn sàng khi có thể trả lời “có” cho các câu hỏi sau:

- Người dùng có nhận ra mục đích màn hình và hành động chính ngay khi nhìn vào không?
- Thông tin được nhóm theo tác vụ, với thứ bậc rõ ràng và khoảng cách đủ thoáng không?
- Typography, màu, spacing, radius và trạng thái có khớp `design-system.md` không?
- Có kiểm tra desktop/mobile và các trạng thái dữ liệu liên quan không?
- Nội dung mẫu, giới hạn và trạng thái chưa chắc chắn có được nói rõ không?

Nếu câu trả lời là “không”, sửa bố cục hoặc quyết định thiết kế trước khi thêm chi tiết trang trí.
