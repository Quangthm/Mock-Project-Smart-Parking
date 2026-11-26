using SmartParking.FacilityService.Domain;
using System.Text.Json.Serialization;

namespace SmartParking.FacilityService.Application;

public sealed record CatalogueFilter(double Lat, double Lon, double RadiusKm, string VehicleType, string? LotType, int Limit);
public sealed record ServiceWindow(DateTimeOffset From, DateTimeOffset To, string State, DateTimeOffset AsOf);
public sealed record RequestedServiceWindow([property: JsonRequired] DateTimeOffset From, [property: JsonRequired] DateTimeOffset To, string State);
public sealed record PublishSite(string Name, string Address, [property: JsonRequired] decimal Latitude,
    [property: JsonRequired] decimal Longitude, string LotType,
    string[] SupportedVehicleTypes, RequestedServiceWindow? ServiceWindow);
public sealed record CandidateSite(Guid SiteId, string Name, string Address, decimal Latitude, decimal Longitude,
    string LotType, string[] SupportedVehicleTypes, Guid ConfigurationVersion, DateTimeOffset AsOf, ServiceWindow? ServiceWindow);
public sealed record CatalogueSlice(CandidateSite[] Items, Guid CatalogueVersion, DateTimeOffset AsOf);
public sealed record CataloguePosition(Guid CatalogueVersion, Guid LastSiteId);
public sealed record ApprovedSite(Guid SiteId, Guid TenantId, string LotType);
public interface IPublicationAuthority
{
    Task<ApprovedSite> RequireApprovedSite(Guid siteId, CancellationToken ct);
    Task<Guid[]> ManagedTenants(CancellationToken ct);
}
public interface ICatalogueStore
{
    Task<CatalogueSlice> List(CatalogueFilter filter, CataloguePosition? position, CancellationToken ct);
    Task Publish(ApprovedSite approval, PublishSite fields, CancellationToken ct);
    Task Withdraw(Guid siteId, Guid[] tenants, CancellationToken ct);
}

public sealed class DiscoveryCatalogue(ICatalogueStore store, IPublicationAuthority authority)
{
    public static void ValidateFilter(CatalogueFilter f)
    {
        if (!double.IsFinite(f.Lat) || f.Lat is < -90 or > 90 || !double.IsFinite(f.Lon) || f.Lon is < -180 or > 180 ||
            !double.IsFinite(f.RadiusKm) || f.RadiusKm is <= 0 or > 50 || f.Limit is < 1 or > 100 ||
            f.VehicleType is not ("CAR" or "MOTORCYCLE") || f.LotType is not (null or "outdoor" or "basement" or "multi_storey"))
            throw Invalid();
    }
    public Task<CatalogueSlice> List(CatalogueFilter f, CataloguePosition? position, CancellationToken ct) =>
        store.List(f, position, ct);

    public async Task Publish(Guid siteId, PublishSite fields, CancellationToken ct)
    {
        if (siteId == Guid.Empty || string.IsNullOrWhiteSpace(fields.Name) || fields.Name.Length > 255 ||
            string.IsNullOrWhiteSpace(fields.Address) || fields.Address.Length > 2000 ||
            fields.Latitude is < -90 or > 90 || fields.Longitude is < -180 or > 180 ||
            fields.LotType is not ("outdoor" or "basement" or "multi_storey") ||
            fields.SupportedVehicleTypes is not { Length: > 0 and <= 2 } ||
            fields.SupportedVehicleTypes.Any(x => x is not ("CAR" or "MOTORCYCLE")) ||
            fields.SupportedVehicleTypes.Distinct().Count() != fields.SupportedVehicleTypes.Length ||
            fields.ServiceWindow is { } w && (w.State is not ("open" or "closed") || w.To <= w.From))
            throw Invalid();
        var approval = await authority.RequireApprovedSite(siteId, ct);
        if (approval.SiteId != siteId || approval.TenantId == Guid.Empty || approval.LotType != fields.LotType)
            throw new StructureException("FORBIDDEN", "Publication is outside the approved lot scope.");
        await store.Publish(approval, fields, ct);
    }
    public async Task Withdraw(Guid siteId, CancellationToken ct)
    {
        var tenants = await authority.ManagedTenants(ct);
        await store.Withdraw(siteId, tenants, ct);
    }
    private static StructureException Invalid() => new("VALIDATION_FAILED", "Invalid discovery fields.");
}
