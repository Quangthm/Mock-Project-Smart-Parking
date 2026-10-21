using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.API.Hubs;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class IdentityMessagingTests
{
    private sealed class RabbitPostgresFactAttribute : FactAttribute
    {
        public RabbitPostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_RABBIT_TEST_PORT")))
                Skip="Requires isolated local PostgreSQL and RabbitMQ fixtures.";
        }
    }
    [RabbitPostgresFact]
    public async Task CommittedOutboxReachesRabbitInboxAndAuthorizedSignalRConnection()
    {
        await using var fixture=await UserServiceDatabaseFixture.CreateAsync();
        var tenant=Guid.NewGuid();
        await fixture.SeedTenantsAsync(tenant);
        var owner=new User {Id=Guid.NewGuid(),Email="owner-live@example.com",EmailVerifiedAt=DateTimeOffset.UtcNow,
            FullName="Live Owner",Status=UserStatus.Active,PasswordHash=new BcryptPasswordService().Hash("Password@123")};
        var account=new Account {Id=Guid.NewGuid(),User=owner,UserId=owner.Id.Value,TenantId=tenant,Status="ACTIVE",Permissions=["OPERATOR_MANAGE"]};
        account.AccountRoles.Add(new(){Account=account,AccountId=account.Id.Value,RoleCode="BUSINESS_OWNER"});owner.Accounts.Add(account);
        await using(var db=fixture.Open()){db.Users.Add(owner);await db.SaveChangesAsync();}
        await using var app=await fixture.StartHostAsync(configure:builder=>
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Messaging:Host"]="127.0.0.1",["Messaging:Port"]=Environment.GetEnvironmentVariable("SMARTPARK_RABBIT_TEST_PORT"),
                ["Messaging:Username"]="identity_test",["Messaging:Password"]="local-disposable-identity-test"
            });
            builder.Services.AddAccessMessaging(builder.Configuration);
        });
        using var client=app.GetTestClient();
        var login=await client.PostAsJsonAsync("/api/auth/login",new{email=owner.Email,password="Password@123"});
        login.EnsureSuccessStatusCode();
        var token=(await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString()!;
        var wsClient=app.GetTestServer().CreateWebSocketClient();
        wsClient.ConfigureRequest=request=>request.Headers.Authorization="Bearer "+token;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var socket=await wsClient.ConnectAsync(new Uri("ws://localhost/hubs/operators"),timeout.Token);
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e")),WebSocketMessageType.Text,true,timeout.Token);
        Assert.Contains("{}",await Receive(socket,timeout.Token));
        var connections=app.Services.GetRequiredService<AccessConnections>();
        while(connections.Items.Count==0) await Task.Delay(20,timeout.Token);
        var message=new OperatorAccessChanged(Guid.NewGuid(),1,Guid.NewGuid(),1,DateTimeOffset.UtcNow,Guid.NewGuid(),owner.Id.Value,[tenant],"pending_verification");
        await using(var scope=app.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<global::UserService.Persistence.AppDbContext>();
            await using var tx=await db.Database.BeginTransactionAsync(timeout.Token);
            scope.ServiceProvider.GetRequiredService<IAccessEventStore>().Stage(message);
            await db.SaveChangesAsync(timeout.Token);await tx.CommitAsync(timeout.Token);
        }
        string received;
        do { received=await Receive(socket,timeout.Token); } while(!received.Contains("OperatorsChanged"));
        Assert.DoesNotContain(owner.Email,received);
        await using(var db=fixture.Open())
        {
            var version=await db.Database.SqlQuery<long>($"SELECT version AS \"Value\" FROM access_projection WHERE user_id={message.AggregateId}").SingleAsync(timeout.Token);
            Assert.Equal(1,version);
            await db.Accounts.Where(a=>a.UserId==owner.Id).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Permissions,Array.Empty<string>()),timeout.Token);
        }
        using(var scope=app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAccessChangeNotifier>().NotifyAsync(message,timeout.Token);
        Assert.Empty(connections.Items);
    }
    private static async Task<string> Receive(WebSocket socket,CancellationToken ct)
    {
        var buffer=new byte[8192];var result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),ct);
        return Encoding.UTF8.GetString(buffer,0,result.Count);
    }
}
