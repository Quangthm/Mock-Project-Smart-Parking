using System.Text.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SmartParking.IoTService.API.Authentication;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.API.Management;
using SmartParking.IoTService.Application.Assistant;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Tests;

public sealed class AssistantManagementApplicationTests
{
    [Fact]
    public async Task HttpManagementRequiresSignedAdminAndRejectsBindingErrorsBeforeWrites()
    {
        using var key = RSA.Create(2048);
        var store = new Store();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAssistantHttp();
        builder.Services.AddAssistantApplication();
        builder.Services.AddAssistantAuthentication("test-user", "test-assistant", key.ExportSubjectPublicKeyInfoPem(), TimeProvider.System);
        builder.Services.AddAuthorization(options => options.AddPolicy("AssistantAdmin", p => p.RequireAuthenticatedUser().RequireRole("admin")));
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<IAssistantManagementStore>(store);
        builder.Services.AddSingleton<IAssistantSecretProtector>(new Protector());
        builder.Services.AddSingleton<IAssistantManagedRuntime>(new Runtime());
        await using var app = builder.Build();
        app.UseAssistantHttp(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapManagement();
        await app.StartAsync();
        using var client = app.GetTestClient();
        string Sign(string role)
        {
            var header = Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
            var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["iss"] = "test-user", ["aud"] = "test-assistant", ["role"] = role,
                ["sub"] = Guid.NewGuid().ToString(), ["account_id"] = Guid.NewGuid().ToString(), ["jti"] = Guid.NewGuid().ToString(),
                ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
            }));
            var unsigned = header + "." + payload;
            return unsigned + "." + Base64UrlEncoder.Encode(key.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        using var anonymous = await client.GetAsync("/api/assistant/admin/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Sign("driver"));
        using var driver = await client.GetAsync("/api/assistant/admin/settings");
        Assert.Equal(HttpStatusCode.Forbidden, driver.StatusCode);
        Assert.Equal(0, store.Calls);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Sign("admin"));
        using var admin = await client.GetAsync("/api/assistant/admin/settings");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.Equal(1, store.Calls);
        var json = "{\"mutationId\":\"" + Guid.NewGuid() + "\",\"expectedRevision\":\"0\",\"action\":\"revoke-key\"}";
        using var quoted = await client.PostAsync("/api/assistant/admin/mutations", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, quoted.StatusCode);
        using var charset = await client.PostAsync("/api/assistant/admin/mutations", new StringContent("{}", Encoding.Unicode, "application/json"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, charset.StatusCode);
        Assert.Equal(1, store.Calls);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("driver", true)]
    [InlineData("Admin", true)]
    public async Task NonAdminCannotReadOrMutateStorage(string? role, bool authenticated)
    {
        var store = new Store();
        var handler = Handler(store, role);
        var read = await Assert.ThrowsAsync<AssistantAccessException>(() => handler.Handle(new GetAssistantManagement(), default));
        Assert.Equal(authenticated, read.Authenticated);
        await Assert.ThrowsAsync<AssistantAccessException>(() => handler.Handle(new ChangeAssistantManagement(new(Guid.NewGuid(), 0, "revoke-key")), default));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task ExpiredAdminCannotReadStorage()
    {
        var store = new Store();
        var handler = Handler(store, "admin", DateTimeOffset.UtcNow.AddMinutes(-1));
        await Assert.ThrowsAsync<AssistantAccessException>(() => handler.Handle(new GetAssistantManagement(), default));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task AdminViewAndDiagnosticStringsDoNotExposeCredentials()
    {
        var store = new Store(); var protector = new Protector();
        var input = new AdminMutation(Guid.NewGuid(), 0, "save-key", Enabled: true,
            Provider: "gemini", Label: "Primary", Secret: "synthetic-secret", Priority: 1);
        var write = ManagementRules.Apply(store.State, input, protector, DateTimeOffset.UtcNow);
        store.State = write.State;
        var view = await Handler(store, "admin").Handle(new GetAssistantManagement(), default);
        var json = JsonSerializer.Serialize(view);
        Assert.DoesNotContain("CredentialVersion", json);
        Assert.DoesNotContain("synthetic-secret", json);
        Assert.DoesNotContain("synthetic-secret", input.ToString());
        Assert.DoesNotContain("ciphertext", write.Credential!.ToString());
    }

    [Fact]
    public void RotationReplacesVersionAndRevocationRemovesCandidate()
    {
        var protector = new Protector();
        var first = ManagementRules.Apply(ManagementState.Empty, new(Guid.NewGuid(), 0, "save-key",
            Enabled: true, Provider: "gemini", Label: "Key", Secret: "first-secret", Priority: 1), protector, DateTimeOffset.UtcNow);
        var original = Assert.Single(first.State.Keys);
        var rotated = ManagementRules.Apply(first.State, new(Guid.NewGuid(), 1, "save-key",
            Enabled: true, KeyId: original.Id, Provider: "gemini", Label: "Key", Secret: "second-secret", Priority: 1), protector, DateTimeOffset.UtcNow);
        Assert.Equal(original.CredentialVersion, rotated.RetireVersion);
        Assert.NotEqual(original.CredentialVersion, Assert.Single(rotated.State.Keys).CredentialVersion);
        var enabled = rotated.State with { Enabled = true, Providers = [new("gemini", "model", true, 1)] };
        Assert.NotNull(ManagementRules.Select(enabled));
        var revoked = ManagementRules.Apply(enabled, new(Guid.NewGuid(), 2, "revoke-key", KeyId: original.Id), protector, DateTimeOffset.UtcNow);
        Assert.Equal(original.Id, revoked.RevokeSlot);
        Assert.Null(ManagementRules.Select(revoked.State));
    }

    [Fact]
    public void SelectionHonorsProviderThenKeyPriorityAndMaintenance()
    {
        var now = DateTimeOffset.UtcNow;
        var gemini = new ManagedKey(Guid.NewGuid(), "gemini", "G", true, 1, Guid.NewGuid(), now, null);
        var openai = new ManagedKey(Guid.NewGuid(), "openai", "O", true, 2, Guid.NewGuid(), now, null);
        var preferred = openai with { Id = Guid.NewGuid(), Priority = 1 };
        var state = new ManagementState(1, true, "Maintenance", [new("gemini", "g", true, 2), new("openai", "o", true, 1)], [gemini, openai, preferred]);
        Assert.Equal(preferred.Id, ManagementRules.Select(state)!.Value.Key.Id);
        Assert.Null(ManagementRules.Select(state with { Enabled = false }));
    }

    private static ManagementHandlers Handler(Store store, string? role, DateTimeOffset? expires = null) =>
        new(store, new Protector(), new Runtime(), new ActorSource(role is null ? null :
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), role, expires ?? DateTimeOffset.UtcNow.AddHours(1))), TimeProvider.System);
    private sealed class ActorSource(CurrentActor? actor) : ICurrentActor { public CurrentActor? Actor => actor; }
    private sealed class Protector : IAssistantSecretProtector
    {
        public bool IsConfigured => true;
        public string Protect(Guid version, string secret) => "ciphertext";
        public string Unprotect(Guid version, string value) => throw new NotSupportedException();
    }
    private sealed class Runtime : IAssistantManagedRuntime
    {
        public bool ReleaseAuthorized => true;
        public Task RefreshAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class Store : IAssistantManagementStore
    {
        public int Calls;
        public ManagementState State = ManagementState.Empty;
        public bool IsConfigured => true;
        public Task<ManagementState> ReadAsync(CancellationToken ct) { Calls++; return Task.FromResult(State); }
        public Task<string?> CredentialAsync(Guid slot, Guid version, CancellationToken ct) => throw new NotSupportedException();
        public Task<ManagementState> MutateAsync(Guid actor, AdminMutation input, Func<ManagementState, ManagementWrite> change, CancellationToken ct)
        { Calls++; State = change(State).State; return Task.FromResult(State); }
    }
}
