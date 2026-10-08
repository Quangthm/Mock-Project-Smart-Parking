# Parking Service — Database Documentation

> **Service**: Parking Service  
> **Database file**: [`02-parking-service-db.sql`](../../../../scripts/database/microservices/02-parking-service-db.sql)  
> **SRS baseline**: v0.9 (2026-10-05)  
> **Scope**: Quản lý cấu trúc vật lý của hệ sinh thái bãi đỗ xe: doanh nghiệp (`tenants`), bãi đỗ (`parking_sites`), đơn vị không gian phân cấp (`spatial_units`), chỗ đỗ vật lý (`parking_slots`), vật phẩm định danh ra/vào (`access_items`), cài đặt vận hành (`site_operational_settings`), biểu giá (`tariffs`), luồng đi (`parking_access_paths`), và sự cố vận hành (`incidents`).

---

## 1. Tổng quan kiến trúc

Parking Service là **service sở hữu toàn bộ cấu trúc vật lý và chính sách vận hành** của hệ thống SmartPark. Nó là nền tảng mà các service khác (Reservation, Payment, IoT) tham chiếu đến thông qua Logical ID.

```
Owner/Admin → API Gateway
                   ↓
          Parking Service DB
          ┌──────────────────────┐
          │ tenants              │ ← Doanh nghiệp chủ bãi
          │ parking_sites        │ ← Bãi đỗ xe vật lý
          │ spatial_units        │ ← Zone/Floor/Block phân cấp
          │ parking_slots        │ ← Chỗ đỗ vật lý (2 trạng thái)
          │ access_items         │ ← Thẻ, QR, biển số, RFID
          │ site_operational_settings │ ← Cấu hình chính sách
          │ tariffs              │ ← Biểu giá động
          │ parking_access_paths │ ← Luồng đi trong bãi
          │ incidents            │ ← Sự cố vận hành [Merged]
          └──────────────────────┘
```

**Nguyên tắc thiết kế cốt lõi:**
- **Hai chiều trạng thái slot**: `physical_state` và `reservation_state` là hai chiều độc lập, không bao giờ gộp thành một field duy nhất (§3.2.2).
- **Composite FK xuyên bảng**: Sử dụng `(id, tenant_id, site_id)` làm compound unique key, cho phép FK `(site_id, tenant_id)` đảm bảo dữ liệu con luôn thuộc đúng tenant cha — thay thế cho Row-Level Security.
- **Soft-delete toàn diện**: Tất cả entity vật lý đều có `deleted_at` để giữ audit trail khi cấu trúc bãi thay đổi.
- **Logical ID với service khác**: `access_items.current_session_id` trỏ về Reservation Service; `operator_grants.account_id` trỏ về User Service — không có FK vật lý xuyên database.

---

## 2. Bảng `tenants` — Doanh nghiệp chủ bãi xe

### 2.1 Mục đích nghiệp vụ

Lưu trữ thông tin của **đơn vị kinh doanh sở hữu và vận hành một hoặc nhiều bãi đỗ xe**. `tenant_id` là unit phân cấp cao nhất trong mô hình multi-tenant của SmartPark — mọi bảng phía dưới đều mang `tenant_id` để phân biệt dữ liệu giữa các chủ bãi khác nhau. Owner registration (tại User Service) cuối cùng tạo ra một `tenant` record ở đây.

### 2.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-LOT-01** | R | Owner tạo/cập nhật/vô hiệu hóa parking lot profile; tenant là root entity của lot |
| **FR-AUTH-05** | C | Sau khi Admin approve owner_application, một tenant được tạo và tenant_id được gắn vào account của Owner |
| **FR-POL-07** | C | Policy hierarchy: Admin → Owner/Parking-Lot → Zone → Booking; tenant là scope Owner |
| **§3.2.1** | — | Owner quản lý parking lot profiles bao gồm name, address, GPS, capacity, rates, policies |
| **§3.2.4** | — | Multi-tenant policy hierarchy: Admin defaults/bounds → Owner override |
| **US-OW01** | US | "Là Owner, tôi muốn tạo và quản lý thông tin bãi đỗ xe của mình" |

