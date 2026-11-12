using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.FacilityService.API.Middleware;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Persistence;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Application.Reservations;

namespace SmartParking.FacilityService.API;

public static class FacilityHost
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        builder.AddDiscoveryCatalogue();
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
        builder.Services.AddScoped<IOwnerOnboardingStore, OwnerOnboardingStore>();
        builder.Services.AddScoped<IFacilityDirectoryStore, FacilityDirectoryStore>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IReservationDependencyRepository, ReservationDependencyRepository>();
        builder.Services.AddSingleton(_ => new ParkingGateConnectionPool(
            builder.Configuration.GetConnectionString("Parking")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:Parking."),
            builder.Configuration.GetValue("Services:ParkingGateLimit", 16)));
        builder.Services.AddScoped<IStructureSynchronizationRepository, PostgresStructureSynchronizationRepository>();
        builder.Services.AddHostedService<StructureSynchronizationWorker>();
        builder.Services.AddScoped<IOccupancyProjectionRepository, OccupancyProjectionRepository>();
        // Occupancy projection is enabled only when the broker is configured; without it the
        // projection tables stay untouched and the operator layout keeps serving its snapshot.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["RabbitMq:ConnectionString"]))
        {
            builder.Services.Configure<RabbitMqProjectionOptions>(builder.Configuration.GetSection("RabbitMq"));
            builder.Services.AddHostedService<OccupancyProjectionWorker>();
        }
        builder.Services.AddScoped<IStructureOperationStore, StructureOperationStore>();
        builder.Services.AddHttpClient<IStructureImpactPlanner, StructureImpactPlanner>(c => { c.BaseAddress = new Uri(builder.Configuration["Services:Reservation"] ?? "http://localhost:5055/"); c.Timeout = TimeSpan.FromSeconds(15); c.DefaultRequestHeaders.Add("X-Service-Key", key); });
        var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"];
        var allowedOrigins = !string.IsNullOrWhiteSpace(configuredOrigins)
            ? configuredOrigins.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443"];
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
        builder.Services.AddHttpClient<OperatorSiteClient>(c => { c.BaseAddress = new(builder.Configuration["Services:User"] ?? "http://localhost:5035/"); c.Timeout = TimeSpan.FromSeconds(10); });
        builder.Services.AddScoped<IOperatorSiteAuthorizer>(s => s.GetRequiredService<OperatorSiteClient>());
        builder.Services.AddScoped<IOperatorOperationsStore, OperatorOperationsStore>();
        builder.Services.AddScoped<OperatorOperationsService>();
        builder.Services.AddScoped<IOperatorGateSlotRepository, OperatorGateSlotRepository>();
        builder.Services.AddScoped<IMaintenanceWindowStore, PostgresMaintenanceWindowStore>();

        // Application Layer CQRS & Pipeline Behaviors
        builder.Services.AddApplicationServices();
        builder.Services.AddExceptionHandler<FacilityExceptionHandler>();
        ApiErrors.Configure(builder.Services);
        builder.Services.AddReservationServiceAuthentication(builder.Configuration);
        builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
            ReservationServiceAuthentication.Scheme, options =>
            {
                options.Events.OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await ApiErrors.WriteAsync(context.HttpContext, 401, "UNAUTHENTICATED",
                        "A valid Operations service credential is required.", ct: context.HttpContext.RequestAborted);
                };
                options.Events.OnForbidden = async context =>
                {
                    await ApiErrors.WriteAsync(context.HttpContext, 403, "FORBIDDEN",
                        "The service is not permitted to use this capability.", ct: context.HttpContext.RequestAborted);
                };
            });

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false));
            });
        var app = builder.Build();
        app.UseCors();
        ApiErrors.Use(app);
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/internal") &&
                !context.Request.Path.StartsWithSegments("/internal/reservation-dependencies") && !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(context.Request.Headers["X-Service-Key"].ToString()), Encoding.UTF8.GetBytes(key)))
            { context.Response.StatusCode = 401; return; }
            try { await next(context); }
            catch (BadHttpRequestException e) when (DiscoveryCatalogueEndpoints.IsDiscoveryPath(context.Request.Path))
            {
                context.Response.StatusCode = e.StatusCode;
                await context.Response.WriteAsJsonAsync(new { success=false, code="VALIDATION_FAILED",
                    message="Discovery request could not complete.", errors=Array.Empty<string>(), traceId=context.TraceIdentifier });
            }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapDiscoveryCatalogue();
        return app;
    }
}
public partial class Program { public static void Main(string[] args) => FacilityHost.Build(args).Run(); }
