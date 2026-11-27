namespace SmartParking.OperationService.Application.Discovery;

public sealed record FacilityWindow(DateTimeOffset From, DateTimeOffset To, string State, DateTimeOffset AsOf);