### 2.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của tenant. Là **Logical ID** được lan truyền xuống tất cả các bảng con như `parking_sites`, `spatial_units`, `parking_slots`, v.v. và được lưu dưới dạng cross-service reference tại User Service (`accounts.tenant_id`) | Tự sinh bởi `uuid_generate_v4()` | FR-LOT-01; §3.2.4 |
| `code` | `VARCHAR(50) NOT NULL` | Mã định danh ngắn gọn của tenant (VD: `'SUNWAH_PARKING'`, `'VINCOM_CENTER'`). Dùng trong URL, log, và giao tiếp nội bộ. Unique theo partial index (chỉ unique khi `deleted_at IS NULL`) | Do Admin/hệ thống gán khi tạo tenant; sau khi set thì ít thay đổi | FR-LOT-01 |
| `name` | `VARCHAR(255) NOT NULL` | Tên thương mại đầy đủ của doanh nghiệp chủ bãi. Hiển thị trên giao diện Owner dashboard | Do Owner cung cấp khi đăng ký; có thể update sau | §3.2.1; FR-LOT-01 |
| `contact_email` | `VARCHAR(255) NOT NULL` | Email liên hệ chính thức của doanh nghiệp; dùng để nhận thông báo hệ thống (hóa đơn, cảnh báo, báo cáo) | Do Owner cung cấp; phải trùng với email trong `owner_applications` | FR-AUTH-05; §3.1.1 |
| `contact_phone` | `VARCHAR(20) NOT NULL` | Số điện thoại liên hệ chính thức của doanh nghiệp; dùng cho thông báo khẩn | Do Owner cung cấp | §3.1.1 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` | Trạng thái vận hành của tenant. `ACTIVE`: hoạt động bình thường. `INACTIVE`: tạm ngưng theo yêu cầu Owner. `SUSPENDED`: bị Admin đình chỉ do vi phạm | `'ACTIVE'` khi tạo; Admin có thể chuyển sang `SUSPENDED`; Owner có thể chuyển sang `INACTIVE` | FR-LOT-01; §3.7.3 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo tenant | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete: cho phép xóa logic tenant mà không mất dữ liệu lịch sử. Khi tenant bị xóa, toàn bộ sites/slots/settings được giữ nguyên trong DB nhưng không truy cập được | Application gán = `NOW()` thay vì DELETE vật lý | §5.1 (Data retention) |

### 2.4 Indexes

| Index | Cột | Loại | Mục đích |
|---|---|---|---|
| `uq_active_tenant_code` | `code WHERE deleted_at IS NULL` | UNIQUE (Partial) | Mỗi mã tenant phải unique trong số các tenant đang hoạt động; cho phép tái sử dụng code sau khi xóa |

---

## 3. Bảng `parking_sites` — Bãi đỗ xe vật lý

### 3.1 Mục đích nghiệp vụ

Đại diện cho **một địa điểm bãi đỗ xe cụ thể** với địa chỉ, tọa độ GPS và tổng sức chứa vật lý. Một tenant có thể sở hữu nhiều `parking_sites` (ví dụ: Vincom sở hữu bãi tại nhiều tòa nhà). Site là đơn vị phân cấp thứ hai và là scope của toàn bộ `spatial_units`, `slots`, `tariffs`, `settings`, và `incidents`.

### 3.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-LOT-01** | R | Owner tạo/cập nhật/vô hiệu hóa lot profile bao gồm name, address, GPS, capacity, rates, policies |
| **FR-LOT-04** | C | Khi capacity giảm ảnh hưởng reservations hiện tại: giữ entitlements, attempt reallocation, không tự động đẩy xe đang đỗ ra |
| **FR-CAP-02** | C | Hiển thị capacity dimensions theo vehicle category; tổng sức chứa bắt đầu từ `total_physical_capacity` |
| **FR-MAP-01** | R/A | Map/directions dùng GPS coordinates của site |
| **§3.2.1** | — | Owner quản lý lot profiles; tọa độ cho tìm kiếm theo địa lý |
| **§3.3.2** | — | Haversine formula dùng latitude/longitude để tìm bãi trong bán kính 5km |
| **US-OW01** | US | Owner tạo và quản lý bãi đỗ xe |
| **US-D03** | US | Driver tìm kiếm bãi đỗ xe gần mình |

### 3.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của bãi đỗ xe. Là **Logical ID** được lưu tại User Service (`accounts.site_id`), Reservation Service (`reservations.site_id`), Payment Service, và Notification Service | Tự sinh bởi `uuid_generate_v4()` | FR-LOT-01 |
| `tenant_id` | `UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT` | FK đến doanh nghiệp sở hữu bãi. `RESTRICT`: không cho xóa tenant khi còn site — bảo vệ tính toàn vẹn dữ liệu phân cấp | Gán bằng `tenants.id` của Owner khi tạo site | FR-LOT-01; §3.2.4 |
| `site_code` | `VARCHAR(50) NOT NULL` | Mã ngắn gọn định danh site trong phạm vi tenant (VD: `'TOWER_A'`, `'BASEMENT_B1'`). Unique theo compound `(tenant_id, site_code)` | Do Owner đặt khi tạo site | FR-LOT-01 |
| `name` | `VARCHAR(255) NOT NULL` | Tên hiển thị của bãi đỗ xe (VD: `'Bãi đỗ xe Tòa Tháp A'`). Xuất hiện trong kết quả tìm kiếm và bản đồ | Do Owner đặt | §3.2.1; §3.3.3 |
| `address` | `TEXT NOT NULL` | Địa chỉ văn bản đầy đủ của bãi đỗ; dùng để hiển thị cho Driver và tìm kiếm theo tên đường | Do Owner nhập | §3.2.1; §3.3.3 |
| `latitude` | `DECIMAL(10,8) NULL` | Vĩ độ GPS của bãi đỗ (độ chính xác ~1.1mm). Dùng cho Haversine distance calculation (§3.3.2) và hiển thị bản đồ | Do Owner nhập hoặc geocoding từ address; `NULL` nếu chưa có GPS | §3.3.2; FR-MAP-01 |
| `longitude` | `DECIMAL(11,8) NULL` | Kinh độ GPS của bãi đỗ. Dùng kết hợp với `latitude` cho tìm kiếm địa lý | Do Owner nhập hoặc geocoding; `NULL` nếu chưa có GPS | §3.3.2; FR-MAP-01 |
| `total_physical_capacity` | `INT NOT NULL DEFAULT 0` CHECK (`>= 0`) | Tổng số chỗ đỗ vật lý của toàn bãi (tất cả vehicle types). Là baseline để tính `Available Capacity` theo công thức §3.4.5: `Available = Total − Occupied − Protected − Pending Payment − Backup`. Không cho phép âm. | Do Owner khai báo khi tạo site; cập nhật khi thêm/bỏ slot vật lý | FR-CAP-02; §3.4.5 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'ACTIVE'` | Trạng thái vận hành của bãi. `ACTIVE`: mở bình thường. `INACTIVE`: đóng tạm thời theo Owner. `MAINTENANCE`: đang bảo trì. `SUSPENDED`: bị Admin đình chỉ. Khi site không `ACTIVE`, Reservation Service từ chối tạo reservation mới cho site đó | Owner cập nhật; Admin có thể `SUSPENDED` | FR-LOT-01; FR-LOT-04 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo site | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete: giữ lại lịch sử khi bãi đóng cửa vĩnh viễn | Application gán = `NOW()` | §5.1 |

### 3.4 Constraints & Indexes

| Tên | Định nghĩa | Ý nghĩa |
|---|---|---|
| chk_`status` | CHECK (`status` IN ('ACTIVE', 'INACTIVE', 'MAINTENANCE', 'SUSPENDED')) | Ràng buộc giá trị hợp lệ cho `status` |
| `UNIQUE (id, tenant_id)` | Composite unique | Cho phép FK `(site_id, tenant_id)` từ bảng con — đảm bảo site con luôn thuộc đúng tenant cha |
| `UNIQUE (tenant_id, site_code)` | Composite unique | Mỗi site_code phải unique trong phạm vi một tenant |
| `idx_parking_sites_tenant_id` | INDEX trên `tenant_id` | Liệt kê nhanh toàn bộ site của một tenant |
| `uq_active_site_code_ci` | `UNIQUE (tenant_id, upper(site_code)) WHERE deleted_at IS NULL` | Case-insensitive unique cho site_code trong tenant (active sites) |

---

## 4. Bảng `spatial_units` — Đơn vị không gian phân cấp

### 4.1 Mục đích nghiệp vụ

Mô hình hóa **cấu trúc phân cấp không gian bên trong một bãi đỗ xe**: Zone, Floor (tầng), Block (khu). Đây là cấu trúc "thư mục" để tổ chức `parking_slots` và hỗ trợ Zone Reservation mode (§3.4.1). Bảng tự tham chiếu qua `parent_id` để biểu diễn cây phân cấp tùy chiều sâu. Chuỗi `path` lưu đường dẫn từ root đến node hiện tại, tương tự path trong hệ thống tệp, giúp truy vấn toàn bộ cây hiệu quả.

### 4.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-LOT-02** | A | Maintain configurable floors, zones, slots và access paths; outdoor lots không cần floor |
| **FR-LOT-03** | A | Maintain slot/zone identifiers, vehicle-category capacities và map associations |
| **FR-RES-03** | C | Zone Reservation mode: Driver chọn zone, hệ thống ưu tiên cấp slot trong zone đó |
| **FR-CAP-02** | C | Hiển thị capacity dimensions theo vehicle category; spatial_unit có `max_capacity` để track |
| **FR-MAP-02** | R | 2D/3D visualization hiển thị cấu trúc phân cấp zone/floor/block |
| **§3.2.3** | — | System hiển thị 2D/3D diagram với cấu trúc phân cấp |
| **§3.4.1** | — | Zone Reservation: Driver reserve capacity trong một zone cụ thể |
| **US-OW01** | US | Owner thiết lập cấu trúc zone/floor cho bãi đỗ |

