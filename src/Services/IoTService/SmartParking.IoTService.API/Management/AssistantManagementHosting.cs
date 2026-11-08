using MediatR;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using Npgsql;
using SmartParking.IoTService.API.Authentication;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Infrastructure.Assistant.Management;
using SmartParking.IoTService.Persistence;
namespace SmartParking.IoTService.API.Management;

public static class AssistantManagementHosting
{
    // Explicit opt-in for authenticated local management. Invalid setup never opens anonymous management.
    public static void AddManagement(this WebApplicationBuilder builder)
    {
        string Required(string key) => !string.IsNullOrWhiteSpace(builder.Configuration[key])
            ? builder.Configuration[key]! : throw new InvalidOperationException($"Missing management configuration: {key}");
        builder.Services.AddAssistantAuthentication(Required("Assistant:Jwt:Issuer"), Required("Assistant:Jwt:Audience"),
            Required("Assistant:Jwt:PublicKeyPem"), TimeProvider.System);
        builder.Services.AddAuthorization(options => options.AddPolicy("AssistantAdmin", p => p.RequireAuthenticatedUser().RequireRole("admin")));
        builder.Services.AddSingleton(TimeProvider.System);
        var directory = Required("Assistant:Management:KeyDirectory");
        if (!Path.IsPathFullyQualified(directory)) throw new InvalidOperationException("Management key directory must be absolute.");
        var protection = builder.Services.AddDataProtection().SetApplicationName("SmartPark.IoT.Assistant")
            .PersistKeysToFileSystem(new DirectoryInfo(directory));
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        else
        {
            var certificatePath = Required("Assistant:Management:KeyEncryptionCertificatePath");
            if (!Path.IsPathFullyQualified(certificatePath)) throw new InvalidOperationException("Certificate path must be absolute.");
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                builder.Configuration["Assistant:Management:KeyEncryptionCertificatePassword"]);
            if (!certificate.HasPrivateKey) throw new InvalidOperationException("Management requires a private encryption certificate.");
            protection.ProtectKeysWithCertificate(certificate);
        }
        var connection = Required("ConnectionStrings:IoT");
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
        builder.Services.AddSingleton<IAssistantManagementStore,AssistantManagementStore>();
        builder.Services.AddSingleton<IAssistantSecretProtector,AssistantSecretProtector>();
        var knowledge = Required("Assistant:Demo:KnowledgeDirectory");
        builder.Services.AddSingleton<IManagedAnswerProvider>(_ => new ManagedAnswerProvider(knowledge));
        builder.Services.AddSingleton<ManagedDemoGenerator>();
        builder.Services.AddSingleton<IAssistantManagedRuntime>(sp => sp.GetRequiredService<ManagedDemoGenerator>());
        builder.Services.AddSingleton<IDemoGroundedGenerator>(sp => sp.GetRequiredService<ManagedDemoGenerator>());
    }
    public static void MapManagement(this WebApplication app)
    {
        var group = app.MapGroup("/api/assistant/admin").RequireAuthorization("AssistantAdmin");
        group.MapGet("/settings", async (HttpContext ctx,IMediator mediator,CancellationToken ct) =>
        {
            if (ctx.Request.QueryString.HasValue) return Results.BadRequest(AssistantErrors.Envelope(ctx,AssistantErrors.ForStatus(400)));
            return Results.Ok(new SuccessEnvelope<ManagementView>(true,await mediator.Send(new GetAssistantManagement(),ct),ctx.TraceIdentifier));
        });
        group.MapPost("/mutations", async (HttpContext ctx,IMediator mediator,CancellationToken ct) =>
        {
            if (ctx.Request.QueryString.HasValue) throw new ManagementValidationException();
            if (!ctx.Request.HasJsonContentType()) return Results.Json(AssistantErrors.Envelope(ctx,AssistantErrors.ForStatus(415)),statusCode:415);
            if (!Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(ctx.Request.ContentType,out var media) ||
                media.Charset.HasValue && !string.Equals(media.Charset.Value.Trim('"'),"utf-8",StringComparison.OrdinalIgnoreCase))
                return Results.Json(AssistantErrors.Envelope(ctx,AssistantErrors.ForStatus(415)),statusCode:415);
            // Limit before deserialization, including chunked requests. Never log input/key.
            using var bytes = new MemoryStream(); var buffer = new byte[4096];
            int count;
            while ((count=await ctx.Request.Body.ReadAsync(buffer,ct))>0)
            {
                if (bytes.Length+count>32768) return Results.Json(AssistantErrors.Envelope(ctx,AssistantErrors.ForStatus(413)),statusCode:413);
                bytes.Write(buffer,0,count);
            }
            AdminMutation? input;
            try { input=System.Text.Json.JsonSerializer.Deserialize<AdminMutation>(bytes.ToArray(),new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
                { NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.Strict }); }
            catch(System.Text.Json.JsonException) { throw new ManagementValidationException(); }
            if(input is null) throw new ManagementValidationException();
            return Results.Ok(new SuccessEnvelope<ManagementView>(true,await mediator.Send(new ChangeAssistantManagement(input),ct),ctx.TraceIdentifier));
        });
    }
}
