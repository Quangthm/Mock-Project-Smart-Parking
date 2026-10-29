# SmartPark Database Architecture

## 1. Overview

SmartPark uses a **Database-per-Service** architecture. The recorded deployment profile has **6 physical PostgreSQL databases** and **7 base DDL files**, because the logical Operations service is split across two databases (`03a` → parking, `03b` → reservation) under T4 decision **D1**.

| Database | Service | Base DDL | Deployment overlays | Port |
|---|---|---|---|---|
| `smartpark_user` | User & Access | `01-user-access-service-db.sql` | — | 5441 |
| `smartpark_facility` | Facility + Parking Operations (03a) | `02-facility-service-db.sql`, `03a-parking-structure-ops-service-db.sql` | `parking-tenants`, `parking-structure-synchronizations`, `parking-operator-tables`, `parking-slot-constraints` | 5442 |
| `smartpark_operations` | Reservation Operations (03b) | `03b-reservation-operations-service-db.sql` | `reservation-site-capacity-pools`, `reservation-structure-safety`, `reservation-structure-receipts`, `reservation-confirmation-time` | 5443 |
| `smartpark_payment` | Payment & Pricing | `04-payment-pricing-service-db.sql` | — | 5444 |
| `smartpark_notification` | Notification & Audit | `05-notification-audit-service-db.sql` | — | 5445 |
| `smartpark_iot` | IoT & Recognition | `06-iot-recognition-service-db.sql` | — | 5446 |

