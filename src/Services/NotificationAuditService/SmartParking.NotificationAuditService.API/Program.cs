using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

// JWT Authentication Setup (Supporting both RSA Public Key and HMAC SecretKey with dynamic configuration)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection["Issuer"] ?? "SmartParking.UserService";
        var audience = jwtSection["Audience"] ?? "SmartParking";
        var secretKey = jwtSection["SecretKey"] ?? jwtSection["Key"];
        var rsaPublicKeyPem = jwtSection["PublicKeyPem"] ?? jwtSection["PublicKey"];

        var signingKeys = new List<SecurityKey>();

        if (!string.IsNullOrWhiteSpace(rsaPublicKeyPem))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(rsaPublicKeyPem);
            signingKeys.Add(new RsaSecurityKey(rsa));
        }

        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            signingKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)));
        }

        if (signingKeys.Count == 0)
        {
            const string defaultDevKey = "SmartParkingDevSecretKeyForLocalJwtTokenVerificationOnlyDoNotUseInProd123456!";
            signingKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(defaultDevKey)));
        }

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { issuer, "SmartParking.UserService", "SmartParking.UserService.Demo" },
            ValidateAudience = true,
            ValidAudiences = new[] { audience, "SmartParking", "SmartParking.Postman.Demo" },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

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