### 4.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của đơn vị không gian. Là **Logical ID** được dùng tại Reservation Service (`reservations.target_spatial_unit_id`, `monthly_passes.target_spatial_unit_id`) khi Driver chọn zone | Tự sinh bởi `uuid_generate_v4()` | FR-LOT-02; FR-RES-03 |
| `tenant_id` | `UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT` | FK đến tenant sở hữu. Mang tenant_id xuống để đảm bảo tính toàn vẹn phân cấp | Gán từ `tenants.id` | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID của bãi đỗ cha. Được validate qua FK compound `(site_id, tenant_id) → parking_sites(id, tenant_id)` | Gán bằng `parking_sites.id` | FR-LOT-02 |
| `parent_id` | `UUID NULL` | Self-referencing FK để xây dựng cây phân cấp: `NULL` = root unit (trực tiếp thuộc site). Không null = unit con của unit khác. Được validate qua FK compound `(parent_id, tenant_id, site_id)` đảm bảo parent và con cùng tenant và site | Gán bằng `spatial_units.id` của unit cha; `NULL` nếu là root | FR-LOT-02; §3.2.3 |
| `path` | `TEXT NOT NULL` | Chuỗi đường dẫn từ root đến node, phân cách bởi `/` (VD: `/uuid-zone-a/uuid-floor-1/uuid-block-c`). Dùng để: (1) Truy vấn toàn bộ descendant của một node mà không cần đệ quy; (2) Xác định vị trí tương đối giữa các unit | Sinh bởi Application khi INSERT: `parent.path + '/' + new_id`; cập nhật khi unit được di chuyển | §3.2.3; FR-MAP-02 |
| `unit_type` | `VARCHAR(50) NOT NULL` | Loại đơn vị không gian. `ZONE`: khu vực logic (VD: Khu A, Khu EV). `FLOOR`: tầng vật lý (VD: Tầng B1, B2). `BLOCK`: khu nhỏ hơn trong floor/zone. Outdoor lots chỉ cần `ZONE`, không cần `FLOOR` (FR-LOT-02) | Do Owner chọn khi tạo cấu trúc | FR-LOT-02; §3.2.3 |
| `name` | `VARCHAR(100) NOT NULL` | Tên hiển thị của unit (VD: `'Khu A'`, `'Tầng B2'`, `'Khu EV'`). Xuất hiện trên bản đồ 2D/3D | Do Owner đặt | §3.2.3; FR-MAP-02 |
| `max_capacity` | `INT NOT NULL DEFAULT 0` CHECK (`>= 0`) | Tổng số chỗ đỗ tối đa trong unit này (tổng tất cả vehicle types). Dùng để tính capacity available theo từng zone khi Reservation Service xử lý Zone Reservation | Do Owner khai báo; cập nhật khi thêm/bỏ slot | FR-CAP-02; §3.4.1 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo unit | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete: giữ lại cấu trúc cũ khi bãi được tái cơ cấu | Application gán = `NOW()` | §5.1 |

### 4.4 Constraints & Indexes

| Tên | Định nghĩa | Ý nghĩa |
|---|---|---|
| chk_`unit_type` | CHECK (`unit_type` IN ('ZONE', 'FLOOR', 'BLOCK')) | Ràng buộc giá trị hợp lệ cho `unit_type` |
| `UNIQUE (id, tenant_id, site_id)` | Composite unique | Cho phép FK compound từ bảng con (`parking_slots`, `parking_access_paths`) đảm bảo tính toàn vẹn phân cấp 3 cấp |
| FK `(site_id, tenant_id) → parking_sites(id, tenant_id)` | Composite FK | Unit phải thuộc một site đang tồn tại; tenant phải khớp |
| FK `(parent_id, tenant_id, site_id) → spatial_units(id, tenant_id, site_id)` | Self-referencing Composite FK | Parent unit phải cùng tenant và site với child |
| `idx_spatial_units_tenant_id` | INDEX | Lọc theo tenant |
| `idx_spatial_units_site_id` | INDEX | Lấy toàn bộ unit trong một site |
| `idx_spatial_units_parent_id` | INDEX | Lấy direct children của một unit |
| `uq_active_unit_name_ci` | `UNIQUE (site_id, parent_id, lower(name)) NULLS NOT DISTINCT WHERE deleted_at IS NULL` | Case-insensitive unique: không có 2 unit cùng tên dưới cùng một parent đang active |

---

## 5. Bảng `parking_slots` — Chỗ đỗ vật lý

### 5.1 Mục đích nghiệp vụ

Đại diện cho **một chỗ đỗ xe vật lý cụ thể** (ví dụ: ô A-01, ô B-15). Đây là đơn vị nhỏ nhất và quan trọng nhất trong cấu trúc bãi. Thiết kế đặc biệt với **hai chiều trạng thái độc lập** tuân thủ nghiêm ngặt §3.2.2:
- `physical_state`: phản ánh tình trạng vật lý thực tế (có xe hay không, có hỏng không).
- `reservation_state`: phản ánh trạng thái bảo lưu/đặt trước (được đặt chỗ, được bảo vệ, backup).

Hai chiều này **có thể coexist** — ví dụ: slot có thể vừa `OCCUPIED` (vật lý) vừa `PROTECTED` (bảo lưu cho reservation khác).

### 5.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-LOT-04** | C | Maintain service/maintenance states; record reasons; khi capacity giảm không tự động đẩy xe đang đỗ ra |
| **FR-CAP-03** | C | Enforce capacity/availability invariants theo §3.4.3–6 và BR-CAP-02/04 |
| **FR-CAP-05** | A | Link fulfilled reservation capacity với actual occupancy; preserve separate physical/protection states |
| **FR-INC-06** | C | Audited manual slot override chỉ cho authorized actor trong approved condition |
| **FR-OPS-03** | C | UNKNOWN resources bị loại khỏi automatic allocation/violation decisions cho đến khi có authoritative observation |
| **FR-VEH-04** | C | Validate vehicle/slot compatibility — `supported_vehicle_type` vs `vehicle_type` của phương tiện |
| **§3.2.2** | — | Định nghĩa đầy đủ các physical states và reservation/protection states; quy tắc coexistence |
| **§3.4.5** | — | `Available Capacity = Total − Occupied − Protected − Pending Payment − Backup` |
| **US-O03** | US | Operator giám sát trạng thái chỗ đỗ real-time |
| **US-D01** | US | Driver chọn slot/zone khi đặt chỗ (Specific Slot Reservation) |

### 5.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của chỗ đỗ. Là **Logical ID** quan trọng nhất của Parking Service: được tham chiếu tại Reservation Service (`slot_allocations.slot_id`, `reservations.target_slot_id`), IoT Service, và Incident records | Tự sinh bởi `uuid_generate_v4()` | §3.2.2; FR-LOT-03 |
| `tenant_id` | `UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT` | FK đến tenant; mang theo để enforce composite FK phân cấp | Gán từ `tenants.id` | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID của bãi cha, validated qua FK compound | Gán từ `parking_sites.id` | FR-LOT-03 |
| `spatial_unit_id` | `UUID NOT NULL` | Logical ID của zone/floor/block chứa slot này, validated qua FK compound `(spatial_unit_id, tenant_id, site_id) → spatial_units(id, tenant_id, site_id)` | Gán từ `spatial_units.id` | FR-LOT-02; §3.4.1 |
| `slot_code` | `VARCHAR(50) NOT NULL` | Mã định danh slot trong phạm vi spatial_unit (VD: `'A-01'`, `'B-15'`, `'EV-03'`). Hiển thị trên bản đồ 2D/3D và báo cáo | Do Owner đặt khi tạo cấu trúc bãi | FR-LOT-03; §3.2.3 |
| `slot_type` | `VARCHAR(50) NOT NULL DEFAULT 'STANDARD'` | Phân loại chức năng đặc biệt của slot. `STANDARD`: chỗ đỗ thông thường. `EV`: slot có trạm sạc điện (lưu ý: EV charging không phải MVP function). `DISABLED`: ưu tiên người khuyết tật. `VIP`: slot hạng cao | Do Owner định nghĩa | FR-LOT-03 |
| `features` | `JSONB NULL` | Metadata mở rộng dạng JSON cho các đặc tính bổ sung của slot (VD: `{"covered": true, "width_cm": 280, "cctv": true}`). Linh hoạt cho các yêu cầu tương lai mà không cần ALTER TABLE | Do Owner cấu hình; `NULL` nếu không có feature đặc biệt | FR-LOT-03 |
| `physical_state` | `VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE'` | **Chiều 1 — Trạng thái vật lý thực tế** của chỗ đỗ. `AVAILABLE`: có thể đỗ ngay. `OCCUPIED`: đang có xe. `UNKNOWN`: không xác định được (mất kết nối sensor, ambiguous LPR). `MAINTENANCE`: đang bảo trì. `UNAVAILABLE`: đóng/không dùng được. UNKNOWN được loại khỏi automatic allocation (FR-OPS-03) | Cập nhật bởi: IoT event (sensor/LPR), Operator manual override, entry/exit event từ Reservation Service. Manual override phải có audit (FR-INC-06) | §3.2.2; FR-LOT-04; FR-OPS-03 |
| `reservation_state` | `VARCHAR(50) NULL` | **Chiều 2 — Trạng thái bảo lưu/đặt trước** của chỗ đỗ. `NULL` = không có reservation/protection nào áp dụng (không cần `NOT_RESERVED` explicit — §3.2.2). `RESERVED`: đã được đặt bởi reservation đã thanh toán. `PROTECTED`: được giữ không cho walk-in (trong protection window). `BACKUP`: là backup capacity cho future reservations | Cập nhật bởi Reservation Service thông qua event/API khi reservation lifecycle thay đổi | §3.2.2; FR-CAP-05; FR-CAP-07 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo slot | Tự sinh bởi database | — |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối (thường là khi state thay đổi) | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete: khi một chỗ đỗ bị phá bỏ vật lý, giữ record để lịch sử allocation còn tham chiếu được | Application gán = `NOW()` | §5.1 |

