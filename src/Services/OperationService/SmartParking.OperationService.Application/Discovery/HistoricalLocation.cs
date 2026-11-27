namespace SmartParking.OperationService.Application.Discovery;

public sealed record HistoricalLocation(Guid SiteId, string SiteName, decimal Latitude, decimal Longitude,
    DateTimeOffset ConfirmedAt);
