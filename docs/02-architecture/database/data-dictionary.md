# SmartPark Data Dictionary — Deployed Profile (SRS v1.0)

This dictionary summarizes the data structures of the **deployed** branch `feature/db-hybrid-srs-v1`: 6 physical databases, 7 base DDL files, and 8 deployment overlays. It reflects the runtime schema, including T4-approved deviations from the arch design baseline.

## 1. Global persistence strategies

- **UTC:** all `TIMESTAMPTZ` values are stored in UTC; presentation converts to the site's local timezone.
- **GUIDs:** primary keys use `UUIDv4` (`uuid_generate_v4()`); tests may use fixed UUIDs.
- **Tenant scope:** `tenant_id` is denormalized on most business tables. Cross-database references (`tenant_id`, `site_id`, `account_id`, `slot_id`, `reservation_id`) are **logical UUIDs**, not physical FKs.
- **RLS:** Row-Level Security is **not enabled** in the deployed schema. It must be enabled with explicit policies if required.
- **Soft delete:** identities and physical resources use `deleted_at`; audit tables retain history via `RESTRICT`/`SET NULL`.

## 2. User & Access DB (`smartpark_user`)

Base: `01-user-access-service-db.sql`; no overlays.

| Table | Key columns / rules |
|---|---|
| `users` | `id`, `phone`, `email`, `password_hash`, `full_name`, `company_name`, `status` (`ACTIVE`, `INACTIVE`, `LOCKED`, `PENDING_APPROVAL`, `PENDING_VERIFICATION`, `REJECTED`), `failed_login_attempts`, `locked_until` |
| `tenants` | Organization root; `code`, `name`, `contact_email`, `contact_phone`, `tax_code`, `status` (`ACTIVE`, `INACTIVE`, `SUSPENDED`) |
| `accounts` | Contextual account; `user_id`, nullable `tenant_id`/`site_id`, `permissions`, `status`; one user may have multiple accounts |
| `roles` | **7 seeded codes:** `DRIVER`, `OWNER`, `OPERATOR`, `ADMIN`, `BUSINESS_OWNER`, `SITE_OPERATOR`, `PLATFORM_ADMIN` |
| `account_roles` | PK `(account_id, role_code)`; FKs to `accounts.id` (CASCADE) and `roles.code` (RESTRICT) |
| `vehicles` | `account_id`, `original_plate`, `normalized_plate`, `vehicle_type`; active-plate uniqueness via `uq_active_vehicle_plate` |
| `user_refresh_tokens` | Refresh-token lifecycle; `access_token_id` unique for strict revocation |
| `driver_registrations` | OTP hash, channel, expiry, failed attempts |
| `system_terms_and_conditions` | Versioned legal terms |
| `owner_applications` | Owner onboarding; `status` (`pending`, `approved`, `rejected`), review fields, `tenant_id` |
| `operator_grants` | 1-1 with operator account; permission array checked by `CHECK` constraints |
| `auth_challenges` | Short-lived authentication challenges |
| `workflow_deliveries` | Durable workflow/event delivery records |
| `mfa_credentials` | TOTP/MFA credentials |

Runtime role mapping: `PLATFORM_ADMIN`/`ADMIN` → admin; `BUSINESS_OWNER`/`OWNER` → owner; `SITE_OPERATOR`/`OPERATOR` → operator; `DRIVER` → driver.

## 3. Facility & Parking DB (`smartpark_parking`)

Base: `02-facility-service-db.sql` + `03a-parking-structure-ops-service-db.sql`.

| Table | Key columns / rules |
|---|---|
| `parking_sites` | `tenant_id`, `site_code`, `name`, `address`, `latitude`, `longitude`, `total_physical_capacity`, `status` (`PENDING_APPROVAL`, `ACTIVE`, `INACTIVE`, `MAINTENANCE`, `SUSPENDED`) |
| `spatial_units` | `tenant_id`, `site_id`, `parent_id`, `path`, `unit_type` (`ZONE`, `FLOOR`, `BLOCK`), `name`, `max_capacity` |
| `spatial_unit_capacities` | Static `max_capacity` per `(spatial_unit_id, vehicle_type)`; no dynamic counters |
| `parking_slots` | `slot_code`, `slot_type` (`STANDARD`, `EV`, `DISABLED`, `VIP`), `coordinates_3d`, `physical_state` (`AVAILABLE`, `OCCUPIED`, `UNKNOWN`, `MAINTENANCE`, `UNAVAILABLE`), runtime `supported_vehicle_type`, runtime `reservation_state` (`PROTECTED`, `RESERVED`, `BACKUP`, `NULL`) |
| `slot_compatibilities` | Allowed `vehicle_type` per slot; unique `(slot_id, vehicle_type)` |
| `parking_access_paths` | `path_code`, `from_unit_id`, `to_unit_id`, `map_data`; unique active code per site |
| `structure_operations` | Idempotent structure changes; `request`, `status` (`pending`, `failed`, `completed`), `physical_committed`; unique `(site_id, idempotency_key)` |
| `structure_slot_audit` | Slot audit; `site_id` RESTRICT, `slot_id` SET NULL |
| `structure_change_audit` | Before/after JSON snapshots; `site_id` RESTRICT, `operation_id` SET NULL |
| `structure_backup_policies` (03a) | `unit_id` (arch name `spatial_unit_id`), `vehicle_type`, `count` (arch name `backup_count`); unique site/unit policy indexes |
| `operator_incident_events` (03a) | `tenant_id`, `site_id`, `actor_id`, `incident_type`, `details`, `status` (`reported`, `resolved`) |

