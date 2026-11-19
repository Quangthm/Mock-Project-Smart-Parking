using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Management;

public static partial class ManagementRules
{
    public static readonly IReadOnlySet<string> Actions = new HashSet<string>(StringComparer.Ordinal)
    { "save-config", "save-key", "revoke-key", "add-configuration", "set-configuration", "rotate-key",
        "reorder-providers", "reorder-configurations", "set-enabled" };

    // Legacy state is upgraded in memory until the reviewed migration is deployed. Never rotate a secret here.
    public static ManagementState Upgrade(ManagementState state) => state with
    {
        Keys = state.Keys.Select(k => k.Model is not null ? k : k with
        { Model = state.Providers.Single(p => p.Id == k.Provider).Model }).ToArray()
    };

    public static bool ModelId(string? model) => model is not null &&
        Regex.IsMatch(model, "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Gemini text model IDs are checked against the provider catalog by the mutation handler.
    // OpenAI retains its explicit Responses + JSON schema compatibility mapping.
    public static bool Compatible(string provider, string model) => ModelId(model) && (provider switch
    {
        "openai" => model is "gpt-4.1" or "gpt-4.1-mini" or "gpt-4.1-nano" or
            "gpt-4.1-2025-04-14" or "gpt-4.1-mini-2025-04-14" or "gpt-4.1-nano-2025-04-14" or
            "gpt-5.4" or "gpt-5.4-mini" or "gpt-5.4-mini-2026-03-17",
        "gemini" => Regex.IsMatch(model, @"^gemini-3(?:\.\d+)?-(?:flash(?:-lite)?|pro)(?:-preview)?(?:-\d{2,4}(?:-\d{2}){0,2})?\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
        _ => false
    });

    private static ManagementWrite ApplyV2(ManagementState state, AdminMutation input,
        IAssistantSecretProtector secrets, DateTimeOffset now)
    {
        var next = state with { Revision = state.Revision + 1 };
        var keys = state.Keys.ToList();
        var existing = keys.FirstOrDefault(k => k.Id == input.KeyId && k.RevokedAt is null);
        CredentialWrite? credential = null;
        Guid? retire = null;
        switch (input.Action)
        {
            case "add-configuration":
                Require(input.Provider is "gemini" or "openai" && input.Enabled is null or false && Compatible(input.Provider!, input.Model ?? ""));
                Require(secrets.IsConfigured);
                if (keys.Count >= 64)
                {
                    var archived = keys.Where(k => k.RevokedAt is not null).OrderBy(k => k.RevokedAt).ThenBy(k => k.Id).FirstOrDefault();
                    Require(archived is not null);
                    keys.Remove(archived!);
                }
                var added = new ManagedKey(Guid.NewGuid(), input.Provider!, Text(input.Label, 80), false,
                    1, Guid.NewGuid(), now, null, input.Model);
                credential = Protect(added, input.Secret, secrets);
                keys.Add(added);
                Move(keys, added, input.Priority);
                break;
            case "set-configuration":
                Require(existing is not null && input.Enabled is not null && ModelId(input.Model) &&
                    (existing.Model == input.Model || Compatible(existing.Provider, input.Model!)));
                var updated = existing! with { Label = Text(input.Label, 80), Model = input.Model, Enabled = input.Enabled!.Value };
                keys[keys.IndexOf(existing!)] = updated;
                Move(keys, updated, input.Priority);
                break;
            case "rotate-key":
                Require(existing is not null && secrets.IsConfigured);
                retire = existing!.CredentialVersion;
                var rotated = existing with { CredentialVersion = Guid.NewGuid() };
                credential = Protect(rotated, input.Secret, secrets);
                keys[keys.IndexOf(existing)] = rotated;
                break;
            case "reorder-providers":
                Require(input.ProviderIds is { Length: 2 } && input.ProviderIds.Distinct().Count() == 2 &&
                    input.ProviderIds.All(id => state.Providers.Any(p => p.Id == id)));
                next = next with { Providers = input.ProviderIds!.Select((id, i) =>
                    state.Providers.Single(p => p.Id == id) with { Priority = i + 1 }).ToArray() };
                break;
            case "reorder-configurations":
                Require(input.Provider is "gemini" or "openai" && input.ConfigurationIds is not null);
                var active = keys.Where(k => k.Provider == input.Provider && k.RevokedAt is null).Select(k => k.Id).ToHashSet();
                Require(input.ConfigurationIds!.Length == active.Count && input.ConfigurationIds.Distinct().Count() == active.Count &&
                    input.ConfigurationIds.All(active.Contains));
                keys = keys.Select(k => active.Contains(k.Id)
                    ? k with { Priority = Array.IndexOf(input.ConfigurationIds, k.Id) + 1 } : k).ToList();
                break;
            case "set-enabled":
                Require(input.Enabled is not null);
                next = next with { Enabled = input.Enabled!.Value };
                if (next.Enabled) Require(secrets.IsConfigured && Select(next) is not null);
                break;
            default: throw new ManagementValidationException();
        }
        return new(next with { Keys = keys.ToArray() }, credential, RetireVersion: retire);
    }

    private static CredentialWrite Protect(ManagedKey key, string? secret, IAssistantSecretProtector secrets)
    {
        Require(secret is { Length: >= 8 and <= 1024 } && !secret.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)));
        return new(key.Id, key.CredentialVersion, secrets.Protect(key.CredentialVersion, secret!));
    }

    private static ManagedKey[] Normalize(ManagedKey[] keys, string scope)
    {
        var priorities = keys.Where(k => k.Provider == scope && k.RevokedAt is null).GroupBy(k => k.Provider)
            .SelectMany(group => group.OrderBy(k => k.Priority).ThenBy(k => k.Id).Select((k, i) => (k.Id, Priority: i + 1)))
            .ToDictionary(x => x.Id, x => x.Priority);
        return keys.Select(k => priorities.TryGetValue(k.Id, out var priority) ? k with { Priority = priority } : k).ToArray();
    }

    private static void Move(List<ManagedKey> keys, ManagedKey moved, int? priority)
    {
        var ordered = keys.Where(k => k.Provider == moved.Provider && k.RevokedAt is null && k.Id != moved.Id)
            .OrderBy(k => k.Priority).ThenBy(k => k.Id).ToList();
        Require(priority >= 1 && priority <= ordered.Count + 1);
        ordered.Insert(priority!.Value - 1, moved);
        for (var i = 0; i < ordered.Count; i++)
        {
            var index = keys.FindIndex(k => k.Id == ordered[i].Id);
            keys[index] = ordered[i] with { Priority = i + 1 };
        }
    }
}
