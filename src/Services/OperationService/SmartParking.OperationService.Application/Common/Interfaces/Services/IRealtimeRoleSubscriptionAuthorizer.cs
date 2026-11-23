namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public interface IRealtimeRoleSubscriptionAuthorizer
{
    Task AuthorizeOwnerSitesAsync(IReadOnlyCollection<Guid> siteIds, string bearerToken, CancellationToken ct);
    Task AuthorizeDriverSitesAsync(IReadOnlyCollection<Guid> siteIds, string bearerToken, CancellationToken ct);
}