Deployment overlays: `tenants` local copy (D5), `structure_synchronizations` (D1/Q2), `support_tickets`/`operator_notifications`/`operator_audit_logs` (D3/D6), and slot constraints `ck_deleted_slot_safe` + `uq_site_slot_code_ci` (D4).

## 4. Reservation & Operations DB (`smartpark_reservation`)

Base: `03b-reservation-operations-service-db.sql`.

| Table | Key columns / rules |
|---|---|
| `operational_capacity_configs` | `site_id`, `spatial_unit_id`, `vehicle_type`, `total_physical_capacity`, `max_reservation_quota`, `is_active` |
| `operational_policies` | Arrival tolerance, payment hold, min/max reservation duration, protection window, allocation lead time |
| `reservations` | `tenant_id`, `site_id`, `account_id`, target unit/slot, plate, time range, `status` (incl. `UNFULFILLABLE`), `confirmed_at`, `hold_expires_at`, `version`; GiST exclusion per vehicle |
| `parking_sessions` | `reservation_id`, `access_item_id`, `current_slot_id`, plate, entry/exit times, `session_status` (`ACTIVE`, `PENDING_PAYMENT`, `COMPLETED`, `OVERSTAYED`, `DISPUTED`) |
| `slot_allocations` | `reservation_id`, `parking_session_id`, `slot_id`, allocated time range; GiST exclusion on slot + time |
| `access_items` | Card/QR/LicensePlate/RFID; `status` (`AVAILABLE`, `IN_USE`, `LOST`, `DISABLED`) |
| `monthly_passes` | `entitlement_type` only `WHEN_SPACE_AVAILABLE`; validity range; GiST exclusion per vehicle |
| `operational_incidents` | `incident_type` (`LOST_TICKET`, `UNKNOWN_SLOT_STATE`, `CHECKOUT_MISMATCH`, `OTHER`), `status` (`OPEN`, `IN_PROGRESS`, `RESOLVED`, `CLOSED`) |
| `search_facility_projections` | Search/read model: coordinates, operational status, open flag |
| `audit_logs` | Gate/operator audit; append-only |

Overlays: `site_capacity_pools` (D7) with `total_capacity`, `max_reservation_quota`, `current_reserved_count`, `pending_payment_count`, `occupied_count`, `protected_count`, `backup_count`; `structure_edit_holds`, `structure_site_state`, `structure_removed_resources` + guard triggers; `structure_release_receipts`; `confirmed_at` trigger.

## 5. Payment & Pricing DB (`smartpark_payment`)

Base: `04-payment-pricing-service-db.sql`; 7 tables: `tariffs`, `policy_revisions`, `payment_orders`, `payment_transactions`, `invoices`, `refund_requests`, `provider_webhooks`. Key invariants: `payment_orders.idempotency_key` unique; `provider_webhooks` unique `(provider, provider_event_id)`; `invoices.invoice_number` unique; money `CHECK (amount >= 0)` and formula checks.

## 6. Notification & Audit DB (`smartpark_notification`)

Base: `05-notification-audit-service-db.sql`; 6 tables: `notification_templates`, `push_device_tokens`, `durable_notifications`, `support_tickets`, `support_ticket_messages`, `central_audit_projections`. `support_tickets` here is the SRS shape; the BE-shape copy in `smartpark_parking` is temporary D6 debt.

## 7. IoT & Recognition DB (`smartpark_iot`)

Base: `06-iot-recognition-service-db.sql`; 6 tables: `devices`, `lpr_events`, `sensor_telemetries`, `assistant_conversations`, `assistant_messages`, `assistant_tool_runs`.

## 8. Deployment deltas

| Mã | Delta |
|---|---|
| D1 | `03a` → `smartpark_parking`; `03b` → `smartpark_reservation` |
| D4 | 7 role seeds; 2 `parking_slots` runtime columns; `unit_id`/`count` renames |
| D5 | `tenants` canonical in User DB, local copy in parking |
| D6 | BE-shape `support_tickets` in parking overlay (temporary) |
| D7 | `site_capacity_pools` recreated in reservation overlay |

See `docs/04-testing/reports/refactor-Docker Setup & Function Test/10-db-docs-deployment-alignment.md` for the full decision record.
