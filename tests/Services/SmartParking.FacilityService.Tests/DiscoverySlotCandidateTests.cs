using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Domain.Reservations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SmartParking.FacilityService.API;
using SmartParking.FacilityService.API.Controllers;
using SmartParking.FacilityService.Application.Discovery;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Persistence;

namespace SmartParking.FacilityService.Tests;

public sealed class DiscoverySlotCandidateTests
{
    private const string PublicationSchema = "scripts/database/migrations/20261106_facility_discovery_publication.sql";
    private static readonly DateTimeOffset At = new(2026, 11, 27, 8, 0, 0, TimeSpan.Zero);
    private static async Task<ReadDiscoverySlotCandidatesQuery> Publish(OccupancyProjectionFixture f)
    {
        var version = Guid.NewGuid();
        await f.ExecuteAsync($"""
            INSERT INTO facility_discovery_publications(site_id,tenant_id,configuration_version,lot_type,categories)
            VALUES ('{f.SiteId}','{f.TenantId}','{version}','outdoor',ARRAY['CAR']);
            UPDATE parking_slots SET supported_vehicle_type='CAR' WHERE id='{f.SlotId}';
            """);
        return new([new(f.SiteId, version)], "CAR", At, null, null);
    }

    [StructurePostgresFact]
    public async Task MaintenanceAtCheckoutAllowsStayWhileBufferedConflictBlocksBothOperationsPaths()
    {
        await using var f = await OccupancyProjectionFixture.CreateAsync(PublicationSchema);
        var end = At.AddHours(1);
        var query = (await Publish(f)) with { StartAt = At, EndAt = end };
        await f.ExecuteAsync($"""
            INSERT INTO structure_maintenance_windows(tenant_id,site_id,service_window,reason,created_by,enforcement_state)
                VALUES ('{f.TenantId}','{f.SiteId}',tstzrange('{end:O}','{end.AddHours(1):O}','[)'),'test','{Guid.NewGuid()}','acknowledged');
            """);
        var site = Assert.Single((await new DiscoverySlotCandidateReader(f.DataSource).ReadAsync(query, default)).Items);
        Assert.True(site.Complete);
        Assert.Equal("ELIGIBLE", Assert.Single(site.Slots).Outcome);
        await using var db = new ParkingDbContext(new DbContextOptionsBuilder<ParkingDbContext>().UseNpgsql(f.DataSource).Options);
        var selected = await new ReservationSlotReader(db).CheckSlotEligibilityAsync(f.SlotId, At, end, default);
        Assert.Equal("ELIGIBLE", selected?.Outcome);
        var buffered = end.AddMinutes(15);
        var proposed = new CapacityDemand(Guid.NewGuid(), f.SlotId, At, buffered);
        foreach (var start in new[] { end, buffered })
        {
            CapacityDemand[] conflicts = [new(Guid.NewGuid(), f.SlotId, start, start.AddHours(1))];
            var admission = ReservationAvailability.Evaluate(proposed, conflicts, [], null, At, TimeSpan.Zero);
            var count = ReservationAvailability.CountAvailable([f.SlotId], new(At, buffered), At,
                TimeSpan.Zero, conflicts, [], [], null, default);
            Assert.Equal(start == buffered, admission.Allowed);
            Assert.Equal(admission.Allowed ? 1 : 0, count);
        }
    }

