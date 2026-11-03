# 02 — Bộ SQL base microservices: 6 file cũ (đã xóa) → 7 file mới (SRS v1.0)

## Meta

| Hạt | Giá trị |
|---|---|
| Ngày | 2026-10-18 |
| Branch | `feature/db-hybrid-srs-v1` |
| Base commit | `0c8a6fb` (= `develop`) |
| Nguồn arch | worktree A `D:\My file\FA26 - OJT FSoft\OJT\Mock-Project-Smart-Parking` @ `arch/database-schema` commit `9a13499` |
| Thư mục | `scripts/database/microservices/` |
| Trạng thái git | 6 file cũ = **`D`** (deletions trong worktree, chưa staged); 7 file mới = **`??`** (untracked) — mọi thay đổi chưa commit |

---

## Thay đổi chính

### 1. Bộ file cũ bị xóa (source of truth cũ = `develop`)

Nội dung 6 file cũ vẫn đọc được qua `git show HEAD:<path>`:

| File cũ (đã xóa) | Dòng (theo `git diff --stat`) | Số bảng | DB/role cũ |
|---|---|---|---|
| `01-user-service-db.sql` | 131 | 9 | user |
| `02-facility-service-db.sql` | 166 | 8 | parking (gồm cả `tenants`, `tariffs`, `access_items`) |
| `03-reservation-session-service-db.sql` | 128 | 4 | reservation/session (gồm `site_capacity_pools`) |
| `04-payment-service-db.sql` | 122 | 5 | payment |
| `05-notification-service-db.sql` | 68 | 3 | notification |
| `06-iot-service-db.sql` | 68 | 3 | iot |
| **Tổng** | **683 deletions(-)** | **32 bảng** | |

### 2. Bộ file mới (SRS v1.0, copy từ arch `9a13499` + sửa T4-approved)

| File mới | Dòng | Số bảng | Mount vào DB |
|---|---|---|---|
| `01-user-access-service-db.sql` | 243 | 14 | `smartpark_user` |
| `02-facility-service-db.sql` | 165 | 9 | `smartpark_parking` |
| `03a-parking-structure-ops-service-db.sql` | 34 | 2 | `smartpark_parking` |
| `03b-reservation-operations-service-db.sql` | 253 | 10 | `smartpark_reservation` |
| `04-payment-pricing-service-db.sql` | 149 | 7 | `smartpark_payment` |
| `05-notification-audit-service-db.sql` | 114 | 6 | `smartpark_notification` |
| `06-iot-recognition-service-db.sql` | 114 | 6 | `smartpark_iot` |
| **Tổng** | — | **54 bảng** | 6 DB vật lý |

### 3. Đây là **thay thế bộ file** (old set → SRS v1.0 set), KHÔNG phải đổi tên 1–1

Bằng chứng không phải rename 1–1 (tên file na ná nhưng **phân bố bảng khác hẳn**, đối chiếu từ `git show HEAD:…` vs file mới):

- Bộ mới có **54 bảng** thay vì **32 bảng** (54 khớp con số "6 file (54 bảng)" trong PLAN §Thông số phiên bản).
- File `02` cũ (parking) gộp nhiều domain, nay bị phân tán: `tenants` → `01` (bản canonical, parking runtime dùng bản overlay), `access_items` → `03b`, `tariffs` → `04-payment-pricing`, còn `02-facility` mới chứa `parking_sites/spatial_units/parking_slots/…`.
- File `03` cũ (reservation-session) bị **tách đôi** theo DB đích (quyết định D1/§3.1): một phần chuyển sang `03a` (parking), phần còn lại sang `03b` (reservation).
- `site_capacity_pools` (bảng thứ 4 của `03` cũ) **không còn trong bất kỳ base file mới nào** → được tái tạo ở overlay `reservation-site-capacity-pools.sql` (D7).
- `site_operational_settings` (cũ ở `02`) **không có** trong bộ base mới (chỉ còn xuất hiện trong script legacy `05.1-Database-Scripts.sql:197,215` — script bị runner chặn).
- File mới thêm nhiều bảng arch chưa có ở develop: `slot_compatibilities`, `structure_*_audit`, `monthly_passes`, `policy_revisions`, `central_audit_projections`, `assistant_*`, …

### 4. Xác minh copy (T2) + các sửa đổi sau copy (post-copy edits)

Hash so với arch `9a13499` (SHA-256, kiểm lại hôm nay):

