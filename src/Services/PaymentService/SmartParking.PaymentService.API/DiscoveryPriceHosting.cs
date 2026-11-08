using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using SmartParking.PaymentService.Application;

namespace SmartParking.PaymentService.API;

public static class DiscoveryPriceHosting
{
    public const string Route = "/internal/discovery/prices:read";
    public const string Header = "X-Discovery-Service-Key";

    public static void AddDiscoveryPriceReads(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDiscoverySiteBindings, ConfiguredSiteBindings>();
        services.AddRateLimiter(options =>
        {
            options.AddConcurrencyLimiter("discovery-prices", limiter =>
            {
                limiter.PermitLimit = 16;
                limiter.QueueLimit = 0;
            });
            options.OnRejected = async (rejection, token) =>
            {
                rejection.HttpContext.Response.Headers.CacheControl = "no-store";
                rejection.HttpContext.Response.Headers.RetryAfter = "1";
                await Error(rejection.HttpContext, 429, "RATE_LIMITED").ExecuteAsync(rejection.HttpContext);
            };
        });
    }

    public static void UseDiscoveryPriceReads(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.ContainsKey(Header) &&
                !string.Equals(context.Request.Path.Value?.TrimEnd('/'), Route, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers.CacheControl = "no-store";
                await Error(context, 403, "SERVICE_SCOPE_DENIED").ExecuteAsync(context);
                return;
            }
            await next(context);
        });
        app.UseRateLimiter();
    }

    public static void MapDiscoveryPriceReads(this WebApplication app)
        => app.MapPost(Route, Handle).RequireRateLimiting("discovery-prices").ExcludeFromDescription();

    private sealed record WireQuery(Guid[] SiteIds, string VehicleType, string At, string? StartAt, string? EndAt);

    private static async Task<IResult> Handle(HttpContext context, IConfiguration config, IMediator mediator, TimeProvider clock)
    {
        context.Response.Headers.CacheControl = "no-store";
        var request = context.Request;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var ct = deadline.Token;
        if (!request.IsHttps || request.Headers.ContainsKey("Authorization") || !request.Headers.TryGetValue(Header, out var key)
            || key.Count != 1 || !Authorized(key[0], config, clock.GetUtcNow()))
            return Error(context, 401, "SERVICE_UNAUTHENTICATED");
        if (!request.HasJsonContentType()) return Error(context, 415, "UNSUPPORTED_MEDIA_TYPE");
        const int maxBody = 16 * 1024;
        if (request.ContentLength > maxBody) return Error(context, 413, "PAYLOAD_TOO_LARGE");
        WireQuery? body;
        try
        {
            var buffer = new byte[maxBody + 1];
            var size = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
            if (size > maxBody) return Error(context, 413, "PAYLOAD_TOO_LARGE");
            body = JsonSerializer.Deserialize<WireQuery>(buffer.AsSpan(0, size), JsonSerializerOptions.Web);
        }
        catch (JsonException) { return Error(context, 400, "VALIDATION_FAILED"); }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        { return Error(context, 504, "DEPENDENCY_TIMEOUT"); }
        if (body == null || !Timestamp(body.At, out var at) || !Timestamp(body.StartAt, out var from)
            || !Timestamp(body.EndAt, out var to) || !at.HasValue)
            return Error(context, 400, "VALIDATION_FAILED");
        try
        {
            var batch = await mediator.Send(new DiscoveryPriceQuery(body.SiteIds, body.VehicleType, at.Value, from, to), ct);
            return Results.Ok(new { success = true, data = batch, traceId = context.TraceIdentifier });
        }
        catch (DiscoveryPriceFailure e) { return Error(context, e.Status, e.Message); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Error(context, 504, "DEPENDENCY_TIMEOUT"); }
        catch (Exception) { return Error(context, 503, "DEPENDENCY_UNAVAILABLE"); }
    }

    private static bool Timestamp(string? text, out DateTimeOffset? instant)
    {
        instant = null;
        if (text == null) return true;
        if (text.Length is < 20 or > 40 || text[10] != 'T' ||
            !(text.EndsWith('Z') || text[^6] is '+' or '-' && text[^3] == ':') ||
            !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        instant = parsed.ToUniversalTime();
        return true;
    }

    private static IResult Error(HttpContext context, int status, string code) =>
        Results.Json(new { success = false, code, message = "Discovery price read could not complete.", errors = Array.Empty<object>(), traceId = context.TraceIdentifier }, statusCode: status);

    private static bool Authorized(string? supplied, IConfiguration config, DateTimeOffset now)
    {
        if (supplied is not { Length: >= 32 and <= 256 }) return false;
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        bool Check(string slot)
        {
            var section = config.GetSection("DiscoveryPricing:Operations:" + slot);
            var value = section["Key"];
            if (value is not { Length: >= 32 and <= 256 } || !Timestamp(section["ExpiresAt"], out var expiry) || expiry == null || expiry <= now) return false;
            return CryptographicOperations.FixedTimeEquals(digest, SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }
        return Check("Current") | Check("Previous");
    }

    private sealed class ConfiguredSiteBindings(IConfiguration config) : IDiscoverySiteBindings
    {
        public IReadOnlyDictionary<Guid, Guid> Require(Guid[] siteIds)
        {
            var map = new Dictionary<Guid, Guid>();
            foreach (var site in siteIds)
            {
                if (!Guid.TryParse(config[$"DiscoveryPricing:SiteTenants:{site:D}"], out var tenant) || tenant == Guid.Empty)
                    throw new DiscoveryPriceFailure(403, "SERVICE_SCOPE_DENIED");
                map.Add(site, tenant);
            }
            return map;
        }
    }
}
