using Npgsql;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Persistence;

// Existing raw SQL physical writers must use the same site key before their first read.
internal static class PhysicalSiteGuard
{
    public static async Task RequireAvailableAsync(NpgsqlConnection db, NpgsqlTransaction tx, Guid siteId, CancellationToken ct)
    {
        await using var gate = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(hashtextextended($1::text,2))", db, tx);
        gate.Parameters.AddWithValue(siteId);
        if (!Equals(await gate.ExecuteScalarAsync(ct), true))
            throw new StructureException("STRUCTURE_BUSY", "Site configuration is being managed.");
    }
}
