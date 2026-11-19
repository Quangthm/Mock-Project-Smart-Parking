using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Tests;

public sealed class ProviderDiagnosticsHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("driver")]
    public async Task UnauthorizedCallsNeverReachCredentialsOrProviders(string? role)
    {
        var f = new Fixture(role);
        await Assert.ThrowsAsync<AssistantAccessException>(() => f.Handler.Handle(new DiscoverModels(new("gemini", "synthetic-secret")), default));
        await Assert.ThrowsAsync<AssistantAccessException>(() => f.Handler.Handle(new TestConnection(f.Key.Id, new(0, Guid.NewGuid())), default));
        Assert.Equal(0, f.Store.Reads); Assert.Equal(0, f.Provider.Calls);
    }
    [Fact]
    public async Task SavedDiscoveryCachesButRotationOrStaleRevisionCannotReuseResults()
    {
        var f = new Fixture();
        var query = new DiscoverModels(new(ConfigurationId: f.Key.Id, ExpectedRevision: 0));
        await f.Handler.Handle(query, default); await f.Handler.Handle(query, default);
        Assert.Equal(1, f.Provider.Calls);
        f.Store.State = f.Store.State with { Revision = 1, Keys = [f.Key with { CredentialVersion = Guid.NewGuid() }] };
        await Assert.ThrowsAsync<ManagementConflictException>(() => f.Handler.Handle(query, default));
        await f.Handler.Handle(new DiscoverModels(new(ConfigurationId: f.Key.Id, ExpectedRevision: 1)), default);
        Assert.Equal(2, f.Provider.Calls);
    }
    [Fact]
    public async Task UnsavedDiscoveryDoesNotCacheAndRejectsMixedUnion()
    {
        var f = new Fixture(); var query = new DiscoverModels(new("gemini", "synthetic-secret"));
        await f.Handler.Handle(query, default); await f.Handler.Handle(query, default);
        Assert.Equal(2, f.Provider.Calls); Assert.Equal(0, f.Store.Reads);
        await Assert.ThrowsAsync<ManagementValidationException>(() => f.Handler.Handle(new DiscoverModels(new("gemini", "synthetic-secret", f.Key.Id, 0)), default));
    }
    [Fact]
    public async Task DisabledConfigurationCanTestAndReplayAfterEditIsNotCurrent()
    {
        var f = new Fixture();
        var request = new TestConnection(f.Key.Id, new(0, Guid.NewGuid()));
        var result = await f.Handler.Handle(request, default);
        Assert.Equal("passed", result.Result!.Outcome);
        Assert.True(result.Result.Current); Assert.False(f.Store.State.Enabled); Assert.False(f.Key.Enabled);
        f.Store.State = f.Store.State with { Revision = 1 };
        var replay = await f.Handler.Handle(request, default);
        Assert.False(replay.Result!.Current); Assert.Equal(1, f.Provider.Calls);
        await Assert.ThrowsAsync<ManagementConflictException>(() => f.Handler.Handle(request with { Input = new(1, request.Input.TestRequestId) }, default));
    }
    [Fact]
    public async Task InFlightReplayDoesNotDispatchAgainAndRevisionChangeMarksResultStale()
    {
        var f = new Fixture(); var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Test = _ => pending.Task;
        var request = new TestConnection(f.Key.Id, new(0, Guid.NewGuid()));
        var running = f.Handler.Handle(request, default);
        var replay = await f.Handler.Handle(request, default);
        Assert.True(replay.Running); Assert.Equal(1, f.Provider.Calls);
        f.Store.State = f.Store.State with { Revision = 1 };
        pending.SetResult();
        Assert.False((await running).Result!.Current);
    }
    [Theory]
    [InlineData(502, "failed")]
    [InlineData(429, "failed")]
    [InlineData(503, "unknown")]
    [InlineData(504, "unknown")]
    public async Task FailureIsRecordedAndReplayDoesNotCallProvider(int status, string outcome)
    {
        var f = new Fixture(); f.Provider.Test = _ => throw new ProviderDiagnosticsException(status, "SAFE_CODE");
        var request = new TestConnection(f.Key.Id, new(0, Guid.NewGuid()));
        var result = await f.Handler.Handle(request, default);
        Assert.Equal(outcome, result.Result!.Outcome);
        Assert.Equal(result.Result, (await f.Handler.Handle(request, default)).Result);
        Assert.Equal(1, f.Provider.Calls);
    }
    [Fact]
    public async Task RevokedConfigurationAndChangedRevisionNeverDispatch()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ManagementConflictException>(() => f.Handler.Handle(new TestConnection(f.Key.Id, new(5, Guid.NewGuid())), default));
        f.Store.State = f.Store.State with { Keys = [f.Key with { RevokedAt = DateTimeOffset.UtcNow }] };
        await Assert.ThrowsAsync<ProviderDiagnosticsException>(() => f.Handler.Handle(new TestConnection(f.Key.Id, new(0, Guid.NewGuid())), default));
        Assert.Equal(0, f.Provider.Calls);
    }
    [Fact]
    public async Task RevocationAfterAdmissionReplays404WithoutProviderDispatch()
    {
        var f = new Fixture();
        f.Receipts.Admitted = () => f.Store.State = f.Store.State with { Keys = [] };
        var request = new TestConnection(f.Key.Id, new(0, Guid.NewGuid()));
        for (var i = 0; i < 2; i++)
        {
            var error = await Assert.ThrowsAsync<ProviderDiagnosticsException>(() => f.Handler.Handle(request, default));
            Assert.Equal(404, error.Status);
        }
        Assert.Equal(0, f.Provider.Calls);
    }

    private sealed class Fixture
    {
        public ManagedKey Key = new(Guid.NewGuid(), "gemini", "Saved", false, 1, Guid.NewGuid(), DateTimeOffset.UtcNow, null, "gemini-test");
        public Store Store;
        public Provider Provider = new();
        public Receipts Receipts = new();
        public ProviderDiagnosticsHandlers Handler;
        public Fixture(string? role = "admin")
        {
            Store = new() { State = ManagementState.Empty with { Keys = [Key] } };
            Handler = new(Store, new Protector(), Provider, Receipts, new(TimeProvider.System), new ActorSource(role), TimeProvider.System);
        }
    }
    private sealed class ActorSource(string? role) : ICurrentActor
    { public CurrentActor? Actor { get; } = role is null ? null : new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), role, DateTimeOffset.UtcNow.AddMinutes(5)); }
    private sealed class Protector : IAssistantSecretProtector
    { public bool IsConfigured => true; public string Protect(Guid version, string secret) => "encrypted"; public string Unprotect(Guid version, string protectedValue) => "synthetic-secret"; }
    private sealed class Store : IAssistantManagementStore
    {
        public ManagementState State = ManagementState.Empty; public int Reads;
        public bool IsConfigured => true;
        public Task<ManagementState> ReadAsync(CancellationToken ct) { Reads++; return Task.FromResult(State); }
        public Task<string?> CredentialAsync(Guid slot, Guid version, CancellationToken ct) => Task.FromResult<string?>("encrypted");
        public Task<ManagementState> MutateAsync(Guid actor, AdminMutation input, Func<ManagementState, ManagementWrite> change, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Provider : IProviderDiagnostics
    {
        public int Calls; public Func<CancellationToken, Task> Test = _ => Task.CompletedTask;
        public Task<ProviderModel[]> DiscoverAsync(string provider, string secret, CancellationToken ct) { Calls++; return Task.FromResult<ProviderModel[]>([new("gemini-test", "Test")]); }
        public Task TestAsync(string provider, string model, string secret, CancellationToken ct) { Calls++; return Test(ct); }
    }
    // Port fake verifies orchestration only, not PostgreSQL locking or durable recovery.
    private sealed class Receipts : IConnectionTestStore
    {
        public Action? Admitted;
        private readonly Dictionary<Guid, (Guid Config, long Revision, ConnectionTestResult? Result)> entries = [];
        public Task<TestAdmission?> FindAsync(Guid actor, Guid configuration, long revision, Guid requestId, CancellationToken ct)
        {
            if (!entries.TryGetValue(requestId, out var entry)) return Task.FromResult<TestAdmission?>(null);
            if (entry.Config != configuration || entry.Revision != revision) throw new ManagementConflictException(true);
            return Task.FromResult<TestAdmission?>(new(false, entry.Result));
        }
        public Task<TestAdmission> AdmitAsync(Guid actor, Guid configuration, long revision, Guid requestId, string model, CancellationToken ct)
        { entries.Add(requestId, (configuration, revision, null)); Admitted?.Invoke(); return Task.FromResult(new TestAdmission(true, null)); }
        public Task CompleteAsync(Guid actor, ConnectionTestResult result, CancellationToken ct)
        { entries[result.TestRequestId] = (result.ConfigurationId, result.TestedRevision, result); return Task.CompletedTask; }
    }
}
