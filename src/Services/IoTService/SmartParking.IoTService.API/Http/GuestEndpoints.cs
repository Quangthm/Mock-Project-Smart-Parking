using MediatR;
using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Guest;
namespace SmartParking.IoTService.API.Http;

public sealed class GuestMessageAdmission;

public static class GuestEndpoints
{
    public static void MapGuest(this WebApplication app)
    {
        app.MapGet("/api/assistant/status", async (HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            if (ctx.Request.QueryString.HasValue) throw new GuestFailure(400, "VALIDATION_FAILED");
            return Results.Ok(new SuccessEnvelope<GuestStatus>(true, await mediator.Send(new GetGuestStatus(), ct), ctx.TraceIdentifier));
        }).RequireRateLimiting("guest-status");
        app.MapPost("/api/assistant/guest/messages", Send).WithMetadata(new GuestMessageAdmission()).RequireRateLimiting("guest-message");
    }
    private static async Task<IResult> Send(HttpContext ctx, IMediator mediator, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            if (ctx.Request.QueryString.HasValue) throw new GuestFailure(400, "VALIDATION_FAILED");
            if (!ctx.Request.HasJsonContentType() ||
                !Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(ctx.Request.ContentType, out var media) ||
                media.Charset.HasValue && !string.Equals(media.Charset.Value.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))
                throw new GuestFailure(415, "UNSUPPORTED_MEDIA_TYPE");
            if (ctx.Request.ContentLength > 32768) throw new GuestFailure(413, "PAYLOAD_TOO_LARGE");
            using var body = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = await ctx.Request.Body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, 32769 - (int)body.Length)), deadline.Token)) > 0)
            {
                body.Write(buffer, 0, count);
                if (body.Length > 32768) throw new GuestFailure(413, "PAYLOAD_TOO_LARGE");
            }
            GuestMessage? request;
            try { request = JsonSerializer.Deserialize<GuestMessage>(body.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { throw new GuestFailure(400, "VALIDATION_FAILED"); }
            if (request is null) throw new GuestFailure(400, "VALIDATION_FAILED");
            var answer = await mediator.Send(request, deadline.Token).WaitAsync(deadline.Token);
            return Results.Ok(new SuccessEnvelope<GuestReply>(true, answer, ctx.TraceIdentifier));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new GuestFailure(504, "REQUEST_TIMEOUT"); }
    }
}
