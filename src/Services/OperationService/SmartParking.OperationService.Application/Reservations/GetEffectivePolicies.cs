using MediatR;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record GetEffectivePoliciesQuery(Guid? TenantId, Guid? SiteId, DateTimeOffset? At)
    : IRequest<EffectiveReservationPolicyDto>;

public sealed class GetEffectivePoliciesHandler(IReservationPolicyRepository policies, TimeProvider clock)
    : IRequestHandler<GetEffectivePoliciesQuery, EffectiveReservationPolicyDto>
{
    public async Task<EffectiveReservationPolicyDto> Handle(GetEffectivePoliciesQuery query, CancellationToken ct)
    {
        if ((query.TenantId is null) != (query.SiteId is null))
            throw new InvalidRequestException(
                "Both tenantId and siteId must be supplied together for site scope resolution, or both omitted for platform scope resolution.");

        var at = query.At ?? clock.GetUtcNow();
        return query.TenantId is null
            ? await PlatformAsync(at, ct)
            : await SiteAsync(query.TenantId.Value, query.SiteId!.Value, at, ct);
    }

    private async Task<EffectiveReservationPolicyDto> PlatformAsync(DateTimeOffset at, CancellationToken ct)
    {
        var platform = await policies.GetActivePlatformRevisionAsync(at, ct)
            ?? throw new ReservationPolicyUnavailableException(
                "No active platform policy revision covers the requested timestamp.");
        var values = new Dictionary<string, EffectivePolicyValueDto>();
        foreach (var item in PlatformPolicyCatalogueMetadata.All)
        {
            if (!platform.Values.TryGetValue(item.Key, out var value))
                throw new ReservationPolicyUnavailableException($"Required policy {item.Key} is missing.");
            values.Add(item.Key, new EffectivePolicyValueDto(value, item.Unit, false, "PLATFORM_DEFAULT",
                value == 0));
        }

        return new EffectiveReservationPolicyDto("PLATFORM", null, null, at,
            new PolicyRevisionReference(platform.Id, platform.Version), null, values,
            new ArrivalLateToleranceEffectiveDto(null, values["ARRIVAL_LATE_TOLERANCE"].Value, false),
            platform.CancellationTimezone ?? "Asia/Ho_Chi_Minh");
    }

    private async Task<EffectiveReservationPolicyDto> SiteAsync(Guid tenantId, Guid siteId, DateTimeOffset at,
        CancellationToken ct)
    {
        var lookup = await policies.GetEffectivePolicyAsync(tenantId, siteId, at, ct);
        if (lookup.TenantSiteMismatch)
            throw new SiteTenantMismatchException("The specified tenant is not the legal owner of the specified site.");
        if (lookup.Site is null)
            throw new PolicyNotFoundException(
                "No active policy revision found for this site at the specified timestamp. The lot owner must publish an initial policy revision to establish late tolerance enablement.");

        var effective = ReservationPolicyResolver.Resolve(lookup.Platform, lookup.Site);
        var values = new Dictionary<string, EffectivePolicyValueDto>();
        foreach (var item in PlatformPolicyCatalogueMetadata.All)
        {
            var explicitSiteValue = lookup.Site.Values.ContainsKey(item.Key);
            var value = effective.Values[item.Key];
            values.Add(item.Key, new EffectivePolicyValueDto(value, item.Unit, !explicitSiteValue,
                explicitSiteValue ? "SITE_OVERRIDE" : "PLATFORM_DEFAULT", value == 0));
        }

        return new EffectiveReservationPolicyDto("SITE", tenantId, siteId, at, effective.Platform, effective.Site,
            values,
            new ArrivalLateToleranceEffectiveDto(lookup.Site.ArrivalLateToleranceEnabled,
                effective.Values["ARRIVAL_LATE_TOLERANCE"], false),
            effective.CancellationTimezone);
    }
}