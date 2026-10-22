# SmartParking Visual Design System

> Trạng thái: chuẩn áp dụng cho prototype `van/`  
> Phạm vi: giao diện web SmartParking, bắt đầu từ trải nghiệm Driver  
> Phong cách: **Organic Tech Bento** — cấu trúc rõ và chính xác như sản phẩm công nghệ, dùng bảng màu tự nhiên, bố cục Bento có nhịp điệu và khoảng thở.

Tài liệu này là nguồn chuẩn duy nhất cho visual identity và quy tắc trình bày. Không dùng các hướng dẫn màu/font cũ trong tài liệu khác để ghi đè lên đây.

## 1. Visual identity

- Bố cục Bento Grid có chủ đích: các nhóm nội dung có quan hệ được đặt cạnh nhau; không biến mọi nội dung thành card.
- Cảm giác phẳng, ấm, rõ ràng và đáng tin cậy. Phân tách vùng bằng khoảng trắng, nền bề mặt và đường viền nhẹ; bóng chỉ dành cho lớp nổi cần nhận biết.
- Giao diện Driver ưu tiên bản đồ, bãi xe, chỗ trống, giá và bước đặt chỗ kế tiếp. Không để chrome, hiệu ứng hoặc tiện ích phụ cạnh tranh với tác vụ chính.
- Mặc định dùng giao diện sáng. Dark mode là biến thể tùy chọn và phải giữ nguyên thứ bậc, độ tương phản và ngữ nghĩa màu.

## 2. Màu sắc và token

### 2.1 Màu thương hiệu

| Token | Giá trị | Vai trò |
|---|---|---|
| `--brand-forest` | `#285337` | Màu thương hiệu đậm; CTA chính, tiêu đề/nhấn quan trọng |
| `--brand-impact` | `#B4E33F` | Điểm nhấn thương hiệu; dùng có chọn lọc cho highlight hoặc trạng thái đặc trưng |
| `--brand-ash` | `#D9D9D9` | Trung tính; viền và bề mặt phụ |
| `--brand-carbon` | `#343434` | Chữ đậm, icon, nền tối và tương phản |

`Impact Green` không được ghép với chữ trắng. Trên nền xanh chanh dùng `Deep Forest` hoặc `Carbon Grey`, sau khi kiểm tra độ tương phản tại kích thước thực tế.

### 2.2 Token giao diện ngữ nghĩa

| Token | Light mặc định | Dark tùy chọn | Dùng cho |
|---|---|---|---|
| `--bg` | `#F4F4F4` | `#181918` | Nền trang |
| `--surface` | `#FFFFFF` | `#222422` | Card và vùng nội dung |
| `--surface-muted` | `#D9D9D9` | `#2E302E` | Vùng phụ, control trung tính |
| `--fg` | `#343434` | `#F3F4F3` | Chữ chính |
| `--muted` | `#626262` | `#B5B8B5` | Chữ phụ; phải còn đọc được |
| `--border` | `#D9D9D9` | `#424542` | Viền phân cách nhẹ |
| `--primary` | `#285337` | `#B4E33F` | CTA chính; chữ tương phản tương ứng là trắng / `#1E3B27` |
| `--focus` | `#285337` | `#B4E33F` | Focus ring nhìn thấy rõ |

Feedback dùng token riêng, không mượn màu thương hiệu làm trạng thái:

| Token | Light | Dark | Ngữ nghĩa |
|---|---|---|---|
| `--success` | `#256B45` | `#74C69D` | Hoàn tất thành công |
| `--warning` | `#9A5B00` | `#F2B544` | Cần chú ý |
| `--danger` | `#B42318` | `#FF7B72` | Lỗi hoặc hành động nguy hiểm |
| `--info` | `#245D8F` | `#78B7E8` | Thông tin/trợ giúp |

Màu feedback thường đi cùng nền tint nhạt, nhãn chữ rõ và icon khi hữu ích. Không dùng màu đơn độc để truyền trạng thái. Trước khi phát hành, kiểm tra tương phản chữ/nền theo WCAG 2.2 AA: 4.5:1 cho chữ thường, 3:1 cho chữ lớn và thành phần giao diện cần nhận biết. Nếu token không đạt, điều chỉnh sắc độ ngữ nghĩa; không sửa mã màu thương hiệu.

### 2.3 Quy tắc dùng màu

