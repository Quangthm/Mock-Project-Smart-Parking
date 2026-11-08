# Database scripts - read this before using anything here

This directory contains **two generations** of schema scripts. Only one of them is
current. Evaluated against the wrong one, the design docs and the running database
will disagree in ways that look like application bugs.

## 1. Canonical (use this)

### User image upgrade and rollback (SPARK-373)

- Fresh UserDb base SQL includes media assets/holds and nullable avatar/vehicle asset FKs. For an initialized supported UserDb, use the reviewed `scripts/apply-workflow-migrations.ps1` path: existing session/outbox preflight, rerunnable the bounded media section of `microservices/01-user-access-service-db.sql`, then `test/verify-user-media-schema.sql` and existing verification. Do not use the media post-check to reject a pre-media database before applying its media section.
- Preserve existing rows, IDs, bindings and legacy URL columns. No automatic backfill or external URL download occurs. Run `test/inventory-user-media.sql` only with authorization for the intended UserDb; its aggregate categories distinguish managed metadata, pending assets, external URLs, missing references and unresolved ownership without outputting personal/provider values. A ready row is not proof that remote bytes still exist. Real deployment counts have NOT RUN in this task.
- Reconcile unknown outcomes by the persisted exact locator and verified hash/byte count. Existing pending rows survive restart. Absent bytes require authorized same-key resubmission; exhausted/ambiguous retries require owner-led investigation. Never manufacture ownership from an HTTPS string.
- Rollback: disable `SMARTPARK_USER_MEDIA_ENABLED` and restart the affected User host through the approved operational process. Keep all metadata and legacy columns. This stops media operations but does **not** restore the removed HTTPS-string registration contract. A code rollback must restore its matching frontend/API together; newly managed images have no synthesized legacy provider URL and may require re-upload in old clients. Do not drop the additive schema or erase pending provider evidence. No application runtime/database rollback was executed here.
- Automatic physical deletion is deferred as [USER-MEDIA-CLEANUP-01](../../docs/05-project-management/decision-log/2026-11-06-user-media-cleanup-deferral.md). Expired reads are blocked now; byte removal, hold/delete serialization and provider original/derived/cache/backup proof remain pending. A later worker can query persisted deadlines; no process must have been counting down since admission.

This image migration does not implement Partner PDF versions, terms replacement, Admin review transport or DB25. [User image API contract](../../docs/03-design/api/user-images-api-design.md).

| Path | What it is |
| :--- | :--- |
| `microservices/01-user-access-service-db.sql` | user / auth / access schema |
| `microservices/02-facility-service-db.sql` | parking lots, spatial units, slots, paths, operations, structure sync, maintenance windows, operator tables |
| `microservices/03a-parking-structure-ops-service-db.sql` | structure-ops schema |
| `microservices/03b-reservation-operations-service-db.sql` | reservations, `slot_allocations`, sessions, capacity pools, structure safety holds, receipts, confirmation triggers |
| `microservices/04-payment-pricing-service-db.sql` | payment, pricing, policy |
| `microservices/05-notification-audit-service-db.sql` | notification, audit |
| `microservices/06-iot-recognition-service-db.sql` | IoT recognition |
| `seeds/` | demo data only, never schema |

The database is created by `compose.schema-integration.yml` (six databases, one per
service, canonical base scripts mounted as init scripts) and driven by `setup.ps1`.
- User & Access MFA, refresh/replay, and Owner lot-setup requirements are part of `microservices/01-user-access-service-db.sql`.
- Facility owner-onboarding, structure synchronizations, slot constraints, maintenance windows, and operator support tables are consolidated directly into `microservices/02-facility-service-db.sql`.
- Operation capacity pools, structure safety holds, edit guards, release receipts, and reservation confirmation triggers are consolidated directly into `microservices/03b-reservation-operations-service-db.sql`.

There is no single-database compose file any more:
`docker-compose.yml` and `scripts/database/docker-compose-smartpark.yml` were deleted.

## 2. Legacy (do not use, do not evaluate against)

`05.1-Database-Scripts.sql` through `05.12` are the old single-database monolith
and incremental patches. User-specific MFA, session-hardening and owner-setup
schema is consolidated into `microservices/01-user-access-service-db.sql`.
Facility schema is consolidated into `microservices/02-facility-service-db.sql`.
Operation/Reservation schema is consolidated into `microservices/03b-reservation-operations-service-db.sql`.

`integration/01..03` are the originals of the structure-safety guards. The **applied**
copies were migrated to `overlay/` and are now consolidated into the base scripts.

Rules:

1. Do not mount legacy `05.1`–`05.12` files into `docker-entrypoint-initdb.d`.
2. Do not use `05.x` to judge whether a design document is correct. Use `microservices/`.
3. Do not add new statements to `05.x`. Put them in `microservices/`.
4. `scripts/apply-workflow-migrations.ps1` registers only canonical base scripts.
   Its legacy guard rejects `05.1`–`05.12` and `integration/0[123]*.sql`.

## 3. Known divergences (why the rule matters)

| Topic | Legacy (`05.x`) | Canonical | Effect if the legacy file is used |
| :--- | :--- | :--- | :--- |
| `slot_allocations.allocation_status` | `VARCHAR(50) NOT NULL`, **no CHECK** | `CHECK IN ('RESERVED','OCCUPIED','RELEASED')` | Any string is accepted; `WHERE allocation_status IN ('RESERVED','OCCUPIED')` silently skips rows, so an occupied slot reads as free |
| `slot_allocations.allocation_mode` | present, `NOT NULL` | column does not exist | inserts written against canonical fail, and vice versa |
| Structure-safety `live` predicate (`reservations`) | `05.6` uses `CHECKED_IN`, omits `ALLOCATED` | `PENDING_PAYMENT, CONFIRMED, ALLOCATED, PARKING` | `CHECKED_IN` never matches (the real terminal state is `PARKING`), and allocated/occupied reservations are treated as not-live, so structure deletion is allowed over them |
| `parking_slots.reservation_state` | absent in `05.1` | nullable projection column | reservation projection cannot be stored |

The authoritative copy of the `live` predicate is now inside
`microservices/03b-reservation-operations-service-db.sql`.

## 4. Related

- Design baseline for the 5x4 slot state model: `docs/03-design/api/3d-digital-twin-api-design.md` section 3.2.
- State and allocation rules: `docs/05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md`.
- Setup walkthrough: `LOCAL-SETUP.md`.
