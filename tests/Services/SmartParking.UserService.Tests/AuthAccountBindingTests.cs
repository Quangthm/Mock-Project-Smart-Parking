using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Services;

namespace SmartParking.UserService.Tests;

public sealed class AuthAccountBindingTests
{
    [Fact]
    public async Task LoginSelectsEligibleAccountInStableIdOrderAndRefreshPreservesIt()
    {
        var f = new AuthTestFixture();
        var selected = f.User.Accounts.Single();
        var other = Account(f, "33333333-3333-3333-3333-333333333333", "OWNER");
        f.User.Accounts = [other, selected];
        var issued = (await f.SignIn()).Session!;
        var old = Assert.Single(f.Sessions.Snapshot);
        Assert.Equal(selected.Id, old.AccountId);
        Assert.Equal(selected.Id.ToString(), new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken)
            .Claims.Single(c => c.Type == "account_id").Value);

        // A newly eligible lower ID must not switch an existing session during refresh.
        f.User.Accounts.Add(Account(f, "11111111-1111-1111-1111-111111111111", "ADMIN"));
        var next = await f.Auth.RotateAsync(f.User, old, default);
        Assert.Equal(selected.Id, Assert.Single(f.Sessions.Snapshot).AccountId);
        Assert.Equal("driver", next.User!.Role);
        Assert.True(await f.Tokens.ValidateAccessTokenAsync(next.AccessToken!));
    }

    [Fact]
    public async Task AnotherActiveAccountCannotRescueRefreshThroughLockedBoundAccount()
    {
        var f = new AuthTestFixture();
        await f.SignIn();
        var old = Assert.Single(f.Sessions.Snapshot);
        f.User.Accounts.Single().Status = "LOCKED";
        f.User.Accounts.Add(Account(f, "33333333-3333-3333-3333-333333333333", "DRIVER"));
        Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(
            () => f.Auth.RotateAsync(f.User, old, default))).Code);
        Assert.Equal(old.AccessTokenId, Assert.Single(f.Sessions.Snapshot).AccessTokenId);
    }

    [Fact]
    public async Task LegacyAccountlessSessionRequiresSignInAndCannotRefresh()
    {
        var f = new AuthTestFixture();
        var session = new AuthSession
        {
            Id = Guid.NewGuid(), UserId = f.User.Id!.Value, AccessTokenId = Guid.NewGuid(),
            TokenHash = "legacy", AccessExpiresAt = f.Clock.Now.AddHours(1), ExpiresAt = f.Clock.Now.AddDays(1)
        };
        await f.Sessions.AddAsync(session, default);
        Assert.False(await f.Sessions.IsActiveAsync(session.AccessTokenId, session.UserId, default));
        await Assert.ThrowsAsync<AuthException>(() => f.Auth.RotateAsync(f.User, session, default));
    }

    [Fact]
    public async Task SignedTokenCannotSubstituteAnotherAccountForPersistedSessionBinding()
    {
        var f = new AuthTestFixture();
        var original = new JwtSecurityTokenHandler().ReadJwtToken((await f.SignIn()).Session!.AccessToken);
        var claims = original.Claims.Where(c => c.Type is "sub" or "jti" or "role").Append(new Claim("account_id", Guid.NewGuid().ToString()));
        var forgedBinding = new JwtSecurityToken(f.Options.Issuer, f.Options.Audience, claims,
            original.ValidFrom, original.ValidTo, new SigningCredentials(f.Keys.SigningKey, SecurityAlgorithms.RsaSha256));
        Assert.False(await f.Tokens.ValidateAccessTokenAsync(new JwtSecurityTokenHandler().WriteToken(forgedBinding)));
    }

    private static Account Account(AuthTestFixture f, string id, string role) => new()
    {
        Id = Guid.Parse(id), UserId = f.User.Id!.Value, User = f.User,
        AccountRoles = [new AccountRole { AccountId = Guid.Parse(id), RoleCode = role }]
    };
}