| File | Kết quả |
|---|---|
| `04-payment-pricing-service-db.sql`, `05-notification-audit-service-db.sql`, `06-iot-recognition-service-db.sql` | **IDENTICAL** với arch (copy còn nguyên vẹn) |
| `01-user-access-service-db.sql`, `02-facility-service-db.sql` | **DIFFERS** — đúng là 2 file bị sửa post-copy theo D4 (diff ở Evidence E3) |
| `03` (arch) vs `03a`+`03b` (branch) | File `03-parking-operations-service-db.sql` arch (14 302 byte, 12 bảng) **bị thay bằng cặp** `03a` (2 bảng) + `03b` (10 bảng) theo D1/D4 |

**Post-copy edits đã xác minh (mỗi cái đều có comment `-- DEVIATION (T4-approved)`):**

1. **`01` — thêm 3 role seed** (D4/U1): `01-user-access-service-db.sql:80-82` thêm `BUSINESS_OWNER`, `SITE_OPERATOR`, `PLATFORM_ADMIN` sau 4 role cũ `ADMIN/DRIVER/OWNER/OPERATOR`; comment at line 84.
2. **`02` — thêm 2 cột vào `parking_slots`** (D4/P1): `02-facility-service-db.sql:79-80` thêm `supported_vehicle_type` (CHECK `CAR/MOTORCYCLE/OVERSIZED`) và `reservation_state` (NULL/`PROTECTED/RESERVED/BACKUP`); comment at line 87.
3. **`03` → `03a` + `03b`** (D1/D4, tách theo §3.1): arch `03` có 12 bảng (`operational_capacity_configs`, `operational_policies`, `structure_backup_policies`, `reservations`, `parking_sessions`, `slot_allocations`, `access_items`, `monthly_passes`, `operational_incidents`, `operator_incident_events`, `search_facility_projections`, `audit_logs`) → `03a` giữ `structure_backup_policies` + `operator_incident_events` (đúng §3.1); `03b` giữ 10 bảng còn lại (danh sách §3.1 khớp từng tên).
4. **Rename cột `structure_backup_policies` — nằm ở `03a`, không phải `03b`** (D4/P2): arch `03:54,56` dùng `spatial_unit_id`/`backup_count` → `03a:15,17` dùng `unit_id`/`count` (2 unique index ở `03a:21-22` cũng đổi theo).

---

## Lý do / quyết định liên quan

- **D1** (6-DB mapping + tách `03a`/`03b`): lý do file mới đi kèm `03a`/`03b` thay vì `03` — mỗi file mount vào đúng DB, không cắt JOIN của BE.
- **D4** (base-file deviations): 3 nhóm sửa sau copy (role seed, 2 cột slot, rename cột backup_policies) đều là sửa **bắt buộc** để BE develop chạy được trên SRS schema (gap U1/P1/P2 trong T3 plan).
- **D2** (UNIQUE plate): `01` giữ `uq_active_vehicle_plate` của arch (line 107) — không bị drop như script legacy `05.8`; BE đã có handler 409 (report 08).
- **T2** (copy + hash-verify 6/6 vs `9a13499`): được ghi trong PLAN nhật ký giám sát (dòng 120); phép kiểm hôm nay xác nhận `04/05/06` vẫn identical, `01/02` diff đúng ở phần D4, `03` đã tách — không phát hiện sai lệch ngoài dự kiến.

---

## Evidence

### E1. `git status` + `git diff --stat` (chỉ đọc)

```
$ git status --porcelain scripts/database
 D scripts/database/microservices/01-user-service-db.sql
 D scripts/database/microservices/02-facility-service-db.sql
 D scripts/database/microservices/03-reservation-session-service-db.sql
 D scripts/database/microservices/04-payment-service-db.sql
 D scripts/database/microservices/05-notification-service-db.sql
 D scripts/database/microservices/06-iot-service-db.sql
?? scripts/database/microservices/01-user-access-service-db.sql
?? scripts/database/microservices/02-facility-service-db.sql
?? scripts/database/microservices/03a-parking-structure-ops-service-db.sql
?? scripts/database/microservices/03b-reservation-operations-service-db.sql
?? scripts/database/microservices/04-payment-pricing-service-db.sql
?? scripts/database/microservices/05-notification-audit-service-db.sql
?? scripts/database/microservices/06-iot-recognition-service-db.sql
?? scripts/database/overlay/

$ git diff --stat -- scripts/database
 …/database/microservices/01-user-service-db.sql  | 131 ----------------
 …/database/microservices/02-facility-service-db.sql  | 166 ---------------------
 …/database/microservices/03-reservation-session-service-db.sql | 128 ----------------
 …/database/microservices/04-payment-service-db.sql  | 122 ---------------
 …/database/microservices/05-notification-service-db.sql  | 68 ---------
 …/database/microservices/06-iot-service-db.sql  | 68 ---------
 6 files changed, 683 deletions(-)
```

