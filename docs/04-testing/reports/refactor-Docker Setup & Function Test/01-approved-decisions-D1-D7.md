# 01 — Quyết định đã phê ở cổng T4: D1–D7 và Q1–Q6

## Meta

| Hạt | Giá trị |
|---|---|
| Ngày | 2026-10-18 |
| Branch | `feature/db-hybrid-srs-v1` |
| Base commit | `0c8a6fb` (= `develop`, full hash `0c8a6fb119ac0bd1c656f4dbdd63bef999b8a939`) |
| Nguồn SQL chuẩn (arch) | worktree A `D:\My file\FA26 - OJT FSoft\OJT\Mock-Project-Smart-Parking` @ `arch/database-schema` commit `9a13499` (full hash `9a1349974f632304857ca4034b9e358232efcbde`, verified `git rev-parse HEAD`) |
| Nguồn quyết định (read-only) | `D:\My file\FA26 - OJT FSoft\T3-DB-Hybrid-Impact-and-Standardization-Plan-2026-10-18.md` (§3 mapping, §4 D1–D7); `D:\My file\FA26 - OJT FSoft\PLAN-DB-Hybrid-Standardization-T1-T6-2026-10-18.md` (Q1–Q6, Nhật ký giám sát) |
| Trạng thái ghi nhận | Toàn bộ thay đổi T5 (SQL base + overlay + compose + runner + BE 409) đang nằm ở **working tree, chưa commit** (HEAD vẫn = `0c8a6fb`) |

Ghi chú đọc hiểu: tiêu đề **D1–D7** lấy nguyên từ bảng §4 của T3 plan (tài liệu phê duyệt chính thức tại cổng T4); **Q1–Q6** lấy từ §"Quyết định Q1–Q6" của PLAN. Các mã gap (P1–P8, R1–R5, U1–U3) là mã của T3 plan.

---

## Thay đổi chính

### D1 — 6-DB mapping BE-first + tách file `03` thành `03a`/`03b`

