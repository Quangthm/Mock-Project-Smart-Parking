# 10 — Đối chiếu DB docs (arch) với DB đang triển khai + phương án cập nhật

## Meta

| Hạng mục | Giá trị |
|---|---|
| Ngày | 2026-10-20 |
| Branch | `feature/db-hybrid-srs-v1` |
| HEAD | `b069e82` (`docs: update quick-start, local-setup, evidence reports [SPARK-183]`) |
| Base | Branch point `0c8a6fb`; `develop` hiện tại `58df087` |
| Arch design baseline | `arch/database-schema` @ `9a13499` (`refactor(database): align 6 microservice schemas and docs with SRS v1.0 RC3 [SPARK-1]`) |
| SRS | `docs/01-requirements/srs/smartpark-srs-v1.0.md` (v1.0 RC3, commit `6165024`) |
| PR / Jira | PR #10 / **SPARK-183** |
| Phạm vi | Database docs only — không sửa SQL, BE, `setup.ps1` |

---

## 1. Vấn đề

Bộ docs trong `arch/database-schema/docs/02-architecture/database/` được sinh từ **6 file SQL arch** + SRS v1.0. Branch triển khai đã chuyển sang profile **6 DB vật lý** với **7 base DDL + 8 overlay** và các deviation đã được phê tại T4 (D1–D7). Vì vậy, nếu copy nguyên bộ docs arch về branch mà không cập nhật, tài liệu sẽ mô tả sai schema đang chạy:

- Tên file cũ (`01-user-service-db.sql`, `02-facility-service-db.sql`, `03-reservation-session-service-db.sql`, …).
- `03` bị mô tả là một file/DB, trong khi triển khai tách `03a` → `smartpark_parking`, `03b` → `smartpark_reservation`.
- `01` docs seed 4 role; triển khai seed 7 mã.
- `02` docs nói `parking_slots` chỉ có `physical_state`; triển khai có thêm `supported_vehicle_type`, `reservation_state`.
- `03` docs dùng `backup_count`; triển khai dùng `count` (và `unit_id` thay `spatial_unit_id`).
- 8 overlay (tenants, structure sync, operator tables, slot constraints, capacity pools, safety, receipts, confirmation trigger) không có trong docs.
- `database-architecture.md` còn khẳng định composite FK/RLS như thể đã bật; `schema-integration.md` còn mô tả workflow 3 DB/arch checkout cũ.

## 2. Bằng chứng đối chiếu

### 2.1 Docs arch vs SQL arch

| Service | Docs | SQL arch | Kết luận |
|---|---|---|---|
| 01 | 14 bảng, 4 role seed | 14 bảng, 4 role seed | Khớp |
| 02 | 9 bảng, không có 2 cột runtime | 9 bảng, không có 2 cột | Khớp |
| 03 | 12 bảng, một file `03` | 12 bảng, một file `03` | Khớp |
| 04 | 7 bảng | 7 bảng | Khớp |
| 05 | 6 bảng | 6 bảng | Khớp |
| 06 | 6 bảng | 6 bảng | Khớp |

Kết luận: docs arch phản ánh đúng **arch @9a13499**, không phản ánh branch triển khai.

### 2.2 Branch vs arch (các deviation đã phê)

| File | Deviation | Bằng chứng |
|---|---|---|
| `01-user-access-service-db.sql` | Thêm `BUSINESS_OWNER`, `SITE_OPERATOR`, `PLATFORM_ADMIN` | L75–84, comment `DEVIATION (T4-approved)`; BE dùng tại `OwnerRegistrationService.cs:40`, `OperatorProvisioningService.cs:83`, `UserManagementService.cs:22-23,35,56-57`, `AccessTokenService.cs:30-32` |
| `02-facility-service-db.sql` | Thêm `parking_slots.supported_vehicle_type`, `reservation_state` | L79–80, L87; BE dùng tại `OperatorGateSlotStore.cs:27-28,51,55`, `PostgresParkingStructureRepository.cs:148-152,205` |
| `03` → `03a`/`03b` | Tách theo DB đích; 03a rename `spatial_unit_id`→`unit_id`, `backup_count`→`count` | `03a` L6, L15, L17; `03b` 10 bảng; `PostgresParkingStructureRepository.cs:163,213` |
| `04`/`05`/`06` | Không đổi | Diff rỗng |

### 2.3 Overlay bắt buộc cho runtime

