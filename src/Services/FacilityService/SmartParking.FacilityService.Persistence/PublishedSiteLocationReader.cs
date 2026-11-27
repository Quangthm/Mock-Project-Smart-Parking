using Npgsql;
using SmartParking.FacilityService.Application.Discovery;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

public sealed class PublishedSiteLocationReader(NpgsqlDataSource source) : IPublishedSiteLocationReader
{
    public async Task<PublishedSiteLocation?> ReadAsync(Guid siteId, CancellationToken ct)
    {
        if (siteId == Guid.Empty) return null;
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT p.tenant_id,s.tenant_id,s.name,s.latitude,s.longitude
            FROM facility_discovery_publications p
            JOIN parking_sites s ON s.id=p.site_id
            JOIN tenants t ON t.id=s.tenant_id
            WHERE p.site_id=@site AND s.status='ACTIVE' AND s.deleted_at IS NULL
                AND t.status='ACTIVE' AND t.deleted_at IS NULL
            """, db);
        command.Parameters.AddWithValue("site", siteId);
        await using var rows = await command.ExecuteReaderAsync(ct);
        if (!await rows.ReadAsync(ct)) return null;
        if (rows.GetGuid(0) != rows.GetGuid(1))
            throw new StructureException("CATALOGUE_UNAVAILABLE", "Published site scope is inconsistent.");
        if (rows.IsDBNull(2) || rows.IsDBNull(3) || rows.IsDBNull(4)) return null;
        var name = rows.GetString(2).Trim();
        var latitude = rows.GetDecimal(3);
        var longitude = rows.GetDecimal(4);
        if (name.Length is 0 or > 255 || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return null;
        return new(siteId, name, latitude, longitude);
    }
}