    [StructurePostgresFact]
    public async Task BatchReturnsEmptySitesAndCompleteCompatibleRegisteredSlotsInTrustedScope()
    {
        await using var f = await OccupancyProjectionFixture.CreateAsync(PublicationSchema);
        var query = await Publish(f);
        var empty = Guid.NewGuid();
        var version = Guid.NewGuid();
        var backup = Guid.NewGuid();
        var excluded = Guid.NewGuid();
        await f.ExecuteAsync($"""
            INSERT INTO parking_sites(id,tenant_id,site_code,name,address) VALUES ('{empty}','{f.TenantId}','empty','empty','empty');
            INSERT INTO facility_discovery_publications(site_id,tenant_id,configuration_version,lot_type,categories)
                VALUES ('{empty}','{f.TenantId}','{version}','outdoor',ARRAY['CAR']);
            UPDATE parking_slots SET supported_vehicle_type='MOTORCYCLE', physical_state='OCCUPIED' WHERE id='{f.SlotId}';
            INSERT INTO slot_compatibilities(tenant_id,site_id,slot_id,vehicle_type) VALUES ('{f.TenantId}','{f.SiteId}','{f.SlotId}','CAR');
            INSERT INTO parking_slots(id,tenant_id,site_id,spatial_unit_id,slot_code,supported_vehicle_type,reservation_state)
                VALUES ('{backup}','{f.TenantId}','{f.SiteId}','{f.UnitId}','backup','CAR','BACKUP'),
                       ('{excluded}','{f.TenantId}','{f.SiteId}','{f.UnitId}','moto','CAR',NULL);
            INSERT INTO slot_compatibilities(tenant_id,site_id,slot_id,vehicle_type) VALUES ('{f.TenantId}','{f.SiteId}','{excluded}','MOTORCYCLE');
            INSERT INTO parking_slots(tenant_id,site_id,spatial_unit_id,slot_code,supported_vehicle_type,deleted_at)
                VALUES ('{f.TenantId}','{f.SiteId}','{f.UnitId}','gone','CAR',now());
            """);
        var reader = new DiscoverySlotCandidateReader(f.DataSource);
        var batch = await reader.ReadAsync(query with { Sites = [new(empty, version), query.Sites[0]] }, default);
        Assert.Equal(2, batch.Items.Count);
        Assert.True(batch.Items[0].Complete);
        Assert.Empty(batch.Items[0].Slots);
        var site = batch.Items[1];
        Assert.Equal((f.SiteId, f.TenantId, query.Sites[0].ConfigurationVersion, true),
            (site.SiteId, site.TenantId, site.ConfigurationVersion, site.Complete));
        Assert.Equal(2, site.Slots.Count);
        var occupied = Assert.Single(site.Slots, s => s.SlotId == f.SlotId);
        Assert.Equal(("ELIGIBLE", "OCCUPIED"), (occupied.Outcome, occupied.PhysicalState));
        Assert.True(occupied.SlotUpdatedAt <= batch.AsOf);
        Assert.Equal("INELIGIBLE", Assert.Single(site.Slots, s => s.SlotId == backup).Outcome);
    }

    [StructurePostgresFact]
    public async Task IntervalAndInstantApplyHalfOpenMaintenanceAndScopeOnlyToAffectedSlots()
    {
        await using var f = await OccupancyProjectionFixture.CreateAsync(PublicationSchema);
        var query = await Publish(f);
        var reader = new DiscoverySlotCandidateReader(f.DataSource);
        var other = Guid.NewGuid();
        await f.ExecuteAsync($"""
            INSERT INTO parking_slots(id,tenant_id,site_id,spatial_unit_id,slot_code,supported_vehicle_type)
                VALUES ('{other}','{f.TenantId}','{f.SiteId}','{f.UnitId}','B','CAR');
            INSERT INTO structure_maintenance_windows(tenant_id,site_id,unit_id,slot_id,service_window,reason,created_by)
                VALUES ('{f.TenantId}','{f.SiteId}','{f.UnitId}','{f.SlotId}',
                    tstzrange('{At:O}','{At.AddMinutes(10):O}','[)'),'test','{Guid.NewGuid()}');
            """);
        var instant = Assert.Single((await reader.ReadAsync(query, default)).Items);
        Assert.False(instant.Complete);
        Assert.Equal("DEPENDENCY_UNAVAILABLE", Assert.Single(instant.Slots, s => s.SlotId == f.SlotId).Outcome);
        Assert.Equal("ELIGIBLE", Assert.Single(instant.Slots, s => s.SlotId == other).Outcome);
        var endingAtStart = query with { StartAt = At.AddMinutes(-30), EndAt = At };
        Assert.True(Assert.Single((await reader.ReadAsync(endingAtStart, default)).Items).Complete);
        Assert.True(Assert.Single((await reader.ReadAsync(query with { At = At.AddMinutes(10) }, default)).Items).Complete);
        var overlap = query with { StartAt = At.AddMinutes(-5), EndAt = At.AddMinutes(1) };
        Assert.False(Assert.Single((await reader.ReadAsync(overlap, default)).Items).Complete);
        await f.ExecuteAsync($"UPDATE structure_maintenance_windows SET slot_id=NULL, unit_id=NULL,enforcement_state='acknowledged' WHERE site_id='{f.SiteId}'");
        var closed = Assert.Single((await reader.ReadAsync(overlap, default)).Items);
        Assert.True(closed.Complete);
        Assert.All(closed.Slots, s => Assert.Equal("INELIGIBLE", s.Outcome));
    }