- Nền trang và bề mặt trung tính chiếm ưu thế. Màu thương hiệu dành cho CTA chính và một vài điểm định hướng.
- Mỗi vùng chỉ có một CTA chính. CTA phụ dùng outline, text hoặc bề mặt trung tính.
- Không dùng `Impact Green` làm nền diện rộng, nền cho đoạn văn, hay làm màu mặc định của mọi badge/nút.
- Không dùng đỏ cho thao tác hủy thông thường; chỉ dùng màu danger khi hậu quả thực sự nguy hiểm hoặc lỗi cần hành động.
- Không dùng gradient để trang trí. Gradient chỉ hợp lệ nếu có vai trò dữ liệu/ảnh được xác định và có phương án fallback.

## 3. Typography

| Vai trò | Font | Weight | Dùng cho |
|---|---|---|---|
| Display/headings | **Britti Sans** | 400, 600, 700 | Display, tiêu đề trang/section, tiêu đề Bento |
| Body/UI/data | **Manrope** | 400, 600, 700 | Nội dung, label, nút, biểu mẫu, bảng và số liệu |

Khai báo fallback rõ ràng: `Britti Sans, Manrope, sans-serif` cho heading; `Manrope, sans-serif` cho body. Britti Sans là font thương hiệu: chỉ dùng khi dự án có quyền sử dụng và file font được cung cấp. Không giả định Google Fonts có font này; nếu thiếu file font, dùng Manrope cho cả hai vai trò và ghi nhận giới hạn. Không âm thầm thay bằng Plus Jakarta Sans, Outfit hoặc font khác.

### Type scale mặc định

| Token | Kích thước / line-height | Weight | Áp dụng |
|---|---|---|---|
| `display` | `clamp(2.5rem, 5vw, 3.5rem)` / 1.1 | 600 | Hero hoặc tiêu đề mở đầu quan trọng |
| `h1` | `2rem` / 1.2 | 600 | Tiêu đề trang |
| `h2` | `1.5rem` / 1.25 | 600 | Tiêu đề section |
| `h3` | `1.25rem` / 1.3 | 600 | Tiêu đề card/nhóm |
| `body-lg` | `1.125rem` / 1.5 | 400 | Đoạn giới thiệu hoặc hướng dẫn quan trọng |
| `body` | `1rem` / 1.5 | 400 | Nội dung mặc định |
| `label` | `0.875rem` / 1.4 | 600 | Label, metadata, chú thích |
| `caption` | `0.75rem` / 1.4 | 400 | Chỉ dùng cho nội dung phụ không thiết yếu |

Không ép mọi chữ tối thiểu 14px; thay vào đó, nội dung quan trọng và điều khiển phải dễ đọc, còn caption chỉ dành cho thông tin phụ. Không dùng caption cho trạng thái, giá, label biểu mẫu hoặc hướng dẫn thiết yếu. Dùng tabular numerals cho tiền, thời gian và số lượng.

## 4. Layout, grid và spacing

### 4.1 Thang spacing

Chỉ dùng các nấc sau cho padding, margin và gap: **4, 8, 12, 16, 24, 32, 48, 64px**. Quy đổi sang utility của dự án; không tạo số lẻ tùy ý. Giá trị ngoại lệ chỉ được dùng khi bị ràng buộc bởi kích thước nội dung/bản đồ hoặc hệ thống ngoài, và cần được đặt tên thành token/component rule.

### 4.2 Khung responsive

| Viewport | Grid | Lề trang | Gutter |
|---|---|---|---|
| Mobile `<640px` | 4 cột | 16px | 12–16px |
| Tablet `640–1023px` | 8 cột | 24px | 16px |
| Desktop `≥1024px` | 12 cột | 32–64px | 24px |

- Max content width mặc định: `1280px`; chỉ dùng `1440px` cho bản đồ/canvas hoặc màn hình mật độ dữ liệu cần thêm chỗ.
- Nội dung không được kéo hết chiều ngang trên desktop nếu làm dòng chữ quá dài. Chia cột theo quan hệ tác vụ, không theo mục tiêu lấp kín lưới.
- Chuyển Bento card thành một cột hoặc bố cục xếp dọc trên mobile; không thu nhỏ một desktop canvas đến mức chữ và thao tác bị bóp.
- Mọi vùng bấm trên mobile tối thiểu `44 × 44px`; khoảng cách giữa các target đủ để tránh bấm nhầm.

### 4.3 Quy tắc phân tầng màn hình

Trước khi code, xác định bằng văn bản hoặc phác thảo ngắn:

1. **Điểm vào:** người dùng đang ở đâu và màn hình giúp hoàn tất việc gì?
2. **Nội dung chính:** thông tin nào quyết định bước tiếp theo?
3. **Hành động chính:** một CTA nổi bật, gắn sát nội dung liên quan.
4. **Hành động phụ:** để gần ngữ cảnh nhưng giảm nhấn mạnh; hành động hiếm/nguy hiểm không đặt cạnh CTA chính.
5. **Điều hướng và trạng thái:** vị trí hiện tại, trạng thái dữ liệu, lối quay lại và thông báo.