| Trường | Nội dung |
|---|---|
| Nội dung | Map 6 DB vật lý → connection string, **không sửa BE data-access**, giữ nguyên tên connection string và tên env port: `smartpark_user`(5441)←`01` · `smartpark_parking`(5442)←`02`+`03a`+overlays · `smartpark_reservation`(5443)←`03b`+overlays · `smartpark_payment`(5444)←`04` · `smartpark_notification`(5445)←`05` · `smartpark_iot`(5446)←`06`. Kèm tách file `03-parking-operations` (arch) theo §3.1: `03a` → `smartpark_parking`, `03b` → `smartpark_reservation` |
| Lý do | 6-DB split của arch cắt các JOIN đang chạy trên 1 connection của BE develop (risk #1 trong PLAN); đặt đúng DB thì toàn bộ conflict C1–C8 được giải bằng *placement* thay vì sửa data-access |
| File(s) bị ảnh hưởng | `scripts/database/microservices/03a-parking-structure-ops-service-db.sql`, `scripts/database/microservices/03b-reservation-operations-service-db.sql` (mới); `compose.schema-integration.yml` (6 services), `scripts/apply-workflow-migrations.ps1`, `setup.ps1` |
| Trạng thái triển khai | ✅ Đã triển khai — compose có đủ 6 container DB với port env `${SMARTPARK_DB_*_PORT:-5441…5446}`; runner map đúng `$database`/`$container` cho 6 service |

### D2 (= Q4) — Giữ UNIQUE index trên plate active + BE trả 409

| Trường | Nội dung |
|---|---|
| Nội dung | Giữ UNIQUE index arch `uq_active_vehicle_plate`; BE bắt lỗi `23505` và trả HTTP 409 (thay vì 500) |
| Lý do | Khớp SRS v1.0 PM yêu cầu (invariant plate); phát hiện Q4: BE develop không pre-check và không bắt 23505 → duplicate plate = 500. Lưu ý của T3: lựa chọn này reverses quyết định sản phẩm VEH-OPEN, PM đã xác nhận SRS là chuẩn cuối |
| File(s) bị ảnh hưởng | `scripts/database/microservices/01-user-access-service-db.sql:107`; `src/Services/UserService/SmartParking.UserService.Application/Services/VehicleRegistrationService.cs:72-74` |
| Trạng thái triển khai | ✅ Đã triển khai (chi tiết chứng cứ xử lý 23505→409 nằm ở **report file 08** do lane khác sở hữu — file này không đi sâu) |

### D3 (= Q5) — Port DDL 4 bảng operator (shape BE) vào parking overlay

| Trường | Nội dung |
|---|---|
| Nội dung | Port DDL đúng shape BE mà `OperatorOperationsStore.EnsureSchemaAsync` từng tạo lúc runtime vào base/overlay của `smartpark_parking`, để 4 endpoint `/api/operator/...` hoạt động trên DB fresh |
| Lý do | Q5 verify: 0 script develop nào tạo `support_tickets/operator_incident_events/operator_notifications/operator_audit_logs`; `EnsureSchemaAsync` là dead-code → middleware trả 503 `PROTOTYPE_SCHEMA_NOT_READY` cho mọi request `/api/operator/*` |
| File(s) bị ảnh hưởng | `scripts/database/overlay/parking-operator-tables.sql` (3 bảng: `support_tickets`, `operator_notifications`, `operator_audit_logs`); `scripts/database/microservices/03a-parking-structure-ops-service-db.sql:24-34` (`operator_incident_events` — có sẵn từ T3 P7, **cố ý không lặp lại** trong overlay, xem ghi chú dòng 2 của overlay) |
| Trạng thái triển khai | ✅ Đã triển khai — tổng cộng 4/4 bảng đã có trong `smartpark_parking` (3 ở overlay + 1 ở `03a`) |

### D4 — Base-file deviations (sửa file sau khi copy từ arch)

| Trường | Nội dung |
|---|---|
| Nội dung | (a) `01`: thêm 3 role seed `BUSINESS_OWNER`, `SITE_OPERATOR`, `PLATFORM_ADMIN`; (b) `02`: thêm lại 2 cột `parking_slots.supported_vehicle_type` (CHECK CAR/MOTORCYCLE/OVERSIZED) + `parking_slots.reservation_state` (NULL/PROTECTED/RESERVED/BACKUP); (c) `03a`: rename `structure_backup_policies.spatial_unit_id`→`unit_id`, `backup_count`→`count`; (d) tách `03`→`03a`+`03b` (thuộc §3.2, chốt cùng gói D4) |
| Lý do | U1: BE insert `BUSINESS_OWNER`/`SITE_OPERATOR` và query `PLATFORM_ADMIN` trên DB fresh → FK violation nếu thiếu seed; P1: BE đọc/ghi 2 cột slot; P2: arch đổi tên cột → `UndefinedColumn` khi BE dùng `unit_id`/`count`; (d): tách theo DB đích (D1) |
| File(s) bị ảnh hưởng | `01-user-access-service-db.sql:80-84`, `02-facility-service-db.sql:79-80,87`, `03a-parking-structure-ops-service-db.sql:6,15,17` |
| Trạng thái triển khai | ✅ Đã triển khai — mỗi sửa đổi đều có comment `-- DEVIATION (T4-approved)` trong file; diff vs arch chứng minh từng dòng |

### D5 — `tenants`: giữ SRS copy ở `01` + duplicate vào parking DB

| Trường | Nội dung |
|---|---|
| Nội dung | Bảng `tenants` theo SRS nằm ở `01-user-access-service-db.sql` (canonical) **và** được duplicate bằng `CREATE TABLE IF NOT EXISTS` vào `smartpark_parking`; mọi read/write của BE đi qua connection `ConnectionStrings:Parking` |
| Lý do | Q3 verify: develop tạo `tenants` ở parking baseline và BE query trên connection Parking (JOIN `parking_sites` trong 1 transaction, `SELECT FOR SHARE`); arch chuyển `tenants` sang `01-user-access` → nếu chỉ có ở DB user thì mọi JOIN của FacilityService vỡ |
| File(s) bị ảnh hưởng | `scripts/database/microservices/01-user-access-service-db.sql:33-46`; `scripts/database/overlay/parking-tenants.sql`; `compose.schema-integration.yml:29` |
| Trạng thái triển khai | ✅ Đã triển khai — copy ở `01` giữ nguyên vai trò SRS-canonical, bản parking DB dùng cho runtime |

### D6 — `support_tickets` bản BE-shape dùng tạm ở parking DB

| Trường | Nội dung |
|---|---|
| Nội dung | Tạo `support_tickets` theo **shape BE** (`user_id`, `message`, status `open/in-progress/resolved`) trong parking overlay để chạy được; bản shape SRS ở `05-notification-audit-service-db.sql` giữ nguyên, chờ migrate BE sau |
| Lý do | T3 P5: shape SRS (`requester_id/category/OPEN…`) ≠ shape mà `OperatorOperationsStore` đọc/ghi → dùng shape SRS sẽ lỗi runtime; không tạo thì 503 (xem D3) |
| File(s) bị ảnh hưởng | `scripts/database/overlay/parking-operator-tables.sql:6-17`; shape SRS tham chiếu tại `scripts/database/microservices/05-notification-audit-service-db.sql:63` |
| Trạng thái triển khai | ✅ Đã triển khai (giải pháp tạm, có chủ đích migrate sau) |

### D7 — `site_capacity_pools` recreate shape-BE tại reservation DB

| Trường | Nội dung |
|---|---|
| Nội dung | Tạo lại `site_capacity_pools` với đúng shape BE dùng (`tenant_id`, `pending_payment_count`, `occupied_count`, `protected_count`, `backup_count`…) trong overlay của `smartpark_reservation` |
| Lý do | T3 R3: DDL gốc (file `03` cũ đã xóa) shape đúng nhưng bị mất khi split arch; biến thể `05.1` shape khác (thiếu 4 cột trên) → không dùng được; `StructureSafetyStore` đọc/ghi các cột này |
| File(s) bị ảnh hưởng | `scripts/database/overlay/reservation-site-capacity-pools.sql:19-39` |
| Trạng thái triển khai | ✅ Đã triển khai — nguồn tái tạo ghi rõ trong header file: `git show develop:scripts/database/microservices/03-reservation-session-service-db.sql` |

### Tổng hợp Q1–Q6 (kết quả verify + chốt tại T4)

| Q | Nội dung chốt | Bằng chứng trên đĩa | Trạng thái |
|---|---|---|---|
| **Q1** | Triển khai **cả 6 DB vật lý** ngay trong `setup.ps1`/compose | `compose.schema-integration.yml` đủ 6 services `user-db…iot-db` với 6 volume; `setup.ps1:112,178` duyệt đủ 6 env `SMARTPARK_DB_{USER,PARKING,RESERVATION,PAYMENT,NOTIFICATION,IOT}_PORT` | ✅ Chốt + đã triển khai |
| **Q2** | Develop dùng **CẢ HAI** structure-sync (Parking worker 10s) và gate-flow (Reservation `structure_edit_holds`…) → overlay tích hợp **bắt buộc giữ** vào base/overlay mới | PLAN Q2 (dòng 26–30); nội dung `integration/03` + `05.9–05.12` đã được tái hiện: `reservation-structure-safety.sql`, `reservation-structure-receipts.sql`, `reservation-confirmation-time.sql`, `parking-structure-synchronizations.sql`, cùng `structure_operations`/`structure_backup_policies` ở base | ✅ Verify + đã triển khai (giữ dưới dạng overlay mới, không mount script cũ) |
| **Q3** | `tenants` sống ở **parking DB**, mọi truy cập qua `ConnectionStrings:Parking` | PLAN Q3 (dòng 32–35); `overlay/parking-tenants.sql`; mount `compose.schema-integration.yml:29` | ✅ Verify + đã triển khai (= D5) |
| **Q4** | Plate index: chọn **(a)** giữ UNIQUE arch + BE handler `23505`→409 | `01-user-access-service-db.sql:107` (`uq_active_vehicle_plate`); `VehicleRegistrationService.cs:72-74`; PLAN nhật ký: *"D2 = UNIQUE + BE handler 23505→409"* | ✅ Chốt + đã triển khai (= D2; chi tiết report 08) |
| **Q5** | Operator 4 bảng: chọn **(a)** port DDL shape-BE vào parking DB | PLAN Q5 (dòng 44–48); `overlay/parking-operator-tables.sql` + `03a:24-34`;BE middleware `FacilityService.API/Program.cs:56-60` (`PROTOTYPE_SCHEMA_NOT_READY`) là thứ được gỡ bỏ nhờ có bảng | ✅ Chốt + đã triển khai (= D3) |
| **Q6** | `gate_session_id` **không tồn tại** ở develop (không script, không BE) → quyết định DROP `02.1` (16/10) là no-op | PLAN Q6 (dòng 50–51) | ✅ Verify — không cần hành động nào trên nhánh này |

---

## Lý do / quyết định liên quan

- **Nguồn phê duyệt:** PLAN dòng 122 ghi nhận kết quả cổng T4: *"`T4 bạn phê D1–D7` — ✅ 2026-10-18: phê toàn bộ; D2 = UNIQUE + BE handler 23505→409"*.
- **Cấu trúc quyết định:** D1 giải bài toán *placement* (đặt bảng đúng DB → không sửa BE data-access); D4 giải các *deviation* bắt buộc giữa arch SRS và runtime BE; D5/D6/D7 là các bản sao/tái tạo tạm thời để từng service chạy được trên DB fresh trong khi vẫn giữ file arch làm canonical.
- **Dòng chảy sau phê:** T3 §5 — ① sửa base files (D4) + tách `03a`/`03b` → ② viết overlay → ③ compose 6 service → ④ viết lại `apply-workflow-migrations.ps1` + `setup.ps1` → ⑥ checklist T6.

---

## Evidence

### E1. Trạng thái git (nhánh + base + các thay đổi chưa commit)

```
$ git branch --show-current
feature/db-hybrid-srs-v1
$ git rev-parse HEAD
0c8a6fb119ac0bd1c656f4dbdd63bef999b8a939
$ git status --porcelain scripts/database
 D scripts/database/microservices/01-user-service-db.sql
 … (6 file cũ = D) …
?? scripts/database/microservices/01-user-access-service-db.sql
 … (7 file mới = ??) …
?? scripts/database/overlay/
$ git status --porcelain   # trích phần liên quan ngoài SQL
 M compose.schema-integration.yml
 M scripts/apply-workflow-migrations.ps1
 M setup.ps1
 M src/Services/UserService/SmartParking.UserService.Application/Services/VehicleRegistrationService.cs
```

### E2. Trích tài liệu quyết định (T3 plan §4, nguyên văn, cắt …)

```
$ (T3-DB-Hybrid-Impact-and-Standardization-Plan-2026-10-18.md:104-112)
| **D1** | 6-DB mapping BE-first (§3.4) + tách 03a/03b (§3.1) — KHÔNG sửa BE data-access | ✅ Đề xuất phê |
| **D2** (=Q4) | Plate index: **(a)** giữ UNIQUE arch + tôi thêm BE handler 23505→409 … | ⚖️ **(a)** …
| **D3** (=Q5) | Operator 4 bảng: **(a)** port DDL shape-BE vào overlay parking DB … | ✅ **(a)** |
| **D4** | Base-file deviations (§3.2): fix roles seed, thêm 2 cột parking_slots, rename backup_policies columns | ✅ Package phê |
| **D5** | `tenants`: SRS copy giữ ở 01 + duplicate ở parking DB (write qua FacilityService) | ✅ |
| **D6** | `support_tickets`: bản BE-shape ở parking DB dùng tạm; shape SRS (05) để đó … | ✅ |
| **D7** | `site_capacity_pools` recreate shape-BE tại reservation DB | ✅ (tự động, chỉ thông báo) |
```

```
$ (PLAN-DB-Hybrid-Standardization-T1-T6-2026-10-18.md:120-123)
| 2026-10-18 | T2 bạn copy 6 SQL — hash-verified 6/6 khớp arch `9a13499` | ✅ |
| 2026-10-18 | T3 impact + standardization plan → `T3-…-2026-10-18.md` | ✅ |
| — | T4 bạn phê D1–D7 | ✅ 2026-10-18: phê toàn bộ; D2 = UNIQUE + BE handler 23505→409 |
| — | T5 thực hiện (4 lane: SQL-parking, SQL-reservation, infra, BE-handler) | ✅ 2026-10-18 — SQL 7 base + 8 overlay, compose/runner/setup 6-DB, BE 23505→409. … |
```

### E3. Chứng cứ grep/đọc file hiện hành (file:line của file ĐANG có trên đĩa)

```
D1/6-DB mapping:
  compose.schema-integration.yml:6      POSTGRES_DB: smartpark_user
  compose.schema-integration.yml:9      ports: ["127.0.0.1:${SMARTPARK_DB_USER_PORT:-5441}:5432"]
  compose.schema-integration.yml:21,41,59,75,91   POSTGRES_DB: smartpark_parking / reservation / payment / notification / iot
  scripts/apply-workflow-migrations.ps1:46   $database = @{ User='smartpark_user'; Parking='smartpark_parking'; … Iot='smartpark_iot' }
  scripts/apply-workflow-migrations.ps1:59   $blockedPattern = '(^|[\\/])05\.[1-9]…|…integration[\\/]0[123]…'   # chặn 05.1–05.9 + integration/01–03 cũ

D2 (UNIQUE + 409):
  scripts/database/microservices/01-user-access-service-db.sql:107
      CREATE UNIQUE INDEX uq_active_vehicle_plate ON vehicles (normalized_plate) WHERE deleted_at IS NULL;
  src/Services/UserService/…/VehicleRegistrationService.cs:72-74
      // Duplicate active plate hits uq_active_vehicle_plate; surface it as a 409 …
      catch (Exception e) when (e.InnerException is DbException { SqlState: "23505" })
      { throw new AuthException("PLATE_EXISTS", "…already registered.", 409); }

D3 (operator 4 bảng):
  scripts/database/overlay/parking-operator-tables.sql:6,22,37   support_tickets / operator_notifications / operator_audit_logs
  scripts/database/microservices/03a-parking-structure-ops-service-db.sql:24   CREATE TABLE operator_incident_events (
  src/Services/FacilityService/SmartParking.FacilityService.API/Program.cs:56,60
      context.Request.Path.StartsWithSegments("/api/operator") && … PROTOTYPE_SCHEMA_NOT_READY …

D4 (deviations):
  01-user-access-service-db.sql:80-82   ('BUSINESS_OWNER', …), ('SITE_OPERATOR', …), ('PLATFORM_ADMIN', …)
  01-user-access-service-db.sql:84      -- DEVIATION (T4-approved): runtime role codes used by UserService (…)
  02-facility-service-db.sql:79-80      supported_vehicle_type VARCHAR(20) CHECK (…) / reservation_state VARCHAR(20) CHECK (…)
  02-facility-service-db.sql:87         -- DEVIATION (T4-approved): columns required by live FacilityService reads/writes (P1)
  03a-parking-structure-ops-service-db.sql:6,15,17
      -- DEVIATION (T4-approved): BE column names (P2) / unit_id UUID NULL / count INT NOT NULL CHECK (count >= 0)

D5 (tenants):
  01-user-access-service-db.sql:33   CREATE TABLE tenants ( …
  overlay/parking-tenants.sql:5      CREATE TABLE IF NOT EXISTS tenants (
  compose.schema-integration.yml:29  ./scripts/database/overlay/parking-tenants.sql:/docker-entrypoint-initdb.d/03-overlay-tenants.sql:ro

D6 (support_tickets BE-shape):
  overlay/parking-operator-tables.sql:6-17   user_id/message/status IN ('open','in-progress','resolved')
  microservices/05-notification-audit-service-db.sql:63   CREATE TABLE support_tickets (   # shape SRS, giữ nguyên

D7 (site_capacity_pools):
  overlay/reservation-site-capacity-pools.sql:19   CREATE TABLE IF NOT EXISTS site_capacity_pools (
  overlay/reservation-site-capacity-pools.sql:24-36  tenant_id, pending_payment_count, occupied_count, protected_count, backup_count …
```

---

## Ảnh hưởng

- **BE:** chỉ 1 file backend đổi để khớp quyết định — `VehicleRegistrationService.cs` (bắt `23505` → 409, D2). Các quyết định còn lại được giải hoàn toàn bằng placement/SQL → **không sửa BE data-access** (mục tiêu của D1).
- **Infra/deploy:** `setup.ps1` + compose giờ khởi tạo **6 DB vật lý** (6 container, 6 volume) thay vì 3 connection string cũ; **volume schema cũ phải recreate** vì init script chỉ chạy trên DB trống.
- **Migration pipeline:** `apply-workflow-migrations.ps1` chặn các script legacy `05.1–05.9` + `integration/01–03` (đã bị thay bằng base mới + overlay) — nếu ai đó mount lại script cũ sẽ conflict (plate index, permissions, shape `backup_policies`).
- **Quyền hạn tạm thời:** `support_tickets` (D6) và các bảng operator là shape-BE tạm, cần migrate sang shape SRS khi BE đổi (technical debt đã chủ đích, ghi trong overlay).
- **Rủi ro còn lại cho T6:** mọi thay đổi **chưa commit** → nếu mất working tree thì mất toàn bộ bằng chứng triển khai; nên commit sau khi T6 xác nhận pass.

---

## Cách tái kiểm chứng

```powershell
# 1) Nhánh + base + trạng thái (chỉ đọc)
git branch --show-current                 # -> feature/db-hybrid-srs-v1
git rev-parse HEAD                        # -> 0c8a6fb119ac0bd1c656f4dbdd63bef999b8a939
git status --porcelain scripts/database   # -> 6 dòng " D " (file cũ) + 7 dòng "??" (file mới) + "?? scripts/database/overlay/"

# 2) Nguồn arch read-only (worktree A)
git -C "D:\My file\FA26 - OJT FSoft\OJT\Mock-Project-Smart-Parking" rev-parse HEAD   # -> 9a13499…

# 3) Chứng cứ từng quyết định (grep file hiện hành)
Select-String -Path "scripts\database\microservices\01-user-access-service-db.sql" -Pattern 'BUSINESS_OWNER|PLATFORM_ADMIN|uq_active_vehicle_plate'
Select-String -Path "scripts\database\microservices\02-facility-service-db.sql" -Pattern 'supported_vehicle_type|reservation_state'
Select-String -Path "scripts\database\microservices\03a-parking-structure-ops-service-db.sql" -Pattern 'unit_id|backup_count|operator_incident_events'
Select-String -Path "src\Services\UserService\SmartParking.UserService.Application\Services\VehicleRegistrationService.cs" -Pattern '23505|409'

# 4) Diff 01/02 trên nhánh vs bản arch (read-only, so file)
git diff --no-index --unified=3 "D:\My file\FA26 - OJT FSoft\OJT\Mock-Project-Smart-Parking\scripts\database\microservices\01-user-access-service-db.sql" "scripts\database\microservices\01-user-access-service-db.sql"

# 5) Compose/runner map 6 DB
Select-String -Path "compose.schema-integration.yml" -Pattern 'POSTGRES_DB|initdb.d'
Select-String -Path "scripts\apply-workflow-migrations.ps1" -Pattern 'blockedPattern|smartpark_(user|parking|reservation|payment|notification|iot)'
```