    [StructurePostgresFact]
    public async Task UnknownEvidenceAndBrokenAncestryNeverLookLikeEmptyCapacity()
    {
        await using var f = await OccupancyProjectionFixture.CreateAsync(PublicationSchema);
        var query = await Publish(f);
        var reader = new DiscoverySlotCandidateReader(f.DataSource);
        async Task Check(bool complete, string outcome)
        {
            var site = Assert.Single((await reader.ReadAsync(query, default)).Items);
            Assert.Equal(complete, site.Complete);
            Assert.Equal(outcome, Assert.Single(site.Slots).Outcome);
        }
        await f.ExecuteAsync($"UPDATE parking_slots SET physical_state='UNKNOWN' WHERE id='{f.SlotId}'");
        await Check(false, "DEPENDENCY_UNAVAILABLE");
        await f.ExecuteAsync($"UPDATE parking_slots SET physical_state='AVAILABLE', supported_vehicle_type=NULL WHERE id='{f.SlotId}'");
        await Check(false, "DEPENDENCY_UNAVAILABLE");
        await f.ExecuteAsync($"UPDATE parking_slots SET supported_vehicle_type='CAR' WHERE id='{f.SlotId}'; UPDATE spatial_units SET deleted_at=now() WHERE id='{f.UnitId}'");
        await Check(true, "INELIGIBLE");
        await f.ExecuteAsync($"UPDATE spatial_units SET deleted_at=NULL,parent_id=id WHERE id='{f.UnitId}'");
        await Check(false, "DEPENDENCY_UNAVAILABLE");
    }

    [StructurePostgresFact]
    public async Task RejectsStalePublicationConflictingTenantAndOversizedInventory()
    {
        await using var f = await OccupancyProjectionFixture.CreateAsync(PublicationSchema);
        var query = await Publish(f);
        var reader = new DiscoverySlotCandidateReader(f.DataSource);
        async Task Reject(ReadDiscoverySlotCandidatesQuery request, string code) =>
            Assert.Equal(code, (await Assert.ThrowsAsync<StructureException>(() => reader.ReadAsync(request, default))).Code);
        await Reject(query with { Sites = [new(f.SiteId, Guid.NewGuid())] }, "SEARCH_CHANGED");
        await f.ExecuteAsync($"UPDATE parking_slots SET tenant_id='{Guid.NewGuid()}' WHERE id='{f.SlotId}'");
        await Reject(query, "CATALOGUE_UNAVAILABLE");
        await f.ExecuteAsync($"""
            UPDATE parking_slots SET tenant_id='{f.TenantId}' WHERE id='{f.SlotId}';
            INSERT INTO parking_slots(tenant_id,site_id,spatial_unit_id,slot_code,supported_vehicle_type)
                SELECT '{f.TenantId}','{f.SiteId}','{f.UnitId}',i::text,'CAR' FROM generate_series(1,2048) i;
            """);
        await Reject(query, "CATALOGUE_UNAVAILABLE");
        await f.ExecuteAsync($"DELETE FROM facility_discovery_publications WHERE site_id='{f.SiteId}'");
        await Reject(query, "SEARCH_CHANGED");
    }

