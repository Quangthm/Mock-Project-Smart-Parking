using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartParking.ParkingService.API;
using SmartParking.ParkingService.Infrastructure;
using SmartParking.ParkingService.Application;
using SmartParking.ReservationService.API;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.API;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

public sealed class DatabaseIntegrationTests
{
    [PostgresFact]
    public async Task RealHostsSeparateDatabasesOwnerOnboardingOperatorAndRevocation()
    {
        var input=Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")!;
        var names=Enumerable.Range(0,3).Select(_=>"smartpark_auth_test_"+Guid.NewGuid().ToString("N")).ToArray();
        await using var admin=new NpgsqlConnection(input);await admin.OpenAsync();
        foreach(var name in names) await new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin).ExecuteNonQueryAsync();
        var connections=names.Select(name=>new NpgsqlConnectionStringBuilder(input){Database=name}.ConnectionString).ToArray();
        try
        {
            var files=new[]{("microservices/01-user-service-db.sql","integration/01-user-service-alignment.sql"),("microservices/02-parking-service-db.sql","integration/02-parking-service-alignment.sql"),("microservices/03-reservation-session-service-db.sql","integration/03-reservation-structure-safety.sql")};
            for(var i=0;i<3;i++)
            {
                await using var db=new NpgsqlConnection(connections[i]);await db.OpenAsync();
                foreach(var file in new[]{files[i].Item1,files[i].Item2}) await new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(ServiceSchema.Root,"scripts/database",file)),db).ExecuteNonQueryAsync();
            }
            const string key="integration-test-service-key-32-characters";
            void Config(WebApplicationBuilder b,string name,string connection)
            {
                b.Logging.ClearProviders();b.WebHost.UseTestServer();
                b.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{[$"ConnectionStrings:{name}"]=connection,["Services:Key"]=key,["Services:User"]="http://user.test/",["Services:Parking"]="http://parking.test/",["Services:Reservation"]="http://reservation.test/"});
            }
            await using var reservations=ReservationHost.Build([],b=>Config(b,"Reservation",connections[2]));await reservations.StartAsync();
            WebApplication? userHost=null;
            await using var parking=ParkingHost.Build([],b=>
            {
                Config(b,"Parking",connections[1]);
                b.Services.AddHttpClient<OwnerIdentityClient>().ConfigurePrimaryHttpMessageHandler(()=>userHost!.GetTestServer().CreateHandler());
                b.Services.AddHttpClient<IStructureCommitments,StructureCommitmentsClient>().ConfigurePrimaryHttpMessageHandler(()=>reservations.GetTestServer().CreateHandler());
            });await parking.StartAsync();
            await using var users=await UserHost.BuildAsync([],b=>
            {
                Config(b,"User",connections[0]);b.Environment.EnvironmentName="Development";
                b.Services.AddHttpClient<IParkingDirectory,ParkingDirectoryClient>().ConfigurePrimaryHttpMessageHandler(()=>parking.GetTestServer().CreateHandler());
            });userHost=users;await users.StartAsync();
            await using(var scope=users.Services.CreateAsyncScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var bootstrap=scope.ServiceProvider.GetRequiredService<DataSeeder>();
                await Assert.ThrowsAsync<InvalidOperationException>(()=>bootstrap.SeedAdminAsync("admin@integration.test","weak"));
                await bootstrap.SeedAdminAsync("admin@integration.test","Password@123");
                await bootstrap.SeedAdminAsync("admin@integration.test","Password@123");
                Assert.Equal(1,await db.Users.CountAsync(u=>u.Email=="admin@integration.test"));
            }
            using var adminClient=users.GetTestClient();using var owner=users.GetTestClient();using var lots=parking.GetTestClient();using var staff=users.GetTestClient();
            async Task<JsonElement> Data(HttpResponseMessage r,HttpStatusCode expected=HttpStatusCode.OK)
            { var body=await r.Content.ReadAsStringAsync();Assert.True(r.StatusCode==expected,$"Expected {expected}; received {r.StatusCode}: {body}");return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone(); }
            async Task<string> Login(HttpClient client,string email,string password="Password@123")
            { var result=await Data(await client.PostAsJsonAsync("/api/auth/login",new{email,password}));var token=result.GetProperty("accessToken").GetString()!;client.DefaultRequestHeaders.Authorization=new("Bearer",token);Assert.Equal(86400,result.GetProperty("expiresIn").GetInt32());return token; }
            var registration=await Data(await owner.PostAsJsonAsync("/api/auth/register/owner",new{fullName="Owner",businessName="Integrated Parking",email="owner@integration.test",phone="0912345678",password="Password@123",lotType="outdoor",agreedToPolicy=true}),HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Unauthorized,(await owner.PostAsJsonAsync("/api/auth/login",new{email="owner@integration.test",password="Password@123"})).StatusCode);
            await Login(adminClient,"admin@integration.test");
            await Data(await adminClient.PatchAsJsonAsync($"/api/owner-applications/{registration.GetProperty("id").GetGuid()}/review",new{status="approved"}));
            var ownerToken=await Login(owner,"owner@integration.test");lots.DefaultRequestHeaders.Authorization=new("Bearer",ownerToken);
            Assert.Equal(HttpStatusCode.Unauthorized,(await parking.GetTestClient().PutAsJsonAsync($"/internal/tenants/{Guid.NewGuid()}",new{name="Forged",email="x",phone="x"})).StatusCode);
            var site=await Data(await lots.PostAsJsonAsync("/api/parking-lots",new{code="S1",name="First site",address="Address"}),HttpStatusCode.Created);var siteId=site.GetProperty("id").GetGuid();
            var unit=await Data(await lots.PostAsJsonAsync($"/api/parking-lots/{siteId}/units",new{type="ZONE",name="Outdoor",capacity=10}));
            await Data(await lots.PostAsJsonAsync($"/api/parking-lots/{siteId}/slots",new{unitId=unit.GetGuid(),code="M1",vehicleType="MOTORCYCLE",type="STANDARD"}));
            var layout=await Data(await lots.GetAsync($"/api/parking-lots/{siteId}/structure"));Assert.Equal(1,layout.GetProperty("slots").GetArrayLength());
            var created=await Data(await owner.PostAsJsonAsync("/api/users",new{fullName="Staff",email="staff@integration.test",password="Password@123",siteIds=new[]{siteId},permissions=new[]{"DEVICE_STATUS_VIEW"}}),HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsJsonAsync("/api/users",new{fullName="Foreign Staff",email="foreign@integration.test",password="Password@123",siteIds=new[]{Guid.NewGuid()},permissions=new[]{"DEVICE_STATUS_VIEW"}})).StatusCode);
            await Login(staff,"staff@integration.test");Assert.Equal(HttpStatusCode.OK,(await staff.GetAsync("/api/auth/me")).StatusCode);
            var assigned=await Data(await staff.GetAsync("/api/operators/me"));Assert.Equal(siteId,assigned[0].GetProperty("siteId").GetGuid());
            Assert.Equal("DEVICE_STATUS_VIEW",assigned[0].GetProperty("permissions")[0].GetString());
            Assert.Single((await Data(await owner.GetAsync("/api/operators"))).EnumerateArray());
            await using(var scope=users.Services.CreateAsyncScope())
            {
                var rights=scope.ServiceProvider.GetRequiredService<IOperatorProvisioningService>();
                Assert.True(await rights.HasPermissionAsync(created.GetProperty("id").GetGuid(),siteId,"DEVICE_STATUS_VIEW",default));
                Assert.False(await rights.HasPermissionAsync(created.GetProperty("id").GetGuid(),siteId,"CASH_COLLECT",default));
            }
            await Data(await adminClient.PatchAsJsonAsync($"/api/users/{created.GetProperty("id").GetGuid()}/status",new{status="locked"}));
            Assert.Equal(HttpStatusCode.Unauthorized,(await staff.GetAsync("/api/auth/me")).StatusCode);
            await Data(await adminClient.PatchAsJsonAsync($"/api/users/{created.GetProperty("id").GetGuid()}/status",new{status="active"}));
            Assert.Equal(HttpStatusCode.Unauthorized,(await staff.GetAsync("/api/auth/me")).StatusCode);await Login(staff,"staff@integration.test");
            await Data(await adminClient.PatchAsJsonAsync($"/api/users/{registration.GetProperty("ownerId").GetGuid()}/status",new{status="locked"}));
            Assert.Equal(HttpStatusCode.Unauthorized,(await lots.GetAsync("/api/parking-lots")).StatusCode);
            for(var i=0;i<2;i++)
            {
                await using var db=new NpgsqlConnection(connections[i]);await db.OpenAsync();
                await using var check=new NpgsqlCommand(i==0?"SELECT to_regclass('parking_sites') IS NULL":"SELECT to_regclass('users') IS NULL",db);Assert.True((bool)(await check.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            foreach(var name in names) await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)",admin).ExecuteNonQueryAsync();
        }
    }
}