### 5.4 Constraints & Indexes

| Tên | Định nghĩa | Ý nghĩa |
|---|---|---|
| chk_`slot_type` | CHECK (`slot_type` IN ('STANDARD', 'EV', 'DISABLED', 'VIP')) | Ràng buộc giá trị hợp lệ cho `slot_type` |
| chk_`physical_state` | CHECK (`physical_state` IN ('AVAILABLE', 'OCCUPIED', 'UNKNOWN', 'MAINTENANCE', 'UNAVAILABLE')) | Ràng buộc giá trị hợp lệ cho `physical_state` |
| chk_`reservation_state` | CHECK (`reservation_state` IN ('RESERVED', 'PROTECTED', 'BACKUP')) | Ràng buộc giá trị hợp lệ cho `reservation_state` |
| `UNIQUE (id, tenant_id, site_id)` | Composite unique | Cho phép FK compound từ bảng khác đảm bảo slot thuộc đúng tenant/site |
| `UNIQUE (tenant_id, site_id, slot_code)` | Composite unique | Mỗi slot_code phải unique trong phạm vi site |
| FK `(site_id, tenant_id) → parking_sites(id, tenant_id)` | Composite FK | Slot phải thuộc site đang tồn tại |
| FK `(spatial_unit_id, tenant_id, site_id) → spatial_units(id, tenant_id, site_id)` | Composite FK | Slot phải thuộc unit cùng tenant/site |
| `idx_parking_slots_tenant_id` | INDEX | Lọc theo tenant |
| `idx_parking_slots_site_id` | INDEX | Lấy toàn bộ slot trong site |
| `idx_parking_slots_spatial_unit_id` | INDEX | Lấy toàn bộ slot trong zone/floor (dùng khi Zone Reservation) |
| `uq_active_slot_code_ci` | `UNIQUE (spatial_unit_id, upper(slot_code)) WHERE deleted_at IS NULL` | Case-insensitive unique cho slot_code trong spatial_unit |

### 5.5 Ghi chú Cross-service — Cập nhật trạng thái

| Bên cập nhật | Trường thay đổi | Cơ chế |
|---|---|---|
| IoT Service | `physical_state` | Kafka event `SlotOccupancyChanged` → Parking Service consumer cập nhật |
| Reservation Service | `reservation_state` | Kafka event `ReservationConfirmed/Cancelled/Expired` → Parking Service cập nhật |
| Operator (manual) | `physical_state` | Trực tiếp qua Parking Service API với SLOT_OVERRIDE permission; audit ghi nhận |
| Reservation Service | `physical_state` (`OCCUPIED`) | Khi session entry được ghi nhận, Reservation Service publish `VehicleEntered` → Parking Service cập nhật |

---

## 5B. Bảng `slot_compatibilities` — Mapping Loại Xe Cho Chỗ Đỗ

### 5B.1 Mục đích nghiệp vụ

Bảng này tách bạch logic tương thích phương tiện khỏi `parking_slots`. Thay vì khai báo tĩnh một slot chỉ chứa được một loại xe (`supported_vehicle_type`), bảng này cho phép thiết lập quan hệ N:N giữa slot vật lý và `vehicle_type`. Thiết kế này cho phép tương lai hệ thống hỗ trợ infer (suy diễn) loại xe dựa trên kích thước slot (ví dụ slot to có thể chứa CAR, MOTORCYCLE).

### 5B.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-VEH-04** | C | Validate vehicle/slot compatibility — so khớp `vehicle_type` của phương tiện với danh sách tương thích của ô đỗ. |

### 5B.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh bản ghi mapping | Tự sinh | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID doanh nghiệp chủ bãi | Context | FR-LOT-01 |
| `site_id` | `UUID NOT NULL` | Logical ID bãi đỗ | Context | FR-LOT-03 |
| `slot_id` | `UUID NOT NULL` | Trỏ về `parking_slots` | Khởi tạo cấu trúc bãi | FR-LOT-03 |
| `vehicle_type` | `VARCHAR(50) NOT NULL` | Loại xe tương thích với slot này | Cấu hình | FR-VEH-04 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Giờ tạo | Database | — |

### 5B.4 Constraints & Indexes

| Tên | Định nghĩa | Ý nghĩa |
|---|---|---|
| chk_`vehicle_type` | CHECK (`vehicle_type` IN ('CAR', 'MOTORCYCLE')) | Ràng buộc giá trị hợp lệ cho `vehicle_type` |
| `uq_slot_compatibility` | `UNIQUE (tenant_id, site_id, slot_id, vehicle_type)` | Ngăn chặn việc khai báo lặp lại cùng một loại xe cho cùng một slot |
| `fk_compat_slot` | `FOREIGN KEY (slot_id, tenant_id, site_id) REFERENCES parking_slots(id, tenant_id, site_id) ON DELETE CASCADE` | Ràng buộc toàn vẹn: Xóa slot thì xóa mapping tương ứng |

---

## 6. Bảng `access_items` — Vật phẩm định danh ra/vào

### 6.1 Mục đích nghiệp vụ

Quản lý **phương tiện vật lý/kỹ thuật số** dùng để nhận diện xe ra/vào bãi: thẻ từ, mã QR, biển số xe, RFID. Một access item là "chìa khóa" liên kết giữa phương tiện vật lý và hệ thống quản lý. Bảng theo dõi trạng thái của từng item (đang dùng, đã mất, bị vô hiệu hóa) và item nào đang gắn với session đỗ xe nào.

