using UserService.Application;
using UserService.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Services;
using UserService.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;

namespace UserService.API;

public static class UserHost
{
    public static async Task<WebApplication> BuildAsync(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var serviceKey = builder.Configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(serviceKey) || serviceKey.Length < 32)
            throw new InvalidOperationException("Configure Services:Key (at least 32 characters).");

        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        builder.Services.AddControllers().AddApplicationPart(typeof(Controllers.AuthController).Assembly);
        builder.Services.AddApplicationServices(builder.Configuration);
        var chatbot=builder.Configuration.GetSection("Chatbot").Get<UserService.Application.Chatbot.ChatbotOptions>()??new();
        if(chatbot.TimeoutSeconds is <1 or >120 || chatbot.AllowedIntents.Any(i=>i is not ("FAQ" or "PARKING_SEARCH")))throw new InvalidOperationException("Invalid chatbot timeout or read intent allowlist.");
        builder.Services.AddSingleton(chatbot);
        builder.Services.AddScoped<UserService.Application.Chatbot.IChatModel,UnconfiguredChatModel>();
        builder.Services.AddScoped<UserService.Application.Chatbot.IChatBackendGateway,UnconfiguredChatBackend>();
        builder.Services.AddScoped<UserService.Application.Chatbot.ChatbotCoordinator>();
        builder.Services.AddPersistenceServices(builder.Configuration);
        builder.Services.AddJWTAuthentication(builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddHttpClient<IParkingDirectory, ParkingDirectoryClient>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["Services:Parking"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);
        });
        builder.Services.AddHttpClient("Otp", client => client.Timeout = TimeSpan.FromSeconds(10));
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
        builder.Services.AddScoped<IOtpSender, UserService.API.Services.QueuedOtpSender>();
        builder.Services.AddHostedService<UserService.API.Services.WorkflowDeliveryWorker>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("DriverOtp", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.Services.AddAuthorization();
        // Only allow the local frontend during this demo. Production uses explicit configured origins.
        builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()));

        var app = builder.Build();
        // Fail early when the signing key is missing or invalid.
        _ = app.Services.GetRequiredService<JwtKeyProvider>();

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

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseMiddleware<UserService.API.Middleware.AuthExceptionMiddleware>();

        app.UseCors("Frontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        return app;
    }
}
public static class Program
{
    public static async Task Main(string[] args) => await (await UserHost.BuildAsync(args)).RunAsync();
}
