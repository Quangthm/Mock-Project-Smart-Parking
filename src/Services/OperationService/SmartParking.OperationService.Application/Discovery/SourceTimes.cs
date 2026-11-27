namespace SmartParking.OperationService.Application.Discovery;

public sealed record SourceTimes(DateTimeOffset Facility, DateTimeOffset? Operations, DateTimeOffset Payment);
