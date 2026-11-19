using MediatR;

namespace SmartParking.IoTService.Application.Assistant.Management;

public sealed record ManagedProvider(string Id, string Model, bool Enabled, int Priority);
public sealed record ManagedKey(Guid Id, string Provider, string Label, bool Enabled, int Priority,
    Guid CredentialVersion, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt, string? Model = null);
public sealed record ManagedKeyView(Guid Id, string Provider, string Label, bool Enabled, int Priority,
    DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
public sealed record ManagementState(long Revision, bool Enabled, string MaintenanceMessage,
    ManagedProvider[] Providers, ManagedKey[] Keys)
{
    public static ManagementState Empty => new(0, false, "Trợ lý đang bảo trì. Vui lòng thử lại sau.",
        [new("gemini", "gemini-3.8-flash", false, 1), new("openai", "gpt-5.4-mini-2026-03-17", false, 2)], []);
}
public sealed record ManagementSettings(long Revision, bool Enabled, string MaintenanceMessage,
    ManagedProvider[] Providers, ManagedKeyView[] Keys, int ContractVersion = 2, ManagedKey[]? Configurations = null);
public sealed record ManagementView(ManagementSettings Settings, bool SecretsConfigured, bool RuntimeReleaseApproved);
public sealed record AdminMutation(Guid MutationId, [property: System.Text.Json.Serialization.JsonRequired] long ExpectedRevision, string Action,
    ManagedProvider[]? Providers = null, bool? Enabled = null, string? MaintenanceMessage = null,
    Guid? KeyId = null, string? Provider = null, string? Label = null, string? Secret = null, int? Priority = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Model = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[]? ProviderIds = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid[]? ConfigurationIds = null)
{
    public override string ToString() => "[ADMIN MUTATION REDACTED]";
}
public sealed record CredentialWrite(Guid SlotId, Guid Version, string ProtectedValue)
{
    public override string ToString() => "[PROTECTED CREDENTIAL]";
}
public sealed record ManagementWrite(ManagementState State, CredentialWrite? Credential = null,
    Guid? RevokeSlot = null, Guid? RetireVersion = null);

public interface IAssistantManagementStore
{
    bool IsConfigured { get; }
    Task<ManagementState> ReadAsync(CancellationToken ct);
    // Implementations lock state, check replay/revision, then atomically save state, credential and safe audit.
    Task<ManagementState> MutateAsync(Guid actor, AdminMutation input,
        Func<ManagementState, ManagementWrite> change, CancellationToken ct);
    Task<string?> CredentialAsync(Guid slot, Guid version, CancellationToken ct);
}
public interface IAssistantSecretProtector
{
    bool IsConfigured { get; }
    string Protect(Guid version, string secret);
    string Unprotect(Guid version, string protectedValue);
}
public interface IAssistantManagedRuntime
{
    bool ReleaseAuthorized { get; }
    Task RefreshAsync(CancellationToken ct);
}
public sealed class ManagementValidationException : Exception;
public sealed class ManagementUnavailableException : Exception;
public sealed class AssistantMaintenanceException(string message) : Exception(message);
public sealed class ManagementConflictException(bool idempotency) : Exception
{
    public bool Idempotency { get; } = idempotency;
}
public sealed record GetAssistantManagement : IRequest<ManagementView>;
public sealed record ChangeAssistantManagement(AdminMutation Input) : IRequest<ManagementView>
{
    public override string ToString() => "[ADMIN COMMAND REDACTED]";
}

public sealed class ClientUpgradeRequiredException : Exception;