### E2. Trích diff xóa file cũ (hunk header giữ nguyên, nội dung cắt …)

```
$ git diff -- scripts/database/microservices/01-user-service-db.sql
diff --git a/scripts/database/microservices/01-user-service-db.sql b/scripts/database/microservices/01-user-service-db.sql
deleted file mode 100644
index a11ce9c..0000000
--- a/scripts/database/microservices/01-user-service-db.sql
+++ /dev/null
@@ -1,131 +0,0 @@
--- ======================================================================================
--- MICROSERVICE: USER SERVICE DATABASE
--- ======================================================================================
-CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
…
```

### E3. Post-copy edit `01` (diff vs arch, nguyên văn)

```
$ git diff --no-index --unified=3 <arch>\01-user-access-service-db.sql scripts\database\microservices\01-user-access-service-db.sql
@@ -76,8 +76,12 @@ INSERT INTO roles (code, name) VALUES
 ('ADMIN', 'System Administrator'),
 ('DRIVER', 'Driver'),
 ('OWNER', 'Owner'),
-('OPERATOR', 'Site Operator / Attendant')
+('OPERATOR', 'Site Operator / Attendant'),
+('BUSINESS_OWNER', 'Business Owner'),
+('SITE_OPERATOR', 'Site Operator'),
+('PLATFORM_ADMIN', 'Platform Administrator')
 ON CONFLICT DO NOTHING;
+-- DEVIATION (T4-approved): runtime role codes used by UserService (OwnerRegistrationService.cs:40, OperatorProvisioningService.cs:83, UserManagementService.cs:22)
```

### E4. Post-copy edit `02` (diff vs arch, nguyên văn)

```
$ git diff --no-index --unified=3 <arch>\02-facility-service-db.sql scripts\database\microservices\02-facility-service-db.sql
@@ -76,12 +76,15 @@ CREATE TABLE parking_slots (
     features JSONB,
     coordinates_3d JSONB NULL,
     physical_state VARCHAR(50) NOT NULL DEFAULT 'AVAILABLE' CHECK (physical_state IN ('AVAILABLE', 'OCCUPIED', 'UNKNOWN', 'MAINTENANCE', 'UNAVAILABLE')),
+    supported_vehicle_type VARCHAR(20) CHECK (supported_vehicle_type IN ('CAR','MOTORCYCLE','OVERSIZED')),
+    reservation_state VARCHAR(20) CHECK (reservation_state IS NULL OR reservation_state IN ('PROTECTED','RESERVED','BACKUP')),
     created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
     …
     CONSTRAINT ck_deleted_slot_not_occupied CHECK (deleted_at IS NULL OR (physical_state NOT IN ('OCCUPIED', 'UNKNOWN')))
 );
+-- DEVIATION (T4-approved): columns required by live FacilityService reads/writes (P1)
```

### E5. Grep file hiện hành (file:line — file đang có trên đĩa)

```
$ Select-String -Path scripts\database\microservices\01-user-access-service-db.sql -Pattern 'BUSINESS_OWNER|SITE_OPERATOR|PLATFORM_ADMIN|uq_active_vehicle_plate'
01-user-access-service-db.sql:80: ('BUSINESS_OWNER', 'Business Owner'),
01-user-access-service-db.sql:81: ('SITE_OPERATOR', 'Site Operator'),
01-user-access-service-db.sql:82: ('PLATFORM_ADMIN', 'Platform Administrator')
01-user-access-service-db.sql:107: CREATE UNIQUE INDEX uq_active_vehicle_plate ON vehicles (normalized_plate) WHERE deleted_at IS NULL;

$ Select-String -Path scripts\database\microservices\02-facility-service-db.sql -Pattern 'supported_vehicle_type|reservation_state'
02-facility-service-db.sql:79: supported_vehicle_type VARCHAR(20) CHECK (supported_vehicle_type IN ('CAR','MOTORCYCLE','OVERSIZED')),
02-facility-service-db.sql:80: reservation_state VARCHAR(20) CHECK (reservation_state IS NULL OR reservation_state IN ('PROTECTED','RESERVED','BACKUP')),

$ Select-String -Path scripts\database\microservices\03a-parking-structure-ops-service-db.sql -Pattern 'unit_id|count|operator_incident_events'
03a-parking-structure-ops-service-db.sql:15:     unit_id UUID NULL,
03a-parking-structure-ops-service-db.sql:17:     count INT NOT NULL CHECK (count >= 0),
03a-parking-structure-ops-service-db.sql:21: CREATE UNIQUE INDEX uq_backup_site_policy ON structure_backup_policies(site_id, vehicle_type) WHERE unit_id IS NULL;
03a-parking-structure-ops-service-db.sql:24: CREATE TABLE operator_incident_events (
03a-parking-structure-ops-service-db.sql:34: CREATE INDEX idx_operator_incident_events_site ON operator_incident_events(site_id, created_at DESC);
```

