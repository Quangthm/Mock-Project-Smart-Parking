using Npgsql;
using SmartParking.FacilityService.Application;
namespace SmartParking.FacilityService.Persistence;
public sealed class FacilityDirectoryStore(NpgsqlDataSource source) : IFacilityDirectoryStore
{
    public async Task<bool> ProvisionTenantAsync(Guid id, TenantBody body, CancellationToken ct)
    {
            await using var db = await source.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand("INSERT INTO tenants(id,code,name,contact_email,contact_phone) VALUES ($1,$2,$3,$4,$5) ON CONFLICT(id) DO NOTHING", db);
            cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue("T-" + id.ToString("N")); cmd.Parameters.AddWithValue(body.Name); cmd.Parameters.AddWithValue(body.Email); cmd.Parameters.AddWithValue(body.Phone);
            await cmd.ExecuteNonQueryAsync(ct);
            await using var check = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM tenants WHERE id=$1 AND name=$2 AND contact_email=$3 AND contact_phone=$4 AND status='ACTIVE' AND deleted_at IS NULL)", db);
            check.Parameters.AddWithValue(id); check.Parameters.AddWithValue(body.Name); check.Parameters.AddWithValue(body.Email); check.Parameters.AddWithValue(body.Phone);
            return (bool)(await check.ExecuteScalarAsync(ct))!;
    }
    public async Task<IReadOnlyList<ValidatedSite>> ValidateSitesAsync(SiteValidation body, CancellationToken ct)
    {
            await using var db = await source.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT s.id,s.tenant_id FROM parking_sites s JOIN tenants t ON t.id=s.tenant_id WHERE s.id=ANY($1) AND s.tenant_id=ANY($2) AND s.status='ACTIVE' AND s.deleted_at IS NULL AND t.status='ACTIVE' AND t.deleted_at IS NULL ORDER BY s.id", db);
            cmd.Parameters.AddWithValue(body.SiteIds); cmd.Parameters.AddWithValue(body.TenantIds);
            var sites = new List<ValidatedSite>(); await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) sites.Add(new ValidatedSite(reader.GetGuid(0), reader.GetGuid(1)));
            return sites;
    }
}
