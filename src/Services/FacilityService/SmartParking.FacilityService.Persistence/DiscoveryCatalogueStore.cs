using System.Data;
using Npgsql;
using NpgsqlTypes;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

public sealed class DiscoveryCatalogueStore(NpgsqlDataSource source) : ICatalogueStore
{
    private const string InsideRadius = """
        6371000.0 * 2 * asin(sqrt(least(1.0,
            power(sin(radians(s.latitude::float8-$3)/2),2) +
            cos(radians($3))*cos(radians(s.latitude::float8))*power(sin(radians(s.longitude::float8-$4)/2),2)
        ))) <= $5*1000.0
        """;
    private static void BindFilter(NpgsqlCommand command, CatalogueFilter f)
    {
        command.Parameters.AddWithValue(f.VehicleType);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Text, Value=(object?)f.LotType ?? DBNull.Value });
        command.Parameters.AddWithValue(f.Lat);
        command.Parameters.AddWithValue(f.Lon);
        command.Parameters.AddWithValue(f.RadiusKm);
    }
    // Stay inside a single snapshot for the generation and the candidate page. An empty page
    // still returns a generation. Radians wrap at the antimeridian in the sine of half-delta.
    public async Task<CatalogueSlice> List(CatalogueFilter f, CataloguePosition? position, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using var version = new NpgsqlCommand("SELECT revision, statement_timestamp() FROM facility_discovery_version WHERE singleton", db, tx);
        Guid revision; DateTimeOffset asOf;
        await using (var r = await version.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) throw new StructureException("CATALOGUE_UNAVAILABLE", "Catalogue is unavailable.");
            revision = r.GetGuid(0); asOf = r.GetFieldValue<DateTimeOffset>(1);
        }
        if (position != null && position.CatalogueVersion != revision)
            throw new StructureException("SEARCH_CHANGED", "Start a new catalogue search.");
        await using var validate = new NpgsqlCommand($"""
            SELECT EXISTS(
              SELECT 1 FROM facility_discovery_publications p JOIN parking_sites s ON s.id=p.site_id
              JOIN tenants t ON t.id=s.tenant_id
              WHERE s.status='ACTIVE' AND s.deleted_at IS NULL AND t.status='ACTIVE' AND t.deleted_at IS NULL
                AND $1=ANY(p.categories) AND ($2::text IS NULL OR p.lot_type=$2)
                AND (s.latitude IS NULL OR s.longitude IS NULL OR s.latitude NOT BETWEEN -90 AND 90
                     OR s.longitude NOT BETWEEN -180 AND 180 OR
                     (({InsideRadius}) AND (btrim(s.name)='' OR btrim(s.address)='' OR p.tenant_id<>s.tenant_id))))
            """, db, tx);
        BindFilter(validate, f);
        if ((bool)(await validate.ExecuteScalarAsync(ct))!)
            throw new StructureException("CATALOGUE_UNAVAILABLE", "Catalogue is unavailable.");
        await using var page = new NpgsqlCommand($"""
            SELECT s.id,s.name,s.address,s.latitude,s.longitude,p.lot_type,p.categories,p.configuration_version,
                   p.window_start,p.window_end,p.window_state,p.published_at
            FROM facility_discovery_publications p
            JOIN parking_sites s ON s.id=p.site_id AND s.tenant_id=p.tenant_id
            JOIN tenants t ON t.id=s.tenant_id
            WHERE s.status='ACTIVE' AND s.deleted_at IS NULL AND t.status='ACTIVE' AND t.deleted_at IS NULL
              AND $1=ANY(p.categories) AND ($2::text IS NULL OR p.lot_type=$2)
              AND ({InsideRadius}) AND ($6::uuid IS NULL OR s.id > $6)
            ORDER BY s.id LIMIT $7
            """, db, tx);
        BindFilter(page, f);
        page.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Uuid, Value=(object?)position?.LastSiteId ?? DBNull.Value });
        page.Parameters.AddWithValue(f.Limit + 1);
        var rows = new List<CandidateSite>();
        await using (var r = await page.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var window = r.IsDBNull(8) ? null : new ServiceWindow(r.GetFieldValue<DateTimeOffset>(8),
                    r.GetFieldValue<DateTimeOffset>(9), r.GetString(10), r.GetFieldValue<DateTimeOffset>(11));
                rows.Add(new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4),
                    r.GetString(5), r.GetFieldValue<string[]>(6), r.GetGuid(7), asOf, window));
            }
        }
        await tx.CommitAsync(ct);
        return new(rows.ToArray(), revision, asOf);
    }

    public async Task Publish(ApprovedSite approval, PublishSite fields, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        // Reviewed public fields must still match current profile. Lock site + tenant so a
        // competing edit/deactivation either precedes this check or invalidates the grant.
        await using var check = new NpgsqlCommand("""
            SELECT s.id FROM parking_sites s JOIN tenants t ON t.id=s.tenant_id
            JOIN owner_onboarding_provisions o ON o.site_id=s.id AND o.application_id=$1
            WHERE s.id=$1 AND s.tenant_id=$2 AND s.status='ACTIVE' AND s.deleted_at IS NULL
              AND t.status='ACTIVE' AND t.deleted_at IS NULL
              AND s.name=$3 AND s.address=$4 AND s.latitude=$5 AND s.longitude=$6
            FOR UPDATE OF s,t
            """, db, tx);
        check.Parameters.AddWithValue(approval.SiteId); check.Parameters.AddWithValue(approval.TenantId);
        check.Parameters.AddWithValue(fields.Name); check.Parameters.AddWithValue(fields.Address);
        check.Parameters.AddWithValue(fields.Latitude); check.Parameters.AddWithValue(fields.Longitude);
        if (await check.ExecuteScalarAsync(ct) == null)
            throw new StructureException("STRUCTURE_CONFLICT", "Review the current site profile before publishing.");
        await using var write = new NpgsqlCommand("""
            INSERT INTO facility_discovery_publications(site_id,tenant_id,lot_type,categories,window_start,window_end,window_state)
            VALUES($1,$2,$3,$4,$5,$6,$7)
            ON CONFLICT(site_id) DO UPDATE SET lot_type=excluded.lot_type,categories=excluded.categories,
                configuration_version=gen_random_uuid(),window_start=excluded.window_start,window_end=excluded.window_end,
                window_state=excluded.window_state,published_at=statement_timestamp()
            """, db, tx);
        write.Parameters.AddWithValue(approval.SiteId); write.Parameters.AddWithValue(approval.TenantId);
        write.Parameters.AddWithValue(fields.LotType); write.Parameters.AddWithValue(fields.SupportedVehicleTypes);
        write.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.TimestampTz, Value=(object?)fields.ServiceWindow?.From.UtcDateTime ?? DBNull.Value });
        write.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.TimestampTz, Value=(object?)fields.ServiceWindow?.To.UtcDateTime ?? DBNull.Value });
        write.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Text, Value=(object?)fields.ServiceWindow?.State ?? DBNull.Value });
        await write.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task Withdraw(Guid siteId, Guid[] tenants, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await using var lockSite = new NpgsqlCommand("SELECT id FROM parking_sites WHERE id=$1 AND tenant_id=ANY($2) FOR UPDATE", db, tx);
        lockSite.Parameters.AddWithValue(siteId); lockSite.Parameters.AddWithValue(tenants);
        if (await lockSite.ExecuteScalarAsync(ct) == null) throw new StructureException("SITE_NOT_FOUND", "Site was not found.");
        await using var delete = new NpgsqlCommand("DELETE FROM facility_discovery_publications WHERE site_id=$1 AND tenant_id=ANY($2)", db, tx);
        delete.Parameters.AddWithValue(siteId); delete.Parameters.AddWithValue(tenants);
        await delete.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
