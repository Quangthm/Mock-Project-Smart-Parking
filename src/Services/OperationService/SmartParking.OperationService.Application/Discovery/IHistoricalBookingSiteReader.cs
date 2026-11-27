namespace SmartParking.OperationService.Application.Discovery;

public interface IHistoricalBookingSiteReader
{
    Task<HistoricalBookingSite?> LatestConfirmedAsync(Guid accountId, CancellationToken ct);
}
