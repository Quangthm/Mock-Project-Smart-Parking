using System.Diagnostics;
using MediatR;
using SmartParking.IoTService.Application.Assistant.Authorization;

namespace SmartParking.IoTService.Application.Assistant.Management;

public sealed class ProviderDiagnosticsHandlers(IAssistantManagementStore store, IAssistantSecretProtector secrets,
    IProviderDiagnostics provider, IConnectionTestStore tests, ModelDiscoveryCache cache,
    ICurrentActor actors, TimeProvider clock)
    : IRequestHandler<DiscoverModels, ModelPage>, IRequestHandler<TestConnection, ConnectionTestResponse>
{
    private CurrentActor Authorize()
    {
        var actor = actors.Actor ?? throw new AssistantAccessException(false);
        if (actor.ExpiresAt <= clock.GetUtcNow()) throw new AssistantAccessException(false);
        if (actor.Role != "admin") throw new AssistantAccessException(true);
        return actor;
    }
    private async Task<(ManagementState State, ManagedKey Key)> Saved(Guid id, long? revision, CancellationToken ct)
    {
        ManagementRules.Require(id != Guid.Empty && revision is >= 0 and <= ManagementRules.MaxSafeInteger);
        if (!store.IsConfigured || !secrets.IsConfigured) throw new ManagementUnavailableException();
        var state = ManagementRules.Upgrade(await store.ReadAsync(ct));
        var key = state.Keys.SingleOrDefault(k => k.Id == id && k.RevokedAt is null)
            ?? throw new ProviderDiagnosticsException(404, "RESOURCE_NOT_FOUND");
        if (state.Revision != revision) throw new ManagementConflictException(false);
        return (state, key);
    }
    private async Task<string> Secret(ManagedKey key, long revision, CancellationToken ct)
    {
        var encrypted = await store.CredentialAsync(key.Id, key.CredentialVersion, ct)
            ?? throw new ManagementUnavailableException();
        var secret = secrets.Unprotect(key.CredentialVersion, encrypted);
        await Saved(key.Id, revision, ct);
        Authorize();
        return secret;
    }
    public async Task<ModelPage> Handle(DiscoverModels request, CancellationToken ct)
    {
        var actor = Authorize();
        var input = request.Input;
        ManagementRules.Require(input is not null && (input.Cursor is null || input.Cursor.Length is > 0 and <= 2048));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            string providerId, binding;
            string? secret = null;
            ManagedKey? key = null;
            if (input!.ConfigurationId is { } id)
            {
                ManagementRules.Require(input.Provider is null && input.Secret is null);
                var snapshot = await Saved(id, input.ExpectedRevision, deadline.Token);
                key = snapshot.Key; providerId = key.Provider;
                binding = cache.Bind($"{actor.AccountId}:{id}:{key.CredentialVersion}:{snapshot.State.Revision}:{providerId}");
            }
            else
            {
                ManagementRules.Require(input.ExpectedRevision is null && input.Provider is "gemini" or "openai" &&
                    input.Secret is { Length: >= 8 and <= 1024 } && !input.Secret.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)));
                providerId = input.Provider!; secret = input.Secret!;
                binding = cache.Bind($"{actor.AccountId}:{providerId}:{secret}");
            }
            var page = input.Cursor is not null ? cache.Page(binding, input.Cursor, null, false)
                : key is null ? null : cache.Get(binding);
            if (page is null)
            {
                if (key is not null) secret = await Secret(key, input.ExpectedRevision!.Value, deadline.Token);
                var models = await provider.DiscoverAsync(providerId, secret!, deadline.Token).WaitAsync(deadline.Token);
                page = cache.Page(binding, null, models, key is not null);
            }
            if (key is not null) await Saved(key.Id, input.ExpectedRevision, deadline.Token);
            Authorize();
            return page;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ProviderDiagnosticsException(504, "PROVIDER_TIMEOUT"); }
    }
    public async Task<ConnectionTestResponse> Handle(TestConnection request, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(ConnectionTestTiming.Execution);
        try { return await Test(request, deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ManagementUnavailableException(); }
    }

    private static ConnectionTestResponse Replay(ConnectionTestResult? result)
    {
        if (result?.Outcome == "not-dispatched")
        {
            throw result.Code switch
            {
                "INVALID_TOKEN" => new AssistantAccessException(false),
                "ASSISTANT_FORBIDDEN" => new AssistantAccessException(true),
                "RESOURCE_NOT_FOUND" => new ProviderDiagnosticsException(404, "RESOURCE_NOT_FOUND"),
                "REVISION_CONFLICT" => new ManagementConflictException(false),
                _ => new ManagementUnavailableException()
            };
        }
        return new(result, result is null);
    }

    private async Task<ConnectionTestResponse> Test(TestConnection request, CancellationToken ct)
    {
        var actor = Authorize();
        ManagementRules.Require(request.Input is not null && request.Input!.TestRequestId != Guid.Empty);
        var input = request.Input!;
        ManagementRules.Require(input.ExpectedRevision is >= 0 and <= ManagementRules.MaxSafeInteger && request.ConfigurationId != Guid.Empty);
        var prior = await tests.FindAsync(actor.AccountId, request.ConfigurationId, input.ExpectedRevision!.Value, input.TestRequestId, ct);
        if (prior is not null)
        {
            var replay = prior.Result;
            if (replay is not null)
            {
                var latest = await store.ReadAsync(ct);
                replay = replay with { Current = latest.Revision == replay.TestedRevision && latest.Keys.Any(k => k.Id == replay.ConfigurationId && k.RevokedAt is null) };
            }
            Authorize();
            return Replay(replay);
        }
        var (state, key) = await Saved(request.ConfigurationId, input.ExpectedRevision, ct);
        var secret = await Secret(key, state.Revision, ct);
        var admission = await tests.AdmitAsync(actor.AccountId, key.Id, state.Revision, request.Input!.TestRequestId, key.Model!, ct);
        if (!admission.Dispatch)
        {
            Authorize();
            return Replay(admission.Result);
        }
        // Durable receipt means browser cancellation must not allow a second generation of this ID.
        var dispatched = false;
        var elapsed = Stopwatch.StartNew();
        var outcome = "passed";
        string? code = null;
        try
        {
            await Saved(key.Id, state.Revision, ct);
            Authorize();
            ct.ThrowIfCancellationRequested();
            dispatched = true;
            await provider.TestAsync(key.Provider, key.Model!, secret, ct).WaitAsync(ct);
        }
        catch (ProviderDiagnosticsException ex)
        { outcome = ex.Status is 504 or 503 ? "unknown" : "failed"; code = ex.Code; }
        catch (OperationCanceledException) { outcome = "unknown"; code = "PROVIDER_TIMEOUT"; }
        catch (ManagementConflictException) { outcome = "failed"; code = "REVISION_CONFLICT"; }
        catch (AssistantAccessException ex) { outcome = "failed"; code = ex.Authenticated ? "ASSISTANT_FORBIDDEN" : "INVALID_TOKEN"; }
        catch (ManagementUnavailableException) { outcome = "unknown"; code = "MANAGEMENT_UNAVAILABLE"; }
        catch (Exception) { outcome = "unknown"; code = "PROVIDER_UNAVAILABLE"; }
        if (!dispatched) outcome = "not-dispatched";
        elapsed.Stop();
        var current = false;
        using var completion = new CancellationTokenSource(ConnectionTestTiming.Completion);
        try { var latest = await store.ReadAsync(completion.Token); current = latest.Revision == state.Revision && latest.Keys.Any(k => k.Id == key.Id && k.RevokedAt is null && k.CredentialVersion == key.CredentialVersion); }
        catch (ManagementUnavailableException) { }
        var result = new ConnectionTestResult(request.Input!.TestRequestId, key.Id, state.Revision, key.Model!, outcome,
            elapsed.ElapsedMilliseconds, clock.GetUtcNow(), current, code);
        await tests.CompleteAsync(actor.AccountId, result, completion.Token);
        Authorize();
        return Replay(result);
    }
}
