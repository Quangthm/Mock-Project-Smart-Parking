using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SmartParking.NotificationAuditService.API.Authentication;
using SmartParking.NotificationAuditService.API.Middleware;
using SmartParking.NotificationAuditService.API.Services;
using SmartParking.NotificationAuditService.Application;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Services;
using SmartParking.NotificationAuditService.Infrastructure;
using SmartParking.NotificationAuditService.Persistence;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Configure logging for clean container and test host lifecycle
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
var rawOrigins = builder.Configuration["Cors:AllowedOrigins"];
var corsOrigins = !string.IsNullOrWhiteSpace(rawOrigins)
    ? rawOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8443", "http://127.0.0.1:8443"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (corsOrigins.Length > 0) policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
}));

// JWT authentication: validates UserService tokens against explicitly configured key
// material (JWKS endpoint, RSA public key PEM or symmetric key). Fails closed without it.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddUserServiceJwtAuthentication(builder.Configuration);

builder.Services.AddAuthorization();

// Clean Architecture DI registrations
builder.Services.AddApplicationServices();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Seed default templates if database is accessible
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<NotificationAuditDbContext>();
        if (await db.Database.CanConnectAsync())
        {
            await NotificationTemplateSeeder.SeedAsync(db);
        }
    }
    catch
    {
        // Safe startup in offline/unit environment
    }
}

// Configure the HTTP request pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (NotificationAuditDbContext dbContext) =>
{
    var dbConnected = false;
    try
    {
        dbConnected = await dbContext.Database.CanConnectAsync();
    }
    catch
    {
        dbConnected = false;
    }

    var response = new
    {
        status = dbConnected ? "Healthy" : "Unhealthy",
        service = "SmartParking.NotificationAuditService",
        timestamp = DateTimeOffset.UtcNow,
        dependencies = new
        {
            database = dbConnected ? "Connected" : "Disconnected",
            messaging = "MassTransit.RabbitMQ Configured"
        }
    };

    return dbConnected
        ? Results.Ok(response)
        : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapControllers();

app.Run();

public partial class Program { }
