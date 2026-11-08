using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using SmartParking.IoTService.API;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Chat;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Management;
using SmartParking.IoTService.Persistence;

namespace SmartParking.IoTService.Tests;

[Collection("Assistant PostgreSQL fixture")]
public sealed class AssistantPostgresTests
{
    [AssistantPostgresFact]
    public async Task SettingsReplayConflictRotationAndRevocationSurviveReopening()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        await using var primary = OpenSource();
        await using var peer = OpenSource();
        var store = new AssistantManagementStore(primary);
        var peerStore = new AssistantManagementStore(peer);
        var ring = Directory.CreateTempSubdirectory("smartpark-assistant-ring-");
        try
        {
            var actor = Guid.NewGuid();
            var protector = new AssistantSecretProtector(DataProtectionProvider.Create(ring));
            var initial = await store.ReadAsync(ct);
            const string originalSecret = "synthetic-original-key";
            const string rotatedSecret = "synthetic-rotated-key";
            var add = new AdminMutation(Guid.NewGuid(), initial.Revision, "add-configuration",
                Provider: "gemini", Label: "Synthetic", Secret: originalSecret, Priority: 1, Model: "gemini-3-flash");
            Task<ManagementState> Save(AssistantManagementStore target, AdminMutation input) =>
                target.MutateAsync(actor, input, state => ManagementRules.Apply(state, input, protector, DateTimeOffset.UtcNow), ct);

            var duplicates = await Task.WhenAll(Save(store, add), Save(peerStore, add));
            Assert.All(duplicates, state => Assert.Equal(initial.Revision + 1, state.Revision));
            var key = Assert.Single(duplicates[0].Keys, item => item.Provider == "gemini");
            Assert.Equal(key.Id, Assert.Single(duplicates[1].Keys, item => item.Provider == "gemini").Id);
            var reused = await Assert.ThrowsAsync<ManagementConflictException>(() => Save(peerStore, add with { Label = "Changed" }));
            Assert.True(reused.Idempotency);

            var edit = new AdminMutation(Guid.NewGuid(), duplicates[0].Revision, "set-configuration",
                Enabled: false, KeyId: key.Id, Label: "Edit A", Priority: 1, Model: key.Model);
            var edits = await Task.WhenAll(TryEdit(store, edit), TryEdit(peerStore, edit with { MutationId = Guid.NewGuid(), Label = "Edit B" }));
            var winner = Assert.Single(edits.OfType<ManagementState>());
            Assert.Equal(edit.ExpectedRevision + 1, winner.Revision);

            var rotate = new AdminMutation(Guid.NewGuid(), winner.Revision, "rotate-key", KeyId: key.Id, Secret: rotatedSecret);
            var rotated = await Save(peerStore, rotate);
            var newKey = Assert.Single(rotated.Keys, item => item.Id == key.Id);
            Assert.NotEqual(key.CredentialVersion, newKey.CredentialVersion);
            Assert.Null(await store.CredentialAsync(key.Id, key.CredentialVersion, ct));

            await using var reopened = OpenSource();
            var restarted = new AssistantManagementStore(reopened);
            var reopenedRing = DataProtectionProvider.Create(ring);
            var persisted = await restarted.ReadAsync(ct);
            Assert.Equal(rotated.Revision, persisted.Revision);
            Assert.Equal(newKey, Assert.Single(persisted.Keys, item => item.Id == key.Id));
            var ciphertext = Assert.IsType<string>(await restarted.CredentialAsync(newKey.Id, newKey.CredentialVersion, ct));
            Assert.DoesNotContain(rotatedSecret, ciphertext);
            Assert.Equal(rotatedSecret, new AssistantSecretProtector(reopenedRing).Unprotect(newKey.CredentialVersion, ciphertext));
            var cursor = new ChatCursor(actor, null, 10, DateTimeOffset.UtcNow, null, null, 10, 10);
            Assert.Equal(cursor, new ChatCursors(reopenedRing).Decode(new ChatCursors(DataProtectionProvider.Create(ring)).Encode(cursor)));

            var revoke = new AdminMutation(Guid.NewGuid(), persisted.Revision, "revoke-key", KeyId: newKey.Id);
            await Save(restarted, revoke);
            Assert.NotNull(Assert.Single((await store.ReadAsync(ct)).Keys, item => item.Id == key.Id).RevokedAt);
            Assert.Null(await peerStore.CredentialAsync(newKey.Id, newKey.CredentialVersion, ct));

            async Task<ManagementState?> TryEdit(AssistantManagementStore target, AdminMutation input)
            {
                try { return await Save(target, input); }
                catch (ManagementConflictException error) when (!error.Idempotency) { return null; }
            }
        }
        finally
        {
            foreach (var file in ring.EnumerateFiles()) file.Delete();
            ring.Delete();
        }
    }

    [AssistantPostgresFact]
    public async Task NormalHostRestartsWithSettingsFaqAndDriverHistoryIntact()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var ct = timeout.Token;
        var directory = Directory.CreateTempSubdirectory("assistant-host-");
        var restoredRing = Directory.CreateTempSubdirectory("assistant-restored-ring-");
        using var signingKey = RSA.Create(2048);
        using var encryptionKey = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var certificateRequest = new CertificateRequest("CN=synthetic-assistant", encryptionKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        var certificatePath = Path.Combine(directory.FullName, "test.pfx");
        const string certificatePassword = "synthetic-pfx";
        const string providerSecret = "synthetic-local-openai-key";
        var adminToken = Token("admin");
        var driverToken = Token("driver");
        var otherDriverToken = Token("driver");
        var requestId = Guid.NewGuid();
        var diagnostics = new SyntheticDiagnostics();
        var answers = new GuestFaqTests.Answers();
        var stages = new ConcurrentQueue<(double Duration, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, active) =>
        {
            if (instrument.Meter.Name == "SmartPark.Assistant" && instrument.Name == "smartpark.assistant.stage.duration")
                active.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, duration, tags, _) => stages.Enqueue((duration, tags.ToArray())));
        listener.Start();
        var knowledge = Encoding.UTF8.GetBytes("""
            ---
            version: smoke-v1
            locale: vi
            audience: [public, driver]
            publication_state: published
            review_status: approved
            ---
            ## [payment-guide] Hướng dẫn thanh toán

            Từ khóa: thanh toán; payment

            ### Nội dung

            Hướng dẫn thanh toán giả lập.
            """);
        try
        {
            await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Pfx, certificatePassword), ct);
            await File.WriteAllBytesAsync(Path.Combine(directory.FullName, "smartpark-knowledge.md"), knowledge, ct);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "publication.json"), JsonSerializer.Serialize(new
            {
                sha256 = Convert.ToHexString(SHA256.HashData(knowledge)), version = "smoke-v1",
                approvedBy = "synthetic-test-only", role = "admin", publishedAt = now.AddHours(-1)
            }), ct);
            Guid configurationId;
            Guid conversationId;
            long revision;
            var diagnosticId = Guid.NewGuid();
            await using (var app = await StartHost(directory.FullName))
            using (var client = app.GetTestClient())
            {
                var initial = await Data(client, HttpMethod.Get, "/api/assistant/admin/settings", adminToken);
                var settings = initial.GetProperty("settings").Deserialize<ManagementSettings>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                var providers = await Data(client, HttpMethod.Post, "/api/assistant/admin/mutations", adminToken,
                    new AdminMutation(Guid.NewGuid(), settings.Revision, "save-config",
                        settings.Providers.Select(provider => provider with { Enabled = provider.Id == "openai" }).ToArray(),
                        Enabled: false, MaintenanceMessage: "Synthetic maintenance"));
                const string model = "gpt-5.4-mini";
                var added = await Data(client, HttpMethod.Post, "/api/assistant/admin/mutations", adminToken,
                    new AdminMutation(Guid.NewGuid(), providers.GetProperty("settings").GetProperty("revision").GetInt64(), "add-configuration",
                        Provider: "openai", Label: "Acceptance fixture", Secret: providerSecret, Priority: 1, Model: model));
                var current = added.GetProperty("settings");
                configurationId = current.GetProperty("configurations").EnumerateArray()
                    .Single(item => item.GetProperty("provider").GetString() == "openai").GetProperty("id").GetGuid();
                var configured = await Data(client, HttpMethod.Post, "/api/assistant/admin/mutations", adminToken,
                    new AdminMutation(Guid.NewGuid(), current.GetProperty("revision").GetInt64(), "set-configuration",
                        Enabled: true, KeyId: configurationId, Label: "Acceptance fixture", Priority: 1, Model: model));
                var enabled = await Data(client, HttpMethod.Post, "/api/assistant/admin/mutations", adminToken,
                    new AdminMutation(Guid.NewGuid(), configured.GetProperty("settings").GetProperty("revision").GetInt64(),
                        "set-enabled", Enabled: true));
                revision = enabled.GetProperty("settings").GetProperty("revision").GetInt64();
                var diagnostic = await Data(client, HttpMethod.Post, $"/api/assistant/admin/configurations/{configurationId}/test", adminToken,
                    new ConnectionTestInput(revision, diagnosticId));
                Assert.Equal("passed", diagnostic.GetProperty("outcome").GetString());
                Assert.Equal(1, diagnostics.Calls);
                Assert.Equal(0, answers.Generations);

                var faq = await Data(client, HttpMethod.Post, "/api/assistant/guest/messages", null,
                    new { requestId = Guid.NewGuid(), message = "How to pay?", locale = "en" });
                Assert.Equal("answered", faq.GetProperty("outcome").GetString());
                Assert.Equal("smoke-v1", faq.GetProperty("sources")[0].GetProperty("version").GetString());
                Assert.Equal(1, answers.Generations);
                var created = await Data(client, HttpMethod.Post, "/api/assistant/conversations", driverToken,
                    new { clientConversationId = Guid.NewGuid(), locale = "en" });
                conversationId = created.GetProperty("id").GetGuid();
                var reply = await Data(client, HttpMethod.Post, $"/api/assistant/conversations/{conversationId}/messages", driverToken,
                    new { clientMessageId = requestId, expectedRevision = 0, message = "payment" });
                Assert.Equal("completed", reply.GetProperty("state").GetString());
                Assert.Equal(2, answers.Generations);
                await app.StopAsync(ct);
            }
            var keyFiles = directory.GetFiles("key-*.xml");
            Assert.NotEmpty(keyFiles);
            foreach (var keyFile in keyFiles)
                keyFile.CopyTo(Path.Combine(restoredRing.FullName, keyFile.Name));
            await using (var restarted = await StartHost(restoredRing.FullName))
            using (var client = restarted.GetTestClient())
            {
                var settings = await Data(client, HttpMethod.Get, "/api/assistant/admin/settings", adminToken);
                Assert.Equal(revision, settings.GetProperty("settings").GetProperty("revision").GetInt64());
                var replay = await Data(client, HttpMethod.Post, $"/api/assistant/admin/configurations/{configurationId}/test", adminToken,
                    new ConnectionTestInput(revision, diagnosticId));
                Assert.Equal("passed", replay.GetProperty("outcome").GetString());
                Assert.Equal(1, diagnostics.Calls);
                var afterRestore = await Data(client, HttpMethod.Post, $"/api/assistant/admin/configurations/{configurationId}/test", adminToken,
                    new ConnectionTestInput(revision, Guid.NewGuid()));
                Assert.Equal("passed", afterRestore.GetProperty("outcome").GetString());
                Assert.Equal(2, diagnostics.Calls);
                var path = $"/api/assistant/conversations/{conversationId}";
                var result = await Data(client, HttpMethod.Get, $"{path}/requests/{requestId}", driverToken);
                Assert.Equal("completed", result.GetProperty("state").GetString());
                var history = await Data(client, HttpMethod.Get, $"{path}/messages", driverToken);
                Assert.Equal(2, history.GetProperty("items").GetArrayLength());
                using var forbidden = await Send(client, HttpMethod.Get, $"{path}/messages", otherDriverToken);
                Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
                Assert.Equal(2, answers.Generations);
                var faq = await Data(client, HttpMethod.Post, "/api/assistant/guest/messages", null,
                    new { requestId = Guid.NewGuid(), message = "payment", locale = "en" });
                Assert.Equal("answered", faq.GetProperty("outcome").GetString());
                Assert.Equal(3, answers.Generations);
                await restarted.StopAsync(ct);
            }
            var observed = stages.Select(entry =>
            {
                Assert.True(entry.Duration >= 0);
                var tag = Assert.Single(entry.Tags);
                Assert.Equal("smartpark.assistant.stage", tag.Key);
                return Assert.IsType<string>(tag.Value);
            }).Distinct().Order().ToArray();
            Assert.Equal(new[] { "chat_admission", "chat_finish", "chat_readiness", "faq_retrieval", "provider_failover" }, observed);
        }
        finally
        {
            foreach (var file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
            foreach (var file in restoredRing.EnumerateFiles()) file.Delete();
            restoredRing.Delete();
        }

        async Task<WebApplication> StartHost(string keyDirectory)
        {
            var app = AssistantGuestHost.Build(["--environment", "Production"], builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.Sources.Clear();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IoT"] = TestConnectionString(),
                    ["Assistant:Jwt:Issuer"] = "fixture-user", ["Assistant:Jwt:Audience"] = "fixture-assistant",
                    ["Assistant:Jwt:PublicKeyPem"] = signingKey.ExportRSAPublicKeyPem(),
                    ["Assistant:Management:KeyDirectory"] = keyDirectory,
                    ["Assistant:Management:KeyEncryptionCertificatePath"] = certificatePath,
                    ["Assistant:Management:KeyEncryptionCertificatePassword"] = certificatePassword,
                    ["Assistant:Guest:KnowledgeDirectory"] = directory.FullName,
                    ["Assistant:Guest:ReleaseEnabled"] = "true", ["Assistant:Chat:ReleaseEnabled"] = "true"
                });
                builder.Services.AddSingleton<IGuestAnswerFactory>(answers);
                builder.Services.AddSingleton<IProviderDiagnostics>(diagnostics);
            });
            await app.StartAsync(ct);
            return app;
        }

        string Token(string role)
        {
            var header = Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
            var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new
            {
                iss = "fixture-user", aud = "fixture-assistant", sub = Guid.NewGuid(), account_id = Guid.NewGuid(),
                jti = Guid.NewGuid(), role, nbf = now.AddMinutes(-1).ToUnixTimeSeconds(), exp = now.AddMinutes(5).ToUnixTimeSeconds()
            }));
            var unsigned = $"{header}.{payload}";
            var signature = signingKey.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{unsigned}.{Base64UrlEncoder.Encode(signature)}";
        }

        async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request, ct);
        }

        async Task<JsonElement> Data(HttpClient client, HttpMethod method, string path, string? token, object? body = null)
        {
            using var response = await Send(client, method, path, token, body);
            var text = await response.Content.ReadAsStringAsync(ct);
            Assert.True(response.IsSuccessStatusCode, text);
            using var json = JsonDocument.Parse(text);
            return json.RootElement.GetProperty("data").Clone();
        }
    }

    private sealed class SyntheticDiagnostics : IProviderDiagnostics
    {
        public int Calls;
        public Task<ProviderModel[]> DiscoverAsync(string provider, string secret, CancellationToken ct) =>
            throw new InvalidOperationException("No discovery expected in the acceptance test.");
        public Task TestAsync(string provider, string model, string secret, CancellationToken ct)
        {
            Assert.Equal("synthetic-local-openai-key", secret);
            Calls++;
            return Task.CompletedTask;
        }
    }

    [AssistantPostgresFact]
    public async Task ChatSerializesAdmissionAndTerminalWritesAndRecoversAnAbandonedRequest()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        await using var primary = OpenSource();
        await using var peer = OpenSource();
        var store = new AssistantChatStore(primary);
        var peerStore = new AssistantChatStore(peer);
        var actor = new CurrentActor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "driver", DateTimeOffset.UtcNow.AddHours(1));
        var account = actor.AccountId;
        var creationId = Guid.NewGuid();
        var creations = await Task.WhenAll(store.Create(actor, creationId, "vi", ct), peerStore.Create(actor, creationId, "vi", ct));
        var conversation = Assert.Single(creations, result => result.Created).Conversation.Id;
        Assert.All(creations, result => Assert.Equal(conversation, result.Conversation.Id));
        var snapshot = new ChatSnapshot(0, "synthetic-fingerprint", "test-v1");
        var input = new SendChat(conversation, Guid.NewGuid(), 0, "payment");
        var admissions = await Task.WhenAll(store.Admit(account, input, "same", snapshot, 15000, ct),
            peerStore.Admit(account, input, "same", snapshot, 15000, ct));
        var admitted = Assert.Single(admissions, result => result.Created).Result;
        Assert.All(admissions, result => Assert.Equal(admitted.RequestId, result.Result.RequestId));
        var conflict = await Assert.ThrowsAsync<ChatFailure>(() => peerStore.Admit(account, input, "different", snapshot, 15000, ct));
        Assert.Equal("IDEMPOTENCY_CONFLICT", conflict.Code);
        var busy = await Assert.ThrowsAsync<ChatFailure>(() => peerStore.Admit(account, input with { ClientMessageId = Guid.NewGuid() }, "other", snapshot, 15000, ct));
        Assert.Equal("CONVERSATION_BUSY", busy.Code);

        var key = input.ClientMessageId;
        var answer = new GuestReply(Guid.Empty, "knowledge", "answered", "Synthetic answer", [], [], []);
        var terminal = await Task.WhenAll(store.Finish(account, conversation, key, null, null, true, ct),
            peerStore.Finish(account, conversation, key, answer, null, false, ct));
        Assert.Equal(terminal[0].State, terminal[1].State);
        Assert.Contains(terminal[0].State, new[] { "cancelled", "completed" });
        var revision = terminal[0].State == "cancelled" ? 1 : 2;
        Assert.All(terminal, result => Assert.Equal(revision, result.Revision));

        var pending = new SendChat(conversation, Guid.NewGuid(), revision, "payment");
        await store.Admit(account, pending, "abandoned", snapshot, 15000, ct);
        await using (var expire = primary.CreateCommand("UPDATE assistant_requests SET deadline=clock_timestamp()-interval '1 second' WHERE conversation_id=$1 AND client_message_id=$2"))
        {
            expire.Parameters.AddWithValue(conversation);
            expire.Parameters.AddWithValue(pending.ClientMessageId);
            Assert.Equal(1, await expire.ExecuteNonQueryAsync(ct));
        }
        await using var reopened = OpenSource();
        var restarted = new AssistantChatStore(reopened);
        await Task.WhenAll(restarted.Recover(ct), peerStore.Recover(ct));
        Assert.Equal(revision + 2, (await restarted.Get(account, conversation, ct)).Revision);
        Assert.False(await restarted.DispatchAllowed(account, conversation, pending.ClientMessageId, ct));
        var recovered = await restarted.Finish(account, conversation, pending.ClientMessageId, answer, null, false, ct);
        Assert.Equal("failed", recovered.State);
        Assert.Equal("REQUEST_INTERRUPTED", recovered.RequestError?.Code);
        Assert.Equal(revision + 2, recovered.Revision);
        Assert.Null(recovered.Answer);
        Assert.Equal(admitted.RequestId, (await restarted.Lookup(account, conversation, key, "same", ct))?.RequestId);
        var cursor = new ChatCursor(account, conversation, 10, DateTimeOffset.UtcNow, null, null, long.MaxValue, long.MaxValue);
        var (messages, active) = await restarted.Messages(account, conversation, cursor, ct);
        Assert.Equal(revision + 1, messages.Count);
        Assert.Equal(revision - 1, messages.Count(message => message.Role == "assistant"));
        Assert.Null(active);
        var foreign = await Assert.ThrowsAsync<ChatFailure>(() => restarted.Get(Guid.NewGuid(), conversation, ct));
        Assert.Equal(404, foreign.Status);
    }

    internal static NpgsqlDataSource OpenSource() => NpgsqlDataSource.Create(TestConnectionString());

    private static string TestConnectionString()
    {
        var nonce = Environment.GetEnvironmentVariable("SMARTPARK_TEST_ASSISTANT_NONCE");
        var password = Environment.GetEnvironmentVariable("SMARTPARK_TEST_ASSISTANT_PASSWORD");
        if (!Guid.TryParseExact(nonce, "N", out _) || !Guid.TryParseExact(password, "N", out _) ||
            !int.TryParse(Environment.GetEnvironmentVariable("SMARTPARK_TEST_ASSISTANT_PORT"), out var port) || port is <= 0 or > 65535)
            throw new InvalidOperationException("Use scripts/test-assistant-postgres.ps1 to create the disposable database.");
        return new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = port, Database = $"assistant_test_{nonce}", Username = "postgres", Password = password,
            Pooling = false, Timeout = 5, CommandTimeout = 5
        }.ConnectionString;
    }
}

internal sealed class AssistantPostgresFactAttribute : FactAttribute
{
    public AssistantPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SMARTPARK_TEST_ASSISTANT_NONCE") is null)
            Skip = "Run scripts/test-assistant-postgres.ps1 to provision disposable PostgreSQL.";
    }
}
