namespace SmartParking.OperationService.Application.Discovery;

public sealed record FacilityPage(DiscoverySite[] Items, string? NextCursor, string CatalogueVersion, DateTimeOffset AsOf);
