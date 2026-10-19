using SmartParking.ParkingService.Application;
using System.Text.Json;
using Npgsql;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Persistence;

public sealed class OwnerOnboardingStore(NpgsqlDataSource source) : IOwnerOnboardingStore
{
    public async Task ProvisionAsync(Guid applicationId, OwnerOnboardingBody body, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(body);
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        async Task<object?> Execute(string sql, params object[] values)
        {
            await using var cmd = new NpgsqlCommand(sql, db, tx);
            foreach (var value in values) cmd.Parameters.Add(new NpgsqlParameter { Value = value });
            return await cmd.ExecuteScalarAsync(ct);
        }
        // Serialize requests for this application, including the first insert.
        await Execute("SELECT pg_advisory_xact_lock(hashtextextended($1, 0))", applicationId.ToString());
        var replay = await Execute("SELECT payload=$2::jsonb FROM owner_onboarding_provisions WHERE application_id=$1", applicationId, payload);
        if (replay is bool same)
        {
            if (!same) throw new StructureException("STRUCTURE_CONFLICT", "Onboarding was already provisioned with another payload.");
            // Never overwrite a site that an owner has subsequently edited or deactivated.
            await tx.CommitAsync(ct);
            return;
        }
        await Execute("INSERT INTO tenants(id,code,name,contact_email,contact_phone) VALUES ($1,$2,$3,$4,$5) ON CONFLICT(id) DO NOTHING",
            body.TenantId, "T-" + body.TenantId.ToString("N"), body.Name, body.Email, body.Phone);
        if (await Execute("SELECT EXISTS(SELECT 1 FROM tenants WHERE id=$1 AND name=$2 AND contact_email=$3 AND contact_phone=$4 AND status='ACTIVE' AND deleted_at IS NULL)",
            body.TenantId, body.Name, body.Email, body.Phone) is not true)
            throw new StructureException("STRUCTURE_CONFLICT", "Tenant provisioning conflict.");
        // Declared capacity stays in the snapshot. Physical capacity is zero until real slots exist.
        await Execute("INSERT INTO parking_sites(id,tenant_id,site_code,name,address,total_physical_capacity,status) VALUES ($1,$2,$3,$4,$5,0,'ACTIVE')",
            applicationId, body.TenantId, "SPK-" + applicationId.ToString("N"), body.Lot.GetProperty("name").GetString()!, body.Lot.GetProperty("address").GetString()!);
        await Execute("INSERT INTO owner_onboarding_provisions(application_id,site_id,payload) VALUES ($1,$1,$2::jsonb)", applicationId, payload);
        await tx.CommitAsync(ct);
    }
}
