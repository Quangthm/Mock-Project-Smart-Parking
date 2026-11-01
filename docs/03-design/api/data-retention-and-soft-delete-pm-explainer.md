# SmartPark — Giải Thích Nghiệp Vụ & Kỹ Thuật Cho PM: Vòng Đời Dữ Liệu 7 Năm, Cơ Chế Purge, Tính Bất Biến Của GUID & So Sánh `deleted_at` vs Cột `status`

| Metadata | Chi tiết |
| :--- | :--- |
| **Mã tài liệu** | `SPARK-PM-DATA-RETENTION-EXPLAINER` |
| **Phiên bản** | `v1.0.0` |
| **Ngày lập** | 2026-10-25 |
| **Đối tượng tiếp nhận** | Project Manager (PM), Technical Lead, Database Architect |
| **Tác giả** | Principal Backend Architect & AI Agent Specification Reviewer |
| **Tài liệu căn cứ** | [Policy Catalogue](../../01-requirements/business-rules/policy-catalogue.md), [Database Architecture](../../02-architecture/database/database-architecture.md), [Decision Log RA-01/02/06](../../05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md) |
| **Tài liệu liên quan** | [3D Digital Twin API Design](3d-digital-twin-api-design.md) |

---

## 1. Tóm Tắt Dành Cho PM (Executive Summary)

Bản giải thích này được biên soạn nhằm giải đáp thấu đáo 4 băn khoăn lớn của PM liên quan đến:
1. **Bản chất của chính sách Data Retention 7 năm**: Hệ thống có tự động "quét sạch và xóa cứng" (*hard-purge*) các slot mà Owner đã soft-delete sau 7 năm hay không?
2. **Khả năng phục hồi dữ liệu sau 7 năm**: Nếu giả định sau 7 năm slot cũ thực sự bị xóa mất khỏi cơ sở dữ liệu, làm cách nào để đưa ô đỗ đó trở lại kinh doanh?
3. **Mức độ ảnh hưởng của mã định danh GUID (`parking_slots.id`)**: Việc tái tạo hay phục hồi slot có làm hỏng lịch sử đặt chỗ cũ hoặc làm lệch tọa độ mô hình 3D không?
4. **Quyết định kiến trúc cột dữ liệu**: Có nên bổ sung thêm một cột `status` riêng hay tiếp tục sử dụng cột `deleted_at` như hiện tại?

---

## 2. Xác Minh Tài Liệu: Hệ Thống Có Tự Động Purge Slot Soft-Delete Sau 7 Năm Không?

> **KẾT LUẬN XÁC MINH:**  
> **Suy nghĩ của PM xuất phát từ sự nhầm lẫn giữa *"Thời hạn lưu trữ kiểm toán tối thiểu bắt buộc của dữ liệu giao dịch"* và *"Cơ chế tự động dọn dẹp (TTL) của dữ liệu cấu trúc"*.**  
> **Bãi đỗ xe đang hoạt động bình thường thì các slot soft-delete TUYỆT ĐỐI KHÔNG BAO GIỜ bị tự động xóa cứng (hard-delete/purge) khỏi Database.**

### Căn cứ từ tài liệu kiến trúc hệ thống:

