# SPARK-188 — Parking Structure backend core

Implemented 2026-10-06 from SRS v0.9 §3.2, FR-LOT-01/02/03 and the existing PostgreSQL schema. The referenced task is **Parking Structure Management (BE)**. This deliverable implements domain, application service and PostgreSQL persistence without assuming an unapproved frontend HTTP contract.

## Ownership and usage

`src/Services/ParkingService` contains three .NET 10 projects: Domain, Application and Persistence. It does not depend on UserService assemblies. Identity is checked against the shared database's existing IAM tables as an interim adapter; a future service deployment can replace this adapter with an agreed identity contract.

Create one `NpgsqlDataSource` for the configured connection string, register `IParkingStructureRepository` as scoped with `PostgresParkingStructureRepository`, and register `ParkingStructureService` as scoped. The HTTP adapter must derive `OwnerScope.UserId` from validated authentication, never from an owner ID in a request. The repository verifies current user, tenant, account and `BUSINESS_OWNER` role on every operation. Only an ACTIVE, non-deleted user with ACTIVE BUSINESS_OPERATOR membership in an ACTIVE tenant may proceed. Approval currently does not provision a tenant; provisioning is required before this service can be used.

Application operations:

- `CreateAsync`, `ListAsync`, `GetAsync`: create/read tenant-scoped sites, including inactive sites.
- `AddUnitAsync`: BUILDING/FLOOR/ZONE/ROW/SECTION hierarchy. Root zones support outdoor lots with no artificial floor.
- `AddSlotAsync`: physical slots with explicit MOTORBIKE/SEDAN/SUV/VAN/TRUCK and STANDARD/VIP/EV_CHARGING/HANDICAPPED categories.
- `AddAccessPathAsync`: site-local optional entry/exit endpoints and JSON map data.
- `ChangeAsync`: trusted server-side domain action for UpdateProfile, SetActive, RenameUnit, SetUnitCapacity, RemoveUnit, MoveSlot, ConfigureSlot, RemoveSlot and RemoveAccessPath. This delegate is an internal transaction boundary, not a payload or script exposed to clients.

Example internal call:

```csharp
var site = await service.CreateAsync(owner, "SITE-01", "Central", "Address", ct: ct);
var zone = await service.AddUnitAsync(owner, site.Id, UnitType.ZONE, "Outdoor", capacity: 100, ct: ct);
await service.AddSlotAsync(owner, site.Id, zone, "M-01", VehicleType.MOTORBIKE, ct: ct);
await service.ChangeAsync(owner, site.Id, s => s.UpdateProfile("SITE-01", "Central Parking", "Address", 10.77m, 106.69m), ct);
```

## Storage and invariants

Apply `scripts/database/05.6-Parking-Structure.sql` after baseline `05.1-Database-Scripts.sql`. The upgrade is transactional and re-runnable. It adds `parking_access_paths`, case-insensitive active identifiers, a check against occupancy of deleted slots, and commitment triggers. Case-insensitive duplicate legacy identifiers must be resolved before applying the unique indexes; the migration intentionally fails rather than rename data silently. The occupancy check is NOT VALID for existing history but enforced for new writes. No production/project database was migrated in this task.

Existing parking_sites, spatial_units and parking_slots are used without renaming fields. Paths contain stable UUIDs; renaming units does not rewrite descendant paths. Parent-child ordering is BUILDING → FLOOR → ZONE → ROW → SECTION, allowing skipped levels. Unit capacity 0 means no configured layout limit, as in the existing schema default. Positive limits cover all descendant physical slots. Site total_physical_capacity is derived from non-deleted slots; client-provided totals cannot override it. Commercial capacity pools remain separate and are not inferred from layout counts.

Slot codes are unique within a spatial unit, unit names among siblings, site codes within a tenant and path codes within a site. Moves keep the slot UUID. Cross-site unit/path references and invalid enums, coordinates or JSON are rejected. ConfigureSlot updates vehicle compatibility, slot category and JSON map/features without writing occupancy. Map JSON is stored as supplied, limited to 65,536 characters, pending an agreed visualization schema. Legacy sites whose declared physical capacity differs from active slot count are readable but edits fail with STRUCTURE_CONFLICT until the layout is reconciled; profile edits must not silently reduce an existing declared capacity.

All changes use one transaction and a site row lock. Slot rows are locked before checking physical occupancy. Operational status OPERATIONAL + occupied flag expresses physical available/occupied; UNKNOWN, MAINTENANCE and BLOCKED correspond to unknown, maintenance and unavailable. Reservation/protection are not folded into that status. This task provides no operational-status override endpoint.

Deletion is soft and preserves historical allocations and sessions. Units must be empty and unreferenced by access paths before removal. Removal, movement, compatibility/map changes and deactivation reject occupied, unknown or actively allocated resources. Live sessions, active/future reservations (including unbounded periods), and configured positive capacity pools conservatively block destructive layout changes for the entire site. Adding capacity and changing profile/name are allowed. Automatic reallocation/refund is deliberately not emulated: until that workflow exists, resolve commitments first. This is a conservative restriction, not the complete SRS capacity-reduction workflow.

The upgrade's BEFORE INSERT/UPDATE triggers lock the affected site for reservations, allocations, sessions and capacity pools, then reject new live commitments to inactive/deleted sites or deleted/cross-site targets. Updates cannot transfer a commitment to another site. Historical/released references remain valid. Future multi-site reservation/allocation workflows should acquire site locks in a consistent order and retry PostgreSQL deadlock/serialization failures at their transaction boundary. Raw layout writes that bypass this repository are not supported.

## HTTP handoff still required

This core is buildable and tested but has no API host/controller, endpoint URLs, transport DTOs, API error mapping, FE integration, or OpenAPI specification. These remain necessary for an integrated SPARK-188 completion. Agree method/path, DTO casing, hierarchy representation, pagination, error/status mapping and revision/concurrency semantics with FE before implementing that adapter. Domain `StructureException.Code` values are internal and are not yet a public API contract. This task does not change frontend mocks, owner onboarding, tariffs, devices, operational overrides or reservation allocation.

Review and executed validation: [SPARK-188 review](../../04-testing/spark-188-parking-structure-review-2026-10-06.md).
