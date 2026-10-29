# Account Role Simplification & Authorization Consolidation — Implementation Report

SPARK-248 / US-U01 · 28 October 2026 · implementation and isolated verification complete; development deployment remains manual.

## 1. Summary

Preserved the existing feature implementation and completed the approved direct-role and focused authorization consolidation. No branch switch/reset/stash, commit or push was performed.

Resumed branch: `feat/SPARK-248-manage-own-profile`, HEAD `7a0532a4cc601bf354aacaea8e6e067816d4ec17` (the user's develop merge). Earlier session HEAD `79fa3708` was treated as historical evidence. Staged additions and unstaged edits were inspected together and preserved; the index was not changed. The task-owned session record moved to `ai/tasks/completed/` after validation.

Final working-tree status: 23 staged additions; 60 unstaged modifications; 5 unstaged deletions; 6 untracked deliverables; and the original staged active-session addition now deleted in the working tree (`AD`) because its completed copy is untracked. The net changed-file inventory below contains 94 paths.

```text
User 1:N Account
Account -> exactly one Role (roles.code)
Account 1:N AuthSession
AuthSession -> immutable UserId + AccountId
Refresh -> same persisted AccountId
```

## 2. Database Changes

**Confirmed root cause: DATABASE DEPLOYMENT/SCHEMA DRIFT.** The user confirmed that script 13 was not applied to the User Service database. The source already contained AuthSession.AccountId, EF mapping and account-bound persistence. No authentication rewrite was needed to explain the missing column.

Compose initializes User base01 only. The existing workflow runner checked base relations and could report “current” without checking the SPARK-248 prerequisites. It now explicitly reports base-only verification and names the required manual steps. Tests previously used a fuller sequence including 13/14, explaining why they did not reveal a base-only deployment.

| Concern | Domain | EF | Base01 | Script13 | Script14 | Test setup / disposable DB | User development DB |
|---|---|---|---|---|---|---|---|
| User → Account | User.Accounts / Account.UserId | Required FK | User FK, cascade | Retained | — | Verified | Not inspected |
| Account role | Required RoleCode / Role | N:1, Restrict | Required varchar(50) FK → roles.code; nonunique index | — | — | Verified direct loading/seeds/constraints | Not inspected |
| AuthSession.UserId | Existing required Guid | FK; immutable after save | Required User FK, cascade | Protects updates; composite ownership | — | Verified | Not inspected |
| AuthSession.AccountId | Existing Guid? init; legacy NULL only | Nullable; immutable | Intentionally absent | Adds column | — | Present after13; pre13 row remains NULL | Reported absent; user confirms13 unapplied |
| Refresh hash | Existing TokenHash | Unique index | Unique token_hash | Retained | — | Verified; no plaintext token persistence | Not inspected |
| Session / access identity | Id / AccessTokenId | PK / unique | PK / unique | Retained | — | Verified; CAS rotation | Not inspected |
| Account ownership | Account.UserId | Alternate key (Id,UserId) | Required User FK | Adds alternate key and composite restrictive session FK | — | Invalid pairing/retargeting rejected | Not inspected |
| Session indexes | Existing token IDs | Composite Account/User and token uniqueness | Hash/access unique; user/expiry indexes | Composite Account/User index replaces old Account-only index | — | Verified | Not inspected |
| Profile outbox | Existing event contract | Existing framework model | Absent | — | Exact framework DDL, unchanged | Verified after14; atomicity tests pass | Deployment not verified |

The canonical base SQL now directly creates `accounts.role_code VARCHAR(50) NOT NULL REFERENCES roles(code) ON DELETE RESTRICT`. No role UUID, multi-role Account, junction table or role ALTER migration was introduced. Roles retain all seven existing codes. Multiple Accounts may share a Role; Users may own multiple Accounts. Seeds preserve intended roles and relationships, including the single seeded Admin in the seed test; no global Admin-singleton redesign was made.

Script13 remains a separate, repeatable session upgrade. Its new composite FK prevents sessions pairing an existing Account with the wrong existing User. Both session identity columns are immutable in EF and PostgreSQL. Existing non-null mismatched pairs fail constraint validation and roll back the script; no repair is guessed. Legacy NULL AccountId rows stay NULL and cannot authorize or refresh. Script14 remains separate and unchanged.

The new verifier reads catalog definitions in a read-only transaction. It checks role/session/User relationships, relevant uniqueness/indexes, immutable binding and framework outbox definitions. Failure includes manual deployment instructions. It never repairs schema or backfills data.

## 3. Entity / EF Changes

Account now has RoleCode and Role; Role has Accounts. AccountRole, its DbSet and mappings are removed. Entity style and BaseEntity conventions remain unchanged; Domain acquired no infrastructure dependencies. Account ownership now has an alternate key solely to support the session composite FK.

## 4. Authentication Changes

Login retains stable eligible Account-ID ordering and anonymous authentication. Token claims retain `sub`, `account_id`, `jti` and one existing normalized role string. Role resolution reads only the selected Account.RoleCode.

Refresh receives only a refresh token. Its hash loads an authoritative session, then a no-tracking User/Account graph. User eligibility includes current lock state; RotateAsync selects only previous.AccountId, verifies its User ownership/status/deletion/current role, and reissues for that same Account. The store CAS compares session ID, UserId, AccountId, previous access-token ID/hash, revocation and expiry; it updates only token identity/hash/access expiry. Absolute refresh expiry is retained. No ICurrentActor or unexpired access token is required by refresh, and no client AccountId can override the binding.

An old access-token role context is rejected when its role claim differs from the Account's current mapped role; an otherwise valid refresh uses the same Account's current role, following the existing design. Existing code aliases that map to the same public role remain equivalent at the token boundary; resource membership rules still use the approved actual role codes. Another Account never rescues an invalid bound Account.

Revocation scopes remain distinct:

- Session: logout revokes the validated exact session/token pair and preserves other sessions.
- Account: live current-access and refresh checks reject an ineligible Account without invalidating a different Account's sessions.
- User: global User status/security restrictions affect every Account; the existing Admin User-lock writer revokes all that User's sessions.

## 5. Authorization Consolidation

### Interfaces

ICurrentActor is preserved. ICurrentAccessService and ICurrentAccessStore generalize the profile foundation. IAccountAuthorizationService provides typed feature actions; IAuthorizationScopeStore supplies memberships/grants.

### Implementations

Application owns CurrentAccessService, CurrentAccessPolicy and AccountAuthorizationService. Persistence owns CurrentAccessStore and AuthorizationScopeStore. API's injected CurrentAccountBearerEvents adapts validated JWT identity to the same Application policy. No new authorization library or generic RBAC engine was added.

### Dependency Injection

ApplicationDependencyInjection and PersistenceDependencyInjection register the respective abstractions. Protected controllers dispatch through IMediator. The real composition test resolves affected handlers without opening external connections. No migrated controller queries AppDbContext or manually constructs an authorization service.

### Current Access

Every protected path first validates the exact User + Account + session and current role. Read paths use fresh facts; protected mutations repeat validation under their transaction. Logs use injected ILogger and safe account/decision/resource identifiers.

### Action Permissions

Preserved current arrays and defaults: Owner actions evaluate each eligible membership's own permissions; BACKUP_MARK remains explicit. Admin user management remains role-only while Owner-application review retains ACCOUNT_ADMIN. That existing distinction is reported rather than silently changed. Admin receives no implicit parking/operator/financial authority.

### Resource Scope

Owner scope is limited to eligible BUSINESS_OWNER tenant memberships with the action permission. Operator scope requires eligible SITE_OPERATOR memberships, current grants and active matching sites; one lot's grant cannot authorize another. Driver resources remain tied to eligible same-user Driver Accounts. Bound-account validity is always checked first; explicit same-user/same-role memberships never contribute a different role.

## 6. SPARK-248 Compatibility

Own-profile access remains valid across approved roles, independent of operational permission arrays. Role/status/permissions/identity/contact/avatar remain non-self-editable. Fresh authority, exact session checks, role mismatch rejection, shared-User serialization and outbox atomicity are retained.

Lock order is User → exact Account → referenced Role (FOR SHARE) → exact session. The Account lock protects RoleCode reassignment; shared Role locks avoid serializing unrelated Accounts with the same Role. Detached authority values and targeted User reload preserve stale-tracking protection. Existing profile API and event/outbox contracts remain unchanged.

## 7. Files Changed

Complete net inventory relative to resumed HEAD, including untracked deliverables. Removed files are shown as plain paths. The original staged active-session addition remains in the untouched index while its working copy is now under completed/.

| File | Change | Reason |
|---|---|---|
| [ai/tasks/completed/SESSION-20261027-account-role-authorization.md](../../ai/tasks/completed/SESSION-20261027-account-role-authorization.md) | Added | Completed task/session evidence, reconciliation and deployment handoff. |
| [docs/02-architecture/database/data-dictionary.md](../../docs/02-architecture/database/data-dictionary.md) | Updated | Align current model, authorization flow and separate manual deployment guidance. |
| [docs/02-architecture/database/microservices/01-user-access-service-db-docs.md](../../docs/02-architecture/database/microservices/01-user-access-service-db-docs.md) | Updated | Align current model, authorization flow and separate manual deployment guidance. |
| [docs/02-architecture/database/schema-integration.md](../../docs/02-architecture/database/schema-integration.md) | Updated | Align current model, authorization flow and separate manual deployment guidance. |
| [docs/03-design/api/own-profile-api-design.md](../../docs/03-design/api/own-profile-api-design.md) | Updated | Align current model, authorization flow and separate manual deployment guidance. |
| [docs/04-testing/account-role-authorization-reconciliation.md](../../docs/04-testing/account-role-authorization-reconciliation.md) | Added | Full implementation, file inventory and verification report. |
| [docs/04-testing/own-profile-outbox.md](../../docs/04-testing/own-profile-outbox.md) | Updated | Align current model, authorization flow and separate manual deployment guidance. |
| [docs/04-testing/own-profile-review.md](../../docs/04-testing/own-profile-review.md) | Updated | Align current model, authorization flow and separate manual deployment guidance. |
| [scripts/apply-workflow-migrations.ps1](../../scripts/apply-workflow-migrations.ps1) | Updated | Distinguish base relation checks from full UserService readiness. |
| [scripts/database/microservices/01-user-access-service-db.sql](../../scripts/database/microservices/01-user-access-service-db.sql) | Updated | Canonical fresh role schema, restrictive FK, nonunique index; no role migration. |
| [scripts/database/microservices/13-own-profile-session-account.sql](../../scripts/database/microservices/13-own-profile-session-account.sql) | Updated | Keep separate overlay; enforce composite ownership and immutable binding. |
| [scripts/database/seeds/demo/01-identity.sql](../../scripts/database/seeds/demo/01-identity.sql) | Updated | Preserve demo identities/scopes with direct role assignments. |
| [scripts/database/verify-user-access-schema.sql](../../scripts/database/verify-user-access-schema.sql) | Added | Read-only definition checks and actionable manual deployment errors. |
| [scripts/seed-demo-site.ps1](../../scripts/seed-demo-site.ps1) | Updated | Use direct role predicates in demo setup. |
| [src/Services/UserService/SmartParking.UserService.API/Controllers/AccountWorkflowController.cs](../../src/Services/UserService/SmartParking.UserService.API/Controllers/AccountWorkflowController.cs) | Updated | Keep protected HTTP paths thin; dispatch through Application MediatR. |
| [src/Services/UserService/SmartParking.UserService.API/Controllers/AuthController.cs](../../src/Services/UserService/SmartParking.UserService.API/Controllers/AuthController.cs) | Updated | Keep protected HTTP paths thin; dispatch through Application MediatR. |
| [src/Services/UserService/SmartParking.UserService.API/Controllers/IdentityScopeController.cs](../../src/Services/UserService/SmartParking.UserService.API/Controllers/IdentityScopeController.cs) | Updated | Keep protected HTTP paths thin; dispatch through Application MediatR. |
| [src/Services/UserService/SmartParking.UserService.API/Controllers/OperatorsController.cs](../../src/Services/UserService/SmartParking.UserService.API/Controllers/OperatorsController.cs) | Updated | Keep protected HTTP paths thin; dispatch through Application MediatR. |
| [src/Services/UserService/SmartParking.UserService.API/Controllers/OwnerApplicationsController.cs](../../src/Services/UserService/SmartParking.UserService.API/Controllers/OwnerApplicationsController.cs) | Updated | Keep protected HTTP paths thin; dispatch through Application MediatR. |
| [src/Services/UserService/SmartParking.UserService.API/Controllers/UsersController.cs](../../src/Services/UserService/SmartParking.UserService.API/Controllers/UsersController.cs) | Updated | Keep protected HTTP paths thin; dispatch through Application MediatR. |
| [src/Services/UserService/SmartParking.UserService.API/Extensions/ApiAuthenticationExtensions.cs](../../src/Services/UserService/SmartParking.UserService.API/Extensions/ApiAuthenticationExtensions.cs) | Updated | Register the bearer-events adapter through API DI. |
| [src/Services/UserService/SmartParking.UserService.API/Extensions/ApiErrorResponseExtensions.cs](../../src/Services/UserService/SmartParking.UserService.API/Extensions/ApiErrorResponseExtensions.cs) | Updated | Preserve shared challenge/forbid envelope with injected bearer events. |
| [src/Services/UserService/SmartParking.UserService.API/Services/CurrentAccountBearerEvents.cs](../../src/Services/UserService/SmartParking.UserService.API/Services/CurrentAccountBearerEvents.cs) | Added | Injected ASP.NET adapter for shared current-access validation. |
| [src/Services/UserService/SmartParking.UserService.Application/ApplicationDependencyInjection.cs](../../src/Services/UserService/SmartParking.UserService.Application/ApplicationDependencyInjection.cs) | Updated | Register shared current-access and authorization services. |
| [src/Services/UserService/SmartParking.UserService.Application/Authorization/AccessRoles.cs](../../src/Services/UserService/SmartParking.UserService.Application/Authorization/AccessRoles.cs) | Added | Centralize existing external role aliases without changing codes. |
| [src/Services/UserService/SmartParking.UserService.Application/Authorization/AccountAuthorizationService.cs](../../src/Services/UserService/SmartParking.UserService.Application/Authorization/AccountAuthorizationService.cs) | Added | Typed action and resource policies with per-membership permissions. |
| [src/Services/UserService/SmartParking.UserService.Application/Authorization/CurrentAccessContext.cs](../../src/Services/UserService/SmartParking.UserService.Application/Authorization/CurrentAccessContext.cs) | Added | Carry authoritative User, Account, Session and current role. |
| [src/Services/UserService/SmartParking.UserService.Application/Authorization/CurrentAccessPolicy.cs](../../src/Services/UserService/SmartParking.UserService.Application/Authorization/CurrentAccessPolicy.cs) | Added | Generalize exact-actor/session/current-state policy from SPARK-248. |
| [src/Services/UserService/SmartParking.UserService.Application/Authorization/CurrentAccessService.cs](../../src/Services/UserService/SmartParking.UserService.Application/Authorization/CurrentAccessService.cs) | Added | Inject actor, fact store, clock and structured logger. |
| [src/Services/UserService/SmartParking.UserService.Application/Authorization/ScopeQueries.cs](../../src/Services/UserService/SmartParking.UserService.Application/Authorization/ScopeQueries.cs) | Added | Application queries for Owner and Operator scopes. |
| [src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/IAuthorizationScopeStore.cs](../../src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/IAuthorizationScopeStore.cs) | Added | Membership and grant fact port. |
| [src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/ICurrentAccessStore.cs](../../src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/ICurrentAccessStore.cs) | Added | Authoritative current-access fact/lock port. |
| `src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/IProfileAccessStore.cs` | Removed | Replace with generalized ICurrentAccessStore. |
| [src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/IUserRepository.cs](../../src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/IUserRepository.cs) | Updated | Remove unused competing profile locking API. |
| [src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/IVehicleRepository.cs](../../src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/IVehicleRepository.cs) | Updated | Remove role-specific authorization query from vehicle port. |
| [src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Services/IAccountAuthorizationService.cs](../../src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Services/IAccountAuthorizationService.cs) | Added | Typed action/resource authorization port. |
| [src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Services/ICurrentAccessService.cs](../../src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Services/ICurrentAccessService.cs) | Added | Application current-access port for read/locked validation. |
| [src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Services/IOperatorProvisioningService.cs](../../src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Services/IOperatorProvisioningService.cs) | Updated | Remove obsolete permission/assignment authorization methods. |
| [src/Services/UserService/SmartParking.UserService.Application/Profiles/Commands/UpdateOwnProfileCommand.cs](../../src/Services/UserService/SmartParking.UserService.Application/Profiles/Commands/UpdateOwnProfileCommand.cs) | Updated | Use shared locking guard; retain profile/outbox transaction. |
| `src/Services/UserService/SmartParking.UserService.Application/Profiles/OwnProfileAccess.cs` | Removed | Replace with generalized CurrentAccessPolicy. |
| [src/Services/UserService/SmartParking.UserService.Application/Profiles/Queries/GetOwnProfileQuery.cs](../../src/Services/UserService/SmartParking.UserService.Application/Profiles/Queries/GetOwnProfileQuery.cs) | Updated | Use shared current-access guard. |
| [src/Services/UserService/SmartParking.UserService.Application/Services/AccessTokenService.cs](../../src/Services/UserService/SmartParking.UserService.Application/Services/AccessTokenService.cs) | Updated | Resolve one role directly from the selected Account. |
| [src/Services/UserService/SmartParking.UserService.Application/Services/VehicleRegistrationService.cs](../../src/Services/UserService/SmartParking.UserService.Application/Services/VehicleRegistrationService.cs) | Updated | Use typed Driver membership authorization within existing transaction. |
| [src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/ManagementRequestValidators.cs](../../src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/ManagementRequestValidators.cs) | Added | Reuse existing DTO constraints in FluentValidation pipeline. |
| [src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/OperatorRequests.cs](../../src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/OperatorRequests.cs) | Added | Application MediatR entry points with typed action checks. |
| [src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/OwnerApplicationRequests.cs](../../src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/OwnerApplicationRequests.cs) | Added | Application reviewer queries/command. |
| [src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/OwnSecurityRequests.cs](../../src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/OwnSecurityRequests.cs) | Added | Application entry points for protected MFA/delivery operations. |
| [src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/UserManagementRequests.cs](../../src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/UserManagementRequests.cs) | Added | Application Admin queries/status command. |
| [src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/VehicleRequests.cs](../../src/Services/UserService/SmartParking.UserService.Application/Usecase/Management/VehicleRequests.cs) | Added | Application Driver vehicle queries/command. |
| `src/Services/UserService/SmartParking.UserService.Application/Usecase/Session/GetCurrentUserQuery.cs` | Removed | Remove redundant old current-user query. |
| [src/Services/UserService/SmartParking.UserService.Application/Usecase/Session/LogoutCommand.cs](../../src/Services/UserService/SmartParking.UserService.Application/Usecase/Session/LogoutCommand.cs) | Updated | Derive logout identity from trusted current access. |
| [src/Services/UserService/SmartParking.UserService.Application/Usecase/Session/RefreshCommand.cs](../../src/Services/UserService/SmartParking.UserService.Application/Usecase/Session/RefreshCommand.cs) | Updated | Validate current User lock state; leave Account choice to exact bound-session rotation. |
| [src/Services/UserService/SmartParking.UserService.Domain/Entities/Account.cs](../../src/Services/UserService/SmartParking.UserService.Domain/Entities/Account.cs) | Updated | Replace role collection with required RoleCode and direct Role navigation. |
| `src/Services/UserService/SmartParking.UserService.Domain/Entities/AccountRole.cs` | Removed | Remove obsolete junction entity. |
| [src/Services/UserService/SmartParking.UserService.Domain/Entities/Role.cs](../../src/Services/UserService/SmartParking.UserService.Domain/Entities/Role.cs) | Updated | Expose Accounts navigation; preserve Code identity. |
| [src/Services/UserService/SmartParking.UserService.Persistence/AppDbContext.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/AppDbContext.cs) | Updated | Map direct role and composite session ownership; enforce immutable session identifiers. |
| [src/Services/UserService/SmartParking.UserService.Persistence/DataSeeder.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/DataSeeder.cs) | Updated | Assign direct RoleCode in existing seed/bootstrap paths. |
| [src/Services/UserService/SmartParking.UserService.Persistence/PersistenceDependencyInjection.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/PersistenceDependencyInjection.cs) | Updated | Register current-access and scope fact stores. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/AccountWorkflowService.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/AccountWorkflowService.cs) | Updated | Direct-role login reads and injected protected security/delivery authorization. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/AuthorizationScopeStore.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/AuthorizationScopeStore.cs) | Added | Persistence-owned membership/grant queries and transaction locks. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/CurrentAccessStore.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/CurrentAccessStore.cs) | Added | Fresh reads and User/Account/Role/session locking protocol. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/DriverRegistrationService.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/DriverRegistrationService.cs) | Updated | Create DRIVER Account directly. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OperatorProvisioningService.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OperatorProvisioningService.cs) | Updated | Create SITE_OPERATOR Accounts directly; use typed Owner/site policy. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OwnerRegistrationService.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/OwnerRegistrationService.cs) | Updated | Create BUSINESS_OWNER Account directly and use shared reviewer policy. |
| `src/Services/UserService/SmartParking.UserService.Persistence/Repositories/ProfileAccessStore.cs` | Removed | Replace with generalized CurrentAccessStore. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/UserManagementService.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/UserManagementService.cs) | Updated | Direct-role projections and shared Admin guard; retain global revocation. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/UserRepository.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/UserRepository.cs) | Updated | Load direct Role; retain fresh no-tracking refresh reads. |
| [src/Services/UserService/SmartParking.UserService.Persistence/Repositories/VehicleRepository.cs](../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/VehicleRepository.cs) | Updated | Remove duplicated Driver authorization SQL. |
| [tests/Services/SmartParking.UserService.Tests/AuthAccountBindingTests.cs](../../tests/Services/SmartParking.UserService.Tests/AuthAccountBindingTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/AuthorizationPersistenceTests.cs](../../tests/Services/SmartParking.UserService.Tests/AuthorizationPersistenceTests.cs) | Added | Real provisioning, direct roles, scoped permissions and revoked grants. |
| [tests/Services/SmartParking.UserService.Tests/AuthorizationScopeTests.cs](../../tests/Services/SmartParking.UserService.Tests/AuthorizationScopeTests.cs) | Added | Role isolation, per-membership permissions, lot grants and real DI composition. |
| [tests/Services/SmartParking.UserService.Tests/AuthorizationTestSupport.cs](../../tests/Services/SmartParking.UserService.Tests/AuthorizationTestSupport.cs) | Added | Real authorization policies and persisted exact-session test actors. |
| [tests/Services/SmartParking.UserService.Tests/AuthSessionTests.cs](../../tests/Services/SmartParking.UserService.Tests/AuthSessionTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/AuthTestFixture.cs](../../tests/Services/SmartParking.UserService.Tests/AuthTestFixture.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/CurrentAccessTests.cs](../../tests/Services/SmartParking.UserService.Tests/CurrentAccessTests.cs) | Added | Role/catalogue/ownership/state/expiry rejection and profile permission independence. |
| [tests/Services/SmartParking.UserService.Tests/DirectRolePersistenceTests.cs](../../tests/Services/SmartParking.UserService.Tests/DirectRolePersistenceTests.cs) | Added | EF shape plus clean SQL/seed/FK/index behavior. |
| [tests/Services/SmartParking.UserService.Tests/DomainEntitiesAndUseCasesTests.cs](../../tests/Services/SmartParking.UserService.Tests/DomainEntitiesAndUseCasesTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/DriverRegistrationHttpTests.cs](../../tests/Services/SmartParking.UserService.Tests/DriverRegistrationHttpTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/DriverRegistrationTests.cs](../../tests/Services/SmartParking.UserService.Tests/DriverRegistrationTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/DriverWorkflowTests.cs](../../tests/Services/SmartParking.UserService.Tests/DriverWorkflowTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/OperatorProvisioningHttpTests.cs](../../tests/Services/SmartParking.UserService.Tests/OperatorProvisioningHttpTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/OwnerRegistrationHttpTests.cs](../../tests/Services/SmartParking.UserService.Tests/OwnerRegistrationHttpTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/OwnerRegistrationTests.cs](../../tests/Services/SmartParking.UserService.Tests/OwnerRegistrationTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/OwnProfilePersistenceTests.cs](../../tests/Services/SmartParking.UserService.Tests/OwnProfilePersistenceTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/OwnProfileTests.cs](../../tests/Services/SmartParking.UserService.Tests/OwnProfileTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/PostgresAuthTests.cs](../../tests/Services/SmartParking.UserService.Tests/PostgresAuthTests.cs) | Updated | Direct-role fixture and replacement Account; retain real rotation/revocation tests. |
| [tests/Services/SmartParking.UserService.Tests/ProfileAuthorizationRaceTests.cs](../../tests/Services/SmartParking.UserService.Tests/ProfileAuthorizationRaceTests.cs) | Updated | Replace role-assignment race with direct role change; retain lock ordering. |
| [tests/Services/SmartParking.UserService.Tests/ProfileOutboxTests.cs](../../tests/Services/SmartParking.UserService.Tests/ProfileOutboxTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/SessionOwnershipPersistenceTests.cs](../../tests/Services/SmartParking.UserService.Tests/SessionOwnershipPersistenceTests.cs) | Added | Composite ownership, legacy rejection, immutable binding and fresh refresh state. |
| [tests/Services/SmartParking.UserService.Tests/SessionSchemaReconciliationTests.cs](../../tests/Services/SmartParking.UserService.Tests/SessionSchemaReconciliationTests.cs) | Added | Exact base/13/14 sequence, negative readiness and actual pre-13 legacy session. |
| [tests/Services/SmartParking.UserService.Tests/UnsupportedVehicleRepository.cs](../../tests/Services/SmartParking.UserService.Tests/UnsupportedVehicleRepository.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/UserManagementTests.cs](../../tests/Services/SmartParking.UserService.Tests/UserManagementTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/VehicleRegistrationTests.cs](../../tests/Services/SmartParking.UserService.Tests/VehicleRegistrationTests.cs) | Updated | Update direct-role/trusted-actor fixtures and affected regression assertions. |
| [tests/Services/SmartParking.UserService.Tests/WorkflowTestSupport.cs](../../tests/Services/SmartParking.UserService.Tests/WorkflowTestSupport.cs) | Updated | Supply injected authorization; preserve existing test overlay sequence. |

