using System.Diagnostics;
using System.Text.Json;
using MediatR;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Management;

namespace SmartParking.IoTService.API.Management;

public static class ProviderDiagnosticsEndpoints
{
    public static void MapProviderDiagnostics(this RouteGroupBuilder group)
    {
        group.MapPost("/models/discover", async (HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            var input = await Read<DiscoverModelsInput>(ctx, ct);
            using var diagnosticsActivity = StartDiagnosticsActivity(ctx);
            var result = await mediator.Send(new DiscoverModels(input), ct);
            return Results.Ok(new SuccessEnvelope<ModelPage>(true, result, ctx.TraceIdentifier));
        });
        group.MapPost("/configurations/{id:guid}/test", async (Guid id, HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            var input = await Read<ConnectionTestInput>(ctx, ct);
            using var diagnosticsActivity = StartDiagnosticsActivity(ctx);
            var response = await mediator.Send(new TestConnection(id, input), ct);
            if (response.Running)
            {
                ctx.Response.Headers.RetryAfter = "2";
                return Results.Json(new SuccessEnvelope<object>(true,
                    new { testRequestId = input.TestRequestId, configurationId = id, outcome = "running" }, ctx.TraceIdentifier), statusCode: 202);
            }
            return Results.Ok(new SuccessEnvelope<ConnectionTestResult>(true, response.Result!, ctx.TraceIdentifier));
        });
    }
    private static Activity? StartDiagnosticsActivity(HttpContext context)
    {
        var activity = Activity.Current is null ? new Activity("Assistant.ProviderDiagnostics").Start() : null;
        Activity.Current?.SetTag(ProviderDiagnostics.CorrelationIdTag, context.TraceIdentifier);
        return activity;
    }

    private static async Task<T> Read<T>(HttpContext ctx, CancellationToken ct)
    {
        if (ctx.Request.QueryString.HasValue) throw new ManagementValidationException();
        if (!ctx.Request.HasJsonContentType() || !Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(ctx.Request.ContentType, out var media) ||
            media.Charset.HasValue && !string.Equals(media.Charset.Value.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))
            throw new BadHttpRequestException("Unsupported content type", 415);
        using var bytes = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = await ctx.Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (bytes.Length + count > 32768) throw new BadHttpRequestException("Payload too large", 413);
            bytes.Write(buffer, 0, count);
        }
        try
        {
            return JsonSerializer.Deserialize<T>(bytes.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict }) ?? throw new ManagementValidationException();
        }
        catch (JsonException) { throw new ManagementValidationException(); }
    }
}
