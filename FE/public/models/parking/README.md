# THƯ MỤC CHỨA MÔ HÌNH 3D BÃI XE (PARKING LOT 3D MODELS)

Thư mục này là nơi đặt các file mô hình 3D định dạng `.glb` cho hệ thống SmartParking.

## 1. Danh sách 3 file mô hình chuẩn được hệ thống chấp nhận

Hệ thống Front-End đã được cấu hình chặt chẽ chỉ chấp nhận và tích hợp **đúng 3 file** sau tương ứng với 3 loại bãi đỗ:

| Tên file bắt buộc | Loại bãi xe tương ứng | Mô tả |
| :--- | :--- | :--- |
| `outdoor_parking_lot.glb` | **Outdoor** (Bãi đỗ ngoài trời) | Mô hình bãi xe mặt đất mở, không có trần, có vạch kẻ và cây xanh. |
| `indoor_parking_lot.glb` | **Multi-Storey** (Bãi nhiều tầng / trong nhà) | Mô hình kết cấu nhiều tầng hoặc trong nhà, có các cột trụ phân làn. |
| `underground_parking_lot.glb` | **Basement** (Bãi tầng hầm ngầm) | Mô hình hầm gửi xe ngầm, trần thấp, hệ thống chiếu sáng nhân tạo. |

> **Lưu ý quan trọng**: Khi Owner chọn loại bãi, hệ thống sẽ tự động liên kết với đúng file `.glb` tương ứng. Nếu Owner tải lên file tùy chỉnh, hệ thống **chỉ chấp nhận các file có đúng tên trên**. Bất kỳ file nào có tên khác sẽ bị hệ thống từ chối tích hợp.

---

## 2. Quy ước đặt tên Mesh / Object (Naming Convention)

Để Front-End có thể tự động nhận diện từng slot, hiển thị màu sắc trạng thái và camera focus vào đúng vị trí ô đỗ, người làm 3D **BẮT BUỘC** đặt tên cho các Mesh ô đỗ xe theo chuẩn sau:

### Quy chuẩn tên:
`[Ký hiệu khu vực]-[Số thứ tự]`

Ví dụ:
- `A-001`, `A-002`, `A-003`, ..., `A-024`
- `B-001`, `B-002`, `B-015`
- `M-001`, `M-002` (Dành cho xe máy - Motorcycle)

*Ghi chú: Front-End hỗ trợ bộ lọc thông minh, có thể tự động nhận diện cả các tên mesh xuất từ Blender như `Slot_A-001`, `slot_A-001`, `A001`.*

---

## 3. Cấu trúc toạ độ và Pivot Point

1. **Hệ trục toạ độ**:
   - Trục **Y-up** (Y hướng lên trên).
   - Trục **Z-forward** (hoặc mặt bằng nằm trên mặt phẳng XZ).
2. **Pivot Point (Gốc toạ độ của Mesh ô đỗ)**:
   - Đặt Pivot tại **chính giữa mặt sàn** của ô đỗ xe.
   - Tránh đặt Pivot lệch ra ngoài ô đỗ vì Front-End dùng tâm của Mesh ô đỗ để điều khiển Camera tự động lướt (focus) vào vị trí đó.
3. **Kích thước khuyến nghị một ô đỗ tiêu chuẩn**:
   - Chiều rộng: `2.5m - 2.8m`
   - Chiều dài: `5.0m - 5.5m`
   - Khoảng cách làn xe chạy ở giữa: `6.0m - 7.5m`

---

## 4. Cách Front-End xử lý và tương tác với Model

1. **Nhận diện**: Front-End quét qua toàn bộ cấu trúc Scene Graph trong file `.glb`, tìm tất cả các Mesh có tên khớp với quy ước (`A-001`, `B-001`...).
2. **Màu sắc trạng thái**: Front-End tự động gán màu dynamic dựa trên dữ liệu thời gian thực:
   - **Xanh lá (`#22c55e`)**: Ô trống (`Available`)
   - **Đỏ (`#ef4444`)**: Đã có xe đỗ (`Occupied`)
   - **Cam (`#f59e0b`)**: Đã được đặt trước (`Reserved`)
   - **Xanh dương phát sáng (`#3b82f6`)**: Đang được Driver chọn hoặc đang xem chi tiết (`Selected`)
3. **Camera Focus**:
   - Khi Driver hoặc Operator bấm vào slot `A-023` (hoặc mở đơn đặt có `slotId = "A-023"`), Camera Controller sẽ tự động tính toán toạ độ và di chuyển mượt mà đến góc nhìn cận cảnh của ô `A-023`.

---

## 5. Quy trình bàn giao

1. 3D Artist xuất file định dạng `.glb` (khuyến nghị nén Draco nếu file dung lượng lớn).
2. Đổi tên file thành 1 trong 3 tên chuẩn:
   - `outdoor_parking_lot.glb`
   - `indoor_parking_lot.glb`
   - `underground_parking_lot.glb`
3. Copy trực tiếp file vào thư mục `public/models/parking/` của dự án Front-End.
4. Mở giao diện Web, hệ thống sẽ tự động hiển thị mô hình thực tế mà không cần viết lại bất kỳ dòng code logic nào!
