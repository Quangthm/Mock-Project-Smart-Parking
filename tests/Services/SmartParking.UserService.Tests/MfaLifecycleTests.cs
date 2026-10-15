using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;

namespace SmartParking.UserService.Tests;

public sealed class MfaLifecycleTests
{
    private const string Email = "driver@gmail.com";
    private const string Password = "Password@123";

    [Theory]
    [InlineData(1, "287082")]
    [InlineData(37037036, "081804")]
    public void TotpMatchesRfc6238Sha1Vectors(long step, string expected)
    {
        Assert.Equal(expected, AccountWorkflowService.Totp("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", step));
    }

    [PostgresFact]
    public async Task PasswordChallengeRequiresProofAndSingleUseRecoveryCodeBeforeIssuingSession()
    {
        await using var fixture = await CreateFixtureAsync();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow.AddHours(-1) };
        await using var host = await fixture.StartHostAsync(clock);
        var client = host.GetTestClient();
        var setup = await EnableAsync(client, clock);
        client.DefaultRequestHeaders.Authorization = null;

        using var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        await using (var db = fixture.Open())
            Assert.False(await db.AuthChallenges.AnyAsync(challenge => challenge.Purpose == "PASSWORD_MFA"));

        var challenge = await PasswordChallengeAsync(client);
        using var invalidProof = await client.PostAsJsonAsync("/api/auth/mfa/login", new
        {
            challenge.ChallengeId, challengeToken = new string('0', 64), recoveryCode = setup.Codes[0]
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidProof.StatusCode);

        var session = await PostDataAsync<UserSessionDto>(client, "/api/auth/mfa/login", new
        {
            challenge.ChallengeId, challenge.ChallengeToken, recoveryCode = setup.Codes[0]
        });
        Assert.NotNull(session.AccessToken);
        Assert.Equal(7, session.RecoveryCodesRemaining);
        using var replayChallenge = await client.PostAsJsonAsync("/api/auth/mfa/login", new
        {
            challenge.ChallengeId, challenge.ChallengeToken, recoveryCode = setup.Codes[1]
        });
        Assert.Equal(HttpStatusCode.Conflict, replayChallenge.StatusCode);

        var secondChallenge = await PasswordChallengeAsync(client);
        using var replayCode = await client.PostAsJsonAsync("/api/auth/mfa/login", new
        {
            secondChallenge.ChallengeId, secondChallenge.ChallengeToken, recoveryCode = setup.Codes[0]
        });
        Assert.Equal(HttpStatusCode.Unauthorized, replayCode.StatusCode);
        await using var verify = fixture.Open();
        var stored = await verify.MfaRecoveryCodes.ToListAsync();
        Assert.Equal(8, stored.Count);
        Assert.Single(stored, code => code.UsedAt is not null);
        Assert.All(stored, code => Assert.DoesNotContain(code.CodeHash, setup.Codes));
    }

    [PostgresFact]
    public async Task ReplacementKeepsOldAuthenticatorUntilConfirmedThenRevokesSessionsAndOldCodes()
    {
        await using var fixture = await CreateFixtureAsync();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow.AddHours(-1) };
        await using var host = await fixture.StartHostAsync(clock);
        var client = host.GetTestClient();
        var initial = await EnableAsync(client, clock);
        var replacement = await PostDataAsync<MfaSetupDto>(client, "/api/auth/mfa/setup",
            new { password = Password, recoveryCode = initial.Codes[0] });
        await using (var db = fixture.Open())
        {
            var credential = await db.MfaCredentials.SingleAsync();
            Assert.True(credential.Enabled);
            Assert.NotNull(credential.PendingProtectedSecret);
        }

