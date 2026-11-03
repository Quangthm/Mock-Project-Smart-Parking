using UserService.Application.Authorization;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Models.Exceptions;

namespace SmartParking.UserService.Tests;

public sealed class CurrentAccessTests
{
    [Theory]
    [InlineData("role-reassigned", "FORBIDDEN")]
    [InlineData("catalogue-missing", "FORBIDDEN")]
    [InlineData("ownership-changed", "FORBIDDEN")]
    [InlineData("timed-lock", "FORBIDDEN")]
    [InlineData("access-expiry-equality", "INVALID_TOKEN")]
    [InlineData("session-expiry-equality", "INVALID_TOKEN")]
    public async Task ProtectedUseCasesRejectChangedAuthorityWithoutRelyingOnHttp(string change, string code)
    {
        var fixture = new AuthTestFixture();
        var actor = await OwnProfileTests.ActorAsync(fixture);
        var state = (await fixture.LoadCurrentAsync(actor.AccountId!.Value, actor.AccessTokenId!.Value, default))!;
        switch (change)
        {
            case "role-reassigned":
                state.Account.RoleCode = "BUSINESS_OWNER";
                state = state with { RoleCode = "BUSINESS_OWNER" };
                break;
            case "catalogue-missing": state = state with { RoleCode = null }; break;
            case "ownership-changed": state.Account.UserId = Guid.NewGuid(); break;
            case "timed-lock": state.User.LockedUntil = fixture.Clock.Now.AddMinutes(1); break;
            case "access-expiry-equality": state.Session.AccessExpiresAt = fixture.Clock.Now; break;
            case "session-expiry-equality": state.Session.ExpiresAt = fixture.Clock.Now; break;
        }
        var access = TestAccess.Service(new Snapshot(state), actor, fixture.Clock);
        Assert.Equal(code, (await Assert.ThrowsAsync<AuthException>(() => access.RequireAsync(default))).Code);
        Assert.Equal(code, (await Assert.ThrowsAsync<AuthException>(() => access.LockAsync(default))).Code);
    }

    [Fact]
    public async Task OperationalPermissionRevocationDoesNotRevokeOwnProfileAccess()
    {
        var fixture = new AuthTestFixture();
        var actor = await OwnProfileTests.ActorAsync(fixture);
        fixture.User.Accounts.Single().Permissions = [];
        var current = await TestAccess.Service(fixture, actor, fixture.Clock).RequireAsync(default);
        Assert.Equal("DRIVER", current.RoleCode);
        Assert.Equal("driver", current.Role);
        Assert.Equal(actor.AccountId, current.Account.Id);
    }

    private sealed class Snapshot(CurrentAccessState state) : ICurrentAccessStore
    {
        public Task<CurrentAccessState?> LoadCurrentAsync(Guid accountId, Guid accessTokenId, CancellationToken cancellationToken) =>
            Task.FromResult<CurrentAccessState?>(state);
        public Task<CurrentAccessState?> LockCurrentAsync(Guid accountId, Guid accessTokenId, CancellationToken cancellationToken) =>
            LoadCurrentAsync(accountId, accessTokenId, cancellationToken);
    }
}
