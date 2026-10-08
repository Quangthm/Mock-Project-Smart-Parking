using System.Text.Json;
using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
namespace SmartParking.ParkingService.Persistence;
public sealed class StructureOperationStore(NpgsqlDataSource source,IOwnerAuthorizer identity):IStructureOperationStore
{
    public async Task<IAsyncDisposable> LockAsync(Guid id,CancellationToken ct)
    {
        var db=await source.OpenConnectionAsync(ct);
        try{await using var cmd=Cmd(db,"SELECT pg_advisory_lock(hashtextextended($1::text,1))",id);await cmd.ExecuteNonQueryAsync(ct);return new Gate(db,id);}
        catch{await db.DisposeAsync();throw;}
    }
    private sealed class Gate(NpgsqlConnection db,Guid id):IAsyncDisposable
    {
        public async ValueTask DisposeAsync(){try{await using var cmd=Cmd(db,"SELECT pg_advisory_unlock(hashtextextended($1::text,1))",id);await cmd.ExecuteNonQueryAsync();}finally{await db.DisposeAsync();}}
    }
    private async Task Require(OwnerScope owner,CancellationToken ct){if(!await identity.IsOwnerAsync(owner,ct))throw new StructureException("FORBIDDEN","Current Owner access is required.");}
    private static NpgsqlCommand Cmd(NpgsqlConnection db,string sql,params object?[] values)
    {var cmd=new NpgsqlCommand(sql,db);foreach(var value in values)cmd.Parameters.Add(new NpgsqlParameter{Value=value??DBNull.Value});return cmd;}
    public async Task<StructureOperation> BeginAsync(OwnerScope owner,Guid site,Guid key,StructureEdit edit,CancellationToken ct)
    {
        await Require(owner,ct);await using var db=await source.OpenConnectionAsync(ct);var request=JsonSerializer.Serialize(edit);
        await using(var cmd=Cmd(db,"INSERT INTO structure_operations(id,site_id,tenant_id,actor_id,idempotency_key,request,status) SELECT $1,s.id,$3,$4,$5,$6::jsonb,'pending' FROM parking_sites s WHERE s.id=$2 AND s.tenant_id=$3 AND s.deleted_at IS NULL ON CONFLICT(site_id,idempotency_key) DO NOTHING",Guid.NewGuid(),site,owner.TenantId,owner.UserId,key,request))await cmd.ExecuteNonQueryAsync(ct);
        await using var read=Cmd(db,"SELECT id,status,reason,impact::text,request=$5::jsonb FROM structure_operations WHERE site_id=$1 AND idempotency_key=$2 AND tenant_id=$3 AND actor_id=$4",site,key,owner.TenantId,owner.UserId,request);
        await using var r=await read.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new StructureException("FORBIDDEN","Operation scope is not available.");
        if(!r.GetBoolean(4))throw new StructureException("DUPLICATE_IDENTIFIER","Idempotency key belongs to a different request.");
        return new(r.GetGuid(0),site,r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3));
    }
    public async Task<StructureOperation> UpdateAsync(OwnerScope owner,Guid id,string status,string? reason,string? impact,CancellationToken ct)
    {
        await Require(owner,ct);await using var db=await source.OpenConnectionAsync(ct);
        await using var cmd=Cmd(db,"UPDATE structure_operations SET status=$2,reason=$3,impact=$4::jsonb,updated_at=now() WHERE id=$1 AND tenant_id=$5 AND actor_id=$6 AND status<>'completed' RETURNING site_id,status,reason,impact::text",id,status,reason,impact,owner.TenantId,owner.UserId);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new StructureException("STRUCTURE_CONFLICT","Operation was finalized or is not in scope.");
        return new(id,r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3));
    }
    public async Task<StructureOperation> ReadAsync(OwnerScope owner,Guid site,Guid id,CancellationToken ct)
    {
        await Require(owner,ct);await using var db=await source.OpenConnectionAsync(ct);
        await using var cmd=Cmd(db,"SELECT status,reason,impact::text FROM structure_operations WHERE id=$1 AND site_id=$2 AND tenant_id=$3 AND actor_id=$4",id,site,owner.TenantId,owner.UserId);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new StructureException("SITE_NOT_FOUND","Operation was not found.");return new(id,site,r.GetString(0),r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2));
    }
}
