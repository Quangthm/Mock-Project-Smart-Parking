namespace SmartParking.IoTService.Application.Assistant.Knowledge;

public interface IDemoGroundedGenerator
{
    Task<GroundedReply> GenerateAsync(string message, CancellationToken cancellationToken);
}
public sealed record GroundedSource(string ArticleId, string Version, string Title, string Excerpt);
public sealed record GroundedReply(string Reply, IReadOnlyList<GroundedSource> Sources, string? Provider, string? Model, string Mode = "provider");
public enum DemoGroundedFailure { Unspecified, Busy, Configuration, Knowledge, Network, Timeout, ProviderRejected, InvalidResponse, ResponseTooLarge }
public sealed class DemoGroundedUnavailableException : Exception
{
    public DemoGroundedFailure Failure { get; }
    public int? ProviderStatus { get; }
    public DemoGroundedUnavailableException(DemoGroundedFailure failure = DemoGroundedFailure.Unspecified, int? providerStatus = null)
        : base("The demo knowledge or model is unavailable.")
    {
        Failure = failure;
        ProviderStatus = providerStatus is >= 100 and <= 599 ? providerStatus : null;
    }
}
