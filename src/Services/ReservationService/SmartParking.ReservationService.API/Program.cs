using System.Security.Cryptography;
using System.Text;
using Npgsql;
using SmartParking.ReservationService.Persistence;

namespace SmartParking.ReservationService.API;

public static class ReservationHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var key = builder.Configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Configure Services:Key (at least 32 characters).");
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Reservation") ?? throw new InvalidOperationException("Configure ConnectionStrings:Reservation.")));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient<OperatorIdentityClient>(client =>
        {
            client.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddHttpClient<IOperatorGateSlotClaims, ParkingGateSlotClient>(client =>
        {
            client.BaseAddress = new(builder.Configuration["Services:Parking"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Service-Key", key);
        });
        builder.Services.AddScoped<StructureSafetyStore>();
        builder.Services.AddScoped<ReservationGateStore>();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443")
            .AllowAnyHeader()
            .AllowAnyMethod()));
        var app = builder.Build();
        app.UseCors();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path.StartsWithSegments("/internal") &&
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()),
                    Encoding.UTF8.GetBytes(key)))
            {
                context.Response.StatusCode = 401;
                return;
            }

            try
            {
                await next(context);
            }
            catch (ReservationGateException error)
            {
                context.Response.StatusCode = error.StatusCode;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = error.Code,
                    message = error.Message
                });
            }
            catch (HoldConflictException)
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new { code = "STRUCTURE_BUSY" });
            }
        });
        app.MapPut("/internal/structure/{siteId:guid}/{token:guid}", async (Guid siteId, Guid token, HoldRequest body, StructureSafetyStore store, CancellationToken ct) =>
            body.TenantId == Guid.Empty || siteId == Guid.Empty || token == Guid.Empty ? Results.BadRequest() : Results.Ok(await store.AcquireAsync(body.TenantId, siteId, token, ct)));
        app.MapPost("/internal/structure/{siteId:guid}/snapshot", async (Guid siteId, HoldRequest body, StructureSafetyStore store, CancellationToken ct) =>
            body.TenantId == Guid.Empty || siteId == Guid.Empty ? Results.BadRequest() : Results.Ok(await store.ReadSnapshotAsync(body.TenantId, siteId, ct)));
        app.MapPost("/internal/structure/{siteId:guid}/impact",async(Guid siteId,HoldRequest body,StructureSafetyStore store,CancellationToken ct)=>body.TenantId==Guid.Empty || siteId==Guid.Empty?Results.BadRequest():Results.Ok(await store.ImpactAsync(body.TenantId,siteId,ct)));
        app.MapPost("/internal/structure/{siteId:guid}/{token:guid}/release", async (Guid siteId, Guid token, ReleaseRequest body, StructureSafetyStore store, CancellationToken ct) =>
        {
            if (body.TenantId == Guid.Empty || siteId == Guid.Empty || token == Guid.Empty ||
                body.Outcome is { RemovedSlots: null } or { RemovedUnits: null } or { Capacities: null } ||
                body.Outcome?.Capacities.Any(c => c.Capacity < 0 || c.Backup<0 || c.Backup>c.Capacity || c.VehicleType is not ("CAR" or "MOTORCYCLE")) == true) return Results.BadRequest();
            await store.ReleaseAsync(body.TenantId, siteId, token, body.Outcome, ct); return Results.NoContent();
        });
        app.MapGet("/api/reservations", async (
            Guid siteId,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            CancellationToken ct) =>
        {
            var scope = await identity.RequireSiteAsync(siteId, ct);
            return Results.Ok(new { success = true, data = await store.ListAsync(scope.TenantId, siteId, ct) });
        });
        app.MapGet("/api/reservations/lookup", async (
            Guid siteId,
            string query,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length > 100)
                return Results.BadRequest(new { success = false, code = "INVALID_QUERY", message = "Enter a reservation ID or plate." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var entry = await store.FindAsync(scope.TenantId, siteId, query.Trim(), ct);
            return entry is null
                ? Results.NotFound(new { success = false, code = "RESERVATION_NOT_FOUND", message = "No matching reservation was found." })
                : Results.Ok(new { success = true, data = entry });
        });
        app.MapGet("/api/parking-sessions/walk-in/lookup", async (
            Guid siteId,
            string plate,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(plate) || plate.Length > 50)
                return Results.BadRequest(new { success = false, code = "INVALID_PLATE", message = "Enter a valid vehicle plate." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var session = await store.FindActiveWalkInAsync(scope.TenantId, siteId, plate, ct);
            return session is null
                ? Results.NotFound(new { success = false, code = "WALK_IN_SESSION_NOT_FOUND", message = "No active walk-in session was found for this plate." })
                : Results.Ok(new { success = true, data = session });
        });
        app.MapPost("/api/parking-sessions/walk-in/check-in", async (
            Guid siteId,
            WalkInGateActionRequest body,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            IOperatorGateSlotClaims slots,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.PlateNumber) || body.PlateNumber.Length > 50)
                return Results.BadRequest(new { success = false, code = "INVALID_PLATE", message = "Enter a valid vehicle plate." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var sessionId = Guid.NewGuid();
            await using var claim = await slots.ClaimAvailableAsync(
                scope.TenantId, siteId, "ANY", null, null, false, ct);
            var session = await store.CreateWalkInAsync(
                scope.TenantId,
                siteId,
                sessionId,
                claim.Slot.Id,
                claim.Slot.Code,
                claim.Slot.VehicleType,
                body.PlateNumber,
                ct);
            claim.Commit();
            return Results.Ok(new { success = true, data = session });
        });
        app.MapPost("/api/parking-sessions/{sessionId:guid}/walk-in/check-out", async (
            Guid sessionId,
            Guid siteId,
            WalkInGateActionRequest body,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            IOperatorGateSlotClaims slots,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.PlateNumber) || body.PlateNumber.Length > 50)
                return Results.BadRequest(new { success = false, code = "INVALID_PLATE", message = "Enter a valid vehicle plate." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var session = await store.CheckOutWalkInAsync(scope.TenantId, siteId, sessionId, body.PlateNumber, ct);
            await slots.ReleaseAsync(scope.TenantId, siteId, session.SlotId, ct);
            return Results.Ok(new { success = true, data = session });
        });
        app.MapPost("/api/reservations/{reservationId:guid}/check-in", async (
            Guid reservationId,
            GateActionRequest body,
            Guid siteId,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            CancellationToken ct) =>
        {
            if (body.PlateNumber is { Length: > 50 })
                return Results.BadRequest(new { success = false, code = "INVALID_PLATE", message = "Plate number is too long." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var entry = await store.CheckInAsync(scope.TenantId, siteId, scope.UserId, reservationId, body.PlateNumber, ct);
            return Results.Ok(new { success = true, data = entry });
        });
        app.MapPost("/api/reservations/{reservationId:guid}/check-out", async (
            Guid reservationId,
            GateActionRequest body,
            Guid siteId,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            CancellationToken ct) =>
        {
            if (body.PlateNumber is { Length: > 50 })
                return Results.BadRequest(new { success = false, code = "INVALID_PLATE", message = "Plate number is too long." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var entry = await store.CheckOutAsync(scope.TenantId, siteId, scope.UserId, reservationId, body.PlateNumber, ct);
            return Results.Ok(new { success = true, data = entry });
        });
        return app;
    }
}
public sealed record HoldRequest(Guid TenantId);
public sealed record ReleaseRequest(Guid TenantId, EditOutcome? Outcome);
public sealed record GateActionRequest(string? PlateNumber);
public sealed record WalkInGateActionRequest(string PlateNumber);
public static class Program { public static void Main(string[] args) => ReservationHost.Build(args).Run(); }
