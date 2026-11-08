using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Tests;

public sealed class ManagedFailoverTests
{
    private static readonly GroundedReply Answer = new("verified answer", [new("booking", "1", "Title", "Content")], "openai", "model");
    private static readonly GroundedReply Documents = new("document guidance", [new("booking", "1", "Title", "Full content")], null, null, "knowledge");

    [Fact]
    public async Task KeyAuthFailureTriesNextEnabledKeyInPriorityOrderOnce()
    {
        var f = Fixture();
        f.Provider.Respond = (call, _) => call.Secret == "key-one"
            ? Task.FromException<GroundedReply>(Rejected(401)) : Task.FromResult(Answer);
        Assert.Equal(Answer, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal(["key-one", "key-two"], f.Provider.Calls.Select(c => c.Secret));
        Assert.Equal(0, f.Provider.GuidanceCalls);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task ProviderWideFailureSkipsSameModelsOtherKeysAndUsesNextProvider(int status)
    {
        var f = Fixture();
        f.Provider.Respond = (call, _) => call.Provider == "gemini"
            ? Task.FromException<GroundedReply>(Rejected(status)) : Task.FromResult(Answer);
        Assert.Equal(Answer, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal(["gemini", "openai"], f.Provider.Calls.Select(c => c.Provider));
    }

    [Fact]
    public async Task ThreeAttemptsMaximumThenRealDocumentPortWithoutRetryingAKey()
    {
        var f = Fixture();
        f.Provider.Respond = (_, _) => Task.FromException<GroundedReply>(Rejected(429));
        var answer = await f.Generator.GenerateAsync("booking", default);
        Assert.Equal(Documents, answer);
        Assert.Equal(["key-one", "key-two", "key-three"], f.Provider.Calls.Select(c => c.Secret));
        Assert.Equal(1, f.Provider.GuidanceCalls);
        Assert.Equal(3, f.Provider.Calls.Select(c => c.Secret).Distinct().Count());
    }

    [Fact]
    public async Task CooldownSkipsFailedModelUntilExpiryAndRefreshClearsIt()
    {
        var f = Fixture();
        f.Provider.Respond = (_, _) => Task.FromException<GroundedReply>(Rejected(503));
        Assert.Equal(Documents, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal(2, f.Provider.Calls.Count); // Gemini + OpenAI, not all keys.
        Assert.Equal(Documents, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal(2, f.Provider.Calls.Count); // No generation during cooldown.
        f.Clock.Advance(TimeSpan.FromSeconds(61));
        await f.Generator.GenerateAsync("booking", default);
        Assert.Equal(4, f.Provider.Calls.Count);
        await f.Generator.RefreshAsync(default);
        await f.Generator.GenerateAsync("booking", default);
        Assert.Equal(6, f.Provider.Calls.Count);
    }

    [Fact]
    public async Task ANewSettingsRevisionDoesNotInheritOldKeyCooldown()
    {
        var f = Fixture();
        f.Provider.Respond = (_, _) => Task.FromException<GroundedReply>(Rejected(429));
        await f.Generator.GenerateAsync("booking", default);
        f.Store.State = f.Store.State with { Revision = 2, Keys = f.Store.State.Keys.Select(k => k with { CredentialVersion = Guid.NewGuid() }).ToArray() };
        f.Provider.Respond = (_, _) => Task.FromResult(Answer);
        Assert.Equal(Answer, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal("key-one", f.Provider.Calls.Last().Secret);
        Assert.Equal(4, f.Provider.Calls.Count);
    }

    [Fact]
    public async Task DisabledAndRevokedKeysAreNeverDispatched()
    {
        var f = Fixture();
        f.Store.State = f.Store.State with { Keys = f.Store.State.Keys.Select((key, i) => i == 0 ? key with { Enabled = false }
            : i == 1 ? key with { RevokedAt = DateTimeOffset.UtcNow } : key).ToArray() };
        Assert.Equal(Answer, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal("key-three", Assert.Single(f.Provider.Calls).Secret);
        f.Store.State = f.Store.State with { Revision = 2, Providers = f.Store.State.Providers.Select(p => p with { Enabled = false }).ToArray() };
        await Assert.ThrowsAsync<ManagementUnavailableException>(() => f.Generator.GenerateAsync("booking", default));
        Assert.Single(f.Provider.Calls);
        Assert.Equal(0, f.Provider.GuidanceCalls);
    }

    [Fact]
    public async Task LateFailureFromOldRevisionCannotChangeNewRevisionHealth()
    {
        var f = Fixture();
        var pending = new TaskCompletionSource<GroundedReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Respond = (_, _) => pending.Task;
        var oldRequest = f.Generator.GenerateAsync("booking", default);
        Assert.Single(f.Provider.Calls);
        f.Store.State = f.Store.State with { Revision = 2 };
        f.Provider.Respond = (call, _) => call.Provider == "gemini" ? Task.FromException<GroundedReply>(Rejected(503)) : Task.FromResult(Answer);
        Assert.Equal(Answer, await f.Generator.GenerateAsync("booking", default));
        pending.SetException(Rejected(401));
        await Assert.ThrowsAsync<ManagementUnavailableException>(() => oldRequest);
        var before = f.Provider.Calls.Count;
        Assert.Equal(Answer, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal(before + 1, f.Provider.Calls.Count);
        Assert.Equal("openai", f.Provider.Calls.Last().Provider);
    }

    [Theory]
    [InlineData(DemoGroundedFailure.Knowledge)]
    [InlineData(DemoGroundedFailure.Configuration)]
    [InlineData(DemoGroundedFailure.InvalidResponse)]
    [InlineData(DemoGroundedFailure.ResponseTooLarge)]
    public async Task GuardAndCorpusFailuresNeverTryAnotherProviderOrReturnDocuments(DemoGroundedFailure failure)
    {
        var f = Fixture();
        f.Provider.Respond = (_, _) => Task.FromException<GroundedReply>(new DemoGroundedUnavailableException(failure));
        var error = await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => f.Generator.GenerateAsync("booking", default));
        Assert.Equal(failure, error.Failure);
        Assert.Single(f.Provider.Calls);
        Assert.Equal(0, f.Provider.GuidanceCalls);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(302)]
    public async Task InvalidProviderConfigurationIsNotHiddenByFallback(int status)
    {
        var f = Fixture();
        f.Provider.Respond = (_, _) => Task.FromException<GroundedReply>(Rejected(status));
        await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => f.Generator.GenerateAsync("booking", default));
        Assert.Single(f.Provider.Calls);
        Assert.Equal(0, f.Provider.GuidanceCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaintenanceBeforeOrDuringFailoverPreventsFurtherDispatchAndDocuments(bool during)
    {
        var f = Fixture();
        if (!during) f.Store.State = f.Store.State with { Enabled = false };
        else f.Provider.Respond = (_, _) => {
            f.Store.State = f.Store.State with { Enabled = false, Revision = 2 };
            return Task.FromException<GroundedReply>(Rejected(401));
        };
        await Assert.ThrowsAsync<AssistantMaintenanceException>(() => f.Generator.GenerateAsync("booking", default));
        Assert.Equal(during ? 1 : 0, f.Provider.Calls.Count);
        Assert.Equal(0, f.Provider.GuidanceCalls);
    }

    [Fact]
    public async Task RevisionChangeDuringDecryptionDoesNotSendStaleCredentials()
    {
        var f = Fixture();
        f.Protector.OnUnprotect = () => f.Store.State = f.Store.State with { Revision = 2 };
        await Assert.ThrowsAsync<ManagementUnavailableException>(() => f.Generator.GenerateAsync("booking", default));
        Assert.Empty(f.Provider.Calls);
        Assert.Equal(0, f.Provider.GuidanceCalls);
    }

    [Fact]
    public async Task RevocationBetweenAttemptsDoesNotDispatchCachedNextKey()
    {
        var f = Fixture();
        f.Provider.Respond = (_, _) => {
            f.Store.State = f.Store.State with { Revision = 2, Keys = [] };
            return Task.FromException<GroundedReply>(Rejected(401));
        };
        await Assert.ThrowsAsync<ManagementUnavailableException>(() => f.Generator.GenerateAsync("booking", default));
        Assert.Single(f.Provider.Calls);
        Assert.Equal(0, f.Provider.GuidanceCalls);
    }

    [Fact]
    public async Task BusyAdmissionFallsBackImmediatelyInsteadOfTryingOtherSlots()
    {
        var f = Fixture();
        f.Provider.Respond = (_, _) => Task.FromException<GroundedReply>(new DemoGroundedUnavailableException(DemoGroundedFailure.Busy));
        Assert.Equal(Documents, await f.Generator.GenerateAsync("booking", default));
        Assert.Single(f.Provider.Calls);
        Assert.Equal(1, f.Provider.GuidanceCalls);
    }

    [Fact]
    public async Task ClientCancellationDoesNotFailoverOrReturnGuidance()
    {
        var f = Fixture();
        using var ct = new CancellationTokenSource();
        f.Provider.Respond = (_, token) => { ct.Cancel(); return Task.FromCanceled<GroundedReply>(token); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Generator.GenerateAsync("booking", ct.Token));
        Assert.Single(f.Provider.Calls);
        Assert.Equal(0, f.Provider.GuidanceCalls);
    }

    [Fact]
    public async Task AttemptTimeoutMovesToDifferentProviderWithinOverallBudget()
    {
        var f = Fixture(new() { TotalTimeout = TimeSpan.FromSeconds(2), AiTimeout = TimeSpan.FromSeconds(1), AttemptTimeout = TimeSpan.FromMilliseconds(40) });
        f.Provider.Respond = async (call, token) => {
            if (call.Provider == "gemini") await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Answer;
        };
        Assert.Equal(Answer, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal(["gemini", "openai"], f.Provider.Calls.Select(c => c.Provider));
    }

    [Fact]
    public async Task NonCooperativeProviderCannotExceedAiBudgetAndHasTimeForGuidance()
    {
        var f = Fixture(new() { TotalTimeout = TimeSpan.FromSeconds(2), AiTimeout = TimeSpan.FromMilliseconds(120), AttemptTimeout = TimeSpan.FromMilliseconds(60) });
        var stalled = new TaskCompletionSource<GroundedReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Respond = (_, _) => stalled.Task;
        var timer = Stopwatch.StartNew();
        try
        {
            Assert.Equal(Documents, await f.Generator.GenerateAsync("booking", default));
            Assert.InRange(f.Provider.Calls.Count, 1, 2); // One stalled call per provider, not per same-model key.
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2));
        }
        finally { stalled.TrySetResult(Answer); }
    }

    [Fact]
    public async Task MissingCredentialsAndFallbackKnowledgeErrorsRemainFailures()
    {
        var f = Fixture();
        f.Store.MissingCredential = true;
        await Assert.ThrowsAsync<ManagementUnavailableException>(() => f.Generator.GenerateAsync("booking", default));
        Assert.Empty(f.Provider.Calls);
        f.Store.MissingCredential = false;
        f.Provider.Respond = (_, _) => Task.FromException<GroundedReply>(Rejected(503));
        f.Provider.GuidanceError = new DemoGroundedUnavailableException(DemoGroundedFailure.Knowledge);
        var error = await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => f.Generator.GenerateAsync("booking", default));
        Assert.Equal(DemoGroundedFailure.Knowledge, error.Failure);
    }

    private static DemoGroundedUnavailableException Rejected(int status) => new(DemoGroundedFailure.ProviderRejected, status);
    [Fact]
    public async Task ModelFailureDoesNotSkipDifferentModelInSameProvider()
    {
        var f = Fixture();
        f.Store.State = f.Store.State with { Keys = f.Store.State.Keys.Select((k, i) => k with { Model = i == 0 ? "model-one" : "model-two" }).ToArray() };
        f.Provider.Respond = (call, _) => call.Model == "model-one" ? Task.FromException<GroundedReply>(Rejected(503)) : Task.FromResult(Answer);
        Assert.Equal(Answer, await f.Generator.GenerateAsync("booking", default));
        Assert.Equal(["model-one", "model-two"], f.Provider.Calls.Select(c => c.Model));
        Assert.All(f.Provider.Calls, call => Assert.Equal("gemini", call.Provider));
    }

    private static (ManagedDemoGenerator Generator, Store Store, Protector Protector, Provider Provider, Clock Clock) Fixture(ManagedFailoverOptions? policy = null)
    {
        var clock = new Clock(); var store = new Store(); var protector = new Protector(); var provider = new Provider();
        return (new(store, protector, provider, clock, NullLogger<ManagedDemoGenerator>.Instance, policy), store, protector, provider, clock);
    }
    private sealed record Call(string Provider, string Model, string Secret);
    private sealed class Provider : IManagedAnswerProvider
    {
        public List<Call> Calls { get; } = [];
        public int GuidanceCalls;
        public Exception? GuidanceError;
        public Func<Call, CancellationToken, Task<GroundedReply>> Respond = (_, _) => Task.FromResult(Answer);
        public Task<GroundedReply> GenerateAsync(string provider, string model, string secret, string message, CancellationToken ct)
        { var call = new Call(provider, model, secret); Calls.Add(call); return Respond(call, ct); }
        public Task<GroundedReply> KnowledgeAsync(string message, CancellationToken ct)
        { GuidanceCalls++; return GuidanceError is null ? Task.FromResult(Documents) : Task.FromException<GroundedReply>(GuidanceError); }
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan span) => now += span;
    }
    private sealed class Protector : IAssistantSecretProtector
    {
        public Action? OnUnprotect;
        public bool IsConfigured => true;
        public string Protect(Guid version, string secret) => secret;
        public string Unprotect(Guid version, string value) { OnUnprotect?.Invoke(); return value; }
    }
    private sealed class Store : IAssistantManagementStore
    {
        public bool IsConfigured => true;
        public bool MissingCredential;
        public ManagementState State { get; set; }
        private readonly Dictionary<Guid, string> credentials;
        public Store()
        {
            var keys = new[] { ("gemini", "key-one", 1), ("gemini", "key-two", 2), ("openai", "key-three", 1), ("openai", "key-four", 2) }
                .Select(x => (Key: new ManagedKey(Guid.NewGuid(), x.Item1, "Test", true, x.Item3, Guid.NewGuid(), DateTimeOffset.UtcNow, null), Secret: x.Item2)).ToArray();
            State = new(1, true, "Maintenance", [new("gemini", "g-model", true, 1), new("openai", "o-model", true, 2)], keys.Select(x => x.Key).ToArray());
            credentials = keys.ToDictionary(x => x.Key.Id, x => x.Secret);
        }
        public Task<ManagementState> ReadAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(State); }
        public Task<string?> CredentialAsync(Guid slot, Guid version, CancellationToken ct) => Task.FromResult(MissingCredential ? null : credentials[slot]);
        public Task<ManagementState> MutateAsync(Guid actor, AdminMutation input, Func<ManagementState, ManagementWrite> change, CancellationToken ct) => throw new NotSupportedException();
    }
}
