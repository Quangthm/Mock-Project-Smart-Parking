# Sprint 2 DB Validation & Edge Case Report

This report documents the validation of database constraints, specifically addressing the edge cases required for Sprint 2 acceptance criteria.

## 1. Constraint Testing Inventory

### 1.1 Unique Identity (Driver & Owner)
- **Scenario**: Attempt to register two drivers with the same email address.
- **Action**: `INSERT INTO users (email) VALUES ('driver@test.com')` (when it already exists).
- **Result**: `PASS`. PostgreSQL raises `23505: duplicate key value violates unique constraint "uq_active_user_email_normalized"`.

### 1.2 Ownership / Scope (Tenant Boundaries)
- **Scenario**: Attempt to create a spatial unit (Zone) belonging to Owner A (`tenant_id = 1111...`), but link it to a site belonging to Owner B.
- **Action**: `INSERT INTO spatial_units (tenant_id, site_id) VALUES ('1111...', 'SiteOfOwnerB')`.
- **Result**: `PASS`. PostgreSQL raises `23503: insert or update on table "spatial_units" violates foreign key constraint "spatial_units_site_id_tenant_id_fkey"`. The composite FK strictly blocks cross-tenant hierarchy leaks.

### 1.3 Cycle / Cross-Lot Hierarchy
- **Scenario**: Attempt to link a spatial unit in Site A to a parent unit in Site B.
- **Action**: `INSERT INTO spatial_units (site_id, parent_id) VALUES ('SiteA', 'UnitInSiteB')`.
- **Result**: `PASS`. The constraint `FOREIGN KEY (parent_id, tenant_id, site_id) REFERENCES spatial_units(id, tenant_id, site_id)` guarantees that parent and child MUST share the exact same `site_id`.

### 1.4 Invalid Backup / Capacity Quantities
- **Scenario**: Attempt to reserve more slots than the allowed quota.
- **Action**: `UPDATE site_capacity_pools SET current_reserved_count = 50 WHERE max_reservation_quota = 40`.
- **Result**: `PASS`. PostgreSQL raises `23514: new row for relation "site_capacity_pools" violates check constraint "site_capacity_pools_check"`. Overbooking is blocked at the DB engine layer.

### 1.5 Duplicate Normalized Contacts
- **Scenario**: Attempt to add a vehicle with a plate that exists but with different casing/spacing.
- **Action**: Application normalizes plate to `30A12345`. Attempt to insert `30A12345` again.
- **Result**: `PASS`. Blocked by `uq_active_vehicle_plate`.

## 2. Unresolved Choices / Limitations
- **Backend Trigger Removal**: As noted in previous reviews, cross-database DB triggers (e.g., `guard_parking_structure_commitment`) are invalid in a microservices architecture. Validation of cross-service references (e.g. checking if a `target_slot_id` exists before making a reservation) MUST be performed by the application layer via gRPC/HTTP calls. This is documented and handed over to the BE team for refactoring.
