namespace SmartParking.ReservationService.Application;
public sealed record OperatorSiteScope(Guid UserId, Guid TenantId, Guid SiteId);

public interface IOperatorIdentityClient { Task<OperatorSiteScope> RequireSiteAsync(Guid siteId, CancellationToken ct); }
