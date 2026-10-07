using System.Security.Cryptography;
using System.Text;
using Npgsql;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
using SmartParking.ParkingService.Persistence;
using SmartParking.ParkingService.Infrastructure;

namespace SmartParking.ParkingService.API;

public static class ParkingHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var key = builder.Configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Configure Services:Key (at least 32 characters).");
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Parking") ?? throw new InvalidOperationException("Configure ConnectionStrings:Parking.")));
        builder.Services.AddHttpContextAccessor();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)));
        builder.Services.AddHttpClient<OwnerIdentityClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<IOwnerAuthorizer>(s => s.GetRequiredService<OwnerIdentityClient>());
        builder.Services.AddHttpClient<IStructureCommitments, StructureCommitmentsClient>(c =>
        { c.BaseAddress = new(builder.Configuration["Services:Reservation"] ?? "http://localhost:5055/"); c.Timeout = TimeSpan.FromSeconds(10); c.DefaultRequestHeaders.Add("X-Service-Key", key); });
        builder.Services.AddScoped<IParkingStructureRepository, PostgresParkingStructureRepository>();
        builder.Services.AddScoped<ParkingStructureService>();
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173").AllowAnyHeader().AllowAnyMethod()));
        var app = builder.Build();
        app.UseCors();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path.StartsWithSegments("/internal") && !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()), Encoding.UTF8.GetBytes(key)))
            { context.Response.StatusCode = 401; return; }
            try { await next(context); }
            catch (StructureException e)
            {
                context.Response.StatusCode = e.Code switch { "UNAUTHENTICATED" => 401, "FORBIDDEN" => 403, "SITE_NOT_FOUND" or "UNIT_NOT_FOUND" or "SLOT_NOT_FOUND" => 404,
                    "IDENTITY_UNAVAILABLE" or "COMMITMENTS_UNAVAILABLE" => 503, "STRUCTURE_BUSY" or "STRUCTURE_IN_USE" or "STRUCTURE_CONFLICT" or "DUPLICATE_IDENTIFIER" => 409, _ => 400 };
                await context.Response.WriteAsJsonAsync(new { success = false, code = e.Code, message = e.Message });
            }
        });
        app.MapPut("/internal/tenants/{id:guid}", async (Guid id, TenantBody body, NpgsqlDataSource source, CancellationToken ct) =>
        {
            if (id == Guid.Empty || string.IsNullOrWhiteSpace(body.Name) || body.Name.Length>255 || string.IsNullOrWhiteSpace(body.Email) || body.Email.Length>255 || string.IsNullOrWhiteSpace(body.Phone) || body.Phone.Length>20) return Results.BadRequest();
            await using var db = await source.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand("INSERT INTO tenants(id,code,name,contact_email,contact_phone) VALUES ($1,$2,$3,$4,$5) ON CONFLICT(id) DO NOTHING", db);
            cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue("T-" + id.ToString("N")); cmd.Parameters.AddWithValue(body.Name); cmd.Parameters.AddWithValue(body.Email); cmd.Parameters.AddWithValue(body.Phone);
            await cmd.ExecuteNonQueryAsync(ct);
            await using var check = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM tenants WHERE id=$1 AND name=$2 AND contact_email=$3 AND contact_phone=$4 AND status='ACTIVE' AND deleted_at IS NULL)", db);
            check.Parameters.AddWithValue(id); check.Parameters.AddWithValue(body.Name); check.Parameters.AddWithValue(body.Email); check.Parameters.AddWithValue(body.Phone);
            return (bool)(await check.ExecuteScalarAsync(ct))! ? Results.NoContent() : Results.Conflict();
        });
        app.MapPost("/internal/sites/validate", async (SiteValidation body, NpgsqlDataSource source, CancellationToken ct) =>
        {
            if (body.SiteIds is null || body.TenantIds is null || body.SiteIds.Length>100 || body.TenantIds.Length>100) return Results.BadRequest();
            await using var db = await source.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT s.id,s.tenant_id FROM parking_sites s JOIN tenants t ON t.id=s.tenant_id WHERE s.id=ANY($1) AND s.tenant_id=ANY($2) AND s.status='ACTIVE' AND s.deleted_at IS NULL AND t.status='ACTIVE' AND t.deleted_at IS NULL ORDER BY s.id", db);
            cmd.Parameters.AddWithValue(body.SiteIds); cmd.Parameters.AddWithValue(body.TenantIds);
            var sites = new List<object>(); await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) sites.Add(new { id = reader.GetGuid(0), tenantId = reader.GetGuid(1) });
            return Results.Ok(sites);
        });
        async Task<OwnerScope> Scope(OwnerIdentityClient identity, Guid? tenant, CancellationToken ct)
        {
            var scope = await identity.ScopeAsync(ct);
            var selected = tenant ?? (scope.TenantIds.Length == 1 ? scope.TenantIds[0] : Guid.Empty);
            if (!scope.TenantIds.Contains(selected)) throw new StructureException("FORBIDDEN", "Select an authorized tenant.");
            return new(scope.UserId, selected);
        }
        app.MapGet("/api/parking-lots", async (Guid? tenantId, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success=true, data=await service.ListAsync(await Scope(identity, tenantId, ct), ct) }));
        app.MapPost("/api/parking-lots", async (CreateSite body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) =>
        {
            var site = await service.CreateAsync(await Scope(identity, body.TenantId, ct), body.Code, body.Name, body.Address, body.Latitude, body.Longitude, ct);
            return Results.Created($"/api/parking-lots/{site.Id}/structure", new { success=true, data=site });
        });
        app.MapGet("/api/parking-lots/{id:guid}/structure", async (Guid id, Guid? tenantId, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success=true, data=await service.GetAsync(await Scope(identity, tenantId, ct), id, ct) }));
        app.MapPost("/api/parking-lots/{id:guid}/units", async (Guid id, UnitBody body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success=true, data=await service.AddUnitAsync(await Scope(identity, body.TenantId, ct), id, body.Type, body.Name, body.ParentId, body.Capacity, ct) }));
        app.MapPost("/api/parking-lots/{id:guid}/slots", async (Guid id, SlotBody body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success=true, data=await service.AddSlotAsync(await Scope(identity, body.TenantId, ct), id, body.UnitId, body.Code, body.VehicleType, body.Type, ct) }));
        app.MapPatch("/api/parking-lots/{id:guid}/structure", async (Guid id, EditStructure body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) =>
        {
            await service.ChangeAsync(await Scope(identity, body.TenantId, ct), id, s =>
            {
                switch(body.Action)
                {
                    case "profile": s.UpdateProfile(body.Code!,body.Name!,body.Address!,body.Latitude,body.Longitude); break;
                    case "active": s.SetActive(body.Active); break;
                    case "renameUnit": s.RenameUnit(body.ResourceId,body.Name!); break;
                    case "capacity": s.SetUnitCapacity(body.ResourceId,body.Capacity); break;
                    case "removeUnit": s.RemoveUnit(body.ResourceId); break;
                    case "removeSlot": s.RemoveSlot(body.ResourceId); break;
                    case "moveSlot": s.MoveSlot(body.ResourceId,body.UnitId,body.Code!); break;
                    case "configureSlot": s.ConfigureSlot(body.ResourceId,body.VehicleType,body.SlotType,body.Coordinates3D,body.Features); break;
                    case "removePath": s.RemoveAccessPath(body.ResourceId); break;
                    default: throw new StructureException("INVALID_STRUCTURE","Unsupported structure action.");
                }
            },ct);
            return Results.Ok(new { success=true });
        });
        app.MapPost("/api/parking-lots/{id:guid}/paths", async (Guid id, PathBody body, OwnerIdentityClient identity, ParkingStructureService service, CancellationToken ct) => Results.Ok(new { success=true, data=await service.AddAccessPathAsync(await Scope(identity,body.TenantId,ct),id,body.Code,body.From,body.To,body.MapData,ct) }));
        return app;
    }
}
public sealed record TenantBody(string Name, string Email, string Phone);
public sealed record SiteValidation(Guid[] SiteIds, Guid[] TenantIds);
public sealed record CreateSite(string Code, string Name, string Address, decimal? Latitude, decimal? Longitude, Guid? TenantId=null);
public sealed record UnitBody(UnitType Type,string Name,Guid? ParentId,int Capacity,Guid? TenantId=null);
public sealed record SlotBody(Guid UnitId,string Code,VehicleType VehicleType,SlotType Type=SlotType.STANDARD,Guid? TenantId=null);
public sealed record PathBody(string Code,Guid? From,Guid? To,string? MapData,Guid? TenantId=null);
public sealed record EditStructure(string Action,Guid ResourceId,Guid UnitId,string? Code,string? Name,string? Address,bool Active,int Capacity,VehicleType VehicleType,SlotType SlotType,string? Coordinates3D,string? Features,decimal? Latitude,decimal? Longitude,Guid? TenantId=null);
public static class Program { public static void Main(string[] args) => ParkingHost.Build(args).Run(); }