### 6.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-GATE-01** | R/X | Vehicle identification và manual LPR/QR fallback: §3.4.11 và §6.2.1 |
| **FR-GATE-03** | C | Record actual entry event và parking session; supported non-plated categories dùng Operator-managed ticket reference |
| **FR-GATE-05** | C | Validate exit identity và payment clearance; record actual departure |
| **FR-GATE-06** | C | Stop automated checkout on plate/ticket/session identity mismatch |
| **FR-GATE-07** | C | Lost-ticket session lookup và audited manual handling |
| **FR-GATE-09** | C | Prevent duplicate open sessions; completed single-use tickets không thể mở session mới |
| **FR-LOT-05** | C | Operator manage và enable/disable lot devices trong assigned scope |
| **§3.4.11** | — | Vehicle identification qua LPR/QR khi xe đến cổng |
| **US-O01** | US | Operator check-in xe vào bãi |
| **US-O02** | US | Operator check-out xe ra bãi |

### 6.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của access item | Tự sinh bởi `uuid_generate_v4()` | FR-GATE-03 |
| `tenant_id` | `UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT` | FK đến tenant sở hữu item; đảm bảo item không dùng sai bãi | Gán từ `tenants.id` | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID của site mà item này được phát hành; validated qua FK compound | Gán từ `parking_sites.id` | FR-LOT-05 |
| `item_type` | `VARCHAR(50) NOT NULL` | Loại vật phẩm. `CARD`: thẻ từ/thẻ chip vật lý. `QR`: mã QR (in ra hoặc hiển thị trên app). `LICENSE_PLATE`: biển số xe nhận diện bằng LPR (§6.2). `RFID`: chip RFID không tiếp xúc | Do Operator/hệ thống xác định khi cấp item | §3.4.11; FR-GATE-01 |
| `identifier_code` | `VARCHAR(100) NOT NULL` | Mã định danh duy nhất của item: số thẻ, chuỗi QR data, biển số chuẩn hóa, hoặc RFID tag ID. Unique theo `(tenant_id, identifier_code)` — đảm bảo mỗi identifier chỉ xuất hiện một lần trong phạm vi tenant | Tùy `item_type`: thẻ → số serial; QR → UUID sinh khi issue; LICENSE_PLATE → normalized_plate; RFID → tag ID | FR-GATE-01; FR-GATE-06 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE'` | Trạng thái của item. `AVAILABLE`: chưa được cấp hoặc đã thu hồi. `IN_USE`: đang gắn với một session đỗ xe active. `LOST`: đã báo mất; không thể dùng để vào bãi. `DISABLED`: bị vô hiệu hóa bởi Operator | Mặc định `AVAILABLE`; `IN_USE` khi session bắt đầu; `AVAILABLE` lại khi session kết thúc; Operator cập nhật khi mất | FR-GATE-09; FR-GATE-07 |
| `current_session_id` | `UUID NULL` | **Cross-service Logical ID** trỏ đến `parking_sessions.id` trong Reservation & Session Service DB. `NULL` = item đang không gắn với session nào. Có giá trị = item đang được dùng trong session hiện tại. Dùng để nhanh chóng xác định xe nào đang trong bãi khi quét item | Gán bằng `parking_sessions.id` khi session tạo; reset về `NULL` khi session kết thúc; truyền qua API, không validate bằng FK vật lý | FR-GATE-03; FR-GATE-05; FR-GATE-09 |
| `issued_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm item được tạo/phát hành. Dùng cho audit | Tự sinh bởi database khi INSERT | FR-GATE-03 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối (thường là khi status thay đổi) | Application cập nhật tại mỗi UPDATE | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete: giữ lại item đã hủy để audit trail | Application gán = `NOW()` | §5.1 |

### 6.4 Constraints & Indexes

| Tên | Định nghĩa | Ý nghĩa |
|---|---|---|
| chk_`item_type` | CHECK (`item_type` IN ('CARD', 'QR', 'LICENSE_PLATE', 'RFID')) | Ràng buộc giá trị hợp lệ cho `item_type` |
| chk_`status` | CHECK (`status` IN ('AVAILABLE', 'IN_USE', 'LOST', 'DISABLED')) | Ràng buộc giá trị hợp lệ cho `status` |
| `UNIQUE (tenant_id, identifier_code)` | Composite unique | Đảm bảo không có hai item cùng mã định danh trong một tenant |
| FK `(site_id, tenant_id) → parking_sites(id, tenant_id)` | Composite FK | Item phải thuộc site đang tồn tại |
| `idx_access_items_site_id` | INDEX | Lấy toàn bộ item của một site |
| `idx_access_items_identifier_code` | INDEX | Tra cứu nhanh khi quét thẻ/QR/biển số tại cổng (hot path) |

---

## 7. Bảng `site_operational_settings` — Cài đặt vận hành bãi

### 7.1 Mục đích nghiệp vụ

Lưu trữ **bộ tham số cấu hình chính sách** cho từng site theo policy hierarchy: Admin định nghĩa default và bounds, Owner override tại lot scope. Đây là nguồn sự thật cho toàn bộ timing và behavior parameters của Reservation & Session workflow. Một bản ghi có thể áp dụng cho toàn tenant (`site_id IS NULL`) hoặc cho site cụ thể.

### 7.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-POL-01** | C | Owner configures applicable policies within Admin-defined defaults và permitted bounds |
| **FR-POL-02** | R | Admin system-policy configuration, bao gồm payment hold: §3.2.4, §3.4.3, §3.7.3 |
| **FR-POL-07** | C | Resolve applicable policy values theo hierarchy: Admin defaults/bounds → Owner/lot override |
| **FR-PAY-09** | C | Free-parking duration inclusive: billable ≤ free_parking_grace → session free |
| **§3.4.3** | — | Payment hold duration: Admin default 5 phút; Owner override trong bounds |
| **§3.4.4** | — | Reservation Protection Window: N hours trước start; Owner configures |
| **§3.4.6** | — | Allocation Lead Time: N minutes trước start; Owner configures |
| **§3.4.5** | — | Occupancy Validity Timespan: 4 giờ (default) |
| **§3.4.9** | — | Danh sách tất cả configurable parameters: min/max reservation duration, hold, protection, lead time, backup, occupancy validity |
| **US-OW02** | US | Owner cấu hình chính sách bãi đỗ |

### 7.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh bản ghi cấu hình | Tự sinh bởi `uuid_generate_v4()` | — |
| `tenant_id` | `UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE` | FK đến tenant; `CASCADE` vì settings là metadata của tenant | Gán từ `tenants.id` | §3.2.4 |
| `site_id` | `UUID NULL` | Logical ID của site áp dụng settings. `NULL` = settings áp dụng cho toàn bộ sites của tenant (tenant-level default). Khác `NULL` = override cho site cụ thể | Gán từ `parking_sites.id` hoặc `NULL`; validated qua FK compound `(site_id, tenant_id)` | FR-POL-07; §3.2.4 |
| `free_parking_grace_minutes` | `INT NOT NULL DEFAULT 15` | Thời gian đỗ xe miễn phí (phút). Theo FR-PAY-09: nếu thời gian tính phí ≤ giá trị này → session free. Giá trị mặc định 15 phút | Do Admin set default; Owner override trong bounds | FR-PAY-09; §3.5.4 |
| `arrival_late_tolerance_minutes` | `INT NOT NULL DEFAULT 15` | Thời gian grace period cho Driver đến trễ sau giờ reservation start (phút). Nếu Owner bật chính sách này: sau `start + tolerance` mà không có authoritative arrival → `NO_SHOW` | Do Owner configure (optional policy); Admin set default | §3.4.10 |
| `payment_hold_duration_minutes` | `INT NOT NULL DEFAULT 5` | Thời gian giữ capacity trong trạng thái `PENDING_PAYMENT` (phút). Admin default **5 phút**. Trong thời gian này, capacity được hold để không có competing claim nào lấy mất | Admin default 5 phút; Owner có thể override trong Admin-defined bounds | §3.4.3; FR-POL-02 |
| `min_reservation_duration_minutes` | `INT NOT NULL DEFAULT 30` | Thời gian tối thiểu của một reservation (phút). Driver không thể tạo reservation ngắn hơn giá trị này | Owner configure; Admin set default | §3.4.9 |
| `max_reservation_duration_hours` | `INT NOT NULL DEFAULT 24` | Thời gian tối đa của một reservation (giờ). Driver không thể tạo reservation dài hơn giá trị này | Owner configure; Admin set default | §3.4.9 |
| `reservation_protection_window_hours` | `INT NOT NULL DEFAULT 4` | Số giờ trước reservation start time mà hệ thống bắt đầu bảo vệ capacity/slot khỏi walk-in. Ví dụ: reservation 09:00, window 4h → protection bắt đầu từ 05:00 | Owner configure; Admin set default và bounds | §3.4.4; FR-CAP-07 |
| `allocation_lead_time_minutes` | `INT NOT NULL DEFAULT 30` | Số phút trước reservation start mà Reservation Service bắt đầu allocation processing. Ví dụ: start 09:30, lead time 30' → allocation time 09:00 | Owner configure; Admin set default và bounds | §3.4.6; §3.4.9 |
| `occupancy_validity_timespan_hours` | `INT NOT NULL DEFAULT 4` | Thời gian (giờ) mà một OCCUPIED slot được tính là sẽ tiếp tục occupied để tránh overbook. Ví dụ: xe đỗ lúc 10:00, timespan 4h → projected occupied đến 14:00 trừ khi có observation mới | Admin set; có thể Owner override | §3.4.5; §3.4.9 |
| `version` | `INT NOT NULL DEFAULT 1` | Version number của settings record; tăng 1 mỗi khi settings được cập nhật. Dùng để track revision history và đảm bảo booking dùng đúng version pricing/policy (FR-POL-05) | Tự tăng bởi Application tại mỗi UPDATE | FR-POL-05; FR-POL-08 |
| `created_by` | `UUID NULL` | **Cross-service Logical ID** trỏ đến `users.id` trong User Service — admin/owner đã tạo settings này. `NULL` nếu được tạo tự động bởi hệ thống | Gán từ `users.id` của người dùng đang đăng nhập | §3.7.3 (audit) |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo settings | Tự sinh bởi database | FR-POL-08 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | FR-POL-05 |

### 7.4 Indexes

| Index | Cột | Mục đích |
|---|---|---|
| `idx_site_settings_site_id` | `site_id` | Tra cứu nhanh settings của một site cụ thể |

---

## 8. Bảng `tariffs` — Biểu giá động

### 8.1 Mục đích nghiệp vụ

Lưu trữ **cấu hình biểu giá** của từng site, bao gồm quy tắc tính phí theo khung giờ, loại xe và các điều kiện đặc biệt. Mỗi tariff là một version có thể kích hoạt/tắt. Theo FR-PAY-10, việc tính phí dùng thời gian thực tế (local timestamps) và tính overlap với từng time block, sum trước khi làm tròn một lần. `tariff_rules` là JSONB linh hoạt để biểu diễn các quy tắc phức tạp mà không cần thêm bảng phụ.

### 8.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-PAY-01** | R/X | Owner tariff configuration: §3.2.1/§3.7.2 |
| **FR-PAY-09** | C | Free-parking duration inclusive |
| **FR-PAY-10** | C | Time-based pricing từ actual local timestamps; intervals crossing midnight split; sum trước khi round |
| **FR-PAY-11** | C | Final price từ actual recorded entry và exit timestamps |
| **FR-POL-05** | C | Record policy revisions và version applied to accepted booking/transaction |
| **FR-BAS-05** | C | Dynamic pricing là MVP capability dùng Owner-configured time blocks |
| **§3.5.1** | — | Parking fee per minute/hour; free-parking grace; time-block pricing với inclusive grace |
| **§3.5.2** | — | Dynamic pricing qua Owner-configured time blocks; event/holiday triggers là Future |
| **US-OW02** | US | Owner cập nhật pricing real-time |

### 8.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh tariff record | Tự sinh bởi `uuid_generate_v4()` | — |
| `tenant_id` | `UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT` | FK đến tenant sở hữu tariff; `RESTRICT` để không mất lịch sử pricing | Gán từ `tenants.id` | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID của site áp dụng tariff; validated qua FK compound | Gán từ `parking_sites.id` | FR-PAY-01 |
| `name` | `VARCHAR(100) NOT NULL` | Tên gợi nhớ của biểu giá (VD: `'Biểu giá thường ngày'`, `'Biểu giá cuối tuần'`, `'Biểu giá đêm'`). Hiển thị cho Owner khi quản lý | Do Owner đặt | §3.7.2 |
| `vehicle_type` | `VARCHAR(50) NOT NULL DEFAULT 'CAR'` | Loại xe mà tariff này áp dụng. Mỗi vehicle type có thể có biểu giá khác nhau; Payment Service dùng `vehicle_type` của session để chọn đúng tariff | Do Owner chỉ định | §3.5.1; FR-CAP-02 |
| `version` | `INT NOT NULL DEFAULT 1` | Version number của tariff; tăng 1 mỗi lần cập nhật rules. Đảm bảo reservation đã confirmed dùng đúng version pricing tại thời điểm booking (FR-POL-05) | Tự tăng bởi Application tại mỗi UPDATE | FR-POL-05 |
| `tariff_rules` | `JSONB NOT NULL` | **Bộ quy tắc tính phí dưới dạng JSON**. Cấu trúc tiêu biểu: mảng time blocks, mỗi block có `from_time`, `to_time`, `price_per_unit`, `unit_minutes`, `day_of_week`. Ví dụ: `[{"from":"06:00","to":"22:00","price":5000,"unit":60,"days":["MON","TUE",...]}]`. Payment Service đọc JSON này để tính overlap với parking session | Do Owner cấu hình qua UI; validate bởi Application (FR-POL-04) trước khi lưu: đảm bảo exhaustive, non-overlapping, valid time ranges | §3.5.1; §3.5.2; FR-PAY-10; FR-POL-04 |
| `is_active` | `BOOLEAN NOT NULL DEFAULT true` | Cờ kích hoạt tariff. `true` = tariff đang được áp dụng cho site. `false` = draft hoặc đã bị thay thế. Một site có thể có nhiều tariff nhưng chỉ một số là active (phân biệt theo vehicle_type) | Owner toggle on/off; hệ thống tự deactivate version cũ khi activate version mới | FR-POL-08; FR-PAY-01 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo tariff record | Tự sinh bởi database | FR-POL-05 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | FR-POL-05 |

### 8.4 Indexes

| Index | Cột | Mục đích |
|---|---|---|
| `idx_tariffs_site_id` | `site_id` | Tra cứu nhanh toàn bộ tariff của một site |

### 8.5 Ghi chú Cross-service

Payment Service **không** truy cập trực tiếp bảng `tariffs`. Luồng:
1. Khi session checkout, Payment Service gọi Parking Service API để lấy `tariff_rules` active tại thời điểm entry.
2. Parking Service trả về snapshot `tariff_rules` + `version` của tariff applicable.
3. Payment Service tính phí dựa trên snapshot đó (đảm bảo pricing không bị ảnh hưởng bởi Owner update tariff giữa chừng).

---

## 9. Bảng `parking_access_paths` — Luồng đi trong bãi

### 9.1 Mục đích nghiệp vụ

Định nghĩa **các tuyến đường đi được** giữa các đơn vị không gian trong bãi đỗ xe: từ cổng vào đến zone A, từ zone A đến zone B, v.v. Thông tin này dùng để hỗ trợ điều hướng cho Driver (FR-MAP-02), và để Reservation Service tính "distance" theo cấu trúc bãi khi xếp hạng candidate slots trong allocation (§3.4.6). `map_data` lưu thông tin tọa độ hoặc render data cho bản đồ 2D/3D.

### 9.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-MAP-02** | R | 2D/3D state visualization; show requested và actual allocated positions |
| **FR-MAP-03** | C | Show vehicle location chỉ theo precision thực tế ghi nhận |
| **FR-LOT-02** | A | Maintain configurable floors, zones, slots và access paths |
| **§3.4.6** | — | Allocation ranking dùng configured layout coordinates/deterministic distance |
| **US-D16** | US | Driver xem bản đồ bãi đỗ xe và điều hướng đến slot |

### 9.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của tuyến đường | Tự sinh bởi `uuid_generate_v4()` | — |
| `tenant_id` | `UUID NOT NULL` | Logical ID của tenant; part of composite unique | Gán từ `tenants.id` | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID của site chứa tuyến đường; validated qua FK compound `(site_id, tenant_id) → parking_sites(id, tenant_id)` | Gán từ `parking_sites.id` | FR-LOT-02 |
| `path_code` | `VARCHAR(50) NOT NULL` | Mã định danh tuyến đường (VD: `'GATE1-TO-ZONE-A'`, `'ZONE-A-TO-ZONE-B'`). Unique per site theo partial index case-insensitive | Do Owner đặt | FR-LOT-02 |
| `from_unit_id` | `UUID NULL` | Logical ID của spatial_unit điểm xuất phát. `NULL` = xuất phát từ ngoài (cổng vào bãi); validated qua FK compound khi không NULL | Gán từ `spatial_units.id`; `NULL` cho điểm xuất phát là cổng | §3.4.6; FR-MAP-02 |
| `to_unit_id` | `UUID NULL` | Logical ID của spatial_unit điểm đến. `NULL` = đến ra ngoài (cổng ra); validated qua FK compound khi không NULL. CHECK đảm bảo `from_unit_id <> to_unit_id` khi cả hai đều có giá trị | Gán từ `spatial_units.id`; `NULL` cho điểm đến là cổng | §3.4.6; FR-MAP-02 |
| `map_data` | `JSONB NULL` | Metadata dùng để render tuyến đường lên bản đồ 2D/3D. Cấu trúc tùy thuộc engine render; có thể gồm: `{"waypoints": [...], "distance_m": 120, "estimated_walk_s": 90}`. Reservation Service dùng `distance_m` làm input cho allocation ranking | Do Owner cấu hình qua layout editor; `NULL` nếu chưa có map data | §3.4.6; FR-MAP-02 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm tạo tuyến đường | Tự sinh bởi database | — |
| `deleted_at` | `TIMESTAMPTZ NULL` | Soft-delete: giữ lịch sử khi cấu trúc bãi thay đổi | Application gán = `NOW()` | §5.1 |

### 9.4 Constraints & Indexes

| Tên | Định nghĩa | Ý nghĩa |
|---|---|---|
| `UNIQUE (id, tenant_id, site_id)` | Composite unique | Cho phép FK compound tham chiếu |
| `CHECK (from_unit_id IS NULL OR to_unit_id IS NULL OR from_unit_id <> to_unit_id)` | CHECK | Tuyến đường không thể bắt đầu và kết thúc tại cùng một unit |
| `uq_active_access_path_code` | `UNIQUE (site_id, upper(path_code)) WHERE deleted_at IS NULL` | Case-insensitive unique cho path_code trong site (active paths) |
| `idx_access_paths_site` | INDEX trên `site_id` | Lấy toàn bộ tuyến đường trong một site |

---

## 10. Bảng `incidents` — Sự cố vận hành bãi

> **[Merged từ `08-incident-service-db.sql`]** — Đây là bảng được sáp nhập vào Parking Service vì tất cả loại sự cố (`LOST_TICKET`, `UNKNOWN_SLOT_STATE`, `CHECKOUT_MISMATCH`) đều gắn liền với trạng thái vật lý và quy trình vận hành của bãi đỗ, và workflow xử lý (OPEN → IN_PROGRESS → RESOLVED) là trách nhiệm của Operator thuộc Parking Service domain.

### 10.1 Mục đích nghiệp vụ

Ghi nhận và theo dõi **sự cố vận hành bãi đỗ** từ khi phát sinh đến khi giải quyết. Workflow: Operator báo cáo sự cố → ghi nhận bằng chứng → phân công xử lý → ghi nhận kết quả. Sự cố có thể ảnh hưởng đến Payment (khi `LOST_TICKET` cần recover session, có thể sinh charge), nhưng bản thân việc ghi nhận và xử lý sự cố thuộc Parking domain.

### 10.2 Traceability Mapping

| Mã yêu cầu | Loại | Nội dung liên quan |
|---|---|---|
| **FR-INC-01** | C | Record incident, affected lot/slot/device/session, observation time, evidence, assigned handler và outcome |
| **FR-INC-03** | C | Operator manually records suspected wrong-slot parking với evidence |
| **FR-INC-04** | R/A | Human verification của alleged violations trước khi hành động |
| **FR-INC-05** | C | Accept appeal flow: appeal → evidence → Operator approval → refund |
| **FR-INC-06** | C | Audited manual slot override chỉ cho authorized actor |
| **FR-INC-07** | C | Hiển thị own appeal/refund status cho Driver; không expose internal evidence cho customer |
| **FR-GATE-06** | C | Stop automated checkout on identity mismatch, alert Operator |
| **FR-GATE-07** | C | Lost-ticket session lookup và audited manual handling |
| **FR-OPS-06** | C | On service failure, record local incident/reconciliation procedure |
| **§3.7.6** | — | Operational, Incident và Gate Workflows |
| **BR-VIOL-01–04** | — | Incident/appeals: Operator approval, Owner escalation |
| **US-O05** | US | Operator ghi nhận và xử lý sự cố tại bãi |
| **US-D07** | US | Driver xem trạng thái khiếu nại của mình |

### 10.3 Bảng thuộc tính chi tiết

| Tên Cột | Kiểu & Ràng buộc | Ý nghĩa nghiệp vụ | Nguồn gốc dữ liệu / Cách sinh | Mapping SRS |
|---|---|---|---|---|
| `id` | `UUID PK DEFAULT uuid_generate_v4()` | Định danh duy nhất của sự cố | Tự sinh bởi `uuid_generate_v4()` | FR-INC-01 |
| `tenant_id` | `UUID NOT NULL` | Logical ID của tenant; part of composite unique; đảm bảo incident scope đúng tenant | Gán từ `tenants.id` của site xảy ra sự cố | §3.2.4 |
| `site_id` | `UUID NOT NULL` | Logical ID của site xảy ra sự cố; cross-service reference (Parking Service validates nội bộ) | Gán từ `parking_sites.id` | FR-INC-01 |
| `incident_type` | `VARCHAR(50) NOT NULL` | Phân loại sự cố. `LOST_TICKET`: Driver mất vé/thẻ → cần recover session bằng evidence (FR-GATE-07). `UNKNOWN_SLOT_STATE`: slot ở trạng thái UNKNOWN không thể resolve tự động (FR-OPS-03). `CHECKOUT_MISMATCH`: identity/plate mismatch khi checkout (FR-GATE-06). `OTHER`: sự cố vận hành khác | Do Operator chọn khi tạo incident | FR-INC-01; FR-GATE-06; FR-GATE-07; FR-OPS-03 |
| `reference_id` | `UUID NULL` | **Cross-service Logical ID** tham chiếu đến entity liên quan đến sự cố. Giá trị cụ thể tùy `incident_type`: `LOST_TICKET` → `parking_sessions.id`; `UNKNOWN_SLOT_STATE` → `parking_slots.id`; `CHECKOUT_MISMATCH` → `parking_sessions.id`; `OTHER` → tùy. `NULL` khi không có entity cụ thể | Gán bởi Operator hoặc hệ thống tùy context | FR-INC-01; FR-INC-03 |
| `description` | `TEXT NULL` | Mô tả chi tiết sự cố do Operator nhập. Bao gồm quan sát thực tế, bối cảnh, và thông tin liên quan | Do Operator nhập khi tạo incident | FR-INC-01; FR-INC-03 |
| `evidence_url` | `VARCHAR(500) NULL` | URL dẫn đến bằng chứng (ảnh chụp, video clip). Hỗ trợ quá trình điều tra (FR-INC-04). Ảnh/video được lưu trên storage riêng; bảng chỉ lưu URL | Do Operator upload ảnh/video, Application lưu URL vào đây | FR-INC-04; FR-INC-07 |
| `reported_by` | `UUID NULL` | **Cross-service Logical ID** trỏ đến `users.id` (User Service) của người báo cáo sự cố — thường là Operator, đôi khi là system account | Gán bằng `users.id` của Operator đang đăng nhập | FR-INC-01 |
| `assigned_to` | `UUID NULL` | **Cross-service Logical ID** trỏ đến `users.id` (User Service) của người được phân công xử lý sự cố — thường là Operator khác hoặc Owner. `NULL` = chưa phân công | Gán bởi hệ thống hoặc Owner khi phân công; `NULL` ban đầu | FR-INC-01; §3.7.5 |
| `status` | `VARCHAR(50) NOT NULL DEFAULT 'OPEN'` | Trạng thái xử lý sự cố. `OPEN`: mới tạo, chưa có người xử lý. `IN_PROGRESS`: đang được xử lý. `RESOLVED`: đã giải quyết, chờ đóng. `CLOSED`: hoàn tất, lưu trữ | `'OPEN'` khi tạo; Operator/Owner cập nhật theo tiến trình | FR-INC-01; FR-INC-07 |
| `resolved_at` | `TIMESTAMPTZ NULL` | Thời điểm sự cố được resolve. `NULL` khi status là `OPEN` hoặc `IN_PROGRESS`. Có giá trị khi `RESOLVED` hoặc `CLOSED` (enforce bởi CHECK constraint) | Gán = `NOW()` khi Operator mark resolved | FR-INC-01 |
| `resolution_notes` | `TEXT NULL` | Ghi chú kết quả xử lý: giải pháp đã áp dụng, lý do quyết định, tham chiếu đến action đã thực hiện (VD: "Recover session bằng entry image, tính phí bình thường") | Do Operator/Owner nhập khi resolve | FR-INC-01; FR-INC-04 |
| `created_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm báo cáo sự cố | Tự sinh bởi database | FR-INC-01 |
| `updated_at` | `TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP` | Thời điểm cập nhật lần cuối | Application cập nhật tại mỗi UPDATE | — |

