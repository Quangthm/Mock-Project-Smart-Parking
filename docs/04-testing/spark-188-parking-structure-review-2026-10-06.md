# SPARK-188 — Parking Structure review

Date: 2026-10-06. Scope: backend core (domain/application/PostgreSQL repository), without an HTTP contract or frontend changes. Reference: SRS v0.9 §3.2 and FR-LOT-01/02/03; existing PostgreSQL baseline 05.1.

## Implemented

- Separate ParkingService projects registered in SmartParking.slnx; no UserService assembly dependency.
- Owner/tenant-scoped site creation, listing, profile editing and guarded deactivation.
- Floor/zone hierarchy with optional floors for outdoor lots; sibling identifiers and descendant physical capacity limits.
- Explicit motorcycle/car categories, slot creation, movement, configuration/map associations and soft deletion preserving slot identity/history.
- Access paths with site-local endpoints and JSON map data.
- Atomic repository changes, current database authorization and site row locking.
- Re-runnable upgrade 05.6 tested against the actual baseline schema; no project database migration was applied.

## Review findings addressed

1. Owner IDs or tenant IDs alone do not authorize structure access. Every operation checks current ACTIVE user/account/tenant and OWNER role; role revocation and account lock are tested.
2. Outdoor layouts must not manufacture a floor. Root zones work independently of floors.
3. A single reserved/occupied status would contradict the SRS. Structure retains physical operational status/occupancy separately and reads active allocation/reservation/session commitments.
4. Destructive edits cannot discard occupancy or future entitlements. Occupied/UNKNOWN/allocated slots and site-wide live commitments block removal, relocation, compatibility/map changes and deactivation. Positive capacity pools conservatively require reconciliation first.
5. Slot-local guards miss unassigned walk-in sessions and capacity reservations. Site-wide guards include sessions without slots, reservations without targets, and unbounded reservation periods.
6. Concurrent reservation writes could invalidate a structure safety check. The upgrade adds commitment triggers taking the same site lock; tests cover a commitment transaction concurrent with slot removal and rejection of claims on deleted slots.
7. Sensor writes after soft deletion could revive occupancy. A database check prevents occupied/unknown deleted slots, including direct SQL writes.
8. Physical removal would cascade historical allocations. Deletions are soft; existing allocation history is verified after slot removal.
9. Multi-step mutations must not partially persist. Tests deliberately add a slot then fail another operation and verify that the transaction saves neither.
10. Legacy declared capacity can differ from materialized slots. The repository rejects edits with STRUCTURE_CONFLICT instead of silently rewriting capacity during a profile update.

## Validation

`dotnet test SmartParking.slnx --no-restore --verbosity minimal` with SMARTPARK_AUTH_TEST_CONNECTION pointing to the isolated existing test container (127.0.0.1:65433): **71 passed, 0 failed, 0 skipped** (11 ParkingService + 60 UserService regression tests).

Parking tests create and remove only a random `smartpark_structure_test_<UUID>` database. They run baseline 05.1 and upgrade 05.6 twice, exercise real Npgsql persistence, optional coordinates/map metadata, Owner/tenant authorization, invalid cross-site references, duplicate/concurrent identifiers, transaction rollback, capacity/occupancy/reservation/session protection, soft deletion, history retention and database guards.

`git diff --check` passed. New .NET projects build without warnings. Frontend build/browser QA is outside this change because no frontend files were edited. Pre-existing auth/registration/admin-user working-tree changes were preserved; no commit or push was performed.

## Remaining integration work

This completes the backend core that can be implemented without API design. It does **not** complete an end-to-end SPARK-188 feature: agree the HTTP contract, add host/controllers/auth adapter, provision Owner tenants and integrate FE. Current Owner approval does not provision tenants. Domain error codes have no agreed HTTP mapping yet.

Automatic reallocation/refund, policy-bound capacity reduction, operational/manual status overrides, IoT ingestion, tariffs and 3D rendering are outside this core. Destructive edits are conservatively refused until commitments are resolved. Identity checks use shared IAM tables pending a confirmed service-to-service identity boundary. Layout edits must go through the repository; external operational writers must respect the installed database guards and use consistent lock ordering/retry handling for transactions.

Implementation/handoff: [Parking Structure core](../02-architecture/service-boundaries/parking-structure-spark-188.md).
