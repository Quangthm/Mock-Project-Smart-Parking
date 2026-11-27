namespace SmartParking.OperationService.Application.Discovery;

public sealed record CarCapacityRead(DateTimeOffset AsOf, int? AvailableCount);