1. **Phân biệt 2 loại dữ liệu trong hệ thống:**
   * **Dữ liệu Giao dịch / Audit Log (Transaction / Log Data):** Lịch sử bấm nút, nhật ký hệ thống, hóa đơn, phiên đỗ xe.
     * Căn cứ theo [`policy-catalogue.md` (Dòng 64–67)](../../01-requirements/business-rules/policy-catalogue.md#L64-L67), các chính sách **`AUDIT_RETENTION`** và **`FINANCIAL_DOCUMENT_RETENTION`** quy định thời hạn **7 năm**.
     * **Ý nghĩa pháp lý:** Đây là **thời hạn lưu giữ tối thiểu bắt buộc** (tuân thủ Luật Kế toán Việt Nam và Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15). Hệ thống bị cấm tiêu hủy các chứng từ kiểm toán này trước 7 năm.
   * **Dữ liệu Cấu trúc / Tài nguyên Quy hoạch (Master Data / Spatial Resources):** Bãi xe (`parking_sites`), tầng (`spatial_units`), ô đỗ (`parking_slots`).
     * Khi một bãi xe đang hoạt động bình thường, các ô đỗ thuộc mô hình 3D của bãi xe đó là **tài nguyên quy hoạch master data cố định**.
     * Khi Owner chọn cấu hình tắt bớt 18 slot trong tổng số 48 slot của mô hình 3D, 18 slot này chuyển sang trạng thái **nằm ngoài phạm vi quản lý kinh doanh hiện tại (Dormant / Unmanaged Capacity)** bằng cách gán `deleted_at = NOW()`.
2. **Không có cơ chế tự động Hard-Purge slot của bãi xe đang hoạt động:**
   * Trong toàn bộ tài liệu kiến trúc [`database-architecture.md`](../../02-architecture/database/database-architecture.md), hệ thống **hoàn toàn không có bất kỳ Cron Job hay Worker nào tự động chạy lệnh `DELETE FROM parking_slots`** đối với một bãi xe đang mở cửa.
   * Quy trình tiêu hủy vật lý (Hard-purge) chỉ xảy ra khi: **Toàn bộ doanh nghiệp / bãi xe chấm dứt hợp đồng dịch vụ hoàn toàn**, bãi xe bị xóa khỏi hệ thống và **đã trải qua đủ 7 năm lưu trữ kiểm toán**, lúc đó Admin mới kích hoạt quy trình tiêu hủy dữ liệu cấp bãi xe (*Site-level Decommissioning*).

---

## 3. Kịch Bản Giả Định: Nếu Sau 7 Năm Dữ Liệu Bị Purge Thật, Có Khởi Tạo Lại Được Không?

> **CÂU TRẢ LỜI:**  
> **HOÀN TOÀN ĐƯỢC VÀ ĐƯỢC THỰC HIỆN MỘT CÁCH TỰ ĐỘNG BỞI CƠ CHẾ SINH THỰC THỂ MỚI (ENTITY LIFECYCLE ARCHITECTURE).**

Theo chuẩn kiến trúc được PM phê duyệt tại [3D Digital Twin API Design (Mục 6.5)](3d-digital-twin-api-design.md#L531-L572), hệ thống áp dụng nguyên tắc **Vòng đời thực thể rời rạc (Discrete Entity Lifecycle)**:
- Soft-delete là sự kiện kết thúc vòng đời bất biến của `GUID_1`. Hệ thống **tuyệt đối không bao giờ tái sinh dòng cũ bằng lệnh `UPDATE ... SET deleted_at = NULL`**.
- Bất kể là sau 3 tháng hay sau 7 năm (kể cả khi dòng cũ `GUID_1` đã bị hard-purge), việc đưa một mã ô đỗ quay trở lại khai thác đều tự động **sinh một thực thể hoàn toàn mới mang `GUID_2`** (`created_at = NOW()`, `deleted_at = NULL`).

```text
LUỒNG TỰ ĐỘNG TÁI TẠO THỰC THỂ MỚI QUA PARTIAL UNIQUE INDEX

Owner tải lại Manifest 3D (ví dụ: muốn mở lại slot PARKING_G_CAR_018)
                           │
                           ▼
Kiểm tra DB: Tìm ô ACTIVE (slot_code = 'PARKING_G_CAR_018' AND deleted_at IS NULL)
                           │
        ┌──────────────────┴──────────────────┐
        ▼                                     ▼
[ĐÃ CÓ ROW ĐANG ACTIVE]               [CHƯA CÓ ROW ACTIVE NÀO]
(Đang kinh doanh bình thường)         (Row cũ đã Soft-Delete hoặc đã Purge sau 7 năm)
        │                                     │
        ▼ (Nhánh 1: In-Place Update)          ▼ (Nhánh 2: Sinh Entity Mới - New GUID)
Cập nhật thuộc tính:                  Chạy INSERT dòng mới:
slot_type, coordinates_3d             Sinh UUID mới (GUID_2)
Giữ nguyên GUID đang active           slot_code = 'PARKING_G_CAR_018'
                                      deleted_at = NULL
                                      (Nhờ Partial Index WHERE deleted_at IS NULL,
                                       GUID_1 lịch sử và GUID_2 cùng tồn tại an toàn!)
```

* **Cơ chế hoạt động:** Khi Owner gửi bản thiết kế 3D có chứa `PARKING_G_CAR_018`, Engine quét DB không thấy dòng active nào khớp `(site_id, slot_code, deleted_at IS NULL)` $\implies$ Hệ thống tự động kích hoạt **Nhánh 2 (Sinh Entity Mới - New GUID)**:
  ```sql
  INSERT INTO parking_slots (id, tenant_id, site_id, spatial_unit_id, slot_code, physical_state, reservation_state, created_at, deleted_at)
  VALUES (uuid_generate_v4(), $tenantId, $siteId, $floorId, 'PARKING_G_CAR_018', 'AVAILABLE', NULL, CURRENT_TIMESTAMP, NULL);
  ```
* **Kết quả:** Ô đỗ xuất hiện trở lại trong cơ sở dữ liệu và hiển thị ngay lập tức lên mô hình 3D để đón các lượt đặt xe mới mà không cần can thiệp thủ công từ kỹ sư DB, đồng thời không bao giờ làm nhiễu dữ liệu lịch sử của `GUID_1`.

---

## 4. Mức Độ Ảnh Hưởng Của Key GUID (`parking_slots.id`) Trong Suốt Quá Trình

PM hoàn toàn có thể yên tâm về tính toàn vẹn của Primary Key GUID:

| Giai đoạn thời gian | Trạng thái kỹ thuật của Slot & GUID | Mức độ ảnh hưởng & Tính toàn vẹn dữ liệu |
| :--- | :--- | :--- |
| **Giai đoạn 1: Năm 0 – Năm 7**<br>*(Slot đang bị Soft-Delete)* | Slot mang `id = GUID_1` (`deleted_at = NOW()`). Dòng dữ liệu vẫn tồn tại trong PostgreSQL. | **Ảnh hưởng = 0%**:<br>Toàn bộ hóa đơn (`invoices`), lượt đặt trước (`reservations.target_slot_id`), phiên đỗ (`parking_sessions`) và audit log (`structure_slot_audit`) trong 7 năm qua vẫn trỏ chính xác về `GUID_1`, bảo đảm 100% tính toàn vẹn quan hệ (khóa ngoại không mồ côi). |
| **Giai đoạn 2: Tái tạo slot bất kỳ lúc nào (sau 3 tháng hoặc sau 7 năm)** | Hệ thống sinh một dòng mới với `id = GUID_2` (UUID v4 mới tinh). Mã định danh 3D Mesh vẫn là `PARKING_G_CAR_018`. | **Ảnh hưởng = 0%**:<br>1. **Trên giao diện 3D:** Three.js ánh xạ vật lý theo `slot_code` (`PARKING_G_CAR_018`), do đó mô hình 3D vẫn gắn kết chính xác 100% vào vị trí không gian ban đầu.<br>2. **Độc lập lịch sử:** `GUID_1` đại diện cho kỷ nguyên vận hành 1; `GUID_2` đại diện cho kỷ nguyên vận hành 2. Không có sự pha trộn dữ liệu giữa 2 vòng đời.<br>3. **Độ an toàn UUID:** Xác suất trùng lặp của UUID v4 là $1 / 2^{122}$ (thực tế bằng 0). |
| **Giai đoạn 3: Sau năm 7**<br>*(Giả định Purge dữ liệu cũ > 7 năm)* | Dữ liệu giao dịch cũ > 7 năm đã hết hạn nghĩa vụ lưu trữ pháp lý theo `AUDIT_RETENTION` và được lưu trữ kho lạnh (cold archive) hoặc tiêu hủy hợp pháp. Row `GUID_1` bị xóa khỏi DB. | **Ảnh hưởng = 0%**:<br>Trách nhiệm pháp lý của dữ liệu lịch sử > 7 năm đã hoàn tất trọn vẹn vòng đời; các nghiệp vụ hiện tại vẫn liên kết ổn định với `GUID_2`. |

---

## 5. Đánh Giá Chốt: Có Cần Thêm Cột `status` Riêng Thay Vì Dùng Cột `deleted_at`?

Dưới đây là bảng phân tích so sánh khách quan giữa 2 phương án thiết kế cơ sở dữ liệu:

| Tiêu chí so sánh | Phương án A: Tiếp tục dùng `deleted_at` (Hiện tại) | Phương án B: Thêm cột `status` riêng (ví dụ `is_active` hoặc `slot_status`) |
| :--- | :--- | :--- |
| **1. Tính nhất quán toàn hệ thống** | **Chuẩn mực 100%**:<br>Tất cả 6 microservices của SmartPark (`parking_sites`, `spatial_units`, `parking_slots`, `vehicles`, `users`) đều dùng `deleted_at` để soft-delete theo chuẩn kiến trúc [`database-architecture.md` (Mục 5)](../../02-architecture/database/database-architecture.md#L54). | **Lệch chuẩn**:<br>Biến `parking_slots` thành thực thể dị biệt duy nhất trong hệ thống có thêm cờ trạng thái xóa riêng, gây khó khăn cho việc bảo trì chung. |
| **2. Ràng buộc duy nhất (Unique Index)** | Tận dụng partial unique index đã được PM phê duyệt:<br>1. `uq_site_slot_code_ci ON parking_slots(tenant_id, site_id, upper(slot_code)) WHERE deleted_at IS NULL;`<br>2. `uq_active_slot_code_ci ON parking_slots(spatial_unit_id, upper(slot_code)) WHERE deleted_at IS NULL;` | Bắt buộc phải drop index cũ, tạo lại index mới theo `WHERE status = 'ACTIVE'`. |
| **3. Trigger bảo vệ đặt chỗ tự động** | Hàm trigger database có sẵn [`guard_parking_structure_commitment()` (Dòng 52)](../../../scripts/database/05.6-Parking-Structure.sql#L52) đang kiểm tra trực tiếp: `WHERE id=target_slot AND deleted_at IS NULL`. | Phải sửa trigger database và sửa code C# của các service phụ thuộc (`ReservationService`, `OperationsService`). |
| **4. Rủi ro xung đột dữ liệu (Dual-State Conflict)** | **Không có rủi ro**:<br>Chỉ có 1 nguồn chân lý duy nhất để xác định slot có đang kinh doanh hay không (`deleted_at IS NULL`). | **Rủi ro rất cao (Brain-split)**:<br>Nếu `deleted_at IS NOT NULL` nhưng `status = 'ACTIVE'` (hoặc ngược lại) thì các câu query kiểm tra chỗ trống và báo cáo doanh thu sẽ bị sai lệch logic nghiêm trọng. |
| **5. Tác động Migration Database** | **Zero Migration**:<br>Sử dụng 100% cấu trúc bảng đã được đóng băng trong `02-facility-service-db.sql`. | **Phải chạy Migration**:<br>Vi phạm nguyên tắc đóng băng schema của giai đoạn hiện tại (*No Unrequested Schema Migrations*). |

---

## 6. Khuyến Nghị Chốt Gửi PM

1. **Giữ nguyên việc sử dụng cột `deleted_at` cho soft-delete:**
   * Đây là chuẩn mực xuyên suốt toàn bộ giải pháp kiến trúc của SmartPark.
   * `deleted_at` đã được bảo vệ chặt chẽ bởi Index nghiệp vụ và Database Trigger.
2. **Làm rõ ranh giới nghiệp vụ trong tài liệu vận hành:**
   * Các slot có `deleted_at IS NOT NULL` thuộc một bãi xe đang mở cửa được định nghĩa là: **Tài nguyên quy hoạch tạm đóng (Dormant/Unmanaged Capacity)**.
   * Chúng là Master Data vật lý và **hoàn toàn không bị xóa bởi các cron job dọn dẹp log định kỳ**.
3. **Cơ chế an toàn tuyệt đối:**
   * Ngay cả trong kịch bản giả định sau 7 năm dữ liệu bị purge sạch, cơ chế **In-Place Reconciliation (New Slot)** của API-03 sẽ tự động tái tạo lại slot với GUID mới dựa trên mô hình 3D mà không gây bất kỳ gián đoạn nào cho hệ thống.