## 8. Tests

| Test / suite | Result | Notes |
|---|---|---|
| Final focused SPARK-248/auth/schema/authorization/outbox | PASS | 95 passed, 0 failed, 0 skipped |
| Final full UserService suite | PASS | 193 passed, 0 failed, 0 skipped |
| Disposable base01 → 13 → 14 verifier, final recheck | PASS | 1 passed; tests each missing stage, legacy preservation, final schema |
| Solution PaymentService suite | PASS | 80 passed, 0 failed, 0 skipped |
| Solution ParkingService suite | PASS | 48 passed; 9 skipped |
| Parking PostgreSQL-dependent cases | SKIPPED | SMARTPARK_STRUCTURE_TEST_CONNECTION absent; unrelated fixture not configured |
| PowerShell runner syntax | PASS | Parser completed without errors |
| git diff --check HEAD | PASS | No whitespace errors |
| Development database deployment | NOT RUN | User-owned manual operation |
| Frontend tests | NOT RUN | No frontend changes |

The first focused run had 93 passes and one failure because the new verifier test checked PostgresException.Message instead of Hint. That assertion was corrected; subsequent focused/full/schema runs passed.

Commands executed from repository root (the ignored wrapper verifies container isolation and supplies only process-local test environment values):

```powershell
dotnet build SmartParking.slnx --no-restore --nologo

powershell -NoProfile -ExecutionPolicy Bypass -File .cache/run-role-authorization-tests.ps1 -Target tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj -Filter 'FullyQualifiedName~SessionSchemaReconciliationTests|FullyQualifiedName~SessionOwnershipPersistenceTests|FullyQualifiedName~AuthorizationPersistenceTests|FullyQualifiedName~AuthorizationScopeTests|FullyQualifiedName~DirectRolePersistenceTests|FullyQualifiedName~AuthAccountBindingTests|FullyQualifiedName~AuthSessionTests|FullyQualifiedName~CurrentAccessTests|FullyQualifiedName~OwnProfile|FullyQualifiedName~ProfileAuthorizationRaceTests|FullyQualifiedName~ProfileOutbox|FullyQualifiedName~PostgresAuthTests' -WithBroker

powershell -NoProfile -ExecutionPolicy Bypass -File .cache/run-role-authorization-tests.ps1 -Target SmartParking.slnx -WithBroker

powershell -NoProfile -ExecutionPolicy Bypass -File .cache/run-role-authorization-tests.ps1 -Target tests/Services/SmartParking.UserService.Tests/SmartParking.UserService.Tests.csproj -Filter FullyQualifiedName~SessionSchemaReconciliationTests

git diff --check HEAD
```

