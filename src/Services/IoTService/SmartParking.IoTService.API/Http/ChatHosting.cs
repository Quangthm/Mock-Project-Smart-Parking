using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.IoTService.Application.Assistant;
using SmartParking.IoTService.Application.Assistant.Chat;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Persistence;

namespace SmartParking.IoTService.API.Http;

public sealed class ChatCursors(IDataProtectionProvider protection) : IChatCursors
{
    private readonly IDataProtector protector = protection.CreateProtector("SmartPark.Assistant.Chat.Cursor.v1");
    public string Encode(ChatCursor cursor) => protector.Protect(JsonSerializer.Serialize(cursor));
    public ChatCursor Decode(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 2048) throw new ChatFailure(400, "INVALID_CURSOR");
        try { return JsonSerializer.Deserialize<ChatCursor>(protector.Unprotect(cursor)) ?? throw new ChatFailure(400, "INVALID_CURSOR"); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { throw new ChatFailure(400, "INVALID_CURSOR"); }
    }
}

// Bounded active tasks are owned and awaited by the host. There is no pending generation queue.
public sealed class ChatExecution(IChatStore store, TimeProvider clock, ILogger<ChatExecution> logger) : BackgroundService, IChatExecution
{
    private readonly SemaphoreSlim slots = new(2, 2);
    private readonly ConcurrentDictionary<long, Task<RequestResult>> work = new();
    private readonly Dictionary<Guid, (DateTimeOffset Start, int Count)> windows = [];
    private readonly object gate = new();
    private readonly CancellationTokenSource stopping = new();
    private long sequence;
    public IDisposable Admit(Guid account)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            foreach (var key in windows.Where(x => now - x.Value.Start >= TimeSpan.FromMinutes(1)).Select(x => x.Key).ToArray()) windows.Remove(key);
            windows.TryGetValue(account, out var window);
            if (window.Count >= 10 || windows.Count >= 10000 && window.Count == 0 || !slots.Wait(0)) throw new ChatFailure(429, "RATE_LIMITED");
            windows[account] = (window.Count == 0 ? now : window.Start, window.Count + 1);
            return new Lease(slots);
        }
    }
    private sealed class Lease(SemaphoreSlim slots) : IDisposable
    { private int disposed; public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) slots.Release(); } }
    public async Task<RequestResult> Run(Func<CancellationToken, Task<RequestResult>> action)
    {
        stopping.Token.ThrowIfCancellationRequested();
        var id = Interlocked.Increment(ref sequence);
        var task = action(stopping.Token);
        work[id] = task;
        try { return await task; } finally { work.TryRemove(id, out _); }
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var purgeAt = DateTimeOffset.MinValue;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(5000);
                await store.Recover(timeout.Token);
                if (clock.GetUtcNow() >= purgeAt) { await store.Purge(timeout.Token); purgeAt = clock.GetUtcNow().AddDays(1); }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch { logger.LogWarning("Assistant recovery unavailable; pending requests remain recoverable in storage."); }
            try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { break; }
        }
    }
    public override async Task StopAsync(CancellationToken ct)
    {
        await stopping.CancelAsync();
        await base.StopAsync(ct);
        try { await Task.WhenAll(work.Values).WaitAsync(ct); } catch (Exception) { /* DB deadlines fence late work; restart sweep does not redispatch. */ }
    }
    private int disposed;
    public override void Dispose() { if (Interlocked.Exchange(ref disposed, 1) != 0) return; stopping.Cancel(); stopping.Dispose(); base.Dispose(); }
}

