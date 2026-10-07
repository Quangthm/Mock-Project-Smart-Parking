using Microsoft.IdentityModel.Tokens;
using SmartParking.UserService.Domain.Enum;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Services;
using UserService.Application.Usecase.Session;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class AuthSessionTests
{
    [Fact]
    public async Task Login_IssuesRs256BearerOneHourAndRealRefreshToken()
    {
        var f = new AuthTestFixture();
        var result = await f.SignIn();
        Assert.True(result.IsSuccess);
        Assert.Equal(f.User.Id, result.Session!.User!.UserId);
        Assert.Equal("driver", result.Session.User.Role);
        Assert.Equal("Bearer", result.Session.TokenType);
        Assert.Equal(86400, result.Session.ExpiresIn);
        Assert.NotEmpty(result.Session.RefreshToken!);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Session.AccessToken);
        Assert.Equal("RS256", jwt.Header.Alg);
        Assert.Equal(86400, (jwt.ValidTo - jwt.ValidFrom).TotalSeconds);
        Assert.True(await f.Tokens.ValidateAccessTokenAsync(result.Session.AccessToken!));
        var stored = Assert.Single(f.Sessions.Snapshot);
        Assert.NotEqual(result.Session.RefreshToken, stored.TokenHash);
        Assert.Equal(AuthSessionService.HashRefreshToken(result.Session.RefreshToken!), stored.TokenHash);
        Assert.Equal(f.Clock.Now.AddDays(7), stored.ExpiresAt);
    }

    [Fact]
    public void Passwords_AreSaltedBcryptCost12_RejectPlaintextAndMalformedHashes()
    {
        var p = new BcryptPasswordService();
        var first = p.Hash("Password@123");
        Assert.NotEqual(first, p.Hash("Password@123"));
        Assert.StartsWith("$2", first);
        Assert.Contains("$12$", first);
        Assert.True(p.Verify("Password@123", first));
        Assert.False(p.Verify("wrong", first));
        Assert.False(p.Verify("Password@123", "Password@123"));
        Assert.False(p.Verify("wrong", ""));
    }

    [Theory]
    [InlineData("driver@gmail.com", "wrong")]
    [InlineData("unknown@example.com", "Password@123")]
    public async Task InvalidCredentials_DoNotCreateSession(string email, string password)
    {
        var f = new AuthTestFixture();
        var result = await f.SignIn(password, email);
        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_FAILED", result.ErrorCode);
        Assert.Empty(f.Sessions.Snapshot);
    }

    [Fact]
    public async Task ThirdFailureLocksAndCorrectPasswordCannotBypassLock()
    {
        var f = new AuthTestFixture();
        Assert.Equal("AUTH_FAILED", (await f.SignIn("wrong")).ErrorCode);
        Assert.Equal("AUTH_FAILED", (await f.SignIn("wrong")).ErrorCode);
        Assert.Equal("ACCOUNT_LOCKED", (await f.SignIn("wrong")).ErrorCode);
        Assert.Equal(3, f.User.FailedLoginAttempts);
        Assert.Equal(UserStatus.Locked, f.User.Status);
        Assert.Equal(f.Clock.Now.AddMinutes(15), f.User.LockedUntil);
        Assert.Equal("ACCOUNT_LOCKED", (await f.SignIn()).ErrorCode);
        Assert.Empty(f.Sessions.Snapshot);
        Assert.Equal(3, f.Saved);
    }

    [Fact]
    public async Task LockExpiresAndSuccessfulLoginResetsCounter()
    {
        var f = new AuthTestFixture();
        for (var i = 0; i < 3; i++) await f.SignIn("wrong");
        f.Clock.Now = f.Clock.Now.AddMinutes(15);
        Assert.True((await f.SignIn()).IsSuccess);
        Assert.Equal(0, f.User.FailedLoginAttempts);
        Assert.Null(f.User.LockedUntil);
        Assert.Equal(UserStatus.Active, f.User.Status);
        await f.SignIn("wrong");
        Assert.Equal(1, f.User.FailedLoginAttempts);
        await f.SignIn();
        Assert.Equal(0, f.User.FailedLoginAttempts);
    }

    [Theory]
    [InlineData("locked")]
    [InlineData("pending")]
    [InlineData("deleted-user")]
    [InlineData("deleted-account")]
    [InlineData("no-role")]
    public async Task UnusableAccountDoesNotIssueTokens(string reason)
    {
        var f = new AuthTestFixture();
        if (reason == "locked") f.User.Status = UserStatus.Locked;
        if (reason == "pending") f.User.Status = UserStatus.PendingVerification;
        if (reason == "deleted-user") f.User.DeletedOn = f.Clock.Now;
        if (reason == "deleted-account") f.User.Accounts.First().DeletedOn = f.Clock.Now;
        if (reason == "no-role") f.User.Accounts.First().AccountRoles.Clear();
        Assert.False((await f.SignIn()).IsSuccess);
        Assert.Empty(f.Sessions.Snapshot);
    }

    [Fact]
    public async Task LogoutRevokesBothTokensAndPreservesOtherSession()
    {
        var f = new AuthTestFixture();
        var first = (await f.SignIn()).Session!;
        var second = (await f.SignIn()).Session!;
        var id = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(first.AccessToken).Id);
        await new LogoutCommandHandler(f.Sessions).Handle(new(id, f.User.Id!.Value, first.RefreshToken), default);
        Assert.False(await f.Tokens.ValidateAccessTokenAsync(first.AccessToken!));
        Assert.True(await f.Tokens.ValidateAccessTokenAsync(second.AccessToken!));
        var refresh = new RefreshCommandHandler(f, f.Sessions, f.Auth, f.Clock);
        Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => refresh.Handle(new(first.RefreshToken), default))).Code);
    }

    [Fact]
    public async Task LogoutRejectsRefreshFromAnotherSessionAndKeepsBothActive()
    {
        var f = new AuthTestFixture();
        var first = (await f.SignIn()).Session!;
        var second = (await f.SignIn()).Session!;
        var id = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(first.AccessToken).Id);
        var ex = await Assert.ThrowsAsync<AuthException>(() => new LogoutCommandHandler(f.Sessions)
            .Handle(new(id, f.User.Id!.Value, second.RefreshToken), default));
        Assert.Equal("INVALID_TOKEN", ex.Code);
        Assert.True(await f.Tokens.ValidateAccessTokenAsync(first.AccessToken!));
        Assert.True(await f.Tokens.ValidateAccessTokenAsync(second.AccessToken!));
    }

    [Fact]
    public async Task RotationInvalidatesOldPairAndDoesNotExtendAbsoluteRefreshExpiry()
    {
        var f = new AuthTestFixture();
        var first = (await f.SignIn()).Session!;
        var expires = f.Sessions.Snapshot.Single().ExpiresAt;
        f.Clock.Now = f.Clock.Now.AddHours(2);
        var refresh = new RefreshCommandHandler(f, f.Sessions, f.Auth, f.Clock);
        var next = await refresh.Handle(new(first.RefreshToken), default);
        Assert.NotEqual(first.RefreshToken, next.RefreshToken);
        Assert.False(await f.Tokens.ValidateAccessTokenAsync(first.AccessToken!));
        Assert.Equal(expires, f.Sessions.Snapshot.Single().ExpiresAt);
        Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => refresh.Handle(new(first.RefreshToken), default))).Code);
    }

    [Fact]
    public async Task StaleSnapshotCannotRotateTwice()
    {
        var f = new AuthTestFixture();
        var first = (await f.SignIn()).Session!;
        var snapshot = (await f.Sessions.FindByRefreshHashAsync(AuthSessionService.HashRefreshToken(first.RefreshToken!), default))!;
        await f.Auth.RotateAsync(f.User, snapshot, default);
        Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => f.Auth.RotateAsync(f.User, snapshot, default))).Code);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("locked")]
    [InlineData("deleted")]
    [InlineData("unknown")]
    public async Task RefreshRejectsUnusableTokenOrAccount(string reason)
    {
        var f = new AuthTestFixture();
        var session = (await f.SignIn()).Session!;
        if (reason == "expired") f.Clock.Now = f.Clock.Now.AddDays(7);
        if (reason == "locked") f.User.Status = UserStatus.Locked;
        if (reason == "deleted") f.User.DeletedOn = f.Clock.Now;
        Assert.Equal("INVALID_TOKEN", (await Assert.ThrowsAsync<AuthException>(() => new RefreshCommandHandler(f, f.Sessions, f.Auth, f.Clock)
            .Handle(new(reason == "unknown" ? "unknown" : session.RefreshToken), default))).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiJhZG1pbiJ9.")]
    public async Task MalformedOrUnsignedTokenIsRejected(string token) => Assert.False(await new AuthTestFixture().Tokens.ValidateAccessTokenAsync(token));

    [Theory]
    [InlineData("key")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expiry")]
    [InlineData("unknown-session")]
    [InlineData("other-user")]
    [InlineData("HS256")]
    public async Task SignedButInvalidTokenIsRejected(string invalid)
    {
        var f = new AuthTestFixture();
        var session = (await f.SignIn()).Session!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        using var otherKeys = new JwtKeyProvider(f.Options, true);
        var credentials = invalid == "HS256"
            ? new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrong-algorithm-test-key-at-least-32-bytes")), SecurityAlgorithms.HmacSha256)
            : new SigningCredentials(invalid == "key" ? otherKeys.SigningKey : f.Keys.SigningKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(invalid == "issuer" ? "wrong" : f.Options.Issuer,
            invalid == "audience" ? "wrong" : f.Options.Audience,
            [new Claim("sub", invalid == "other-user" ? Guid.NewGuid().ToString() : f.User.Id.ToString()!),
             new Claim("jti", invalid == "unknown-session" ? Guid.NewGuid().ToString("N") : jwt.Id)],
            DateTime.UtcNow.AddMinutes(-10), invalid == "expiry" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5), credentials);
        Assert.False(await f.Tokens.ValidateAccessTokenAsync(new JwtSecurityTokenHandler().WriteToken(token)));
    }

    [Fact]
    public async Task TamperingAndExpiredServerSessionAreRejected()
    {
        var f = new AuthTestFixture();
        var first = (await f.SignIn()).Session!;
        var parts = first.AccessToken!.Split('.');
        parts[1] = Base64UrlEncoder.Encode("{\"sub\":\"admin\",\"role\":\"admin\"}");
        Assert.False(await f.Tokens.ValidateAccessTokenAsync(string.Join('.', parts)));
        f.Clock.Now = f.Clock.Now.AddHours(24);
        Assert.False(await f.Tokens.ValidateAccessTokenAsync(first.AccessToken));
    }
}
