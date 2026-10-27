# API documentation — v1.1 review candidate

This is a **v1.1 documentation review candidate based on approved decisions** (SPARK-296), not product acceptance or completed API approval. Requirements govern behavior; existing implementation is evidence only.

Start with [shared contracts and pending API work](v1.1-contracts.md). The following are the active, minimal designs; their explicit design-pending items require FE/BE agreement before implementation acceptance.

> **API Design Template:** All new and updated API endpoints must follow the standard specification template at [API Design Template](../../03-design/api/api-design-template.md).

| Capability | Active design |
|---|---|
| Partner Registration, existing Owner role | [Registration and first-lot setup](../../03-design/api/owner-register-api-design.md) |
| Initial application review and document replacement | [Admin review](../../03-design/api/admin-approve-owner-api-design.md) |
| Driver registration and verification | [Driver](../../03-design/api/driver-register-api-design.md) |
| Operator invitation and activation | [Operator](../../03-design/api/owner-create-operator-api-design.md) |
| Admin directory and restricted access changes | [Admin management](../../03-design/api/admin-manage-users-api-design.md) |
| Additional lots, structure, deactivation | [Facility](../../03-design/api/parking-structure-api-design.md) |
| Registered vehicles and soft removal | [Vehicles](../../03-design/api/vehicle-registration-api-design.md) |
| Geographic discovery, products and booking handoff | [Shared integration contracts](v1.1-contracts.md#discovery-and-parking-products) |

The [all-API compatibility entry](SmartPark-All-API-Design.md) preserves the U01–U33/P01–P13/R01–R03/D01 navigation to the historical implementation inventory. [The dated workflow entry](workflows-2026-10-08.md) points to preserved history and current contracts. Neither snapshot approves legacy service ownership or superseded behavior.

Historical implementation/review records, non-authoritative for v1.1: [Driver, 6 October](driver-registration-otp.md), [Owner, 6 October](owner-registration-approval.md), [Operator, 6 October](operator-provisioning.md), [Admin, 6 October](admin-user-management.md), [auth alignment](auth-design-alignment-2026-10-06.md), [coverage review](api-coverage-vs-srs-2026-10-06.md). Their past test results remain historical.
