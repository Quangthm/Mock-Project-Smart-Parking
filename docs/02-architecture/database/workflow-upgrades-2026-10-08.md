# Workflow upgrades — 08/10/2026

> **Historical arch workflow — not part of the deployed 6-DB profile.** The current runner blocks legacy `05.1–05.9` and `integration/01–03`; equivalent behavior now lives in base `01`/`02`/`03a`/`03b` plus the 8 overlays. Do not follow the commands below on `feature/db-hybrid-srs-v1`; see `README.md` and `schema-integration.md`.

> **T4-B2 naming note:** this dated note is kept for history, but the target database names below are shown with their current names. At the time of this workflow (2026-10-08), the same databases were named `smartpark_parking` and `smartpark_reservation` (containers `parking-db`/`reservation-db`, volumes `parking-schema-data`/`reservation-schema-data`). Those old names were superseded by T4-B2 and appear here only as historical context.

Companion code release on `develop`: `feat(workflows): complete auth, vehicle and parking flows with QA handoff`.

Apply these service-specific upgrades after the canonical microservice baseline and `integration/01`, `02`, `03` overlays. They are not the legacy monolith migrations despite the `05.*` filename prefix.

| Upgrade under `scripts/database/` | Target DB | Purpose |
| --- | --- | --- |
| `05.8-Account-Workflow-Vehicles.sql` | `smartpark_user` | Contact normalization/verification, current permissions, auth challenges, encrypted delivery outbox, MFA, vehicle mappings, Operator grant constraints |
| `05.9-Parking-Backup-Operations.sql` | `smartpark_facility` | Backup capacity policies, idempotent operations and physical-commit marker, slot/structure audit |
| `05.10-Reservation-Confirmation-Time.sql` | `smartpark_operations` | Actual confirmation timestamps; historical unknown confirmation remains unknown |

The matching code checkout also contains byte-identical copies because its migration runner and integration tests currently load upgrades from the code checkout. These are the same upgrades: do not apply the branch copies twice as separate migrations. Compare SHA-256 file hashes before deployment if either branch changes.

Deployment operator: back up the target DB, stop its writers, confirm baseline/overlays already exist, then apply the corresponding upgrade transaction with `ON_ERROR_STOP`. Existing normalized contact collisions cause rollback; reconcile data rather than merging accounts or fabricating verification. Do not apply baseline CREATE TABLE files again to populated databases.

From the matching code checkout, with Docker databases started through this schema checkout's `compose.schema-integration.yml`:

```powershell
./scripts/apply-workflow-migrations.ps1 -Service User -SchemaRoot '<this-schema-checkout>'
./scripts/apply-workflow-migrations.ps1 -Service Parking -SchemaRoot '<this-schema-checkout>'
./scripts/apply-workflow-migrations.ps1 -Service Reservation -SchemaRoot '<this-schema-checkout>'
```

Then start User/Parking/Reservation APIs on ports 5035/5045/5055 using `scripts/run-schema-service.ps1` from the code checkout. Configure DB password, shared service key, QA SMTP/SMS and onboarding origin outside source. FE defaults to User port 5035 and Parking port 5045. Merely starting FE does not start these services. Legacy monolith demo contacts are not automatically copied into the service databases.

Tester guide, 53-request Postman collection and review evidence are in the code checkout at `docs/04-testing/tester-api-handoff-2026-10-08.md`. Record both branch commit SHAs and migration execution evidence. This commit does not deploy SQL or mark tester UAT complete.
