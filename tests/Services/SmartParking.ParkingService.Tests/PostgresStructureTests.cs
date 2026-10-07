using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Persistence;
using SmartParking.ReservationService.Persistence;

namespace SmartParking.ParkingService.Tests;

public sealed class ParkingPostgresFactAttribute : FactAttribute
{
    public ParkingPostgresFactAttribute()
    { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION"))) Skip = "Set SMARTPARK_AUTH_TEST_CONNECTION to an isolated PostgreSQL test server."; }
}
public sealed class PostgresStructureTests
{
    private sealed class Identity(OwnerScope owner) : IOwnerAuthorizer
    {
        public bool Active=true;
        public Task<bool> IsOwnerAsync(OwnerScope value,CancellationToken ct)=>Task.FromResult(Active&&value==owner);
    }
    private sealed class Commitments(StructureSafetyStore store) : IStructureCommitments
    {
        public async Task<IStructureLease> AcquireAsync(Guid tenant,Guid site,CancellationToken ct)
        {
            var token=Guid.NewGuid(); var snapshot=await store.AcquireAsync(tenant,site,token,ct);
            return new Lease(store,tenant,site,token,new(snapshot.ProtectedSlots,snapshot.HasLiveCommitments));
        }
        private sealed class Lease(StructureSafetyStore store,Guid tenant,Guid site,Guid token,CommitmentSnapshot snapshot):IStructureLease
        {
            private bool committing;
            public CommitmentSnapshot Snapshot=>snapshot;
            public void BeginCommit()=>committing=true;
            public Task CompleteAsync(StructureOutcome outcome,CancellationToken ct)=>store.ReleaseAsync(tenant,site,token,new(outcome.SiteActive,outcome.RemovedSlots,outcome.RemovedUnits,outcome.Capacities.Select(c=>new SmartParking.ReservationService.Persistence.PoolCapacity(c.UnitId,c.VehicleType,c.Capacity)).ToArray()),ct);
            public async ValueTask DisposeAsync() { if(!committing) await store.ReleaseAsync(tenant,site,token,null,default); }
        }
    }
    [ParkingPostgresFact]
    public async Task SeparateSchemaScopeSafetyRollbackAndConcurrency()
    {
        var input=Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var names=new[]{"smartpark_structure_test_"+Guid.NewGuid().ToString("N"),"smartpark_structure_test_"+Guid.NewGuid().ToString("N")};
        await using var admin=new NpgsqlConnection(input); await admin.OpenAsync();
        foreach(var name in names) await new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin).ExecuteNonQueryAsync();
        try
        {
            await using var source=NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(input){Database=names[0]}.ConnectionString);
            await using var claimsSource=NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(input){Database=names[1]}.ConnectionString);
            await using var db=await source.OpenConnectionAsync(); await using var claims=await claimsSource.OpenConnectionAsync();
            async Task Sql(NpgsqlConnection c,string sql,params object[] values)
            { await using var cmd=new NpgsqlCommand(sql,c); foreach(var value in values) cmd.Parameters.Add(new NpgsqlParameter{Value=value}); await cmd.ExecuteNonQueryAsync(); }
            var root=new DirectoryInfo(AppContext.BaseDirectory); while(root!=null&&!File.Exists(Path.Combine(root.FullName,"SmartParking.slnx")))root=root.Parent;
            var schemaRoot=Environment.GetEnvironmentVariable("SMARTPARK_SCHEMA_ROOT");
            if(string.IsNullOrWhiteSpace(schemaRoot)) schemaRoot=root!.FullName;
            if(!File.Exists(Path.Combine(schemaRoot,"scripts/database/microservices/02-parking-service-db.sql")))
                throw new InvalidOperationException("Set SMARTPARK_SCHEMA_ROOT to the arch/database-schema checkout before running database tests.");
            async Task Apply(NpgsqlConnection c,string path)=>await Sql(c,await File.ReadAllTextAsync(Path.Combine(schemaRoot,"scripts/database",path)));
            await Apply(db,"microservices/02-parking-service-db.sql");
            await Apply(db,"integration/02-parking-service-alignment.sql"); await Apply(db,"integration/02-parking-service-alignment.sql");
            await Apply(claims,"microservices/03-reservation-session-service-db.sql");
            await Apply(claims,"integration/03-reservation-structure-safety.sql"); await Apply(claims,"integration/03-reservation-structure-safety.sql");
            var owner=new OwnerScope(Guid.NewGuid(),Guid.NewGuid()); var identity=new Identity(owner);
            await Sql(db,"INSERT INTO tenants(id,code,name,contact_email,contact_phone) VALUES ($1,'T','Company','owner@example.com','0911111111')",owner.TenantId);
            var safety=new StructureSafetyStore(claimsSource);
            var service=new ParkingStructureService(new PostgresParkingStructureRepository(source,identity,new Commitments(safety)));
            async Task Error(string code,Func<Task> action)=>Assert.Equal(code,(await Assert.ThrowsAsync<StructureException>(action)).Code);
            await Error("FORBIDDEN",()=>service.CreateAsync(owner with{UserId=Guid.NewGuid()},"X","X","X"));
            var site=await service.CreateAsync(owner," s1 ","Parking","Address");
            var zone=await service.AddUnitAsync(owner,site.Id,UnitType.ZONE,"Outdoor",capacity:3);
            var slot=await service.AddSlotAsync(owner,site.Id,zone,"M1",VehicleType.MOTORCYCLE);
            var other=await service.AddUnitAsync(owner,site.Id,UnitType.ZONE,"Other");
            await Error("DUPLICATE_IDENTIFIER",()=>service.AddSlotAsync(owner,site.Id,other,"m1",VehicleType.CAR));
            await Error("DUPLICATE_IDENTIFIER",()=>service.ChangeAsync(owner,site.Id,s=>{s.AddSlot(zone,"C1",VehicleType.CAR);s.AddSlot(zone,"M1",VehicleType.CAR);}));
            Assert.Single((await service.GetAsync(owner,site.Id)).Slots);
            await service.ChangeAsync(owner,site.Id,s=>s.ConfigureSlot(slot,VehicleType.MOTORCYCLE,SlotType.VIP,"{\"x\":1}","{\"covered\":true}"));
            Assert.Contains("covered",(await service.GetAsync(owner,site.Id)).Slots.Single().Features);
            await Sql(db,"UPDATE parking_slots SET physical_state='OCCUPIED' WHERE id=$1",slot);
            await Error("STRUCTURE_IN_USE",()=>service.ChangeAsync(owner,site.Id,s=>s.RemoveSlot(slot)));
            await Sql(db,"UPDATE parking_slots SET physical_state='UNKNOWN' WHERE id=$1",slot);
            await Error("STRUCTURE_IN_USE",()=>service.ChangeAsync(owner,site.Id,s=>s.SetActive(false)));
            await Sql(db,"UPDATE parking_slots SET physical_state='AVAILABLE' WHERE id=$1",slot);
            var booking=Guid.NewGuid();
            await Sql(claims,"INSERT INTO reservations(id,tenant_id,site_id,account_id,expected_start_time,expected_end_time,status) VALUES ($1,$2,$3,$4,now(),now()+interval '1 hour','CONFIRMED')",booking,owner.TenantId,site.Id,Guid.NewGuid());
            await Error("STRUCTURE_IN_USE",()=>service.ChangeAsync(owner,site.Id,s=>s.RemoveSlot(slot)));
            await Sql(claims,"UPDATE reservations SET status='CANCELLED' WHERE id=$1",booking);
            var token=Guid.NewGuid(); await safety.AcquireAsync(owner.TenantId,site.Id,token,default);
            var locked=await Assert.ThrowsAsync<PostgresException>(()=>Sql(claims,"UPDATE reservations SET status='CONFIRMED' WHERE id=$1",booking));Assert.Equal("55P03",locked.SqlState);
            await Assert.ThrowsAsync<HoldConflictException>(()=>safety.AcquireAsync(owner.TenantId,site.Id,Guid.NewGuid(),default));
            await safety.ReleaseAsync(owner.TenantId,site.Id,token,null,default);
            // Configured capacity alone is not a booking. Reductions publish fresh totals and clamp quotas.
            await Sql(claims,"INSERT INTO site_capacity_pools(tenant_id,site_id,vehicle_type,total_capacity,max_reservation_quota) VALUES ($1,$2,'MOTORCYCLE',1,1)",owner.TenantId,site.Id);
            await service.ChangeAsync(owner,site.Id,s=>s.SetActive(false));
            await using(var preserved=new NpgsqlCommand("SELECT total_capacity=1 AND max_reservation_quota=1 FROM site_capacity_pools WHERE site_id=$1",claims))
            {preserved.Parameters.AddWithValue(site.Id);Assert.True((bool)(await preserved.ExecuteScalarAsync())!);}
            var closed=await Assert.ThrowsAsync<PostgresException>(()=>Sql(claims,"UPDATE reservations SET status='CONFIRMED' WHERE id=$1",booking));Assert.Equal("23514",closed.SqlState);
            await service.ChangeAsync(owner,site.Id,s=>s.SetActive(true));
            await service.ChangeAsync(owner,site.Id,s=>s.RemoveSlot(slot));
            await using(var pool=new NpgsqlCommand("SELECT total_capacity=0 AND max_reservation_quota=0 FROM site_capacity_pools WHERE site_id=$1",claims))
            {pool.Parameters.AddWithValue(site.Id);Assert.True((bool)(await pool.ExecuteScalarAsync())!);}
            var removed=await Assert.ThrowsAsync<PostgresException>(()=>Sql(claims,"UPDATE reservations SET status='CONFIRMED',target_slot_id=$2 WHERE id=$1",booking,slot));Assert.Equal("23514",removed.SqlState);
            var occupied=await Assert.ThrowsAsync<PostgresException>(()=>Sql(db,"UPDATE parking_slots SET physical_state='OCCUPIED' WHERE id=$1",slot));Assert.Equal("23514",occupied.SqlState);
            await service.ChangeAsync(owner,site.Id,s=>s.SetActive(false));
            var inactive=await Assert.ThrowsAsync<PostgresException>(()=>Sql(claims,"UPDATE reservations SET status='CONFIRMED',target_slot_id=NULL WHERE id=$1",booking));Assert.Equal("23514",inactive.SqlState);
            await service.ChangeAsync(owner,site.Id,s=>s.UpdateProfile("S1","Renamed","Address",null,null));
            Assert.Equal("INACTIVE",(await service.GetAsync(owner,site.Id)).Site.Status);
            identity.Active=false;await Error("FORBIDDEN",()=>service.GetAsync(owner,site.Id));
            await using var check=new NpgsqlCommand("SELECT to_regclass('users') IS NULL AND to_regclass('reservations') IS NULL",db);Assert.True((bool)(await check.ExecuteScalarAsync())!);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            foreach(var name in names) await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)",admin).ExecuteNonQueryAsync();
        }
    }
}
