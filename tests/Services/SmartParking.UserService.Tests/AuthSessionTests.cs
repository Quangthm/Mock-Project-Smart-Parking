using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Application.Services;
using UserService.Application.Usecase.Login;
using UserService.Application.Usecase.Session;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class AuthSessionTests
{
    private const string Key = "test-only-key-for-auth-session-tests-at-least-32-bytes";
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly InMemoryAuthSessionStore sessions = new(TimeProvider.System);

    private AccessTokenService TokenService() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience"
        }).Build(), sessions, TimeProvider.System);

    private static User Account() => new()
    {
        Id = UserId, Email = "driver@gmail.com", FullName = "Demo Driver",
        Status = UserStatus.Active, UserRoles = [new UserRole { RoleCode = "Driver" }]
    };

    [Fact]
    public async Task Login_UsesBackendIdentityAndRole_AndDoesNotIssueFakeRefreshToken()
    {
        var result = await new LoginCommandHandler(new UnitOfWork(new UserRepository()), TokenService())
            .Handle(new LoginCommand { Email = "driver@gmail.com", Password = "Password@123" }, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, result.Session!.User!.UserId);
        Assert.Equal("driver", result.Session.User.Role);
        Assert.Null(result.Session.RefreshToken);
        Assert.Equal(300, result.Session.ExpiresIn);
        Assert.True(TokenService().ValidateAccessToken(result.Session.AccessToken!));
    }

    [Theory]
    [InlineData("driver@gmail.com", "wrong")]
    [InlineData("unknown@example.com", "Password@123")]
    public async Task InvalidCredentials_DoNotCreateSession(string email, string password)
    {
        var result = await new LoginCommandHandler(new UnitOfWork(new UserRepository()), TokenService())
            .Handle(new LoginCommand { Email = email, Password = password }, default);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task Logout_RevokesOnlyTheCurrentToken()
    {
        var service = TokenService();
        var first = service.GenerateAccessToken(Account());
        var second = service.GenerateAccessToken(Account());
        var sessionId = new JwtSecurityTokenHandler().ReadJwtToken(first).Id;
        await new LogoutCommandHandler(sessions).Handle(new LogoutCommand(sessionId), default);
        Assert.False(service.ValidateAccessToken(first));
        Assert.True(service.ValidateAccessToken(second));
        await new LogoutCommandHandler(sessions).Handle(new LogoutCommand(sessionId), default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiJhZG1pbiJ9.")]
    public void MalformedOrUnsignedToken_IsRejected(string token) =>
        Assert.False(TokenService().ValidateAccessToken(token));

    [Theory]
    [InlineData("key")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expiry")]
    [InlineData("unknown-session")]
    [InlineData("other-user")]
    public void SignedButInvalidToken_IsRejected(string invalidPart)
    {
        var id = Guid.NewGuid().ToString("N");
        sessions.Add(id, UserId, DateTimeOffset.UtcNow.AddMinutes(5));
        var token = new JwtSecurityToken(
            invalidPart == "issuer" ? "wrong-issuer" : "test-issuer",
            invalidPart == "audience" ? "wrong-audience" : "test-audience",
            [new Claim(JwtRegisteredClaimNames.Sub, invalidPart == "other-user" ? Guid.NewGuid().ToString() : UserId.ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, invalidPart == "unknown-session" ? "missing-session" : id)],
            DateTime.UtcNow.AddMinutes(-10),
            invalidPart == "expiry" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                invalidPart == "key" ? "different-test-key-that-is-also-long-enough" : Key)), SecurityAlgorithms.HmacSha256));
        Assert.False(TokenService().ValidateAccessToken(new JwtSecurityTokenHandler().WriteToken(token)));
    }

    [Fact]
    public void ExpiredSession_AndWrongUserAreRejected()
    {
        var clock = new TestClock();
        var store = new InMemoryAuthSessionStore(clock);
        store.Add("session", UserId, clock.GetUtcNow().AddMinutes(5));
        Assert.True(store.IsActive("session", UserId));
        Assert.False(store.IsActive("session", Guid.NewGuid()));
        clock.Now = clock.Now.AddMinutes(5);
        Assert.False(store.IsActive("session", UserId));
    }

    [Fact]
    public void TokenTampering_IsRejected()
    {
        var service = TokenService();
        var parts = service.GenerateAccessToken(Account()).Split('.');
        parts[1] = Base64UrlEncoder.Encode("{\"sub\":\"admin\",\"role\":\"admin\"}");
        Assert.False(service.ValidateAccessToken(string.Join('.', parts)));
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
