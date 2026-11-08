using System.Diagnostics;
using System.Net;
using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Persistence;

namespace SmartParking.IoTService.Tests;

[Collection("Assistant PostgreSQL fixture")]
public sealed partial class AuthenticatedChatTests
{
    private const string CrashDirectoryVariable = "SMARTPARK_TEST_ASSISTANT_CRASH_DIR";
    private sealed record PendingChat(Guid Account, Guid User, Guid Conversation, Guid Message);

    [AssistantPostgresFact]
    public async Task AssistantPostgresHidesExpiredHistoryAndPurgesOnlyWithoutHold()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var ct = timeout.Token;
        await using var database = AssistantPostgresTests.OpenSource();
        var store = new AssistantChatStore(database);
        var fixtureActor = new CurrentActor(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "driver", DateTimeOffset.UtcNow.AddMinutes(1));
        var startupProbe = (await store.Create(fixtureActor, Guid.NewGuid(), "en", ct)).Conversation.Id;
        await using (var expireProbe = database.CreateCommand("UPDATE assistant_conversations SET expires_at = clock_timestamp() WHERE id=$1"))
        {
            expireProbe.Parameters.AddWithValue(startupProbe);
            Assert.Equal(1, await expireProbe.ExecuteNonQueryAsync(ct));
        }
        await using var host = await Fixture.Create(chatStore: store);
        // Ensure startup purge is finished before expiring the rows whose pre-purge state we assert.
        while ((await CountRows(startupProbe))[0] != 0)
            await Task.Delay(50, ct);
        var active = await CreateConversation();
        var expired = await CreateConversation();
        var held = await CreateConversation();

        await using (var expire = database.CreateCommand("""
            UPDATE assistant_conversations
            SET created_at = statement_timestamp() - interval '30 days', expires_at = statement_timestamp(), retention_hold = id = $2
            WHERE id IN ($1, $2)
            """))
        {
            expire.Parameters.AddWithValue(expired.Conversation);
            expire.Parameters.AddWithValue(held.Conversation);
            Assert.Equal(2, await expire.ExecuteNonQueryAsync(ct));
        }

        await using (var clock = database.CreateCommand("SELECT clock_timestamp()"))
            host.Clock.Now = Assert.IsType<DateTime>(await clock.ExecuteScalarAsync(ct));
        var token = host.Token();
        var visible = await host.Data(await host.Get("/api/assistant/conversations", token));
        Assert.Equal(active.Conversation, Assert.Single(visible.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        foreach (var (conversation, message, creation) in new[] { expired, held })
        {
            var path = $"/api/assistant/conversations/{conversation}";
            using var details = await host.Get(path, token);
            using var history = await host.Get($"{path}/messages", token);
            using var lookup = await host.Get($"{path}/requests/{message}", token);
            using var replay = await host.Post($"{path}/messages", new { clientMessageId = message, expectedRevision = 0, message = "payment" }, token);
            using var recreate = await host.Post("/api/assistant/conversations", new { clientConversationId = creation, locale = "en" }, token);
            Assert.All(new[] { details, history, lookup, replay, recreate }, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        }
        Assert.Equal(new long[] { 1, 2, 1, 1 }, await CountRows(expired.Conversation));
        await store.Purge(ct);
        Assert.Equal(new long[] { 0, 0, 0, 0 }, await CountRows(expired.Conversation));
        Assert.Equal(new long[] { 1, 2, 1, 1 }, await CountRows(held.Conversation));
        Assert.Equal(new long[] { 1, 2, 1, 1 }, await CountRows(active.Conversation));

        async Task<(Guid Conversation, Guid Message, Guid Creation)> CreateConversation()
        {
            var creation = Guid.NewGuid();
            var conversation = await host.Conversation(creation);
            var initial = await store.Get(host.Account, conversation, ct);
            Assert.InRange(initial.ExpiresAt - initial.CreatedAt, TimeSpan.FromDays(30), TimeSpan.FromDays(30).Add(TimeSpan.FromSeconds(1)));
            var message = Guid.NewGuid();
            var reply = await host.Data(await host.Post($"/api/assistant/conversations/{conversation}/messages",
                new { clientMessageId = message, expectedRevision = 0, message = "payment" }));
            Assert.Equal("completed", reply.GetProperty("state").GetString());
            Assert.Equal(initial.ExpiresAt, (await store.Get(host.Account, conversation, ct)).ExpiresAt);
            await using var tool = database.CreateCommand("""
                INSERT INTO assistant_tool_runs(conversation_id, tool_name, input_parameters)
                VALUES ($1, 'synthetic_fixture', '{}'::jsonb)
                """);
            tool.Parameters.AddWithValue(conversation);
            Assert.Equal(1, await tool.ExecuteNonQueryAsync(ct));
            return (conversation, message, creation);
        }

        async Task<long[]> CountRows(Guid conversation)
        {
            await using var query = database.CreateCommand("""
                SELECT ARRAY[
                    (SELECT count(*) FROM assistant_conversations WHERE id=$1),
                    (SELECT count(*) FROM assistant_messages WHERE conversation_id=$1),
                    (SELECT count(*) FROM assistant_requests WHERE conversation_id=$1),
                    (SELECT count(*) FROM assistant_tool_runs WHERE conversation_id=$1)]
                """);
            query.Parameters.AddWithValue(conversation);
            return Assert.IsType<long[]>(await query.ExecuteScalarAsync(ct));
        }
    }

    [AssistantPostgresFact]
    public async Task AssistantPostgresRestartsAfterKillingAProcessInsideGeneration()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(65));
        var ct = timeout.Token;
        var directory = Directory.CreateTempSubdirectory("assistant-crash-");
        var marker = Path.Combine(directory.FullName, "running.json");
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(AuthenticatedChatTests).Assembly.Location);
        start.ArgumentList.Add($"/TestCaseFilter:FullyQualifiedName={typeof(AuthenticatedChatTests).FullName}.{nameof(InterruptedChatWorker)}");
        start.Environment[CrashDirectoryVariable] = directory.FullName;