| DB | Overlay | Vì sao |
|---|---|---|
| `smartpark_parking` | `parking-tenants.sql` | `FacilityService.API/Program.cs:76-91`; JOIN `tenants` tại `OperatorGateSlotStore.cs:40`, `PostgresParkingStructureRepository.cs:22`, `PostgresStructureSynchronizationStore.cs:55` |
| `smartpark_parking` | `parking-structure-synchronizations.sql` | `PostgresStructureSynchronizationStore` dùng bảng, BE không tự tạo |
| `smartpark_parking` | `parking-operator-tables.sql` | `/api/operator/*` cần `support_tickets`, `operator_notifications`, `operator_audit_logs`; `operator_incident_events` ở 03a |
| `smartpark_parking` | `parking-slot-constraints.sql` | `ck_deleted_slot_safe` + `uq_site_slot_code_ci` |
| `smartpark_reservation` | `reservation-site-capacity-pools.sql` | `StructureSafetyStore` đọc/ghi counters; shape bị mất khi split arch (D7) |
| `smartpark_reservation` | `reservation-structure-safety.sql` | `structure_edit_holds`, `structure_site_state`, `structure_removed_resources` + guard triggers |
| `smartpark_reservation` | `reservation-structure-receipts.sql` | `structure_release_receipts` |
| `smartpark_reservation` | `reservation-confirmation-time.sql` | Stamp `confirmed_at`; base chỉ có cột, không có writer |

## 3. Các phương án đã cân nhắc

### Phương án A — Thay SQL branch bằng arch, dùng compose arch

**Kết quả:** không khả thi.

- Compose arch dùng một `POSTGRES_DB: smartpark_db`, không phải 6 DB.
- Arch không có `03a`/`03b`; `setup.ps1` và `apply-workflow-migrations.ps1` hard-code 7 base + 8 overlay.
- `parking-slot-constraints.sql` fail `42703` vì arch `parking_slots` không có `reservation_state`.
- BE fail khi đọc `supported_vehicle_type`, `reservation_state`, `unit_id`, `count`; role `BUSINESS_OWNER`/`SITE_OPERATOR`/`PLATFORM_ADMIN` thiếu seed.
- `tenants`, `structure_synchronizations`, `site_capacity_pools`, `structure_edit_holds`… thiếu ở DB đích.

### Phương án B — Hybrid: giữ arch base + tự viết overlay tương thích

**Kết quả:** vẫn fail runtime.

- Overlay có thể bù bảng, nhưng không bù được các cột `parking_slots`/`structure_backup_policies` mà BE query trực tiếp (trừ khi ALTER base, tức là quay lại D4).
- Role seed vẫn thiếu; `setup.ps1` vẫn cần sửa lớn.
- Chi phí cao hơn phương án C, rủi ro regression BE cao hơn.

### Phương án C — Giữ branch là runtime canonical, cập nhật docs theo deployment (đã chọn)

**Nội dung:**

1. Copy toàn bộ docs DB từ arch vào branch.
2. Cập nhật docs theo schema **đang triển khai** (6 DB, 7 base, 8 overlay, D1–D7).
3. Ghi lại deviation và lý do trong docs + report này.
4. Không sửa SQL/BE/`setup.ps1` trong đợt này.

**Lý do chọn:** giữ nguyên hành vi BE đã kiểm chứng; docs trở thành mô tả đúng runtime; PM có evidence để đối chất; tránh regression do đổi schema.

## 4. Đã thực hiện

### 4.1 Copy docs

Đã copy từ arch `docs/02-architecture/database/`:

- 5 file top-level: `README.md`, `database-architecture.md`, `data-dictionary.md`, `schema-integration.md`, `workflow-upgrades-2026-10-08.md`.
- `db-update-log/` (3 file).
- `microservices/` (8 file: 00 workflow, 00 template, 01–06).

Giữ nguyên file riêng của branch: `alignment-backlog.md`, `drawdb-schema.sql`.

### 4.2 Cập nhật docs theo deployment

| File | Thay đổi chính |
|---|---|
| `database-architecture.md` | 6 DB/7 base/8 overlay; bỏ claim composite FK/RLS; invariants thực tế; D1–D7 |
| `data-dictionary.md` | 7 role codes; 2 cột slot runtime; 03a/03b; `site_capacity_pools`; bỏ Subscription/Incident DB cũ |
| `schema-integration.md` | Baseline branch; topology 6 DB; setup/runner; vì sao không chạy arch; D1–D7 |
| `README.md` | Index mới; runtime truth; report 10 |
| `microservices/01` | §0 deployment; 7 role seeds + mapping 4 actor → 7 code |
| `microservices/02` | §0; principle #2/#4; 2 cột runtime; constraints base/overlay; §12 overlay |
| `microservices/03` | §0; 03a/03b; bảng cột §2/§3/§4/§5.3; `UNFULFILLABLE`/`confirmed_at`; operator events; overlay; deviations |
| `microservices/04/05/06` | §0 deployment; 05 note D6 `support_tickets` |
| `microservices/00-doc-generation-workflow.md` | Inputs base+overlay+SRS v1.0+deployment truth; deployment profile/deviation bắt buộc; audit scope |
| `microservices/00-service-db-doc-template.md` | Header/§0 deployment; deviations section; SRS example v1.0 |
| `drawdb-schema.sql` | Header cảnh báo historical/non-runtime (không regenerate) |
| `workflow-upgrades-2026-10-08.md` | Header historical; `05.8–05.10` không chạy trên branch này |
| `alignment-backlog.md` | Header historical; cảnh báo path/claim dùng tên pre-D1 |