This recorded physical topology includes deliberate compatibility debt; it is not a new ownership decision. The v1.1 target retains [service ownership](../service-boundaries/ownership-and-events.md) and [DB25–DB33 alignment work](alignment-backlog.md#v11-schema-and-seed-alignment-pending). User owns tenant identity/review, Facility configuration, Operations accepted occupancy/claims/discovery/coverage, Payment tariffs/financial terms and IoT raw observations. Cross-service references are **logical UUIDs**; there is no physical foreign key across databases. The overlays are deployment composition for the live BE, not additional services.

## 2. Multi-tenancy & security boundary

- `tenant_id` is intentionally denormalized across most business tables so a service can enforce tenant scope without a cross-DB JOIN.
- **Row-Level Security is not enabled** in the deployed schema. The schema stores `tenant_id` for application scoping, but enabling RLS requires explicit policies and roles as a separate step.
- Deployed tables use **simple intra-database foreign keys**; the schema does not rely on composite `(id, tenant_id, site_id)` foreign keys for tenant isolation. Cross-tenant authorization is enforced by application validation and logical IDs.
- No database contains a foreign key to another service's database.

## 3. Spatial hierarchy

The Facility database models physical infrastructure as:

```text
parking_sites -> spatial_units -> parking_slots
```

- `spatial_units` supports `ZONE`, `FLOOR`, and `BLOCK` through `unit_type` and a recursive `parent_id`.
- `path TEXT` stores a materialized path for subtree queries; it is maintained by the application.
- Active-name uniqueness is enforced by `uq_active_unit_name_ci` on `(site_id, parent_id, lower(name)) WHERE deleted_at IS NULL`.
- Active-slot uniqueness is enforced by `uq_active_slot_code_ci` on `(spatial_unit_id, upper(slot_code)) WHERE deleted_at IS NULL`; the parking overlay adds `uq_site_slot_code_ci` on `(tenant_id, site_id, upper(slot_code))` so identifiers stay reserved after soft delete.
- Static capacity lives in `spatial_unit_capacities` (`max_capacity` only). Dynamic counters live in `site_capacity_pools` in the Operations database.

## 4. Hard invariants & concurrency

- **Reservation double-booking:** `reservations` uses `EXCLUDE USING gist` on `(tenant_id, site_id, normalized_plate, tstzrange(expected_start_time, expected_end_time))` for statuses `CONFIRMED`, `ALLOCATED`, and `PARKING`.
- **Slot allocation double-booking:** `slot_allocations` uses `EXCLUDE USING gist` on `slot_id` and the allocated time range. Pooled-capacity allocation is handled by the Reservation capacity ledger, not by this table.
- **Monthly-pass overlap:** `monthly_passes` uses `EXCLUDE USING gist` per vehicle and validity range.
- **Capacity:** the `site_capacity_pools` overlay enforces `current_reserved_count >= 0`, `current_reserved_count <= max_reservation_quota`, and `max_reservation_quota <= total_capacity`. It also stores `pending_payment_count`, `occupied_count`, `protected_count`, and `backup_count`.
- **Payment idempotency:** `payment_orders.idempotency_key` is unique; `provider_webhooks` is unique on `(provider, provider_event_id)`; `payment_transactions` has a partial unique index on `(payment_method, gateway_transaction_id)` where `gateway_transaction_id IS NOT NULL`; `invoices.invoice_number` is unique.
- **Audit retention:** `structure_slot_audit` and `structure_change_audit` use `RESTRICT`/`SET NULL` instead of `CASCADE`; `audit_logs` is append-only by design.
- **State validation:** status and type columns use inline `CHECK` constraints.

## 5. State lifecycles

- **Physical state:** `parking_slots.physical_state` is one of `AVAILABLE`, `OCCUPIED`, `UNKNOWN`, `MAINTENANCE`, `UNAVAILABLE`.
- **Reservation projection:** the deployed `parking_slots` also carries `supported_vehicle_type` and `reservation_state` (`PROTECTED`, `RESERVED`, `BACKUP`, or `NULL`). These are T4-approved runtime columns (**D4/P1**) read and written by FacilityService; Operations/Reservation remain authoritative for reservation state. When the projection and Operations disagree, Operations wins.
- **Confirmation time:** `reservations.confirmed_at` exists in base `03b`; the `reservation-confirmation-time` overlay adds the trigger that stamps it.
- **Soft delete:** identities and physical resources use `deleted_at`; audit records are retained. D11/D12 additionally require explicit dependency handling, atomic same-lot deactivation and retained vehicle-binding history; existing cascades/columns are schema evidence, not proof of those behaviors (DB29/DB30).

## 6. Deployment artifacts & deviations

Runtime truth is defined by:

1. `scripts/database/microservices/*.sql` — 7 base files.
2. `scripts/database/overlay/*.sql` — 8 overlays.
3. `compose.schema-integration.yml` — 6 DB containers and mount order.
4. `scripts/apply-workflow-migrations.ps1` and `setup.ps1` — migration/probe orchestration.

The design baseline is `arch/database-schema` @ `9a13499`. The deployed branch deliberately differs in these T4-approved points:

| Mã | Deviation | Lý do |
|---|---|---|
| D1 | Split `03` into `03a` (parking) + `03b` (reservation); 6 physical DBs | Keep live BE data access unchanged |
| D2 | Keep `uq_active_vehicle_plate`; BE maps `23505` to HTTP 409 | SRS invariant + correct API behavior |
| D3/D6 | Operator tables and a BE-shape `support_tickets` in parking overlay | `/api/operator/*` works on a fresh DB |
| D4 | 7 role seeds; 2 `parking_slots` runtime columns; `unit_id`/`count` renames | Live BE reads/writes these names |
| D5 | `tenants` canonical in `01`, local copy in parking | FacilityService joins in one transaction |
| D7 | Recreate `site_capacity_pools` in reservation overlay | Shape was lost in the arch split |

Full evidence: `docs/04-testing/reports/refactor-Docker Setup & Function Test/01-approved-decisions-D1-D7.md` and `10-db-docs-deployment-alignment.md`.

## 7. v1.1 implementation boundary

Preserve the documented columns/defaults until separately approved schema work. `ACTIVE` is not proof of approval, completed provisioning or operational opening; yearly support cannot be inferred from a monthly table. DB25–DB33 define the target gaps. Development reset/seed waits for documentation approval and compatible implementation, with environment/owner/scripts/date TBD under DB31; no execution occurs here.