    [StructurePostgresFact]
    public async Task PrivateEndpointRequiresOperationsJwtWithOnlyTheDiscoveryPermissionNeeded()
    {
        await using var f = await OccupancyProjectionFixture.CreateAsync(PublicationSchema);
        var query = await Publish(f);
        using var rsa = RSA.Create(2048);
        await using var host = FacilityHost.Build([], builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Configuration["Services:Key"] = "candidate-endpoint-synthetic-service-key";
            builder.Configuration["ReservationIntegration:Enabled"] = "true";
            builder.Configuration["ReservationIntegration:OperationsPublicKeyPem"] = rsa.ExportRSAPublicKeyPem();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(FacilityHost).Assembly);
            builder.Host.ConfigureContainer<IServiceCollection>((_, services) =>
            {
                services.AddSingleton(f.DataSource);
                var worker = services.Single(s => s.ImplementationType == typeof(StructureSynchronizationWorker));
                services.Remove(worker);
            });
        });
        await host.StartAsync();
        using var http = host.GetTestClient();
        const string route = DiscoverySlotCandidatesController.RoutePath;
        using var anonymous = await http.PostAsJsonAsync(route, query);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        http.DefaultRequestHeaders.Add("X-Service-Key", "candidate-endpoint-synthetic-service-key");
        using var legacy = await http.PostAsJsonAsync(route, query);
        Assert.Equal(HttpStatusCode.Unauthorized, legacy.StatusCode);
        http.DefaultRequestHeaders.Remove("X-Service-Key");
        void Authorize(string permission)
        {
            var issuer = "SmartParking.OperationService";
            var jwt = new JwtSecurityToken(issuer, "SmartParking.FacilityService",
                [new Claim("sub", issuer), new Claim("sender", issuer), new Claim("permission", permission)],
                expires: DateTime.UtcNow.AddMinutes(1),
                signingCredentials: new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }
        Authorize("reservation:facility-dependency");
        using var wrong = await http.PostAsJsonAsync(route, query);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Authorize(ReservationServiceAuthentication.DiscoveryCandidates);
        using var reservation = await http.GetAsync($"/internal/reservation-dependencies/v1/slots/{f.SlotId}");
        Assert.Equal(HttpStatusCode.Forbidden, reservation.StatusCode);
        using var response = await http.PostAsJsonAsync(route, query);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(f.SlotId, json.RootElement.GetProperty("data").GetProperty("items")[0]
            .GetProperty("slots")[0].GetProperty("slotId").GetGuid());
        using var invalid = await http.PostAsJsonAsync(route, query with { VehicleType = "MOTORCYCLE" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var changed = await http.PostAsJsonAsync(route, query with { Sites = [new(f.SiteId, Guid.NewGuid())] });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public void RejectsDuplicateOversizedAndUnpairedRequests()
    {
        var site = new DiscoverySiteRequest(Guid.NewGuid(), Guid.NewGuid());
        var query = new ReadDiscoverySlotCandidatesQuery([site], "CAR", At, null, null);
        var validator = new ReadDiscoverySlotCandidatesValidator();
        Assert.True(validator.Validate(query).IsValid);
        Assert.False(validator.Validate(query with { Sites = [site, site] }).IsValid);
        Assert.False(validator.Validate(query with { Sites = [] }).IsValid);
        Assert.False(validator.Validate(query with { Sites = Enumerable.Range(0,101)
            .Select(_ => new DiscoverySiteRequest(Guid.NewGuid(), Guid.NewGuid())).ToArray() }).IsValid);
        Assert.False(validator.Validate(query with { StartAt = At }).IsValid);
        Assert.False(validator.Validate(query with { StartAt = At, EndAt = At }).IsValid);
    }
}
