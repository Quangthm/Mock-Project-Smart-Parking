using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Persistence;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.API;

public static class FacilityHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var key = builder.Configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Configure Services:Key (at least 32 characters).");
        builder.Services.AddDbContext<ParkingDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("Parking")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:Parking.")));
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Parking")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Parking.")));
        builder.Services.AddHttpContextAccessor();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddHttpClient<OwnerIdentityClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<IOwnerScopeReader>(s => s.GetRequiredService<OwnerIdentityClient>());
        builder.Services.AddScoped<IOwnerAuthorizer>(s => s.GetRequiredService<OwnerIdentityClient>());
        builder.Services.AddHttpClient<SlotOperatorClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<ISlotOperatorAuthorizer>(s => s.GetRequiredService<SlotOperatorClient>());
        builder.Services.AddHttpClient<LayoutCallerResolver>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<ILayoutCallerResolver>(s => s.GetRequiredService<LayoutCallerResolver>());
        builder.Services.AddHttpClient<IStructureCommitments, StructureCommitmentsClient>(c =>
        { c.BaseAddress = new(builder.Configuration["Services:Reservation"] ?? "http://localhost:5055/"); c.Timeout = TimeSpan.FromSeconds(10); c.DefaultRequestHeaders.Add("X-Service-Key", key); });
        builder.Services.AddScoped<IParkingStructureRepository, PostgresParkingStructureRepository>();
        builder.Services.AddScoped<IOperationalLayoutReader, PostgresOperationalLayoutReader>();
        builder.Services.AddScoped<ParkingStructureService>();
        builder.Services.AddScoped<IOwnerOnboardingStore, OwnerOnboardingStore>();
        builder.Services.AddScoped<OwnerOnboardingService>();
        builder.Services.AddScoped<IFacilityDirectoryStore, FacilityDirectoryStore>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<FacilityDirectoryService>();
        builder.Services.AddSingleton(_ => new ParkingGateConnectionPool(
            builder.Configuration.GetConnectionString("Parking")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:Parking."),
            builder.Configuration.GetValue("Services:ParkingGateLimit", 16)));
        builder.Services.AddScoped<IStructureSynchronizationStore, PostgresStructureSynchronizationStore>();
        builder.Services.AddScoped<StructureSynchronizationService>();
        builder.Services.AddHostedService<StructureSynchronizationWorker>();
        builder.Services.AddScoped<IStructureOperationStore, StructureOperationStore>();
        builder.Services.AddScoped<StructureOperationService>();
        builder.Services.AddHttpClient<IStructureImpactPlanner, StructureImpactPlanner>(c => { c.BaseAddress = new Uri(builder.Configuration["Services:Reservation"] ?? "http://localhost:5055/"); c.Timeout = TimeSpan.FromSeconds(15); c.DefaultRequestHeaders.Add("X-Service-Key", key); });
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443").AllowAnyHeader().AllowAnyMethod()));
        builder.Services.AddHttpClient<OperatorSiteClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<IOperatorSiteAuthorizer>(s => s.GetRequiredService<OperatorSiteClient>());
        builder.Services.AddScoped<OperatorLayoutService>();
        builder.Services.AddScoped<IOperatorOperationsStore, OperatorOperationsStore>();
        builder.Services.AddScoped<OperatorOperationsService>();
        builder.Services.AddScoped<IOperatorGateSlotStore, OperatorGateSlotStore>();
        builder.Services.AddScoped<OperatorGateSlotService>();
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false));
            });
        var app = builder.Build();
        app.UseCors();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path.StartsWithSegments("/internal") && !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()), Encoding.UTF8.GetBytes(key)))
            { context.Response.StatusCode = 401; return; }
            try { await next(context); }
            catch (PostgresException error) when (
                context.Request.Path.StartsWithSegments("/api/operator") &&
                (error.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn))
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { success = false, code = "PROTOTYPE_SCHEMA_NOT_READY", message = "Operator data is unavailable until database reconciliation is complete." });
            }
            catch (StructureException e)
            {
                context.Response.StatusCode = e.Code switch
                {
                    "UNAUTHENTICATED" => 401,
                    "FORBIDDEN" => 403,
                    "SITE_NOT_FOUND" or "UNIT_NOT_FOUND" or "SLOT_NOT_FOUND" => 404,
                    "IDENTITY_UNAVAILABLE" or "COMMITMENTS_UNAVAILABLE" => 503,
                    "STRUCTURE_BUSY" or "STRUCTURE_IN_USE" or "STRUCTURE_CONFLICT" or "DUPLICATE_IDENTIFIER" or "NO_AVAILABLE_SLOT" => 409,
                    _ => 400
                };
                await context.Response.WriteAsJsonAsync(new { success = false, code = e.Code, message = e.Message });
            }
        });
        app.MapControllers();
        return app;
    }
}
public sealed record GateSlotClaim(Guid TenantId, string VehicleType, Guid? PreferredSlotId = null, Guid? TargetUnitId = null, bool AllowReservedSlot = false);
public sealed record GateSlotRelease(Guid TenantId);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record BackupMarkBody(string Reason);
public sealed record OperationBody(Guid IdempotencyKey, StructureEdit Edit, Guid? TenantId = null);
public sealed record CreateSite(string Code, string Name, string Address, decimal? Latitude, decimal? Longitude, Guid? TenantId = null);
public sealed record UnitBody(UnitType Type, string Name, Guid? ParentId, int Capacity, Guid? TenantId = null);
public sealed record SlotBody(Guid UnitId, string Code, VehicleType VehicleType, SlotType Type = SlotType.STANDARD, Guid? TenantId = null);
public sealed record PathBody(string Code, Guid? From, Guid? To, string? MapData, Guid? TenantId = null);
public sealed record EditStructure(string Action, Guid ResourceId, Guid UnitId, string? Code, string? Name, string? Address, bool Active, int Capacity, VehicleType VehicleType, SlotType SlotType, string? Coordinates3D, string? Features, decimal? Latitude, decimal? Longitude, Guid? TenantId = null);
public partial class Program { public static void Main(string[] args) => FacilityHost.Build(args).Run(); }