**Constraint**:
```sql
CONSTRAINT chk_incident_resolved_state CHECK (
    (status IN ('OPEN', 'IN_PROGRESS') AND resolved_at IS NULL) OR
    (status IN ('RESOLVED', 'CLOSED')  AND resolved_at IS NOT NULL)
)
```
Đảm bảo tính nhất quán: sự cố đang xử lý không có `resolved_at`; sự cố đã xử lý bắt buộc phải có timestamp.

### 10.4 Constraints & Indexes

| Tên | Định nghĩa | Ý nghĩa |
|---|---|---|
| chk_`incident_type` | CHECK (`incident_type` IN ('LOST_TICKET', 'UNKNOWN_SLOT_STATE', 'CHECKOUT_MISMATCH', 'OTHER')) | Ràng buộc giá trị hợp lệ cho `incident_type` |
| chk_`status` | CHECK (`status` IN ('OPEN', 'IN_PROGRESS', 'RESOLVED', 'CLOSED')) | Ràng buộc giá trị hợp lệ cho `status` |
| `UNIQUE (id, tenant_id, site_id)` | Composite unique | Cho phép FK compound tham chiếu từ bảng khác |
| `idx_incidents_tenant_site` | INDEX trên `(tenant_id, site_id)` | Lọc nhanh incidents theo bãi |
| `idx_incidents_status` | INDEX trên `status` | Dashboard Operator filter incidents theo status (OPEN, IN_PROGRESS) |

