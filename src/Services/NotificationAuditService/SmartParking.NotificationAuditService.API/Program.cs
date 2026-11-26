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
using SmartParking.NotificationAuditService.Infrastructure.Security;
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

// JWT Authentication Setup (Supports UserService JWKS RS256, static RSA Public Key, and HMAC SecretKey)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration, IJwksSigningKeyProvider>((options, configuration, jwksProvider) =>
    {
        var jwtSection = configuration.GetSection("Jwt");
        var jwtValidationSection = configuration.GetSection("JwtValidation");
        var issuer = jwtValidationSection["Issuer"] ?? jwtSection["Issuer"] ?? "SmartParking.UserService";
        var audience = jwtValidationSection["Audience"] ?? jwtSection["Audience"] ?? "SmartParking";
        var secretKey = jwtSection["SecretKey"] ?? jwtSection["Key"];
        var rsaPublicKeyPem = jwtSection["PublicKeyPem"] ?? jwtSection["PublicKey"];

        var staticSigningKeys = new List<SecurityKey>();

        if (!string.IsNullOrWhiteSpace(rsaPublicKeyPem))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(rsaPublicKeyPem);
            staticSigningKeys.Add(new RsaSecurityKey(rsa));
        }

        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            staticSigningKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)));
        }

        if (!jwksProvider.IsConfigured && staticSigningKeys.Count == 0)
        {
            const string defaultDevKey = "SmartParkingDevSecretKeyForLocalJwtTokenVerificationOnlyDoNotUseInProd123456!";
            staticSigningKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(defaultDevKey)));
        }

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { issuer, jwtSection["Issuer"] ?? issuer, "SmartParking.UserService", "SmartParking.UserService.Demo" }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal),
            ValidateAudience = true,
            ValidAudiences = new[] { audience, jwtSection["Audience"] ?? audience, "SmartParking", "SmartParking.Postman.Demo" }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = staticSigningKeys,
            IssuerSigningKeyResolver = (_, _, kid, _) =>
            {
                var resolved = new List<SecurityKey>(staticSigningKeys);
                if (jwksProvider.IsConfigured)
                {
                    foreach (var jwk in jwksProvider.ResolveKeys(kid))
                    {
                        resolved.Add(new RsaSecurityKey(jwk.Parameters) { KeyId = jwk.Kid });
                    }
                }
                return resolved;
            },
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.Claims.Any(c =>
                (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) &&
                string.Equals(c.Value, "Admin", StringComparison.OrdinalIgnoreCase)));
    });
});

var allowedCorsOrigins = (builder.Configuration["Cors:AllowedOrigins"]
    ?? builder.Configuration["SMARTPARK_NOTIFICATION_CORS_ORIGINS"]
    ?? "http://localhost:5173;http://127.0.0.1:5173;http://localhost:8443;http://127.0.0.1:8443")
    .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

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

app.UseCors("FrontendCors");
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
