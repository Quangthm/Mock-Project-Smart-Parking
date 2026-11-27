namespace SmartParking.OperationService.Application.Discovery;

public sealed record DiscoveryPolicy(bool Enabled, TimeSpan MaxAge, TimeSpan Skew);
