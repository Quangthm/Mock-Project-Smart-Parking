namespace SmartParking.OperationService.Application.Discovery;

public sealed record DiscoveryCursor(string FilterHash, string CatalogueVersion, DateTimeOffset Anchor,
    double Distance, Guid SiteId, string Sort, long? Primary, string? RankingDigest);
