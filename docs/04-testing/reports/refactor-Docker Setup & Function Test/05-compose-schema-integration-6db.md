# 05 — compose.schema-integration.yml: 6 DB services (SRS v1.0 hybrid)

## Meta
- **Ngày:** 2026-10-18
- **Branch:** `feature/db-hybrid-srs-v1`
- **Base commit:** `develop` @ `0c8a6fb`
- **File:** `compose.schema-integration.yml` (root)
- **Lane:** T5 infra (compose)

## Thay đổi chính
- Chuyển compose từ 3 DB (user/parking/reservation) sang **6 DB vật lý**: `user-db`, `parking-db`, `reservation-db`, `payment-db`, `notification-db`, `iot-db`.
- Tổng **15 mount** SQL vào `/docker-entrypoint-initdb.d/`, đặt tên target có **tiền tố số** (`01-`, `02-`, …) để ép thứ tự init — postgres chạy `*.sql` theo alphabet nên số thứ tự quyết định thứ tự apply:
  - `user-db`: 1 mount
  - `parking-db`: 6 mount
  - `reservation-db`: 5 mount
  - `payment-db` / `notification-db` / `iot-db`: mỗi DB 1 mount
- Bỏ toàn bộ mount trỏ tới file đã xoá (`microservices/01-user-service-db.sql` … `06-iot-service-db.sql`), các script `05.x` và `integration/*`.
- Giữ nguyên style cũ: `name: smartpark-schema`, pattern port `127.0.0.1:${SMARTPARK_DB_*_PORT:-544x}:5432`, `healthcheck` (`pg_isready`), named volumes, `POSTGRES_DB` names, `:ro`.

## Bảng mount cuối cùng (15/15 source path Test-Path OK)
| DB service | POSTGRES_DB | Port env (default) | Target trong container | Source (relative) |
|---|---|---|---|---|
| user-db | smartpark_user | `SMARTPARK_DB_USER_PORT` (5441) | `01-base.sql` | `scripts/database/microservices/01-user-access-service-db.sql` |
| parking-db | smartpark_parking | `SMARTPARK_DB_PARKING_PORT` (5442) | `01-base.sql` | `scripts/database/microservices/02-facility-service-db.sql` |
| parking-db | smartpark_parking | — | `02-base.sql` | `scripts/database/microservices/03a-parking-structure-ops-service-db.sql` |
| parking-db | smartpark_parking | — | `03-overlay-tenants.sql` | `scripts/database/overlay/parking-tenants.sql` |
| parking-db | smartpark_parking | — | `04-overlay-synchronizations.sql` | `scripts/database/overlay/parking-structure-synchronizations.sql` |
| parking-db | smartpark_parking | — | `05-overlay-operator.sql` | `scripts/database/overlay/parking-operator-tables.sql` |
| parking-db | smartpark_parking | — | `06-overlay-slot-constraints.sql` | `scripts/database/overlay/parking-slot-constraints.sql` |
| reservation-db | smartpark_reservation | `SMARTPARK_DB_RESERVATION_PORT` (5443) | `01-base.sql` | `scripts/database/microservices/03b-reservation-operations-service-db.sql` |
| reservation-db | smartpark_reservation | — | `02-overlay-capacity-pools.sql` | `scripts/database/overlay/reservation-site-capacity-pools.sql` |
| reservation-db | smartpark_reservation | — | `03-overlay-structure-safety.sql` | `scripts/database/overlay/reservation-structure-safety.sql` |
| reservation-db | smartpark_reservation | — | `04-overlay-receipts.sql` | `scripts/database/overlay/reservation-structure-receipts.sql` |
| reservation-db | smartpark_reservation | — | `05-overlay-confirmation.sql` | `scripts/database/overlay/reservation-confirmation-time.sql` |
| payment-db | smartpark_payment | `SMARTPARK_DB_PAYMENT_PORT` (5444) | `01-base.sql` | `scripts/database/microservices/04-payment-pricing-service-db.sql` |
| notification-db | smartpark_notification | `SMARTPARK_DB_NOTIFICATION_PORT` (5445) | `01-base.sql` | `scripts/database/microservices/05-notification-audit-service-db.sql` |
| iot-db | smartpark_iot | `SMARTPARK_DB_IOT_PORT` (5446) | `01-base.sql` | `scripts/database/microservices/06-iot-recognition-service-db.sql` |

## Lý do / quyết định liên quan
- **D1 (T4):** map 6 DB vật lý — `user→smartpark_user`, `parking→smartpark_parking`, `reservation→smartpark_reservation`, `payment→smartpark_payment`, `notification→smartpark_notification`, `iot→smartpark_iot`; tên connection-string/port env **không đổi**.
- **Q1 (T4):** triển khai cả 6 DB vật lý ngay trong `setup.ps1` để tránh phải sửa script lại sau.
- **Risk #2 (plan):** overlay `05.1–05.8` cũ nếu apply trùng → regression (permissions, plate index) ⇒ compose **không** mount chúng nữa.
- **Risk #3 (plan):** volume cũ = schema cũ nên init script không chạy lại ⇒ cần `-RecreateDb` (xem report 07).
- **Risk #7 (plan):** compose của worktree arch bị broken ⇒ viết compose mới theo bảng map, không tái dùng.