---

## 11. Tóm tắt quan hệ giữa các bảng

```
tenants (1) ──── (N) parking_sites
tenants (1) ──── (N) site_operational_settings  [tenant-level settings]

parking_sites (1) ──── (N) spatial_units
parking_sites (1) ──── (N) parking_slots         [qua spatial_units]
parking_sites (1) ──── (N) access_items
parking_sites (1) ──── (1) site_operational_settings  [site-level override]
parking_sites (1) ──── (N) tariffs
parking_sites (1) ──── (N) parking_access_paths
parking_sites (1) ──── (N) incidents

spatial_units (1) ──── (N) parking_slots
parking_slots (1) ──── (N) slot_compatibilities
spatial_units (tree) ── parent_id → spatial_units.id  [self-referencing]

parking_access_paths.from_unit_id ──── spatial_units.id  [nullable]
parking_access_paths.to_unit_id   ──── spatial_units.id  [nullable]
```

---

## 12. Tóm tắt Cross-service References

| Cột | Bảng | Trỏ đến Service | Cơ chế đồng bộ |
|---|---|---|---|
| `parking_slots.id` (published) | — | Reservation Service (`slot_allocations.slot_id`, `reservations.target_slot_id`) | Logical ID; Reservation Service gọi Parking Service API để validate slot trước reservation |
| `spatial_units.id` (published) | — | Reservation Service (`reservations.target_spatial_unit_id`) | Logical ID; validate qua API |
| `access_items.current_session_id` | `access_items` | Reservation & Session Service (`parking_sessions.id`) | Gán/xóa qua API call khi session start/end |
| `site_operational_settings.created_by` | `site_operational_settings` | User Service (`users.id`) | Logical ID; audit only |
| `incidents.reported_by` | `incidents` | User Service (`users.id`) | Logical ID; User Service không biết về incident |
| `incidents.assigned_to` | `incidents` | User Service (`users.id`) | Logical ID; resolve khi cần display name |
| `incidents.reference_id` | `incidents` | Reservation Service hoặc Parking Service | Logical ID; context phụ thuộc `incident_type` |
| `parking_sites.id` (published) | — | User Service (`accounts.site_id`) | Parking Service publish `SiteCreated`; User Service consume để validate |
| `tenants.id` (published) | — | User Service (`accounts.tenant_id`) | Parking Service publish `TenantCreated`; User Service consume để validate |

---

## 13. Extensions PostgreSQL sử dụng

| Extension | Lý do sử dụng |
|---|---|
| `uuid-ossp` | Cung cấp hàm `uuid_generate_v4()` để sinh UUID v4 làm primary key cho tất cả bảng |