        client.DefaultRequestHeaders.Authorization = null;
        clock.Now = clock.Now.AddSeconds(30);
        var oldChallenge = await PasswordChallengeAsync(client);
        var oldSession = await PostDataAsync<UserSessionDto>(client, "/api/auth/mfa/login", new
        {
            oldChallenge.ChallengeId, oldChallenge.ChallengeToken, totp = Code(initial.Secret, clock)
        });
        client.DefaultRequestHeaders.Authorization = new("Bearer", oldSession.AccessToken);
        var activated = await PostDataAsync<MfaRecoveryCodesDto>(client, "/api/auth/mfa/enable",
            new { code = Code(replacement.Secret, clock) });
        Assert.True(activated.SignInRequired);
        Assert.Equal(8, activated.RecoveryCodes.Length);
        using var revokedSession = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedSession.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var newChallenge = await PasswordChallengeAsync(client);
        using var oldCode = await client.PostAsJsonAsync("/api/auth/mfa/login", new
        {
            newChallenge.ChallengeId, newChallenge.ChallengeToken, recoveryCode = initial.Codes[1]
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldCode.StatusCode);
        var newSession = await PostDataAsync<UserSessionDto>(client, "/api/auth/mfa/login", new
        {
            newChallenge.ChallengeId, newChallenge.ChallengeToken, recoveryCode = activated.RecoveryCodes[0]
        });
        client.DefaultRequestHeaders.Authorization = new("Bearer", newSession.AccessToken);
        var refreshed = await PostDataAsync<MfaRecoveryCodesDto>(client, "/api/auth/mfa/recovery-codes",
            new { password = Password, recoveryCode = activated.RecoveryCodes[1] });
        Assert.False(refreshed.SignInRequired);
        Assert.Equal(8, refreshed.RecoveryCodes.Length);

        using var disabled = await client.PostAsJsonAsync("/api/auth/mfa/disable",
            new { password = Password, recoveryCode = refreshed.RecoveryCodes[0] });
        disabled.EnsureSuccessStatusCode();
        await using (var db = fixture.Open())
        {
            Assert.False(await db.MfaCredentials.AnyAsync());
            Assert.False(await db.MfaRecoveryCodes.AnyAsync());
            Assert.All(await db.AuthSessions.ToListAsync(), session => Assert.True(session.IsRevoked));
        }
        client.DefaultRequestHeaders.Authorization = null;
        var ordinaryLogin = await PostDataAsync<UserSessionDto>(client, "/api/auth/login", new { email = Email, password = Password });
        Assert.NotNull(ordinaryLogin.AccessToken);
    }

