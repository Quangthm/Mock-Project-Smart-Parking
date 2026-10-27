using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Infrastructure;
using SmartParking.PaymentService.Persistence;

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
    builder.Services.AddAuthentication();
}

builder.Services.AddAuthorization();

// Clean Architecture & Hexagonal DI registrations
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddPersistenceServices(builder.Configuration);

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

app.Run();

public partial class Program { }
