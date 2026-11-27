namespace SmartParking.OperationService.Application.Discovery;

public sealed record HistoricalBookingSite(Guid SiteId, DateTimeOffset ConfirmedAt);
