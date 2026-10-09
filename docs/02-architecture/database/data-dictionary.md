# SmartPark Data Dictionary (Sprint 2 Focus)

This dictionary details the microservice-owned data structures for Sprint 2, focusing on Authentication, Account States, Owner/Operator Scopes, Spatial Hierarchy, and Capacity.

## 1. Global Persistence Strategies
- **UTC/Local Time:** All timestamps (`TIMESTAMPTZ`) are stored in UTC. Local business time presentation (e.g., parking late tolerance or operational hours) must be converted dynamically by the frontend or backend application layer based on the Site's timezone configuration (to be defined in Site Settings).
- **Generated GUIDs:** `UUIDv4` is used for all primary keys. In tests, static UUIDs are used for deterministic seeds.
- **Normalization:** The schema uses 3NF structurally, except for denormalized `tenant_id` and `site_id` added to child tables to enforce **Row-Level Security (RLS)** and distributed validation without cross-DB JOINS.

## 2. User Service DB
| Table | Column | Type | Constraints / Rules |
|-------|--------|------|----------------------|
| `users` | `id` | UUID | PK |
| `users` | `phone` | VARCHAR(20) | NULLABLE. Modified per SPARK-176. |
| `users` | `email` | VARCHAR(255) | NULLABLE. UNIQUE index on `lower(email)`. |
| `users` | `status` | VARCHAR | CHECK: `ACTIVE`, `INACTIVE`, `LOCKED`, `PENDING_APPROVAL` |
| `users` | `failed_login_attempts` | INT | DEFAULT 0. Locks user at 3 attempts. |
| `owner_applications`| `user_id` | UUID | UNIQUE FK to `users.id`. Status: `pending`, `approved`, `rejected` |
| `driver_registrations`| `code_hash` | TEXT | OTP hash storage. Retries locked after 3 attempts. |
| `operator_grants` | `account_id` | UUID | PK. FK to `accounts.id`. Stores permission arrays. |
| `user_refresh_tokens`| `access_token_id` | UUID | UNIQUE. Enforces 1-to-1 strict token revocation. |

## 3. Parking Service DB
| Table | Column | Type | Constraints / Rules |
|-------|--------|------|----------------------|
| `parking_sites` | `tenant_id` | UUID | FK to `tenants(id)`. Part of composite PK for children. |
| `spatial_units` | `id, tenant_id, site_id` | UUID | Composite UNIQUE constraint to enforce scope boundaries. |
| `spatial_units` | `unit_type` | VARCHAR | CHECK: `ZONE`, `FLOOR`, `BLOCK`. Supports both floorless and multifloor. |
| `spatial_units` | `path` | TEXT | Materialized Path for hierarchy traversals. |
| `parking_slots` | `physical_state`| VARCHAR | CHECK: `AVAILABLE`, `OCCUPIED`, `UNKNOWN`, `MAINTENANCE`. |

## 4. Reservation & Session Service DB (Capacity)
| Table | Column | Type | Constraints / Rules |
|-------|--------|------|----------------------|
| `site_capacity_pools`| `current_reserved_count`| INT | `CHECK (current_reserved_count <= max_reservation_quota)` |
| `site_capacity_pools`| `backup_count` | INT | Tracks backup/emergency quotas per BR-CAP-01-05 |
| `reservations` | `expected_end_time` | TIMESTAMPTZ | `CHECK (expected_end_time > expected_start_time)` |