public static class ChatHosting
{
    public static void AddChat(this WebApplicationBuilder builder)
    {
        builder.AddReadTools();
        builder.Services.TryAddSingleton(new ChatOptions(builder.Configuration.GetValue<bool>("Assistant:Chat:ReleaseEnabled")));
        builder.Services.TryAddSingleton<IChatStore, AssistantChatStore>();
        builder.Services.TryAddSingleton<IChatCursors, ChatCursors>();
        builder.Services.TryAddSingleton<ChatReadiness>();
        builder.Services.AddScoped<ChatBudget>();
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ChatDeadlineBehavior<,>));
        builder.Services.TryAddSingleton<ChatExecution>();
        builder.Services.TryAddSingleton<IChatExecution>(sp => sp.GetRequiredService<ChatExecution>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ChatExecution>());
    }
    private sealed record MessageInput(Guid ClientMessageId, long? ExpectedRevision, string? Message);
    private static async Task<T> Body<T>(HttpContext ctx, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ctx.RequestServices.GetRequiredService<ChatBudget>().Remaining);
        ct = timeout.Token;
        if (ctx.Request.QueryString.HasValue) throw new ChatFailure(400, "VALIDATION_FAILED");
        if (!ctx.Request.HasJsonContentType() || !Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(ctx.Request.ContentType, out var media) ||
            media.Charset.HasValue && !string.Equals(media.Charset.Value.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)) throw new ChatFailure(415, "UNSUPPORTED_MEDIA_TYPE");
        using var bytes = new MemoryStream(); var buffer = new byte[4096]; int count;
        while ((count = await ctx.Request.Body.ReadAsync(buffer, ct)) > 0)
        { if (bytes.Length + count > 16384) throw new ChatFailure(413, "PAYLOAD_TOO_LARGE"); bytes.Write(buffer, 0, count); }
        try { return JsonSerializer.Deserialize<T>(bytes.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict }) ?? throw new ChatFailure(400, "VALIDATION_FAILED"); }
        catch (JsonException) { throw new ChatFailure(400, "VALIDATION_FAILED"); }
    }
    private static Guid Id(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : throw new ChatFailure(400, "VALIDATION_FAILED");
    private static IResult Result(HttpContext ctx, object data, int status = 200)
    {
        if (data is RequestResult request)
        {
            ctx.Response.Headers.Location = $"/api/assistant/conversations/{request.ConversationId}/requests/{request.ClientMessageId}";
            if (request.State == "running") ctx.Response.Headers.RetryAfter = "2";
        }
        return Results.Json(new SuccessEnvelope<object>(true, data, ctx.TraceIdentifier), statusCode: status);
    }
    public static void MapChat(this WebApplication app)
    {
        var group = app.MapGroup("/api/assistant").RequireAuthorization("AssistantDriver").RequireRateLimiting("chat-read");
        group.AddEndpointFilter(async (context, next) =>
        {
            _ = context.HttpContext.RequestServices.GetRequiredService<ChatBudget>();
            try { return await next(context); }
            catch (OperationCanceledException) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
            { throw new ChatFailure(504, "REQUEST_TIMEOUT"); }
        });
        group.MapGet("/capabilities", async (HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        { NoQuery(ctx); return Result(ctx, await mediator.Send(new GetAssistantCapabilitiesQuery(), ct)); });
        group.MapPost("/conversations", async (HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            var input = await Body<CreateConversation>(ctx, ct);
            var result = await mediator.Send(input, ct);
            ctx.Response.Headers.Location = $"/api/assistant/conversations/{result.Conversation.Id}";
            return Result(ctx, result.Conversation, result.Created ? 201 : 200);
        });
        group.MapGet("/conversations", (HttpContext ctx, IMediator mediator, CancellationToken ct) => Page(ctx, mediator, null, ct));
        group.MapGet("/conversations/{id}", async (string id, HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        { NoQuery(ctx); return Result(ctx, await mediator.Send(new ReadChat(Id(id)), ct)); });
        group.MapGet("/conversations/{id}/messages", (string id, HttpContext ctx, IMediator mediator, CancellationToken ct) => Page(ctx, mediator, Id(id), ct));
        group.MapPost("/conversations/{id}/messages", async (string id, HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            var input = await Body<MessageInput>(ctx, ct);
            var result = await mediator.Send(new SendChat(Id(id), input.ClientMessageId, input.ExpectedRevision, input.Message), ct);
            return Result(ctx, result, result.State == "running" ? 202 : 200);
        });
        group.MapGet("/conversations/{id}/requests/{key}", async (string id, string key, HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        { NoQuery(ctx); return Result(ctx, await mediator.Send(new ReadChat(Id(id), Id(key)), ct)); });
        group.MapPost("/conversations/{id}/requests/{key}/cancel", async (string id, string key, HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            var body = await Body<JsonElement>(ctx, ct);
            if (body.ValueKind != JsonValueKind.Object) throw new ChatFailure(400, "VALIDATION_FAILED");
            return Result(ctx, await mediator.Send(new ReadChat(Id(id), Id(key), true), ct));
        });
    }
    private static void NoQuery(HttpContext ctx) { if (ctx.Request.QueryString.HasValue) throw new ChatFailure(400, "VALIDATION_FAILED"); }
    private static async Task<IResult> Page(HttpContext ctx, IMediator mediator, Guid? id, CancellationToken ct)
    {
        if (ctx.Request.Query.Any(x => x.Key is not ("limit" or "cursor") || x.Value.Count != 1 || string.IsNullOrEmpty(x.Value[0]))) throw new ChatFailure(400, "VALIDATION_FAILED");
        var limit = id is null ? 20 : 50;
        if (ctx.Request.Query.TryGetValue("limit", out var raw) && (!int.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out limit))) throw new ChatFailure(400, "VALIDATION_FAILED");
        return Result(ctx, await mediator.Send(new ListChat(id, limit, ctx.Request.Query.TryGetValue("cursor", out var cursor) ? cursor.ToString() : null), ct));
    }
}
