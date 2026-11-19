using SmartParking.IoTService.Application.Assistant.Knowledge;
using Microsoft.Extensions.Logging;
namespace SmartParking.IoTService.Application.Assistant.Management;
public interface IManagedAnswerProvider
{
    Task<GroundedReply> GenerateAsync(string provider, string model, string secret, string message, CancellationToken ct);
    Task<GroundedReply> KnowledgeAsync(string message, CancellationToken ct);
}
public sealed record ManagedFailoverOptions
{
    public int MaximumAttempts { get; init; } = 3;
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(20);
    public TimeSpan AiTimeout { get; init; } = TimeSpan.FromSeconds(18);
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(7);
    public TimeSpan Cooldown { get; init; } = TimeSpan.FromSeconds(60);
}

// Application owns selection, budgets and current-settings checks. Adapters own HTTP/doc I/O.
public sealed class ManagedDemoGenerator(IAssistantManagementStore store, IAssistantSecretProtector secrets,
    IManagedAnswerProvider provider, TimeProvider clock, ILogger<ManagedDemoGenerator> logger, ManagedFailoverOptions? options = null,
    AssistantStageMetrics? metrics = null)
    : IDemoGroundedGenerator, IAssistantManagedRuntime
{
    private readonly ManagedFailoverOptions policy = Validate(options ?? new());
    private readonly object healthGate = new();
    private readonly Dictionary<Guid, DateTimeOffset> keyCooldowns = [];
    private readonly Dictionary<string, DateTimeOffset> modelCooldowns = new(StringComparer.Ordinal);
    private long healthRevision = -1;
    public bool ReleaseAuthorized => store.IsConfigured && secrets.IsConfigured;
    public Task RefreshAsync(CancellationToken ct)
    {
        lock (healthGate) { keyCooldowns.Clear(); modelCooldowns.Clear(); healthRevision = -1; }
        return Task.CompletedTask;
    }

    public Task<GroundedReply> GenerateAsync(string message, CancellationToken ct) => GenerateAsync(message, provider, ct);

    public async Task<GroundedReply> GenerateAsync(string message, IManagedAnswerProvider answerProvider, CancellationToken ct)
    {
        using var measurement = metrics?.Start("provider_failover");
        ct.ThrowIfCancellationRequested();
        if (!store.IsConfigured || !secrets.IsConfigured) throw new ManagementUnavailableException();
        using var total = CancellationTokenSource.CreateLinkedTokenSource(ct);
        total.CancelAfter(policy.TotalTimeout);
        using var ai = CancellationTokenSource.CreateLinkedTokenSource(total.Token);
        ai.CancelAfter(policy.AiTimeout); // Reserve time for final state check + local documents.
        try
        {
            var state = await store.ReadAsync(total.Token).WaitAsync(total.Token);
            if (!state.Enabled) throw new AssistantMaintenanceException(state.MaintenanceMessage);
            var candidates = ManagementRules.Candidates(state);
            if (candidates.Count == 0) throw new ManagementUnavailableException();
            SynchronizeHealth(state.Revision);
            var attempts = 0;
            foreach (var (chosen, key) in candidates)
            {
                ct.ThrowIfCancellationRequested();
                if (attempts >= policy.MaximumAttempts || ai.IsCancellationRequested) break;
                await RequireCurrent(state, total.Token);
                if (Cooling(state.Revision, chosen.Id + ":" + chosen.Model, key.CredentialVersion)) continue;
                var encrypted = await store.CredentialAsync(key.Id, key.CredentialVersion, total.Token).WaitAsync(total.Token);
                if (encrypted is null) throw new ManagementUnavailableException();
                var secret = secrets.Unprotect(key.CredentialVersion, encrypted);
                await RequireCurrent(state, total.Token); // Rotation/revocation/maintenance during decryption.
                if (ai.IsCancellationRequested) break;
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ai.Token);
                attempt.CancelAfter(policy.AttemptTimeout);
                attempts++;
                try
                {
                    var answer = await answerProvider.GenerateAsync(chosen.Id, chosen.Model, secret, message, attempt.Token).WaitAsync(attempt.Token);
                    attempt.Token.ThrowIfCancellationRequested();
                    await RequireCurrent(state, total.Token);
                    return answer; // Valid answer (including no-answer) stops failover.
                }
                catch (DemoGroundedUnavailableException failure) when (Recoverable(failure))
                {
                    LogFailure(chosen, failure);
                    if (failure.Failure == DemoGroundedFailure.Busy) break;
                    Cool(state.Revision, chosen.Id + ":" + chosen.Model, key.CredentialVersion, failure);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    var failure = new DemoGroundedUnavailableException(DemoGroundedFailure.Timeout);
                    LogFailure(chosen, failure);
                    Cool(state.Revision, chosen.Id + ":" + chosen.Model, key.CredentialVersion, failure);
                }
            }
            ct.ThrowIfCancellationRequested();
            await RequireCurrent(state, total.Token);
            return await answerProvider.KnowledgeAsync(message, total.Token).WaitAsync(total.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new DemoGroundedUnavailableException(DemoGroundedFailure.Timeout); }
    }

    private async Task RequireCurrent(ManagementState admitted, CancellationToken ct)
    {
        var current = await store.ReadAsync(ct).WaitAsync(ct);
        if (!current.Enabled) throw new AssistantMaintenanceException(current.MaintenanceMessage);
        if (current.Revision != admitted.Revision) throw new ManagementUnavailableException();
    }
    private static bool Recoverable(DemoGroundedUnavailableException failure) =>
        failure.Failure is DemoGroundedFailure.Network or DemoGroundedFailure.Timeout or DemoGroundedFailure.Busy ||
        failure.Failure == DemoGroundedFailure.ProviderRejected && failure.ProviderStatus is 401 or 403 or 408 or 429 or 500 or 502 or 503 or 504;
    private void LogFailure(ManagedProvider chosen, DemoGroundedUnavailableException failure) =>
        logger.LogWarning("Assistant candidate failed. Provider: {Provider}; Model: {Model}; Failure: {Failure}; ProviderStatus: {ProviderStatus}",
            chosen.Id, chosen.Model, failure.Failure, failure.ProviderStatus);
    private void SynchronizeHealth(long revision)
    {
        lock (healthGate)
        {
            if (revision <= healthRevision) return;
            keyCooldowns.Clear(); modelCooldowns.Clear(); healthRevision = revision;
        }
    }
    private bool Cooling(long revision, string providerId, Guid key)
    {
        lock (healthGate)
        {
            if (revision != healthRevision) return false;
            var now = clock.GetUtcNow();
            return keyCooldowns.GetValueOrDefault(key) > now || modelCooldowns.GetValueOrDefault(providerId) > now;
        }
    }
    private void Cool(long revision, string providerId, Guid key, DemoGroundedUnavailableException failure)
    {
        lock (healthGate)
        {
            if (revision != healthRevision) return; // A late old-setting failure must not poison the new config.
            var until = clock.GetUtcNow() + policy.Cooldown;
            if (failure.Failure == DemoGroundedFailure.ProviderRejected && failure.ProviderStatus is 401 or 403 or 429)
                keyCooldowns[key] = until;
            else modelCooldowns[providerId] = until; // Same provider/model's other keys won't cure its503.
        }
    }
    private static ManagedFailoverOptions Validate(ManagedFailoverOptions value)
    {
        if (value.MaximumAttempts is < 1 or > 3 || value.TotalTimeout <= TimeSpan.Zero ||
            value.AiTimeout <= TimeSpan.Zero || value.AiTimeout >= value.TotalTimeout ||
            value.AttemptTimeout <= TimeSpan.Zero || value.Cooldown <= TimeSpan.Zero)
            throw new ArgumentException("Invalid bounded Assistant failover policy.");
        return value;
    }
}
