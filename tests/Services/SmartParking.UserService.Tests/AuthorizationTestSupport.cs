using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Authorization;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

internal static class TestAuthorization
{
    // HTTP fixtures use the real Application policies and trusted HTTP actor. Only facts
    // and unused feature services are substituted; an unexpected feature call fails loudly.
    public static void AddServices(IServiceCollection services, AuthTestFixture fixture)
    {
        services.TryAddSingleton<IAuthorizationScopeStore>(new MemoryScopes(fixture));
        services.TryAddSingleton<IParkingDirectory, TestParkingDirectory>();
        services.TryAddSingleton<IUserManagementService>(UnsupportedFeature.Create<IUserManagementService>());
        services.TryAddSingleton<IOwnerRegistrationService>(UnsupportedFeature.Create<IOwnerRegistrationService>());
        services.TryAddSingleton<IOperatorProvisioningService>(UnsupportedFeature.Create<IOperatorProvisioningService>());
        services.TryAddSingleton<IAccountWorkflows>(UnsupportedFeature.Create<IAccountWorkflows>());
    }

    private sealed class MemoryScopes(AuthTestFixture fixture) : IAuthorizationScopeStore
    {
        public Task<IReadOnlyList<AccountMembership>> LoadAsync(Guid userId, bool lockRows, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AccountMembership>>(fixture.User.Accounts.Where(a => a.UserId == userId)
                .Select(a => new AccountMembership(a.Id!.Value, a.UserId, a.RoleCode, a.Status, a.DeletedOn,
                    a.TenantId, a.SiteId, a.Permissions, null)).ToArray());
    }

    public static IAccountAuthorizationService Anonymous(AppDbContext db, TimeProvider clock) =>
        Policy(db, clock, new TestCurrentActor(null), new TestParkingDirectory());

    // Bind direct persistence-service tests to a real persisted session before invoking
    // their use case. Subsequent changes are checked by the production current-access store.
    public static async Task<IAccountAuthorizationService> ForUserAsync(AppDbContext db, TimeProvider clock,
        Guid userId, IParkingDirectory? parking = null, Guid? accountId = null)
    {
        var account = await db.Accounts.AsNoTracking().Where(a => a.UserId == userId
            && (accountId == null || a.Id == accountId)).OrderBy(a => a.Id).FirstAsync();
        var session = await db.AuthSessions.AsNoTracking().Where(s => s.AccountId == account.Id)
            .OrderBy(s => s.CreatedAt).FirstOrDefaultAsync();
        if (session is null)
        {
            var now = clock.GetUtcNow();
            session = new AuthSession { Id = Guid.NewGuid(), UserId = userId, AccountId = account.Id,
                AccessTokenId = Guid.NewGuid(), TokenHash = Guid.NewGuid().ToString("N"),
                CreatedAt = now, AccessExpiresAt = now.AddHours(1), ExpiresAt = now.AddDays(7) };
            db.AuthSessions.Add(session);
            await db.SaveChangesAsync();
        }
        return Policy(db, clock, new TestCurrentActor(userId, AccessRoles.Map(account.RoleCode),
            account.Id, session.AccessTokenId), parking ?? new TestParkingDirectory());
    }

    private static IAccountAuthorizationService Policy(AppDbContext db, TimeProvider clock,
        ICurrentActor actor, IParkingDirectory parking) => new AccountAuthorizationService(
            TestAccess.Service(new CurrentAccessStore(db), actor, clock), new AuthorizationScopeStore(db),
            parking, NullLogger<AccountAuthorizationService>.Instance);
}

public class UnsupportedFeature : DispatchProxy
{
    public static T Create<T>() where T : class => Create<T, UnsupportedFeature>();
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException($"Configure the {targetMethod?.DeclaringType?.Name} fixture before using this feature.");
}
