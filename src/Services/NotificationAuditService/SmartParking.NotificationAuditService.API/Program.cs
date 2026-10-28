using System.Text.Json.Serialization;
using SmartParking.NotificationAuditService.API.Middleware;
using SmartParking.NotificationAuditService.API.Services;
using SmartParking.NotificationAuditService.Application;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Services;
using SmartParking.NotificationAuditService.Infrastructure;
using SmartParking.NotificationAuditService.Persistence;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

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

    return Results.Ok(new
    {
        status = "Healthy",
        service = "SmartParking.NotificationAuditService",
        timestamp = DateTimeOffset.UtcNow,
        dependencies = new
        {
            database = dbConnected ? "Connected" : "Disconnected",
            messaging = "MassTransit.RabbitMQ Configured"
        }
    });
});

app.MapControllers();

app.Run();

public partial class Program { }