### E6. Đối chiếu arch (read-only — worktree A không bị thay đổi)

```
# arch 03 (nguồn tách) — structure_backup_policies dùng tên cột cũ:
arch …\03-parking-operations-service-db.sql:54:     spatial_unit_id UUID NULL,
arch …\03-parking-operations-service-db.sql:56:     backup_count INT NOT NULL CHECK (backup_count >= 0),
arch …\03-parking-operations-service-db.sql:60: CREATE UNIQUE INDEX uq_backup_site_policy ON structure_backup_policies(site_id, vehicle_type) WHERE spatial_unit_id IS NULL;

# 12 bảng arch 03 (line: tên bảng)
13: operational_capacity_configs · 30: operational_policies · 50: structure_backup_policies · 65: reservations
102: parking_sessions · 137: slot_allocations · 161: access_items · 182: monthly_passes
214: operational_incidents · 236: operator_incident_events · 250: search_facility_projections · 268: audit_logs

$ git -C <worktree A> rev-parse HEAD
9a1349974f632304857ca4034b9e358232efcbde
```

---

## Ảnh hưởng

- **Compose phải đổi mount** theo tên mới — nếu vẫn mount tên cũ thì `docker compose config` fail / DB khởi tạo rỗng (chính là known-broken của T1; nay `compose.schema-integration.yml` đã mount 7 file mới + 8 overlay).
- **Seeds/scripts legacy tham chiếu tên bảng cũ** (`site_operational_settings`, `notification_logs`, shape `03` cũ…) không còn khớp → runner mới chặn `05.1–05.9` + `integration/01–03` (block pattern tại `apply-workflow-migrations.ps1:59`).
- **Volume cũ ≠ schema mới** → phải recreate volume trước khi `up` (init chỉ chạy khi data dir trống).
- **Deviation có chủ đích vs arch:** `01`/`02`/`03a` không giống arch 100% (đã ghi rõ ở mục 4) → khi sync lại từ arch trong tương lai phải giữ lại các chỉnh sửa D4.
- **BE liên quan:** các cột `supported_vehicle_type`/`reservation_state`, role seeds, `unit_id`/`count`, UNIQUE plate đều là chỗ BE đọc/ghi trực tiếp — thiếu bất kỳ edit nào ở trên là lỗi runtime (P1/P2/U1).

---

## Cách tái kiểm chứng

```powershell
# 1) Danh sách file cũ/mới (chỉ đọc)
git status --porcelain scripts/database
git diff --stat -- scripts/database                       # -> 6 files changed, 683 deletions(-)

# 2) Đọc lại nội dung file cũ đã xóa (không cần checkout)
git show HEAD:scripts/database/microservices/02-facility-service-db.sql | Select-String 'CREATE TABLE'

# 3) So sánh với arch (read-only; git diff --no-index không đụng worktree A)
$a = "D:\My file\FA26 - OJT FSoft\OJT\Mock-Project-Smart-Parking\scripts\database\microservices"
git diff --no-index --unified=3 "$a\01-user-access-service-db.sql" "scripts\database\microservices\01-user-access-service-db.sql"
git diff --no-index --unified=3 "$a\02-facility-service-db.sql"   "scripts\database\microservices\02-facility-service-db.sql"

# 4) Hash nhanh 3 file chưa bị sửa (phải IDENTICAL)
foreach ($f in '04-payment-pricing-service-db.sql','05-notification-audit-service-db.sql','06-iot-recognition-service-db.sql') {
  (Get-FileHash "$a\$f").Hash -eq (Get-FileHash "scripts\database\microservices\$f").Hash
}

# 5) Đếm bảng: bộ mới = 54
(Get-ChildItem scripts\database\microservices\*.sql | Select-String -Pattern 'CREATE TABLE').Count
(Get-Content scripts\database\microservices\03b-reservation-operations-service-db.sql | Select-String 'CREATE TABLE').Count   # -> 10

# 6) Không còn site_operational_settings trong bộ base mới (kết quả: 0 dòng)
Select-String -Path "scripts\database\microservices\*.sql" -Pattern 'site_operational_settings'
```
