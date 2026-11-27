namespace SmartParking.OperationService.Application.Discovery;

public sealed record Availability(string Kind, int? AvailableCount, DateTimeOffset? AsOf, string? Reason);
