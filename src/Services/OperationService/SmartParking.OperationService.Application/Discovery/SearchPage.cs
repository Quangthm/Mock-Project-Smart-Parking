namespace SmartParking.OperationService.Application.Discovery;

public sealed record SearchPage(ParkingSummary[] Items, string? NextCursor, DateTimeOffset AsOf);
