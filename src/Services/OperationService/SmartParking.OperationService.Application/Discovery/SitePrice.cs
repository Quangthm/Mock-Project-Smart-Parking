namespace SmartParking.OperationService.Application.Discovery;

public sealed record SitePrice(Guid SiteId, DiscoveryPrice? Price, string? Reason);