### 4.3 Không thay đổi

- Không sửa `scripts/database/**`, `compose.schema-integration.yml`, `setup.ps1`, BE.
- Không bật RLS.
- Không sửa arch checkout (đang có thay đổi cục bộ của người dùng).
- Không regenerate `drawdb-schema.sql`.

## 5. Hệ quả và follow-up

| # | Follow-up | Mức độ |
|---|---|---|
| 1 | Sửa mojibake comment trong `03a`/`03b` (UTF-8 double-encoded; DDL ASCII nên không ảnh hưởng chạy) | P2 |
| 2 | Chuẩn hóa role alias (`OWNER`/`BUSINESS_OWNER`, `OPERATOR`/`SITE_OPERATOR`, `ADMIN`/`PLATFORM_ADMIN`) hoặc thêm migration seed cho production | P2 |
| 3 | Migrate `support_tickets` parking → shape SRS (D6) | P2 |
| 4 | Xác nhận nguồn canonical của `site_capacity_pools` (D7) và `confirmed_at` trigger | P2 |
| 5 | Regenerate `drawdb-schema.sql` nếu PM cần bản vẽ 6-DB mới | P3 |
| 6 | Nếu PM muốn một nguồn canonical duy nhất: backport D1–D7 vào arch docs/SQL | P2 |

## 6. Kiểm chứng

```powershell
# 1) File tree sau copy
Get-ChildItem docs\02-architecture\database -Recurse -File

# 2) Không còn tên file/claim cũ trong docs hiện hành.
#    db-update-log/DB-20261009-01.md và alignment-backlog.md là snapshot lịch sử,
#    giữ nguyên tên file/claim tại thời điểm ghi.
Get-ChildItem docs\02-architecture\database -Recurse -Filter *.md |
  Where-Object { $_.FullName -notmatch '\\db-update-log\\|alignment-backlog\.md$' } |
  Select-String -Pattern '01-user-service-db\.sql|02-facility-service-db\.sql|03-reservation-session-service-db\.sql|smartpark-srs-v0\.9|4 role codes|chỉ lưu `physical_state`'

# 3) UTF-8 strict + BOM/mojibake
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
$files = Get-ChildItem docs\02-architecture\database -Recurse -Filter *.md
foreach ($f in $files) {
  $b = [IO.File]::ReadAllBytes($f.FullName)
  if ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { "BOM: $($f.FullName)" }
  try { [void]$utf8.GetString($b) } catch { "INVALID UTF-8: $($f.FullName)" }
}

# 4) Diff
git status --short
git diff --stat -- "docs/02-architecture/database" "docs/04-testing/reports/refactor-Docker Setup & Function Test"
```

**Kết quả chạy 2026-10-20:** 0 BOM / 0 invalid UTF-8 / 0 mojibake; 0 bảng Markdown lệch cột; 15 link nội bộ hợp lệ; không còn stale term trong docs hiện hành (các tên pre-D1 chỉ còn trong snapshot lịch sử đã đánh dấu).

## 7. Kết luận

- Docs DB giờ mô tả **đúng profile đang triển khai** trên `feature/db-hybrid-srs-v1`, không còn là bản arch thuần.
- Các deviation D1–D7 được ghi rõ, có file:line evidence và link tới report 01.
- Không có thay đổi runtime trong đợt này; BE/setup giữ nguyên.
- Đề nghị PM xác nhận: **branch là runtime canonical**, arch chỉ là design baseline; các follow-up P2/P3 được xử lý ở task riêng.

## 8. Tham chiếu

- `docs/02-architecture/database/README.md`
- `docs/02-architecture/database/database-architecture.md`
- `docs/02-architecture/database/schema-integration.md`
- `docs/02-architecture/database/data-dictionary.md`
- `docs/02-architecture/database/microservices/00-doc-generation-workflow.md`
- `docs/04-testing/reports/refactor-Docker Setup & Function Test/01-approved-decisions-D1-D7.md`
- PR #10: https://github.com/Quangthm/Mock-Project-Smart-Parking/pull/10
