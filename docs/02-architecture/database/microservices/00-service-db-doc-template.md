# [Tên Service] — Database Documentation

> **Service**: [Tên Service, VD: User Service]  
> **Database file**: [`[tên-file-db.sql]`](../../../../scripts/database/microservices/[tên-file-db.sql])  
> **SRS baseline**: [Phiên bản SRS, VD: v0.9 (2026-10-05)]  
> **Scope**: [Mô tả ngắn gọn phạm vi và chức năng chính của database này, liệt kê các bảng chính]

---

## 1. Tổng quan kiến trúc

[Mô tả tổng quan về vai trò của database này trong toàn bộ hệ thống SmartPark.]

```text
[Sơ đồ luồng dữ liệu / kiến trúc dạng ASCII Art]
Ví dụ:
Client → API Gateway
                 ↓
          [Tên Service] DB
          ┌───────────────┐
          │ table_1       │ ← [Mô tả ngắn]
          │ table_2       │ ← [Mô tả ngắn]
          └───────────────┘
```

**Nguyên tắc thiết kế cốt lõi:**
- [Nguyên tắc 1: VD: Không có FK vật lý xuyên database, dùng Logical ID]
- [Nguyên tắc 2: VD: Soft-delete toàn diện]
- [Nguyên tắc 3: ...]

---

## 2. Bảng `[table_1_name]` — [Mô tả ngắn gọn về bảng]

### 2.1 Mục đích nghiệp vụ

[Giải thích ý nghĩa nghiệp vụ của bảng này. Bảng này giải quyết bài toán gì? Lưu trữ đối tượng nào? Quan hệ chính với các bảng khác là gì?]

### 2.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **[Mã FR/BR/US]** | [R/C/A/US] | [Tóm tắt nội dung yêu cầu nghiệp vụ liên quan đến bảng này] |

### 2.3 Bảng thuộc tính chi tiết

*(Lưu ý: Quét 100% các cột có trong DDL, không bỏ sót. Không gộp chung Constraint/Index vào đây)*

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT...` | Định danh duy nhất của... | Tự sinh bởi database | — |
| `[column_name]` | `[TYPE] [NULL/NOT NULL] [DEFAULT]` | [Giải thích business logic] | [Ai/Luồng nào ghi data này?] | [Mã yêu cầu] |

### 2.4 Constraints & Indexes

*(Lưu ý: Bắt buộc dùng format bảng markdown dưới đây cho toàn bộ UNIQUE, CHECK, FK và INDEX)*

| Tên / Index | Định nghĩa / Cột | Ý nghĩa |
|---|---|---|
| `uq_[name]` / `FK_...` | `UNIQUE (...)` / `REFERENCES ...` | [Giải thích mục đích của ràng buộc] |
| `idx_[name]` | `[cột được index]` | [Tra cứu nhanh cho luồng nào?] |

### 2.5 Ghi chú Cross-service (Tùy chọn)

[Chỉ thêm mục này nếu bảng có logic liên kết phức tạp với các service khác qua Kafka events hoặc API Gateway. Nếu không có, bỏ qua mục này.]

---

*(Lặp lại cấu trúc mục 2 cho các bảng tiếp theo: `3. Bảng [table_2]`, `4. Bảng [table_3]`, v.v.)*

---

## [N-2]. Tóm tắt quan hệ giữa các bảng

*(Lưu ý: Bắt buộc phải có sơ đồ ERD dạng ASCII Art thể hiện quan hệ 1-N, 1-1 giữa các bảng trong nội bộ Service)*

```text
[Bảng cha] (1) ──── (N) [Bảng con 1]
[Bảng cha] (1) ──── (0..1) [Bảng con 2]
[Bảng con 1] (N) ──── (N) [Bảng khác]  [qua bảng trung gian]
```

---

## [N-1]. Tóm tắt Cross-service References

*(Tổng hợp toàn bộ các Logical IDs tham chiếu đến các Service khác)*

| Cột | Bảng hiện tại | Trỏ đến Service (Bảng tham chiếu) | Cơ chế đồng bộ |
|---|---|---|---|
| `[column_id]` | `[table_name]` | [Tên Service Đích] (`[table_đích.id]`) | [Cách lấy/validate dữ liệu: API call lúc tạo, Kafka event, hay Duplicate Cache?] |

---

## [N]. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Cung cấp hàm `uuid_generate_v4()` để sinh UUID v4 làm primary key. |
| `[extension_khác]` | [Lý do, VD: `btree_gist` dùng cho constraint EXCLUDE chống đè lịch] |
