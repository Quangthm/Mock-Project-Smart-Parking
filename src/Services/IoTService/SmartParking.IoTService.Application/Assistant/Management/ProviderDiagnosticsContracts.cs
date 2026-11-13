using MediatR;

namespace SmartParking.IoTService.Application.Assistant.Management;

public sealed record DiscoverModelsInput(string? Provider = null, string? Secret = null,
    Guid? ConfigurationId = null, long? ExpectedRevision = null, string? Cursor = null)
{
    public override string ToString() => "[MODEL DISCOVERY REDACTED]";
}
public sealed record DiscoverModels(DiscoverModelsInput Input) : IRequest<ModelPage>
{
    public override string ToString() => "[MODEL DISCOVERY REDACTED]";
}
public sealed record ProviderModel(string Id, string DisplayName, bool SupportsChat = true);
public sealed record ModelPage(ProviderModel[] Items, string? NextCursor, DateTimeOffset AsOf);
public sealed record ConnectionTestInput(long? ExpectedRevision, Guid TestRequestId);
public sealed record TestConnection(Guid ConfigurationId, ConnectionTestInput Input) : IRequest<ConnectionTestResponse>;
public sealed record ConnectionTestResult(Guid TestRequestId, Guid ConfigurationId, long TestedRevision,
    string Model, string Outcome, long LatencyMs, DateTimeOffset CheckedAt, bool Current, string? Code);
public sealed record ConnectionTestResponse(ConnectionTestResult? Result, bool Running);
public sealed record TestAdmission(bool Dispatch, ConnectionTestResult? Result);

public static class ConnectionTestTiming
{
    public static readonly TimeSpan Execution = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan Completion = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan PendingGrace = Execution + Completion + TimeSpan.FromSeconds(5);
}

public interface IProviderDiagnostics
{
    Task<ProviderModel[]> DiscoverAsync(string provider, string secret, CancellationToken ct);
    Task TestAsync(string provider, string model, string secret, CancellationToken ct);
}

// Durable reservation precedes outbound generation. A timed-out/crashed reservation is unknown, never redispatched.
public interface IConnectionTestStore
{
    Task<TestAdmission?> FindAsync(Guid actor, Guid configuration, long revision, Guid requestId, CancellationToken ct);
    Task<TestAdmission> AdmitAsync(Guid actor, Guid configuration, long revision, Guid requestId, string model, CancellationToken ct);
    Task CompleteAsync(Guid actor, ConnectionTestResult result, CancellationToken ct);
}

public sealed class ProviderDiagnosticsException(int status, string code) : Exception
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
