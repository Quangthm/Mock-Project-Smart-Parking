namespace SmartParking.OperationService.Infrastructure.Clients;

internal sealed record DiscoveryEnvelope<T>(bool Success, T Data);
