namespace SmartParking.OperationService.Application.Discovery;

public interface IDiscoveryOwners
{
    Task<CandidateBatch> Candidates(DiscoverySite[] sites, SearchFilter filter, DateTimeOffset at, CancellationToken ct);
    Task<FacilityPage> Sites(SearchFilter filter, string? cursor, CancellationToken ct);
    Task<PriceBatch> Prices(Guid[] ids, SearchFilter filter, DateTimeOffset at, CancellationToken ct);
}
