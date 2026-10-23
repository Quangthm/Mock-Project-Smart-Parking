# Database Documentation

Thư mục này là tài liệu database của **profile triển khai đã ghi nhận** trên branch `feature/db-hybrid-srs-v1` (bằng chứng schema, không phải kiểm chứng runtime mới của SPARK-296), không chỉ là bản thiết kế arch.

## Baseline

- **6 DB vật lý:** `smartpark_user`, `smartpark_facility`, `smartpark_operations`, `smartpark_payment`, `smartpark_notification`, `smartpark_iot`.
- **7 base DDL + 8 overlay:** `scripts/database/microservices/*.sql` + `scripts/database/overlay/*.sql`.
- **Runtime truth:** `compose.schema-integration.yml`, `scripts/apply-workflow-migrations.ps1`, `setup.ps1`.
- **Design baseline:** `arch/database-schema` @ `9a13499`; các deviation D1–D7 được ghi rõ trong tài liệu service và report 10.

## Danh mục

| File / thư mục | Nội dung |
|---|---|
| `database-architecture.md` | Kiến trúc 6 DB, invariants, state, deployment artifacts |
| `data-dictionary.md` | Từ điển dữ liệu theo DB, gồm các cột runtime/overlay |
| `schema-integration.md` | Topology, thứ tự mount, chạy local, fence/recovery |
| `microservices/` | Tài liệu chi tiết 01–06; `03` tách 03a/03b |
| `db-update-log/` | Workflow/template change log database (arch copy) |
| `workflow-upgrades-2026-10-08.md` | Historical arch workflow `05.8–05.10`; superseded by base + overlay, không chạy trên branch này |
| [alignment-backlog.md](alignment-backlog.md) | DB25–DB33: v1.1 target work; DB01–DB24: retained historical findings |
| `drawdb-schema.sql` | Snapshot lịch sử cho drawDB, **không phải runtime schema** |

## v1.1 reset and alignment gate

The schema facts below remain the recorded deployment profile; v1.1 requirements are in [DB25–DB33](alignment-backlog.md#v11-schema-and-seed-alignment-pending), not fabricated deployed columns. Partner review/document versions, simulation, provisioning, yearly products, discovery, same-lot batch deactivation and vehicle-binding history need schema/seed design.

Development database reset is a **future task after the documentation update is approved** (V11-08), replacing migration/grandfathering of invalid development records. Target development environments, responsible teammate, reviewed reset/seed scripts and execution date are TBD under DB31. Existing setup commands are described in [schema integration](schema-integration.md) as implementation evidence; no setup/reset is executed or authorized by this documentation task.

## Evidence

- Quyết định D1–D7/Q1–Q6: [report 01](<../../04-testing/reports/refactor-Docker Setup & Function Test/01-approved-decisions-D1-D7.md>)
- Đối chiếu docs arch vs DB triển khai + phương án: [report 10](<../../04-testing/reports/refactor-Docker Setup & Function Test/10-db-docs-deployment-alignment.md>)
- PR: https://github.com/Quangthm/Mock-Project-Smart-Parking/pull/10
