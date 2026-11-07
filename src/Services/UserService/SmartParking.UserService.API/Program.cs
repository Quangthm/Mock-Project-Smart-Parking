using UserService.Application;
using UserService.API.Extensions;
using UserService.API.Services;
using UserService.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Services;
using UserService.Infrastructure.Configurations;
using UserService.Infrastructure.Services;
using UserService.Infrastructure.Messaging;
using Microsoft.AspNetCore.DataProtection;
using MassTransit;

namespace UserService.API;

public static class UserHost
{
    public static async Task<WebApplication> BuildAsync(string[] args, Action<WebApplicationBuilder>? configure = null,
        Action<WebApplicationBuilder>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var serviceKey = builder.Configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(serviceKey) || serviceKey.Length < 32)
            throw new InvalidOperationException("Configure Services:Key (at least 32 characters).");
        var parkingUrl = RequireHttpUrl(builder.Configuration["Services:Parking"], "Services:Parking");
        var origins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var origin in origins)
        {
            var uri = RequireHttpUrl(origin, "Cors:AllowedOrigins");
            if (uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || origin.EndsWith('/'))
                throw new InvalidOperationException("Configure exact CORS origins without paths or trailing slash.");
        }

        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        builder.Services.AddControllers().AddApplicationPart(typeof(Controllers.AuthController).Assembly);
        builder.Services.AddApiErrorResponses();
        builder.Services.AddApplicationServices(builder.Configuration.GetSection("AuthenticationPolicy").Get<global::UserService.Application.Common.Models.JwT.AuthenticationPolicy>() ?? new());
        builder.Services.AddIdentityWorkflowServices();
        builder.Services.AddPersistenceServices(builder.Configuration);
        builder.Services.AddProfileMessaging(builder.Configuration,
            registration => registration.AddProfileOutbox(builder.Configuration),
            (endpoint, context) => endpoint.UseEntityFrameworkOutbox<AppDbContext>(context));
        builder.Services.AddJWTAuthentication(builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddApiCurrentActor();
        builder.Services.AddHttpClient<IParkingDirectory, ParkingDirectoryClient>(client =>
        {
            client.BaseAddress = parkingUrl;
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);
        });
        builder.Services.AddHttpClient("Otp", client => client.Timeout = TimeSpan.FromSeconds(10));

        var turnstileOptions = builder.Configuration.GetSection(TurnstileOptions.SectionName).Get<TurnstileOptions>() ?? new();
        turnstileOptions.ExpectedHostnames = turnstileOptions.ExpectedHostnames
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToArray();
        builder.Services.AddSingleton(turnstileOptions);
        builder.Services.AddHttpClient<IBotVerificationService, TurnstileVerifier>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false
        });

        if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
        {
            if (!turnstileOptions.Enabled || string.IsNullOrWhiteSpace(turnstileOptions.SecretKey) ||
                turnstileOptions.ExpectedHostnames is not { Length: > 0 })
            {
                throw new InvalidOperationException(
                    "Non-development environments (Production, Staging) require Turnstile to be enabled with a valid SecretKey and non-empty ExpectedHostnames.");
            }
            if (builder.Environment.IsProduction())
            {
                var forbiddenInProd = new[] { "localhost", "127.0.0.1", "::1", "staging" };
                if (turnstileOptions.ExpectedHostnames.Any(h => string.IsNullOrWhiteSpace(h) ||
                    forbiddenInProd.Any(f => h.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                    h.Contains('/') || h.Contains(':') || h.Contains('*')))
                {
                    throw new InvalidOperationException(
                        "Production ExpectedHostnames contains forbidden entries (localhost, 127.0.0.1, ::1, staging, ports, schemes, or wildcards).");
                }
            }
        }

        var protection=builder.Services.AddDataProtection().SetApplicationName("SmartPark.UserService");
        var keyDirectory=builder.Configuration["Workflow:KeyDirectory"];
        if(!string.IsNullOrWhiteSpace(keyDirectory))
        {
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
            if(OperatingSystem.IsWindows())protection.ProtectKeysWithDpapi();
        }
        else if(!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
            throw new InvalidOperationException("Configure durable Workflow:KeyDirectory for encrypted workflow state.");
        builder.Services.AddSingleton<IWorkflowProtector, UserService.Infrastructure.Services.WorkflowProtector>();
        builder.Services.AddScoped<IWorkflowTransport, UserService.Infrastructure.Services.WorkflowTransport>();
        builder.Services.AddScoped<IOtpSender, UserService.Application.Services.QueuedOtpSender>();
        builder.Services.AddHostedService<UserService.API.Services.WorkflowDeliveryWorker>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("DriverOtp", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("RegistrationRateLimit", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("LoginRateLimit", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddAuthorization();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<UserService.API.Hubs.AccessConnections>();
        builder.Services.AddScoped<IAccessChangeNotifier, UserService.API.Hubs.AccessChangeNotifier>();

        // Empty configuration permits no cross-origin browser access.
        builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

        configureServices?.Invoke(builder);
        var app = builder.Build();
        // Fail early when the signing key is missing or invalid.
        _ = app.Services.GetRequiredService<JwtKeyProvider>();
        app.MapUserServiceJwks();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedDemoUser"))
        {
            using var scope = app.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<UserService.Persistence.DataSeeder>();
            await seeder.SeedAsync();
        }
        if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(builder.Configuration["DevelopmentAdmin:Email"]))
        {
            var password = builder.Configuration["DevelopmentAdmin:Password"];
            if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Configure DevelopmentAdmin:Password explicitly.");
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAdminAsync(builder.Configuration["DevelopmentAdmin:Email"]!, password);
        }
        if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(builder.Configuration["DevelopmentOwner:Email"]))
        {
            var password = builder.Configuration["DevelopmentOwner:Password"];
            if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Configure DevelopmentOwner:Password explicitly.");
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedRoleUserAsync(builder.Configuration["DevelopmentOwner:Email"]!, password, "Demo Owner", "BUSINESS_OWNER", "Business Owner");
        }
        if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(builder.Configuration["DevelopmentOperator:Email"]))
        {
            var password = builder.Configuration["DevelopmentOperator:Password"];
            if (string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Configure DevelopmentOperator:Password explicitly.");
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedRoleUserAsync(builder.Configuration["DevelopmentOperator:Email"]!, password, "Demo Operator", "SITE_OPERATOR", "Site Operator");
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseApiErrorResponses();

        app.Use(async (context, next) =>
        {
            if (context.Request.ContentLength > 65536)
            {
                throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
            }
            await next();
        });

        app.UseCors("Frontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        app.MapHub<UserService.API.Hubs.OperatorAccessHub>("/hubs/operators", options => options.CloseOnAuthenticationExpiration = true);
        return app;
    }

    private static Uri RequireHttpUrl(string? value, string key)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0 || uri.Host.Contains('*'))
            throw new InvalidOperationException($"Configure a valid HTTP(S) URL for {key}.");
        return uri;
    }
}
public static class Program
{
    public static async Task Main(string[] args) => await (await UserHost.BuildAsync(args)).RunAsync();
}
