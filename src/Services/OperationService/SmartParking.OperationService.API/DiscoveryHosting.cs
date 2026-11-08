using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Infrastructure.Clients;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SmartParking.OperationService.API;

public sealed class DiscoveryCursors(IDataProtectionProvider provider) : IDiscoveryCursors
{
    private readonly IDataProtector protector = provider.CreateProtector("Operations.PublicDiscovery.Cursor.v1");
    public string Encode(DiscoveryCursor value) => protector.Protect(JsonSerializer.Serialize(value));
    public DiscoveryCursor Decode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) throw new DiscoveryFailure(400, "INVALID_CURSOR");
        try
        {
            return JsonSerializer.Deserialize<DiscoveryCursor>(protector.Unprotect(value))
                ?? throw new DiscoveryFailure(400, "INVALID_CURSOR");
        }
        catch (Exception e) when (e is CryptographicException or FormatException or JsonException)
        {
            throw new DiscoveryFailure(400, "INVALID_CURSOR");
        }
    }
}

public static class DiscoveryHosting
{
    private static Uri? Origin(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0
            ? uri : null;
    public static void AddParkingSearch(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("Discovery");
        var origins = new DiscoveryOrigins(Origin(settings["FacilityOrigin"]), Origin(settings["PaymentOrigin"]), settings["PaymentServiceKey"]);
        var age = int.TryParse(settings["MaxAgeSeconds"], out var a) ? a : 0;
        var skew = int.TryParse(settings["ClockSkewSeconds"], out var s) ? s : -1;
        var enabled = bool.TryParse(settings["Enabled"], out var flag) && flag &&
            origins.Facility is not null && origins.Payment is not null && origins.PaymentKey is { Length: >= 32 and <= 256 } key &&
            key.All(c => c >= '!' && c <= '~') && age is > 0 and <= 3600 && skew is >= 0 and <= 60;
        builder.Services.AddSingleton(origins);
        builder.Services.AddSingleton(new DiscoveryPolicy(enabled, TimeSpan.FromSeconds(age), TimeSpan.FromSeconds(skew)));
        builder.Services.AddSingleton<IDiscoveryCursors, DiscoveryCursors>();
        builder.Services.AddScoped<IDiscoveryOwners, DiscoveryOwners>();
        builder.Services.TryAddScoped<IDiscoveryCapacitySource, UnsupportedDiscoveryCapacity>();
        foreach (var name in new[] { "discovery-facility", "discovery-payment" })
            builder.Services.AddHttpClient(name).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, ActivityHeadersPropagator = null });
        // Hosting.Diagnostics logs the raw query on request start/finish before endpoint middleware.
        // Suppress this message category so anonymous search origins and cursors stay out of application logs.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Services.AddRateLimiter(options =>
        {
            options.AddConcurrencyLimiter("parking-search", limits =>
            {
                limits.PermitLimit = 16;
                limits.QueueLimit = 0;
            });
            options.OnRejected = async (rejection, ct) =>
            {
                var ctx = rejection.HttpContext;
                ctx.Response.Headers.CacheControl = "no-store";
                ctx.Response.Headers.RetryAfter = "1";
                await Error(ctx, 429, "RATE_LIMITED").ExecuteAsync(ctx);
            };
        });
    }
    public static void MapParkingSearch(this WebApplication app)
    {
        app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api/parking-search"), branch =>
        {
            branch.Use(async (ctx, next) => { ctx.Response.Headers.CacheControl = "no-store"; await next(ctx); });
            branch.UseStatusCodePages(async status =>
            {
                var ctx = status.HttpContext;
                await Error(ctx, ctx.Response.StatusCode, ctx.Response.StatusCode == 404
                    ? "RESOURCE_NOT_FOUND" : "VALIDATION_FAILED").ExecuteAsync(ctx);
            });
        });
        app.UseRateLimiter();
        app.MapGet("/api/parking-search", async (HttpContext ctx, IMediator mediator) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            try
            {
                var query = Parse(ctx.Request);
                var data = await mediator.Send(query, ctx.RequestAborted);
                return Results.Json(new { success = true, data, traceId = ctx.TraceIdentifier });
            }
            catch (DiscoveryFailure error)
            {
                if (error.RetryAfter is int seconds) ctx.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                return Error(ctx, error.Status, error.Code);
            }
            catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { throw; }
            catch (Exception) { return Error(ctx, 500, "INTERNAL_ERROR"); }
        }).AllowAnonymous().RequireRateLimiting("parking-search");
    }
    private static IResult Error(HttpContext ctx, int status, string code) => Results.Json(new
        { success = false, code, message = "Parking search could not be completed.", errors = Array.Empty<object>(), traceId = ctx.TraceIdentifier },
        statusCode: status);

    private static SearchParking Parse(HttpRequest request)
    {
        if (request.ContentLength > 0 || request.Headers.TransferEncoding.Count != 0) throw DiscoveryFailure.Invalid();
        string? Get(string key) => !request.Query.TryGetValue(key, out var values) ? null :
            values.Count == 1 && !string.IsNullOrWhiteSpace(values[0]) ? values[0] : throw DiscoveryFailure.Invalid();
        double Number(string key, double? fallback = null) => Get(key) is not string value
            ? fallback ?? throw DiscoveryFailure.Invalid() : double.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number) ? number : throw DiscoveryFailure.Invalid();
        long? Money(string key) => Get(key) is not string value ? null :
            long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : throw DiscoveryFailure.Invalid();
        DateTimeOffset? Time(string key)
        {
            if (Get(key) is not string raw) return null;
            if (!raw.Contains('T') || !(raw.EndsWith('Z') || raw.Length > 6 && raw[^6] is '+' or '-' && raw[^3] == ':') ||
                !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) throw DiscoveryFailure.Invalid();
            return result.ToUniversalTime();
        }
        var flag = Get("availableOnly") is not string rawFlag ? false :
            bool.TryParse(rawFlag, out var b) ? b : throw DiscoveryFailure.Invalid();
        var limit = Get("limit") is not string rawLimit ? 20 :
            int.TryParse(rawLimit, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : throw DiscoveryFailure.Invalid();
        return new(new(Number("lat"), Number("lon"), Get("vehicleType") ?? throw DiscoveryFailure.Invalid(),
            Number("radiusKm", 5), Get("lotType"), flag, Time("startAt"), Time("endAt"), Get("timeZone") ?? "Asia/Ho_Chi_Minh",
            Money("minTotalVnd"), Money("maxTotalVnd"), limit), Get("cursor"));
    }
}
