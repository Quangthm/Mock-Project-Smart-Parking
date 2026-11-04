using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Infrastructure;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Outbox;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:8443",
        "http://127.0.0.1:8443")
    .AllowAnyHeader()
    .AllowAnyMethod()));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = DevAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = DevAuthenticationHandler.SchemeName;
    }).AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, null);
}
else
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
        {
            var jwtSection = builder.Configuration.GetSection("Jwt");
            var issuer = jwtSection["Issuer"] ?? "SmartParking.UserService";
            var audience = jwtSection["Audience"] ?? "SmartParking";
            var secretKey = jwtSection["SecretKey"] ?? "smartpark-default-insecure-dev-jwt-key-replace-in-production-min-32-chars";
            var pemKey = jwtSection["PublicKeyPem"] ?? jwtSection["PrivateKeyPem"];

            var signingKeys = new List<SecurityKey>();
            if (!string.IsNullOrWhiteSpace(pemKey))
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(pemKey);
                signingKeys.Add(new RsaSecurityKey(rsa));
            }
            if (!string.IsNullOrWhiteSpace(secretKey))
            {
                signingKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)));
            }

            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                IssuerSigningKey = signingKeys.FirstOrDefault(),
                RoleClaimType = "role",
                NameClaimType = "sub"
            };
        });
}

builder.Services.AddAuthorization();

// Clean Architecture & Hexagonal DI registrations
builder.Services.AddApplicationServices();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration, x => x.AddPaymentOutbox(builder.Configuration));

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program { }