Wrapper execution is equivalent to `dotnet test <target> --no-build --no-restore --nologo --logger 'trx;LogFilePrefix=role-authorization' --results-directory .cache/role-authorization-results`, with the listed optional filter. For reproduction, configure SMARTPARK_AUTH_TEST_CONNECTION only for an explicitly isolated disposable server and SMARTPARK_PROFILE_RABBITMQ_PORT only for the isolated broker. Never substitute a development connection.

Evidence in ignored `.cache/role-authorization-results/`: focused `20261028231715`; solution Parking `20261028231757`, Payment `20261028231759`, User `20261028231839`; final schema `20261028233429`.

Only the pre-existing SPARK-248-labeled PostgreSQL/RabbitMQ containers were used, with tmpfs data, no persistent mounts and loopback ports. Generated test databases were guarded UUID names. Both containers were stopped afterward, restoring their original state. The actual development database was neither queried nor modified.

## 9. Build Result

PASS: `dotnet build SmartParking.slnx --no-restore --nologo`, zero warnings and errors (final build 16.10s). Existing packages were restored after an initial stale-assets/network-sandbox failure; no package versions changed.

## 10. Remaining Old-Model References

Search scope included source, tests, scripts, current docs and history, using:

```powershell
rg -n 'AccountRole|AccountRoles|account_roles|RoleIds|role assignments|AccountRole.role_code' src tests scripts docs ai
rg -n 'ThenInclude.*Role|Roles\.Any|SelectMany.*Role' src tests
rg -n 'AppDbContext|UserService.Persistence|IProfileAccessStore|OwnProfileAccess|new CurrentAccessService|new AccountAuthorizationService' src/Services/UserService/SmartParking.UserService.API/Controllers src/Services/UserService/SmartParking.UserService.Application
```

