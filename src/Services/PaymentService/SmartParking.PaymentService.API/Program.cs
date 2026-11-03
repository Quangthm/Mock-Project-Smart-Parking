using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Infrastructure;
using SmartParking.PaymentService.Persistence;
using MassTransit;
using SmartParking.PaymentService.Infrastructure.Messaging;

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
if (builder.Configuration.GetValue<bool>("Messaging:Enabled"))
{
    builder.Services.AddMassTransit(registration =>
    {
        registration.AddEntityFrameworkOutbox<PaymentDbContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.UseBusOutbox(busOutbox =>
            {
                if (!builder.Configuration.GetValue<bool>("Messaging:OutboxDeliveryEnabled"))
                    busOutbox.DisableDeliveryService();
            });
        });
        registration.UsingRabbitMq((context, bus) =>
        {
            bus.Message<PaymentSucceededV1>(message =>
                message.SetEntityName("smartpark.payment.payment-succeeded.v1"));
            bus.Host(
                builder.Configuration["SMARTPARK_RABBITMQ_HOST"] ?? "127.0.0.1",
                builder.Configuration.GetValue<ushort?>("SMARTPARK_RABBITMQ_PORT") ?? 5672,
                builder.Configuration["SMARTPARK_RABBITMQ_VHOST"] ?? "smartpark",
                host =>
                {
                    host.Username(builder.Configuration["SMARTPARK_RABBITMQ_USER"] ?? "smartpark");
                    host.Password(builder.Configuration["SMARTPARK_RABBITMQ_PASSWORD"]
                        ?? throw new InvalidOperationException("Configure SMARTPARK_RABBITMQ_PASSWORD."));
                });
        });
    });
}

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
