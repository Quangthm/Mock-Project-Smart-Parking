# Database scripts - read this before using anything here

This directory contains **two generations** of schema scripts. Only one of them is
current. Evaluated against the wrong one, the design docs and the running database
will disagree in ways that look like application bugs.

## 1. Canonical (use this)

| Path | What it is |
| :--- | :--- |
| `microservices/01-user-access-service-db.sql` | user / auth / access schema |
| `microservices/02-facility-service-db.sql` | parking lots, spatial units, slots, paths, operations |
| `microservices/03a-parking-structure-ops-service-db.sql` | structure-ops schema |
| `microservices/03b-reservation-operations-service-db.sql` | reservations, `slot_allocations`, sessions |
| `microservices/04-payment-pricing-service-db.sql` | payment, pricing, policy |
| `microservices/05-notification-audit-service-db.sql` | notification, audit |
| `microservices/06-iot-recognition-service-db.sql` | IoT recognition |
| `overlay/` | 8 post-schema migrations, applied **after** the six scripts |
| `seeds/` | demo data only, never schema |

The database is created by `compose.schema-integration.yml` (six databases, one per
service, all six scripts plus the eight overlays mounted as init scripts) and driven
by `setup.ps1`. There is no single-database compose file any more:
`docker-compose.yml` and `scripts/database/docker-compose-smartpark.yml` were deleted.

`05.11` and `05.12` also live in `overlay/` now, as
`overlay/parking-structure-synchronizations.sql` and
`overlay/reservation-structure-receipts.sql`. You do not need the top-level copies.

## 2. Legacy (do not use, do not evaluate against)

`05.1-Database-Scripts.sql` through `05.10-Reservation-Confirmation-Time.sql` are the
old single-database monolith plus its incremental patches. Their content was merged
and corrected into the six `microservices/` scripts. They are kept only for history.

`integration/01..03` are the originals of the structure-safety guards. The **applied**
copies are in `overlay/`; the integration folder is reference material.

Rules:

1. Do not mount any `05.x` file into `docker-entrypoint-initdb.d`.
2. Do not use `05.x` to judge whether a design document is correct. Use
   `microservices/` + `overlay/`.
3. Do not add new statements to `05.x`. Put them in `microservices/` or `overlay/`.
4. `scripts/apply-workflow-migrations.ps1` already hard-blocks `05.[1-9]*.sql` and
   `integration/0[123]*.sql` and throws "superseded". That is intentional.

## 3. Known divergences (why the rule matters)

| Topic | Legacy (`05.x`) | Canonical | Effect if the legacy file is used |
| :--- | :--- | :--- | :--- |
| `slot_allocations.allocation_status` | `VARCHAR(50) NOT NULL`, **no CHECK** | `CHECK IN ('RESERVED','OCCUPIED','RELEASED')` | Any string is accepted; `WHERE allocation_status IN ('RESERVED','OCCUPIED')` silently skips rows, so an occupied slot reads as free |
| `slot_allocations.allocation_mode` | present, `NOT NULL` | column does not exist | inserts written against canonical fail, and vice versa |
| Structure-safety `live` predicate (`reservations`) | `05.6` uses `CHECKED_IN`, omits `ALLOCATED` | `PENDING_PAYMENT, CONFIRMED, ALLOCATED, PARKING` | `CHECKED_IN` never matches (the real terminal state is `PARKING`), and allocated/occupied reservations are treated as not-live, so structure deletion is allowed over them |
| `parking_slots.reservation_state` | absent in `05.1` | nullable projection column | reservation projection cannot be stored |

The authoritative copies of the `live` predicate are
`overlay/reservation-structure-safety.sql:40` (applied) and
`integration/03-reservation-structure-safety.sql:20` (original). They agree.

## 4. Related

- Design baseline for the 5x4 slot state model: `docs/03-design/api/3d-digital-twin-api-design.md` section 3.2.
- State and allocation rules: `docs/05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md`.
- Setup walkthrough: `LOCAL-SETUP.md`.
