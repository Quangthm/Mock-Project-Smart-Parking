using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.ReservationService.Persistence;
using SmartParking.ReservationService.Application;
using SmartParking.ReservationService.Domain;

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
        builder.Services.TryAddScoped<IStructureSafetyStore, StructureSafetyStore>();
        builder.Services.AddScoped<StructureSafetyService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient<OperatorIdentityClient>(client =>
        {
            client.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddScoped<ReservationGateStore>();
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443")
            .AllowAnyHeader()
            .AllowAnyMethod()));
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.UseCors();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Path.StartsWithSegments("/internal") && !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()), Encoding.UTF8.GetBytes(key)))
            { context.Response.StatusCode = 401; return; }
            try { await next(context); }
            catch (PostgresException error) when (
                (context.Request.Path.StartsWithSegments("/api/reservations") || context.Request.Path.StartsWithSegments("/api/parking-sessions")) &&
                (error.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn))
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { success = false, code = "PROTOTYPE_SCHEMA_NOT_READY", message = "Reservation data is unavailable until database reconciliation is complete." });
            }
            catch (InvalidStructureEditException error)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { code = "INVALID_STRUCTURE", message = error.Message });
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
            catch (HoldConflictException) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { code = "STRUCTURE_BUSY" }); }
        });

        app.MapPut("/internal/structure/{siteId:guid}/{token:guid}",
            async (Guid siteId, Guid token, HoldRequest body, StructureSafetyService service, CancellationToken ct) =>
                Results.Ok(await service.PrepareAsync(body.TenantId, siteId, token, ct)));

        app.MapPost("/internal/structure/{siteId:guid}/impact",
            async (Guid siteId, HoldRequest body, StructureSafetyService service, CancellationToken ct) =>
                Results.Ok(await service.ImpactAsync(body.TenantId, siteId, ct)));

        app.MapPost("/internal/structure/{siteId:guid}/{token:guid}/release",
            async (Guid siteId, Guid token, ReleaseRequest body, StructureSafetyService service, CancellationToken ct) =>
            {
                await service.CompleteAsync(body.TenantId, siteId, token, body.Outcome, ct);
                return Results.NoContent();
            });
        app.MapPost("/internal/structure/{siteId:guid}/snapshot", async (Guid siteId, HoldRequest body, StructureSafetyService service, CancellationToken ct) =>
            Results.Ok(await service.ReadSnapshotAsync(body.TenantId, siteId, ct)));
        app.MapGet("/api/reservations", async (
            Guid siteId,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            CancellationToken ct) =>
        {
            var scope = await identity.RequireSiteAsync(siteId, ct);
            return Results.Ok(new { success = true, data = await store.ListAsync(scope.TenantId, siteId, ct) });
        });
        app.MapGet("/api/parking-sessions", async (
            Guid siteId,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            CancellationToken ct) =>
        {
            var scope = await identity.RequireSiteAsync(siteId, ct);
            return Results.Ok(new { success = true, data = await store.ListSessionsAsync(scope.TenantId, siteId, ct) });
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
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.PlateNumber) || body.PlateNumber.Length > 50)
                return Results.BadRequest(new { success = false, code = "INVALID_PLATE", message = "Enter a valid vehicle plate." });
            if (!IsValidImageData(body.ImageData))
                return Results.BadRequest(new { success = false, code = "INVALID_IMAGE", message = "Gate image must be a JPEG, PNG, or WebP data URL no larger than 14 MB." });
            var vehicleType = NormalizeVehicleType(body.VehicleType);
            if (vehicleType is null)
                return Results.BadRequest(new { success = false, code = "INVALID_VEHICLE_TYPE", message = "Vehicle type must be CAR or MOTORCYCLE." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var sessionId = Guid.NewGuid();
            var session = await store.CreateWalkInAsync(
                scope.TenantId,
                siteId,
                sessionId,
                vehicleType,
                body.PlateNumber,
                body.ImageData,
                ct);
            return Results.Ok(new { success = true, data = session });
        });
        app.MapPost("/api/parking-sessions/{sessionId:guid}/walk-in/check-out", async (
            Guid sessionId,
            Guid siteId,
            WalkInGateActionRequest body,
            OperatorIdentityClient identity,
            ReservationGateStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.PlateNumber) || body.PlateNumber.Length > 50)
                return Results.BadRequest(new { success = false, code = "INVALID_PLATE", message = "Enter a valid vehicle plate." });
            if (!IsValidImageData(body.ImageData))
                return Results.BadRequest(new { success = false, code = "INVALID_IMAGE", message = "Gate image must be a JPEG, PNG, or WebP data URL no larger than 14 MB." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var session = await store.CheckOutWalkInAsync(scope.TenantId, siteId, sessionId, body.PlateNumber, body.ImageData, ct);
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
            if (!IsValidImageData(body.ImageData))
                return Results.BadRequest(new { success = false, code = "INVALID_IMAGE", message = "Gate image must be a JPEG, PNG, or WebP data URL no larger than 14 MB." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var entry = await store.CheckInAsync(scope.TenantId, siteId, scope.UserId, reservationId, body.PlateNumber, body.ImageData, ct);
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
            if (!IsValidImageData(body.ImageData))
                return Results.BadRequest(new { success = false, code = "INVALID_IMAGE", message = "Gate image must be a JPEG, PNG, or WebP data URL no larger than 14 MB." });
            var scope = await identity.RequireSiteAsync(siteId, ct);
            var entry = await store.CheckOutAsync(scope.TenantId, siteId, scope.UserId, reservationId, body.PlateNumber, body.ImageData, ct);
            return Results.Ok(new { success = true, data = entry });
        });
        return app;
    }
    private static string? NormalizeVehicleType(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            null or "" => "CAR",
            "MOTORCYCLE" or "MOTORBIKE" => "MOTORCYCLE",
            "CAR" => "CAR",
            _ => null
        };

    private static bool IsValidImageData(string? imageData) =>
        imageData is null ||
        imageData.Length <= 14 * 1024 * 1024 &&
        (imageData.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
         imageData.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
         imageData.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase));
}
public sealed record HoldRequest(Guid TenantId);
public sealed record ReleaseRequest(Guid TenantId, EditOutcome? Outcome);
public sealed record GateActionRequest(string? PlateNumber, string? ImageData = null);
public sealed record WalkInGateActionRequest(string PlateNumber, string? ImageData = null, string? VehicleType = null);
public static class Program { public static void Main(string[] args) => ReservationHost.Build(args).Run(); }