## Evidence
`git diff 0c8a6fb -- compose.schema-integration.yml` (trích hunk chính, đã lược):

```diff
@@ user-db @@
-      - ./scripts/database/microservices/01-user-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
-      - ./scripts/database/integration/01-user-service-alignment.sql:/docker-entrypoint-initdb.d/02-alignment.sql:ro
-      - ./scripts/database/05.8-Account-Workflow-Vehicles.sql:/docker-entrypoint-initdb.d/03-workflows.sql:ro
+      - ./scripts/database/microservices/01-user-access-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
…
@@ parking-db @@
-      - ./scripts/database/microservices/02-facility-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
-      - ./scripts/database/integration/02-facility-service-alignment.sql:/docker-entrypoint-initdb.d/02-alignment.sql:ro
-      - ./scripts/database/05.9-Parking-Backup-Operations.sql:/docker-entrypoint-initdb.d/03-workflows.sql:ro
-      - ./scripts/database/05.11-Parking-Structure-Synchronization.sql:/docker-entrypoint-initdb.d/04-synchronization.sql:ro
+      - ./scripts/database/microservices/02-facility-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
+      - ./scripts/database/microservices/03a-parking-structure-ops-service-db.sql:/docker-entrypoint-initdb.d/02-base.sql:ro
+      - ./scripts/database/overlay/parking-tenants.sql:/docker-entrypoint-initdb.d/03-overlay-tenants.sql:ro
+      - ./scripts/database/overlay/parking-structure-synchronizations.sql:/docker-entrypoint-initdb.d/04-overlay-synchronizations.sql:ro
+      - ./scripts/database/overlay/parking-operator-tables.sql:/docker-entrypoint-initdb.d/05-overlay-operator.sql:ro
+      - ./scripts/database/overlay/parking-slot-constraints.sql:/docker-entrypoint-initdb.d/06-overlay-slot-constraints.sql:ro
…
@@ reservation-db @@
-      - ./scripts/database/microservices/03-reservation-session-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
-      - ./scripts/database/integration/03-reservation-structure-safety.sql:/docker-entrypoint-initdb.d/02-alignment.sql:ro
-      - ./scripts/database/05.10-Reservation-Confirmation-Time.sql:/docker-entrypoint-initdb.d/03-workflows.sql:ro
-      - ./scripts/database/05.12-Reservation-Structure-Receipts.sql:/docker-entrypoint-initdb.d/04-receipts.sql:ro
+      - ./scripts/database/microservices/03b-reservation-operations-service-db.sql:/docker-entrypoint-initdb.d/01-base.sql:ro
+      - ./scripts/database/overlay/reservation-site-capacity-pools.sql:/docker-entrypoint-initdb.d/02-overlay-capacity-pools.sql:ro
+      - ./scripts/database/overlay/reservation-structure-safety.sql:/docker-entrypoint-initdb.d/03-overlay-structure-safety.sql:ro
+      - ./scripts/database/overlay/reservation-structure-receipts.sql:/docker-entrypoint-initdb.d/04-overlay-receipts.sql:ro
+      - ./scripts/database/overlay/reservation-confirmation-time.sql:/docker-entrypoint-initdb.d/05-overlay-confirmation.sql:ro
…
@@ payment/notification/iot @@
-      - ./scripts/database/microservices/04-payment-service-db.sql:…
+      - ./scripts/database/microservices/04-payment-pricing-service-db.sql:…
-      - ./scripts/database/microservices/05-notification-service-db.sql:…
+      - ./scripts/database/microservices/05-notification-audit-service-db.sql:…
-      - ./scripts/database/microservices/06-iot-service-db.sql:…
+      - ./scripts/database/microservices/06-iot-recognition-service-db.sql:…
```

Kiểm chứng 15/15 source path bằng `Test-Path` (kết quả thực tế): `TOTAL=15 OK=15 MISSING=0`; quét legacy mount: `LEGACY/SEED MOUNTS: none`.

## Ảnh hưởng
- `docker compose -f compose.schema-integration.yml config` không còn fail vì mount file đã xoá.
- Init order trong mỗi DB được ép bằng tiền tố số; overlay chạy sau base.
- 3 DB mới (`payment`, `notification`, `iot`) có container + volume riêng; chưa có BE service tương ứng (theo Q1).
- Không đụng tới `docker-compose.yml` (legacy) hay `compose.sonar.yml`.

## Cách tái kiểm chứng
```powershell
git diff 0c8a6fb -- compose.schema-integration.yml
docker compose -f compose.schema-integration.yml config
# liệt kê + kiểm tra tồn tại mọi source .sql trong compose:
Select-String -Path compose.schema-integration.yml -Pattern '\.sql:/docker-entrypoint-initdb.d'
```
