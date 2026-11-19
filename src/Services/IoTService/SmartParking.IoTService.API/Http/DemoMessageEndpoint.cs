using System.Text.Json;
using MediatR;
using SmartParking.IoTService.Application.Assistant.Demo;

namespace SmartParking.IoTService.API.Http;

public static class DemoMessageEndpoint
{
    public const int MaximumBodyBytes = 32 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict
    };

    public static async Task<IResult> SendAsync(HttpContext context, IMediator mediator, CancellationToken ct)
    {
        IResult Error(int status) => Results.Json(AssistantErrors.Envelope(context, AssistantErrors.ForStatus(status)), statusCode: status);
        if (context.Request.QueryString.HasValue) return Error(400);
        if (!context.Request.HasJsonContentType()) return Error(415);
        if (!Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var media) ||
            media.Charset.HasValue && !string.Equals(media.Charset.Value.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))
            return Error(415);
        if (context.Request.ContentLength > MaximumBodyBytes) return Error(413);
        // Read at most one byte beyond the limit, including requests without Content-Length.
        using var body = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var count = await context.Request.Body.ReadAsync(buffer.AsMemory(0,
                Math.Min(buffer.Length, MaximumBodyBytes + 1 - (int)body.Length)), ct);
            if (count == 0) break;
            body.Write(buffer, 0, count);
            if (body.Length > MaximumBodyBytes) return Error(413);
        }
        DemoMessageRequest? request;
        try { request = JsonSerializer.Deserialize<DemoMessageRequest>(body.GetBuffer().AsSpan(0, (int)body.Length), JsonOptions); }
        catch (JsonException) { return Error(400); }
        if (request is null) return Error(400);
        var reply = await mediator.Send(new SendDemoMessageCommand(request.Message), ct);
        return Results.Ok(new SuccessEnvelope<DemoMessageReply>(true, reply, context.TraceIdentifier));
    }
}
