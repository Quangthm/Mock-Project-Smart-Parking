using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Persistence;

namespace SmartParking.ParkingService.Tests;

public sealed class ParkingPostgresFactAttribute : FactAttribute
{
    public ParkingPostgresFactAttribute()
    { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION"))) Skip = "Set SMARTPARK_AUTH_TEST_CONNECTION to an isolated PostgreSQL test server."; }
}

public sealed class PostgresStructureTests
{
    [ParkingPostgresFact]
    public async Task RealSchemaAuthorizationRollbackSoftDeleteAndConcurrency()
    {
        var input = Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var database = "smartpark_structure_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(input); await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin).ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(input) { Database = database };
        try
        {
            await using var source = NpgsqlDataSource.Create(connection.ConnectionString);
            await using var db = await source.OpenConnectionAsync();
            async Task Sql(string sql, params object[] values)
            {
                await using var cmd = new NpgsqlCommand(sql, db);
                foreach (var value in values) cmd.Parameters.Add(new NpgsqlParameter { Value = value });
                await cmd.ExecuteNonQueryAsync();
            }
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
            await Sql(await File.ReadAllTextAsync(Path.Combine(root!.FullName, "scripts/database/05.1-Database-Scripts.sql")));
            var upgrade = await File.ReadAllTextAsync(Path.Combine(root.FullName, "scripts/database/05.6-Parking-Structure.sql"));
            await Sql(upgrade); await Sql(upgrade);
            var owner = new OwnerScope(Guid.NewGuid(), Guid.NewGuid()); var accountId = Guid.NewGuid();
            await Sql("INSERT INTO tenants(id,code,name) VALUES ($1,'TENANT','Company')", owner.TenantId);
            await Sql("INSERT INTO users(id,phone,password_hash,full_name) VALUES ($1,'0900000000','unused','Owner')", owner.UserId);
            await Sql("INSERT INTO accounts(id,user_id,tenant_id,account_type) VALUES ($1,$2,$3,'BUSINESS_OPERATOR')", accountId, owner.UserId, owner.TenantId);
            await Sql("INSERT INTO account_roles(account_id,role_code) VALUES ($1,'BUSINESS_OWNER')", accountId);
            var repo = new PostgresParkingStructureRepository(source); var service = new ParkingStructureService(repo);
            async Task Error(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<StructureException>(action)).Code);
            await Error("FORBIDDEN", () => service.CreateAsync(owner with { UserId = Guid.NewGuid() }, "X", "X", "X"));
            await Error("FORBIDDEN", () => service.ListAsync(owner with { TenantId = Guid.NewGuid() }));
            var site = await service.CreateAsync(owner, " p01 ", "Parking", "Address");
            await Error("DUPLICATE_IDENTIFIER", () => service.CreateAsync(owner, "P01", "Parking", "Address"));
            Assert.Equal("P01", site.Code); Assert.Null(site.Latitude);
            var zone = await service.AddUnitAsync(owner, site.Id, UnitType.ZONE, "Outdoor", capacity: 3);
            var slot = await service.AddSlotAsync(owner, site.Id, zone, "M1", VehicleType.MOTORBIKE);
            var access = await service.AddAccessPathAsync(owner, site.Id, "Entry", null, zone, "{\"points\":[]}");
            Assert.Equal(1, (await service.GetAsync(owner, site.Id)).Site.TotalPhysicalCapacity);
            await Sql("UPDATE parking_sites SET total_physical_capacity=100 WHERE id=$1", site.Id);
            await Error("STRUCTURE_CONFLICT", () => service.ChangeAsync(owner, site.Id, s => s.UpdateProfile("P01", "Changed", "Address", null, null)));
            Assert.Equal("Parking", (await service.GetAsync(owner, site.Id)).Site.Name);
            await Sql("UPDATE parking_sites SET total_physical_capacity=1 WHERE id=$1", site.Id);
            await service.ChangeAsync(owner, site.Id, s => s.ConfigureSlot(slot, VehicleType.MOTORBIKE, SlotType.VIP, "{\"x\":1}", "{\"covered\":true}"));
            var configured = (await service.GetAsync(owner, site.Id)).Slots.Single();
            Assert.Equal(SlotType.VIP, configured.Type); Assert.Contains("covered", configured.Features);
            // A failure halfway through an application mutation cannot save partial structure.
            await Error("DUPLICATE_IDENTIFIER", () => service.ChangeAsync(owner, site.Id, s =>
            { s.AddSlot(zone, "M2", VehicleType.MOTORBIKE); s.AddSlot(zone, "M1", VehicleType.SEDAN); }));
            Assert.Single((await service.GetAsync(owner, site.Id)).Slots);
            var another = await service.CreateAsync(owner, "P02", "Second", "Address", 10.77m, 106.69m);
            var foreignUnit = await service.AddUnitAsync(owner, another.Id, UnitType.ZONE, "Foreign");
            await Error("UNIT_NOT_FOUND", () => service.AddSlotAsync(owner, site.Id, foreignUnit, "X", VehicleType.SEDAN));
            await Error("SITE_NOT_FOUND", () => service.GetAsync(owner, Guid.NewGuid()));
            // Two connections adding the same code are serialized by the site row lock.
            async Task<bool> AddSame()
            {
                try { await service.AddSlotAsync(owner, site.Id, zone, "M2", VehicleType.MOTORBIKE); return true; }
                catch (StructureException e) when (e.Code == "DUPLICATE_IDENTIFIER") { return false; }
            }
            Assert.Single(await Task.WhenAll(AddSame(), AddSame()), success => success);
            await Sql("UPDATE parking_slots SET is_physically_occupied=true WHERE id=$1", slot);
            await Error("STRUCTURE_IN_USE", () => service.ChangeAsync(owner, site.Id, s => s.RemoveSlot(slot)));
            await Sql("UPDATE parking_slots SET is_physically_occupied=false, operational_status='UNKNOWN' WHERE id=$1", slot);
            await Error("STRUCTURE_IN_USE", () => service.ChangeAsync(owner, site.Id, s => s.SetActive(false)));
            await Sql("UPDATE parking_slots SET operational_status='OPERATIONAL' WHERE id=$1", slot);
            // A committed capacity pool is also a conservative block until capacity/reallocation support exists.
            var pool = Guid.NewGuid();
            await Sql("INSERT INTO site_capacity_pools(id,site_id,total_capacity,max_reservation_quota) VALUES ($1,$2,1,1)", pool, site.Id);
            await Error("STRUCTURE_IN_USE", () => service.ChangeAsync(owner, site.Id, s => s.RemoveSlot(slot)));
            await Sql("DELETE FROM site_capacity_pools WHERE id=$1", pool);
            var allocation = Guid.NewGuid();
            await Sql("""
                INSERT INTO slot_allocations(id,tenant_id,site_id,slot_id,allocated_period,allocation_mode)
                VALUES ($1,$2,$3,$4,tstzrange(now(),now()+interval '1 hour'),'HARD_PREBOOKED')
                """, allocation, owner.TenantId, site.Id, slot);
            await Error("STRUCTURE_IN_USE", () => service.ChangeAsync(owner, site.Id, s => s.RemoveSlot(slot)));
            await Sql("UPDATE slot_allocations SET allocation_status='RELEASED' WHERE id=$1", allocation);
            var reservation = Guid.NewGuid();
            await Sql("""
                INSERT INTO reservations(id,tenant_id,site_id,account_id,plate_number,reserved_period,status)
                VALUES ($1,$2,$3,$4,'TEST',tstzrange(now()+interval '1 day',now()+interval '2 days'),'CONFIRMED')
                """, reservation, owner.TenantId, site.Id, accountId);
            await Error("STRUCTURE_IN_USE", () => service.ChangeAsync(owner, site.Id, s => s.RemoveSlot(slot)));
            await Sql("UPDATE reservations SET status='CANCELLED' WHERE id=$1", reservation);
            await Sql("UPDATE reservations SET status='CONFIRMED', reserved_period=tstzrange(now(),NULL) WHERE id=$1", reservation);
            await Error("STRUCTURE_IN_USE", () => service.ChangeAsync(owner, site.Id, s => s.RemoveSlot(slot)));
            await Sql("UPDATE reservations SET status='CANCELLED' WHERE id=$1", reservation);
            var session = Guid.NewGuid();
            await Sql("INSERT INTO parking_sessions(id,tenant_id,site_id,plate_number) VALUES ($1,$2,$3,'WALKIN')", session, owner.TenantId, site.Id);
            await Error("STRUCTURE_IN_USE", () => service.ChangeAsync(owner, site.Id, s => s.SetActive(false)));
            await Sql("UPDATE parking_sessions SET exit_time=now(),session_status='COMPLETED' WHERE id=$1", session);
            // The trigger holds the same site lock as a structure edit. The waiting edit reloads commitments after commit.
            await using (var tx = await db.BeginTransactionAsync())
            {
                await Sql("UPDATE reservations SET status='CONFIRMED' WHERE id=$1", reservation);
                var removal = service.ChangeAsync(owner, site.Id, s => s.RemoveSlot(slot));
                await tx.CommitAsync();
                await Error("STRUCTURE_IN_USE", () => removal);
            }
            await Sql("UPDATE reservations SET status='CANCELLED' WHERE id=$1", reservation);
            await service.ChangeAsync(owner, site.Id, s => s.RemoveSlot(slot));
            Assert.Single((await service.GetAsync(owner, site.Id)).Slots);
            // Historical allocation remains, but a new claim on the deleted slot is rejected at the DB boundary.
            var failure = await Assert.ThrowsAsync<PostgresException>(() => Sql("""
                INSERT INTO slot_allocations(id,tenant_id,site_id,slot_id,allocated_period,allocation_mode)
                VALUES ($1,$2,$3,$4,tstzrange(now(),now()+interval '1 hour'),'HARD_PREBOOKED')
                """, Guid.NewGuid(), owner.TenantId, site.Id, slot));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            var occupancyFailure = await Assert.ThrowsAsync<PostgresException>(() => Sql("UPDATE parking_slots SET is_physically_occupied=true WHERE id=$1", slot));
            Assert.Equal(PostgresErrorCodes.CheckViolation, occupancyFailure.SqlState);
            var structure = await service.GetAsync(owner, site.Id);
            await service.ChangeAsync(owner, site.Id, s => { s.RemoveAccessPath(access); s.RemoveSlot(structure.Slots.Single().Id); s.RemoveUnit(zone); });
            Assert.Empty((await service.GetAsync(owner, site.Id)).Units);
            await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM slot_allocations WHERE id=$1", db))
            { cmd.Parameters.AddWithValue(allocation); Assert.Equal(1L, await cmd.ExecuteScalarAsync()); }
            await Sql("UPDATE account_roles SET role_code='SITE_OPERATOR' WHERE account_id=$1", accountId);
            await Error("FORBIDDEN", () => service.GetAsync(owner, site.Id));
            await Sql("UPDATE account_roles SET role_code='BUSINESS_OWNER' WHERE account_id=$1", accountId);
            await Sql("UPDATE users SET status='LOCKED' WHERE id=$1", owner.UserId);
            await Error("FORBIDDEN", () => service.ListAsync(owner));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }
}
