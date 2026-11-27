namespace SmartParking.OperationService.Application.Discovery;

public sealed record DiscoveryPrice(string Kind, long AmountVnd, string BillingUnit, int PolicyVersion, DateTimeOffset AsOf);
