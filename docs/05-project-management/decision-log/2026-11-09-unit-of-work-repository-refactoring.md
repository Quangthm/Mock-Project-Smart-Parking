# Deferred UserService unit-of-work repository refactoring

**Date:** 9 November 2026 (Asia/Saigon).
**Backlog ID:** USER-UOW-01 (repository tracking only; no Jira issue created).
**Status:** DEFERRED — documented at the user's request; refactoring not implemented.
**Owner:** UserService / backend tests.

## Context and current behavior

`IUnitOfWork` already exposes `IUserRepository UserRepository`. The production `UnitOfWork` receives that repository and `AppDbContext` through its constructor. Production persistence DI registers both interfaces separately.

`MediaUseCases` currently requests both `IUnitOfWork work` and `IUserRepository users`. Its two direct user-repository calls can instead use `work.UserRepository`. This is a concrete redundant dependency under the existing interface, not evidence that every repository must be added to the unit of work.

On 9 November, the HTTP fixture failed during dependency validation because it registered `IUnitOfWork` without separately registering `IUserRepository`. The fixture implements both interfaces, but DI does not automatically register every interface or traverse repository properties. The immediate test fix adds `TryAddSingleton<IUserRepository>(fixture)` to `TestAuthorization.AddServices`; 20 affected offline tests then passed. Retain that fix until the deferred change and its verification are complete.

## Deferred cases

| Case | Future work | Boundary |
|---|---|---|
| Duplicate user-repository dependency in `MediaUseCases` | Remove the `IUserRepository users` constructor parameter. Replace the calls to `users.GetByIdWithRolesAsync(...)` and `users.GetByIdForUpdateAsync(...)` with `work.UserRepository` calls. Update direct constructors and fixtures. | Preserve query semantics, cancellation, locking, authorization, transaction boundaries and save ordering. Other constructor dependencies remain necessary. |
| Fixture registration after constructor simplification | Audit all consumers of the shared HTTP fixture. Remove its direct `IUserRepository` registration only if no remaining resolved service requires it; keep an explicit registration wherever it is still required. | Do not disable DI validation, filter out real handlers or substitute a real database merely to make startup pass. |
| Similar redundant constructor dependencies | Inspect other UserService handlers for repositories already exposed by their injected unit of work; record and refactor confirmed duplicates individually. | Broader duplication is **OPEN / not established**. `IUnitOfWork` currently exposes only the user repository. Direct vehicle, claim, media and identity-access repository dependencies are not automatically redundant. |

## DI and architecture constraints

- Handler dependencies and container registrations are separate concerns. A handler may access users only through `IUnitOfWork`, while production DI must still register `IUserRepository` to construct `UnitOfWork`.
- Preserve scoped production registrations and the shared owning `AppDbContext`; this refactoring must not create a second repository/context or change atomicity.
- Do not turn this item into a universal repository-container convention, expand `IUnitOfWork` to every repository, or introduce a generic service locator. Broader architectural changes require their own review and decision.
- No schema, API, environment, package, or business-behavior change is intended.

## Acceptance and verification

- [ ] `MediaUseCases` uses `work.UserRepository` for both existing user reads and no longer injects `IUserRepository` directly.
- [ ] Update every direct construction site; production DI still constructs `UnitOfWork` and all media handlers with validation enabled.
- [ ] Shared HTTP fixtures resolve all application handlers; justify retaining or removing the temporary direct repository registration based on actual consumers.
- [ ] Rebuild the solution and rerun the affected offline classes: `OwnProfileTests`, `DriverRegistrationHttpTests`, `OwnerRegistrationHttpTests`, `OperatorProvisioningHttpTests`, and the offline `UserManagementTests` case.
- [ ] Run relevant media read/attachment tests, including PostgreSQL-backed avatar attachment and locking/transaction behavior where applicable. Use isolated test infrastructure from `tests/README.md`; disclose any unrun integration checks.
- [ ] Preserve response contracts, Account/User ownership, current-access checks, and existing transaction behavior.

**Verification status:** Refactoring NOT IMPLEMENTED / NOT RUN. The earlier 20 passing tests establish the immediate fixture fix only, not this future refactoring.

## References

- [Unit-of-work interface](../../../src/Services/UserService/SmartParking.UserService.Application/Common/Interfaces/Persistence/IUnitOfWork.cs)
- [Media handlers](../../../src/Services/UserService/SmartParking.UserService.Application/Media/MediaUseCases.cs)
- [Unit-of-work implementation](../../../src/Services/UserService/SmartParking.UserService.Persistence/Repositories/UnitOfWork.cs)
- [Production registrations](../../../src/Services/UserService/SmartParking.UserService.Persistence/PersistenceDependencyInjection.cs)
- [Shared HTTP test helper](../../../tests/Services/SmartParking.UserService.Tests/AuthorizationTestSupport.cs)
- [Original troubleshooting evidence](../../../ai/tasks/completed/SESSION-20261109-http-test-fixture.md)
- [Integration setup](../../../tests/README.md)
