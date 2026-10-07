# HƯỚNG DẪN DÀNH CHO 3D MODELER & TÍCH HỢP 3D PARKING

Tài liệu này mô tả chi tiết quy chuẩn thiết kế, xuất file và tích hợp mô hình 3D bãi đỗ xe vào hệ thống Front-End **SmartParking**.

---

## 1. Nguyên Tắc Phối Hợp & Kiến Trúc Tách Biệt

```
[3D Modeler / Artist]
  │  Tạo mô hình 3D (.glb / .gltf)
  │  Đặt tên mesh theo chuẩn (A-001, B-001...)
  ▼
[public/models/parking/]
  │  outdoor_parking_lot.glb
  │  indoor_parking_lot.glb
  │  underground_parking_lot.glb
  ▼
[Three.js Engine (Frontend)]
  │  Quét scene graph & nhận diện slot
  │  Gán dynamic material trạng thái
  │  Điều khiển Smooth Camera Focus
  ▼
[React UI & Business Logic]
  │  Owner: Quản lý bãi đỗ, tích hợp và preview mô hình 3D
  │  Driver: Xem mô hình 3D tương tác, click chọn slot đặt chỗ
  │  Operator: Giám sát trạng thái bãi đỗ theo thời gian thực (Digital Twin)
```

**Mục tiêu quan trọng nhất**: Người làm mô hình 3D chỉ cần làm đúng model + quy ước đặt tên (naming convention). Toàn bộ logic nghiệp vụ (Booking, Giữ chỗ, Thanh toán, Phân quyền) được tách biệt hoàn toàn và không bị ảnh hưởng khi thay thế model mới.

---

## 2. Ba File Mô Hình Được Phép Tích Hợp

Hệ thống chỉ chấp nhận đúng 3 file mô hình tương ứng với 3 loại bãi xe:

1. **`outdoor_parking_lot.glb`** ➔ Loại bãi: `outdoor` (Bãi ngoài trời)
2. **`indoor_parking_lot.glb`** ➔ Loại bãi: `multi-storey` (Bãi trong nhà / nhiều tầng)
3. **`underground_parking_lot.glb`** ➔ Loại bãi: `basement` (Bãi tầng hầm ngầm)

Hệ thống có cơ chế kiểm tra (validation) nghiêm ngặt: Khi Owner tạo bãi xe hoặc tải file lên, nếu file không thuộc 3 tên trên thì hệ thống sẽ từ chối tích hợp.

---

## 3. Quy Ước Đặt Tên Slot (Naming Convention)

Mỗi vị trí đỗ xe trong mô hình phải là một **Mesh riêng biệt** (hoặc Group) và được đặt tên theo định dạng chuẩn:

```
[Mã_Khu_Vực]-[Số_Thứ_Tự]
```

### Ví dụ hợp lệ:
- `A-001`, `A-002`, `A-003`, ..., `A-024`
- `B-001`, `B-002`, ..., `B-018`
- `M-001`, `M-002` (Khu xe máy)
- `C-101`, `C-102`

### Các biến thể được hỗ trợ tự động:
- Xuất từ Blender: `Slot_A-001`, `slot-A-001`, `Slot_A_001`, `A001`, `A-001.001` (hệ thống sẽ tự động lọc và chuẩn hóa về `A-001`).

---

## 4. Bảng Màu Trạng Thái Ô Đỗ Xe

Front-End sẽ tự động can thiệp vật liệu của Mesh ô đỗ dựa trên trạng thái thời gian thực:

| Trạng thái | Mã màu Three.js | Ý nghĩa |
| :--- | :--- | :--- |
| **Available** (Còn trống) | `#22c55e` (Xanh lá) | Driver có thể click để chọn đặt |
| **Occupied** (Đã có xe) | `#ef4444` (Đỏ) | Cảm biến/hệ thống ghi nhận đang có xe |
| **Reserved** (Đã giữ chỗ) | `#f59e0b` (Cam) | Đã có tài xế đặt trước hoặc đang thanh toán |
| **Selected** (Đang chọn) | `#3b82f6` (Xanh dương phát sáng) | Slot tài xế đang click chọn hoặc đơn booking hiện tại |

---

## 5. Cơ Chế Camera Focus

Khi có sự kiện:
- Driver click vào slot trên giao diện 3D
- Driver mở thông tin đơn đặt chỗ (ví dụ: `currentBooking.slotId = "A-023"`)
- Operator chọn slot trong danh sách quản lý

Bộ điều khiển `CameraController` của Front-End sẽ:
1. Xác định vị trí Mesh `A-023` trong không gian 3D.
2. Tính toán tâm khối hộp bao (`BoundingBox.getCenter`).
3. Dùng thuật toán lerp nội suy mượt mà để lướt Camera đến góc nhìn cận cảnh rõ nét của ô xe đó.
