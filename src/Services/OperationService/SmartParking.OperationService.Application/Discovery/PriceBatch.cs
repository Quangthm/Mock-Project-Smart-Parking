namespace SmartParking.OperationService.Application.Discovery;

public sealed record PriceBatch(SitePrice[] Items, DateTimeOffset AsOf);
