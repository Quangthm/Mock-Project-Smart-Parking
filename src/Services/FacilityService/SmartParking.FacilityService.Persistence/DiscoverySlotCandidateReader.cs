using System.Data;
using Npgsql;
using NpgsqlTypes;
using SmartParking.FacilityService.Application.Discovery;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

public sealed class DiscoverySlotCandidateReader(NpgsqlDataSource source) : IDiscoverySlotCandidateReader
{
    // This bounds the whole batch, including incompatible slots examined for scope integrity.
    // The sentinel prevents an oversized universe from becoming a partial successful response.
    private const int MaxRegisteredSlots = 2048;

    public async Task<DiscoveryCandidateBatch> ReadAsync(ReadDiscoverySlotCandidatesQuery query, CancellationToken ct)
    {
        var ids = query.Sites.Select(s => s.SiteId).ToArray();
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", db, tx))
            await readOnly.ExecuteNonQueryAsync(ct);
        await using var publications = new NpgsqlCommand("""
            SELECT p.site_id,p.tenant_id,s.tenant_id,p.configuration_version,statement_timestamp()
            FROM facility_discovery_publications p JOIN parking_sites s ON s.id=p.site_id
            JOIN tenants t ON t.id=s.tenant_id
            WHERE p.site_id=ANY(@ids) AND 'CAR'=ANY(p.categories)
              AND s.status='ACTIVE' AND s.deleted_at IS NULL AND t.status='ACTIVE' AND t.deleted_at IS NULL
            """, db, tx);
        publications.Parameters.AddWithValue("ids", ids);

        var sites = new Dictionary<Guid, DiscoveryCandidateSite>();
        var slots = ids.ToDictionary(id => id, _ => new List<DiscoveryCandidateSlot>());
        DateTimeOffset asOf = default;
        await using (var rows = await publications.ExecuteReaderAsync(ct))
        {
            while (await rows.ReadAsync(ct))
            {
                var id = rows.GetGuid(0);
                var tenant = rows.GetGuid(1);
                if (tenant == Guid.Empty || tenant != rows.GetGuid(2)) throw InvalidScope();
                sites.Add(id, new(id, tenant, rows.GetGuid(3), true, slots[id]));
                asOf = rows.GetFieldValue<DateTimeOffset>(4);
            }
        }
        foreach (var expected in query.Sites)
            if (!sites.TryGetValue(expected.SiteId, out var site) || site.ConfigurationVersion != expected.ConfigurationVersion)
                throw new StructureException("SEARCH_CHANGED", "Restart the search with the current publication.");

        await using var candidates = new NpgsqlCommand("""
            SELECT s.site_id,s.tenant_id,s.id,s.physical_state,s.updated_at,eligibility.car,eligibility.outcome
            FROM (SELECT * FROM parking_slots WHERE site_id=ANY(@ids) AND deleted_at IS NULL LIMIT @limit) s
            """ + "\n" + SlotEligibilitySql.Decision + "\nORDER BY s.site_id,s.id", db, tx);
        candidates.Parameters.AddWithValue("ids", ids);
        candidates.Parameters.AddWithValue("limit", MaxRegisteredSlots + 1);
        candidates.Parameters.AddWithValue("start", query.StartAt ?? query.At);
        candidates.Parameters.AddWithValue("end", NpgsqlDbType.TimestampTz, (object?)query.EndAt ?? DBNull.Value);
        var examined = 0;
        await using (var rows = await candidates.ExecuteReaderAsync(ct))
        {
            while (await rows.ReadAsync(ct))
            {
                if (++examined > MaxRegisteredSlots)
                    throw new StructureException("CATALOGUE_UNAVAILABLE", "Candidate batch exceeds the supported read size.");
                var site = sites[rows.GetGuid(0)];
                if (site.TenantId != rows.GetGuid(1) || rows.GetGuid(2) == Guid.Empty) throw InvalidScope();
                // A missing compatibility declaration is insufficient evidence for a complete discovery result.
                var outcome = rows.IsDBNull(5) ? "DEPENDENCY_UNAVAILABLE" : rows.GetString(6);
                if (outcome == "DEPENDENCY_UNAVAILABLE") sites[site.SiteId] = site with { Complete = false };
                else if (!rows.GetBoolean(5)) continue;
                slots[site.SiteId].Add(new(rows.GetGuid(2), outcome, rows.GetString(3), rows.GetFieldValue<DateTimeOffset>(4)));
            }
        }
        await tx.CommitAsync(ct);
        return new(asOf, query.At, query.StartAt, query.EndAt, ids.Select(id => sites[id]).ToArray());
    }

    private static StructureException InvalidScope() =>
        new("CATALOGUE_UNAVAILABLE", "Candidate scope could not be established.");
}
