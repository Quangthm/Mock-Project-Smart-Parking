using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.DataProtection;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;
using UserService.Persistence;
using UserService.Persistence.Repositories;
namespace SmartParking.UserService.Tests;
internal sealed class TestWorkflowProtector:IWorkflowProtector
{
    private static readonly IDataProtector Protector=new EphemeralDataProtectionProvider().CreateProtector("test-workflow");
    public string Protect(string value)=>Protector.Protect(value);
    public string Unprotect(string value)=>Protector.Unprotect(value);
}
internal static class WorkflowTestSupport
{
    public static AccountWorkflowService Create(AppDbContext db,TimeProvider clock)=>new(db,new BcryptPasswordService(),new TestWorkflowProtector(),clock);
}

internal static class ServiceSchema
{
    public static string Root
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("SMARTPARK_SCHEMA_ROOT");
            if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName,"SmartParking.slnx"))) dir=dir.Parent;
            var root = dir?.FullName ?? throw new InvalidOperationException("Repository root missing.");
            if (!File.Exists(Path.Combine(root,"scripts/database/microservices/01-user-service-db.sql")))
                throw new InvalidOperationException("Set SMARTPARK_SCHEMA_ROOT to the arch/database-schema checkout before running database tests.");
            return root;
        }
    }
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        foreach(var path in new[] {"microservices/01-user-service-db.sql", "integration/01-user-service-alignment.sql", "integration/01-user-service-alignment.sql"})
        {
            await using var cmd=new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(Root,"scripts/database",path)),(NpgsqlConnection)db.Database.GetDbConnection());
            await cmd.ExecuteNonQueryAsync();
        }
        await db.Database.CloseConnectionAsync();
        await ApplyFeaturesAsync(db);
    }
    public static string FeaturePath(string file)
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null && !File.Exists(Path.Combine(dir.FullName,"SmartParking.slnx")))dir=dir.Parent;
        var featurePath = dir is null ? null : Path.Combine(dir.FullName,"scripts/database",file);
        return featurePath is not null && File.Exists(featurePath) ? featurePath : Path.Combine(Root,"scripts/database",file);
    }
    public static async Task ApplyFeaturesAsync(AppDbContext db)
    {
        var sql=await File.ReadAllTextAsync(FeaturePath("05.8-Account-Workflow-Vehicles.sql"));
        await db.Database.OpenConnectionAsync();
        await using var cmd=new NpgsqlCommand(sql,(NpgsqlConnection)db.Database.GetDbConnection());await cmd.ExecuteNonQueryAsync();
        await db.Database.CloseConnectionAsync();
    }
}
internal sealed class TestParkingDirectory : IParkingDirectory
{
    public bool Unavailable { get; set; }
    public Dictionary<Guid,Guid> Sites { get; } = [];
    public HashSet<Guid> InactiveSites { get; } = [];
    public HashSet<Guid> InactiveTenants { get; } = [];
    public HashSet<Guid> Provisioned { get; } = [];
    public Task ProvisionTenantAsync(Guid id,string name,string email,string phone,CancellationToken ct)
    {
        if(Unavailable) throw new global::UserService.Application.Common.Models.Exceptions.AuthException("PARKING_UNAVAILABLE","Unavailable",503);
        lock(Provisioned) Provisioned.Add(id);
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<DirectorySite>> ActiveSitesAsync(Guid[] siteIds,Guid[] tenantIds,CancellationToken ct)
    {
        if(Unavailable) throw new global::UserService.Application.Common.Models.Exceptions.AuthException("PARKING_UNAVAILABLE","Unavailable",503);
        return Task.FromResult<IReadOnlyList<DirectorySite>>(Sites.Where(s=>siteIds.Contains(s.Key)&&tenantIds.Contains(s.Value)&&!InactiveSites.Contains(s.Key)&&!InactiveTenants.Contains(s.Value)).Select(s=>new DirectorySite(s.Key,s.Value)).ToArray());
    }
}