    [PostgresFact]
    public async Task InvalidCodesPersistLockoutAndExpiredChallengesNeverCreateSessions()
    {
        await using var fixture = await CreateFixtureAsync();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow.AddHours(-1) };
        await using var host = await fixture.StartHostAsync(clock);
        var client = host.GetTestClient();
        var setup = await EnableAsync(client, clock);
        client.DefaultRequestHeaders.Authorization = null;
        var challenge = await PasswordChallengeAsync(client);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var failure = await client.PostAsJsonAsync("/api/auth/mfa/login", new
            {
                challenge.ChallengeId, challenge.ChallengeToken, recoveryCode = new string('0', 32)
            });
            Assert.Equal(attempt == 3 ? (HttpStatusCode)423 : HttpStatusCode.Unauthorized, failure.StatusCode);
        }
        var retryChallenge = await PasswordChallengeAsync(client);
        using var locked = await client.PostAsJsonAsync("/api/auth/mfa/login", new
        {
            retryChallenge.ChallengeId, retryChallenge.ChallengeToken, recoveryCode = setup.Codes[0]
        });
        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
        clock.Now = clock.Now.AddMinutes(15);
        using var expired = await client.PostAsJsonAsync("/api/auth/mfa/login", new
        {
            retryChallenge.ChallengeId, retryChallenge.ChallengeToken, recoveryCode = setup.Codes[0]
        });
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        var freshChallenge = await PasswordChallengeAsync(client);
        var recovered = await PostDataAsync<UserSessionDto>(client, "/api/auth/mfa/login", new
        {
            freshChallenge.ChallengeId, freshChallenge.ChallengeToken, recoveryCode = setup.Codes[0]
        });
        Assert.NotNull(recovered.AccessToken);
    }

    [PostgresFact]
    public async Task ConcurrentRecoveryUseSucceedsOnceAndOtpSignInCannotBypassMfa()
    {
        await using var fixture = await CreateFixtureAsync();
        var clock = new TestClock { Now = DateTimeOffset.UtcNow.AddHours(-1) };
        await using var host = await fixture.StartHostAsync(clock);
        var client = host.GetTestClient();
        var setup = await EnableAsync(client, clock);
        client.DefaultRequestHeaders.Authorization = null;
        var first = await PasswordChallengeAsync(client);
        var second = await PasswordChallengeAsync(client);
        var responses = await Task.WhenAll(new[] { first, second }.Select(challenge =>
            client.PostAsJsonAsync("/api/auth/mfa/login", new
            {
                challenge.ChallengeId, challenge.ChallengeToken, recoveryCode = setup.Codes[0]
            })));
        try
        {
            Assert.Single(responses, response => response.IsSuccessStatusCode);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
        }
        finally { foreach (var response in responses) response.Dispose(); }

        var otp = await PostDataAsync<ChallengeResult>(client, "/api/auth/otp/request", new { contact = Email });
        string otpCode;
        await using (var db = fixture.Open())
        {
            // Test delivery payload is encrypted by the running host's ephemeral protector.
            var delivery = await db.WorkflowDeliveries.SingleAsync(item => item.ChallengeId == otp.ChallengeId);
            var protector = host.Services.GetRequiredService<IWorkflowProtector>();
            var message = JsonSerializer.Deserialize<WorkflowMessage>(protector.Unprotect(delivery.ProtectedPayload))!;
            otpCode = System.Text.RegularExpressions.Regex.Match(message.Body, @"\b\d{6}\b").Value;
        }
        using var missingMfa = await client.PostAsJsonAsync("/api/auth/otp/login", new { otp.ChallengeId, code = otpCode });
        Assert.Equal(HttpStatusCode.Unauthorized, missingMfa.StatusCode);
        var session = await PostDataAsync<UserSessionDto>(client, "/api/auth/otp/login",
            new { otp.ChallengeId, code = otpCode, recoveryCode = setup.Codes[1] });
        Assert.Equal(6, session.RecoveryCodesRemaining);
    }

    private static string Code(string secret, TestClock clock) =>
        AccountWorkflowService.Totp(secret, clock.Now.ToUnixTimeSeconds() / 30);

    private static async Task<UserServiceDatabaseFixture> CreateFixtureAsync()
    {
        var fixture = await UserServiceDatabaseFixture.CreateAsync();
        await using var db = fixture.Open();
        await new DataSeeder(db, new BcryptPasswordService()).SeedAsync();
        var user = await db.Users.SingleAsync();
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return fixture;
    }

    private static async Task<(string Secret, string[] Codes)> EnableAsync(HttpClient client, TestClock clock)
    {
        var session = await PostDataAsync<UserSessionDto>(client, "/api/auth/login", new { email = Email, password = Password });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var status = await GetDataAsync<MfaStatusDto>(client, "/api/auth/mfa/status");
        Assert.False(status.Enabled);
        var setup = await PostDataAsync<MfaSetupDto>(client, "/api/auth/mfa/setup", new { password = Password });
        Assert.Contains("otpauth://totp/", setup.Uri);
        var result = await PostDataAsync<MfaRecoveryCodesDto>(client, "/api/auth/mfa/enable", new { code = Code(setup.Secret, clock) });
        Assert.Equal(8, result.RecoveryCodes.Length);
        Assert.Equal(8, result.RecoveryCodes.Distinct().Count());
        return (setup.Secret, result.RecoveryCodes);
    }

    private static async Task<MfaLoginChallengeDto> PasswordChallengeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mfaRequired").GetBoolean());
        Assert.False(body.GetProperty("data").TryGetProperty("accessToken", out _));
        return body.GetProperty("data").Deserialize<MfaLoginChallengeDto>(JsonSerializerOptions.Web)!;
    }

    private static async Task<T> PostDataAsync<T>(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("data").Deserialize<T>(JsonSerializerOptions.Web)!;
    }

    private static async Task<T> GetDataAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("data").Deserialize<T>(JsonSerializerOptions.Web)!;
    }
}
