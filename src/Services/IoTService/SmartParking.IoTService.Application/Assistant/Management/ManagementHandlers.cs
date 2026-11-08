using System.Text.RegularExpressions;
using MediatR;
using SmartParking.IoTService.Application.Assistant.Authorization;

namespace SmartParking.IoTService.Application.Assistant.Management;

public sealed class ManagementHandlers(IAssistantManagementStore store, IAssistantSecretProtector secrets,
    IAssistantManagedRuntime runtime, ICurrentActor currentActor, TimeProvider clock)
    : IRequestHandler<GetAssistantManagement, ManagementView>, IRequestHandler<ChangeAssistantManagement, ManagementView>
{
    private CurrentActor Authorize()
    {
        var actor = currentActor.Actor ?? throw new AssistantAccessException(false);
        if (actor.ExpiresAt <= clock.GetUtcNow()) throw new AssistantAccessException(false);
        if (actor.Role != "admin") throw new AssistantAccessException(true);
        return actor;
    }
    private ManagementView View(ManagementState state) => new(
        new(state.Revision, state.Enabled, state.MaintenanceMessage, state.Providers.ToArray(),
            state.Keys.Select(k => new ManagedKeyView(k.Id, k.Provider, k.Label, k.Enabled, k.Priority, k.CreatedAt, k.RevokedAt)).ToArray()),
        secrets.IsConfigured, runtime.ReleaseAuthorized);
    public async Task<ManagementView> Handle(GetAssistantManagement request, CancellationToken cancellationToken)
    {
        Authorize();
        if (!store.IsConfigured) throw new ManagementUnavailableException();
        var state = await store.ReadAsync(cancellationToken);
        Authorize();
        return View(state);
    }
    public async Task<ManagementView> Handle(ChangeAssistantManagement request, CancellationToken cancellationToken)
    {
        var actor = Authorize();
        if (!store.IsConfigured) throw new ManagementUnavailableException();
        var input = request.Input;
        ManagementRules.Require(input is not null && input.MutationId != Guid.Empty &&
            input.ExpectedRevision is >= 0 and < ManagementRules.MaxSafeInteger &&
            input.Action is "save-config" or "save-key" or "revoke-key");
        var state = await store.MutateAsync(actor.AccountId, input!, before =>
        {
            Authorize();
            return ManagementRules.Apply(before, input!, secrets, clock.GetUtcNow());
        }, cancellationToken);
        // The durable mutation is already committed. Refresh failure must not imply it was rejected.
        try { await runtime.RefreshAsync(cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        Authorize();
        return View(state);
    }
}

public static class ManagementRules
{
    public const long MaxSafeInteger = 9_007_199_254_740_991;
    public static void Require(bool condition) { if (!condition) throw new ManagementValidationException(); }
    private static string Text(string? text, int maximum, bool required = true)
    {
        Require(text is not null && text.Length <= maximum && (!required || !string.IsNullOrWhiteSpace(text)) &&
            !text.Any(char.IsControl));
        return text!.Trim();
    }
    public static (ManagedProvider Provider, ManagedKey Key)? Select(ManagementState state)
    {
        var candidates = Candidates(state);
        return candidates.Count == 0 ? null : candidates[0];
    }
    public static IReadOnlyList<(ManagedProvider Provider, ManagedKey Key)> Candidates(ManagementState state) => !state.Enabled ? [] :
        state.Providers.Where(p => p.Enabled).OrderBy(p => p.Priority).ThenBy(p => p.Id, StringComparer.Ordinal)
            .SelectMany(p => state.Keys.Where(k => k.Provider == p.Id && k.Enabled && k.RevokedAt is null)
                .OrderBy(k => k.Priority).ThenBy(k => k.Id).Select(k => (p, k))).ToArray();
    public static ManagementWrite Apply(ManagementState state, AdminMutation input,
        IAssistantSecretProtector secrets, DateTimeOffset now)
    {
        Require(state.Revision is >= 0 and < MaxSafeInteger);
        var next = state with { Revision = state.Revision + 1 };
        switch (input.Action)
        {
            case "save-config":
                Require(input.Providers is { Length: 2 } && input.Enabled is not null);
                Require(input.Providers!.All(p => p is not null));
                var providers = input.Providers!.Select(p => p with { Model = Text(p.Model, 128) }).ToArray();
                Require(providers.Select(p => p.Id).Distinct().Count() == 2 && providers.Select(p => p.Priority).Distinct().Count() == 2);
                foreach (var provider in providers)
                    Require(provider.Id is "gemini" or "openai" && provider.Priority is >= 1 and <= 1000 &&
                        Regex.IsMatch(provider.Model, "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
                next = next with { Enabled = input.Enabled!.Value, Providers = providers,
                    MaintenanceMessage = Text(input.MaintenanceMessage, 500) };
                if (next.Enabled) Require(secrets.IsConfigured && Select(next) is not null);
                return new(next);
            case "save-key":
                Require(secrets.IsConfigured && input.Provider is "gemini" or "openai" &&
                    input.Enabled is not null && input.Priority is >= 1 and <= 1000);
                var existing = state.Keys.FirstOrDefault(k => k.Id == input.KeyId);
                Require(input.KeyId is null || existing is not null);
                Require(existing is null || existing.Provider == input.Provider && existing.RevokedAt is null);
                var keys = state.Keys.ToList();
                if (existing is null && keys.Count >= 64)
                {
                    var archived = keys.Where(k => k.RevokedAt is not null).OrderBy(k => k.RevokedAt).ThenBy(k => k.Id).FirstOrDefault();
                    Require(archived is not null); keys.Remove(archived!);
                }
                var label = Text(input.Label, 80);
                var slot = existing?.Id ?? Guid.NewGuid();
                var version = existing?.CredentialVersion ?? Guid.Empty;
                CredentialWrite? credential = null;
                if (input.Secret is not null)
                {
                    Require(input.Secret.Length is >= 8 and <= 1024 && !input.Secret.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)));
                    version = Guid.NewGuid(); credential = new(slot, version, secrets.Protect(version, input.Secret));
                }
                Require(version != Guid.Empty);
                var key = new ManagedKey(slot, input.Provider!, label, input.Enabled!.Value, input.Priority!.Value, version, existing?.CreatedAt ?? now, null);
                if (existing is null) keys.Add(key); else keys[keys.IndexOf(existing)] = key;
                return new(next with { Keys = keys.ToArray() }, credential, RetireVersion: credential is not null ? existing?.CredentialVersion : null);
            case "revoke-key":
                var revoked = state.Keys.FirstOrDefault(k => k.Id == input.KeyId);
                Require(revoked is not null && revoked.RevokedAt is null);
                return new(next with { Keys = state.Keys.Select(k => k.Id == revoked!.Id ? k with { Enabled = false, RevokedAt = now } : k).ToArray() }, RevokeSlot: revoked!.Id);
            default: throw new ManagementValidationException();
        }
    }
}