- **Runtime:** no AccountRole/account_roles model, query, insert, DbSet or old profile-only access port remains. Direct ThenInclude(a => a.Role) is intentional. Roles.Any on the catalogue is seed existence checking. Managed-user Roles arrays are display projections across Accounts, not JWT or authority aggregation. The existing GetByIdWithRolesAsync name now loads one direct Role per Account.
- **Tests:** DirectRolePersistenceTests and SessionSchemaReconciliationTests deliberately assert that the old table is absent.
- **Setup diagnostic:** verify-user-access-schema.sql deliberately rejects an obsolete table.
- **Current documentation:** model descriptions are updated; this report records the removed model for audit.
- The phrase “fixed role assignments” in the current deferred-and-implementation register describes the approved fixed-role business invariant, not a junction-table dependency; it is retained.
- **Historical/archive:** retained scripts/database/05.1-Database-Scripts.sql, historical docs/02-architecture/database/drawdb-schema.sql, decision logs dated 2026-10-05/2026-10-13, archived baseline review, and old Sonar coverage report.
- The legacy IsActiveAsync(tokenId,userId) overload has no production caller; tests use it for session state/legacy rejection. Production token validation uses the exact Account overload and protected operations use CurrentAccessService.

## 11. Unrelated Problems Found

Deferred without changes: legacy access-token lifetime/login payload; Admin singleton bootstrap; existing role-only user-management versus ACCOUNT_ADMIN reviewer distinction; legacy test-overlay permission/vehicle differences. ServiceSchema still runs its old alignment/05.8 sequence in existing tests; dedicated canonical/deployment tests now independently prove the supported current SQL. No account-switching UX, avatar mutation, consumer, onboarding/MFA redesign, frontend or unrelated service work was added.

## 12. Scope Deviations

None. Session composite ownership, refresh eligibility checks and read-only schema diagnostics are covered by the user's later reconciliation instructions. Scripts13/14 were not merged into base01; only the existing session overlay13 was strengthened.

## 13. Manual Action Required

**Development database reset/recreation remains the user's responsibility.** No scripts were applied to it.

For the intended fresh development rebuild, review/run canonical01, then the separate updated13, then14, and run verify-user-access-schema.sql with ON_ERROR_STOP=1 on the explicitly selected User database. Script14 and base01 are not blind rerun scripts. Existing data/schema requires review before any application. Legacy accountless sessions must sign in again.

Repository implementation: complete. Disposable database verification: passed. User database deployment: not performed; script13 was confirmed unapplied. Account switching and the excluded follow-ups remain deferred.