        try
        {
            using var child = Process.Start(start) ?? throw new InvalidOperationException("Unable to start the Assistant crash fixture.");
            var stdout = child.StandardOutput.ReadToEndAsync(ct);
            var stderr = child.StandardError.ReadToEndAsync(ct);
            await using var database = AssistantPostgresTests.OpenSource();
            var store = new AssistantChatStore(database);
            PendingChat pending;
            try
            {
                var ready = Path.Combine(directory.FullName, "ready");
                while (!File.Exists(ready))
                {
                    if (child.HasExited)
                        throw new InvalidOperationException($"Assistant crash fixture exited before dispatch. {await stdout} {await stderr}");
                    await Task.Delay(100, ct);
                }
                pending = JsonSerializer.Deserialize<PendingChat>(await File.ReadAllTextAsync(marker, ct))!;
                Assert.True(await store.DispatchAllowed(pending.Account, pending.Conversation, pending.Message, ct));
            }
            finally
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }

            // Wait for startup/periodic recovery rather than allowing GET request lookup to repair the row.
            await using var restarted = await Fixture.Create(chatStore: store);
            restarted.EnableProvider();
            while ((await store.Get(pending.Account, pending.Conversation, ct)).Revision == 1)
                await Task.Delay(200, ct);

            var token = restarted.Token(pending.Account, pending.User);
            var path = $"/api/assistant/conversations/{pending.Conversation}";
            var result = await restarted.Data(await restarted.Get($"{path}/requests/{pending.Message}", token));
            Assert.Equal("failed", result.GetProperty("state").GetString());
            Assert.Equal("REQUEST_INTERRUPTED", result.GetProperty("requestError").GetProperty("code").GetString());
            Assert.Equal(2, result.GetProperty("revision").GetInt64());
            var replay = await restarted.Data(await restarted.Post($"{path}/messages",
                new { clientMessageId = pending.Message, expectedRevision = 0, message = "payment" }, token));
            Assert.Equal(result.GetProperty("requestId").GetGuid(), replay.GetProperty("requestId").GetGuid());
            Assert.Equal("failed", replay.GetProperty("state").GetString());
            var history = await restarted.Data(await restarted.Get($"{path}/messages", token));
            Assert.Equal("user", Assert.Single(history.GetProperty("items").EnumerateArray()).GetProperty("role").GetString());
            Assert.Equal(0, restarted.Answers.Calls);
        }
        finally
        {
            foreach (var file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    [InterruptedChatWorkerFact]
    public async Task InterruptedChatWorker()
    {
        var directory = Environment.GetEnvironmentVariable(CrashDirectoryVariable)!;
        Assert.True(Path.IsPathFullyQualified(directory) && Directory.Exists(directory));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var database = AssistantPostgresTests.OpenSource();
        await using var child = await Fixture.Create(chatStore: new AssistantChatStore(database));
        child.EnableProvider();
        child.Answers.Block = true;
        var conversation = await child.Conversation();
        var message = Guid.NewGuid();
        var request = child.Post($"/api/assistant/conversations/{conversation}/messages",
            new { clientMessageId = message, expectedRevision = 0, message = "payment" });
        await child.Answers.Entered.Task.WaitAsync(timeout.Token);
        Assert.Equal(1, child.Answers.Generations);
        var pending = new PendingChat(child.Account, child.User, conversation, message);
        await File.WriteAllTextAsync(Path.Combine(directory, "running.json"), JsonSerializer.Serialize(pending), timeout.Token);
        await File.WriteAllTextAsync(Path.Combine(directory, "ready"), "", timeout.Token);
        using var unexpectedResponse = await request;
        Assert.Fail("The parent was expected to kill this process during generation.");
    }

    private sealed class InterruptedChatWorkerFactAttribute : FactAttribute
    {
        public InterruptedChatWorkerFactAttribute()
        {
            if (Environment.GetEnvironmentVariable(CrashDirectoryVariable) is null)
                Skip = "Child process for the disposable PostgreSQL crash test.";
        }
    }
}