Không đặt mọi khối lên cùng một mặt phẳng. Dùng kích thước, vị trí, khoảng cách, typography và bề mặt để biểu đạt thứ bậc. Card chỉ dành cho nhóm nội dung có ranh giới/quan hệ; ưu tiên section phẳng khi card không giúp quét hoặc thao tác tốt hơn. Tránh nhiều dock/panel nổi cùng chiếm một vùng nhìn.

## 5. Shape, border và elevation

| Thành phần | Radius mặc định |
|---|---:|
| Bento card lớn | 20px |
| Card nhỏ, popover, dialog | 16px |
| Button/input | 10px |
| Badge/pill | 9999px |

- Pill chỉ dành cho badge, chip hoặc control có ý nghĩa phù hợp; không biến mọi nút thành viên thuốc.
- Viền mặc định 1px `--border`; dùng viền đứt chỉ cho placeholder/drop target hoặc vùng minh họa cần phân biệt.
- Giao diện ưu tiên phẳng. Shadow chỉ dùng cho dropdown, popover, dialog, tooltip hoặc đối tượng nổi khỏi canvas; độ mờ thấp, không dùng glow.
- Khi lồng bề mặt, dùng radius và padding nhất quán; tránh cardception. Không áp dụng công thức radius máy móc nếu hai khối không thật sự lồng nhau.

## 6. Components và interaction

- Icon dùng **Lucide React**, stroke mặc định `1.75–2`, cỡ `16/20/24px`. Icon trang trí chỉ dùng khi tăng nhận diện hoặc hỗ trợ quét thông tin; không lặp icon và nhãn vô ích.
- Buttons có phân cấp: primary (một CTA chính/ngữ cảnh), secondary, tertiary/text, destructive khi phù hợp. Không buộc dùng animation/morph cho mọi nút.
- Interactive controls phải có default, hover (nếu thiết bị hỗ trợ), pressed, focus-visible và disabled. Focus phải thấy rõ trên cả nền sáng/tối.
- Mọi thao tác icon-only có accessible name và target đủ lớn. Trạng thái không chỉ thể hiện bằng màu.
- Tôn trọng `prefers-reduced-motion`; chuyển động chỉ giúp phản hồi hoặc định hướng, không làm chậm thao tác.

## 7. Screen states và nội dung

Thiết kế các trạng thái cần thiết cho luồng: loading, empty, error, success và pending/unknown. Giữ riêng trạng thái booking, payment, pass/ticket và tình trạng vật lý của chỗ đỗ; không gộp chúng thành một badge tổng quát.

Không hiển thị fixture như dữ liệu trực tiếp. Gắn nhãn “Dữ liệu mẫu”/“Prototype” đúng ngữ cảnh; không dùng “LIVE” nếu chưa có tích hợp live. Hiển thị đơn vị tiền tệ, thời gian, vị trí và chính sách cần thiết trước hành động có hệ quả.

## 8. 3D và bản đồ

- Bản đồ/canvas là nội dung chính khi tác vụ là khám phá không gian; panel phủ lên chỉ chiếm phần cần thiết và không che mất đối tượng đang thao tác.
- Duy trì tương phản cho marker và status ở cả map style sáng/tối. Luôn có label/legend hoặc affordance khác cho status; không chỉ mã hóa bằng màu.
- Trên màn hình nhỏ, chuyển controls/panels thành sheet hoặc vùng cuộn có cấu trúc; không giữ nguyên layout desktop dạng nhiều lớp nổi.
- Không gọi fixture là vị trí, chỗ trống hoặc giá hiện thời.

## 9. Quy trình kiểm tra thiết kế

Trước khi coi một màn hình hoàn thành, kiểm tra:

- Có nhìn ra mục đích và CTA chính trong vài giây đầu không?
- Thứ bậc nội dung có hợp luồng tác vụ và không có quá nhiều thành phần cùng nổi bật không?
- Có đủ khoảng thở quanh nhóm nội dung; label, số liệu và thao tác có dễ đọc/quét không?
- Có dùng đúng font, token, spacing, radius và trạng thái không?
- Có hoạt động ở mobile, bàn phím và trạng thái dữ liệu khác nhau không?
- Fixture, giới hạn, pending/unknown và hậu quả tài chính có được nói trung thực không?

Nếu một câu trả lời là “không”, sửa quyết định bố cục hoặc nội dung trước khi tinh chỉnh bóng, animation hay trang trí.
