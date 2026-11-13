using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;
using SmartParking.FacilityService.Persistence;

namespace SmartParking.FacilityService.API;

public static class DiscoveryCatalogueEndpoints
{
    public static void AddDiscoveryCatalogue(this WebApplicationBuilder builder)
    {
        builder.Services.AddDataProtection();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ICatalogueStore, DiscoveryCatalogueStore>();
        builder.Services.AddScoped<DiscoveryCatalogue>();
        builder.Services.AddHttpClient<IPublicationAuthority, PublicationAuthority>(client =>
        {
            if (Uri.TryCreate(builder.Configuration["Discovery:UserOrigin"], UriKind.Absolute, out var uri)
                && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.PathAndQuery == "/" && uri.Fragment.Length == 0)
                client.BaseAddress = uri;
            client.Timeout = TimeSpan.FromSeconds(5);
            client.MaxResponseContentBufferSize = 32768;
        }).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        // Hosting request-start messages contain the unredacted URI even before endpoint routing.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Services.AddRateLimiter(o =>
        {
            o.AddConcurrencyLimiter("discovery-catalogue", p => { p.PermitLimit = 16; p.QueueLimit = 0; });
            o.OnRejected = async (ctx, ct) =>
            {
                var response = ctx.HttpContext.Response;
                response.StatusCode = 429;
                response.Headers.CacheControl = "no-store";
                response.Headers.RetryAfter = "1";
                await response.WriteAsJsonAsync(new { success=false, code="RATE_LIMITED", message="Try again later.",
                    errors=Array.Empty<string>(), traceId=ctx.HttpContext.TraceIdentifier }, ct);
            };
        });
    }

    public static void MapDiscoveryCatalogue(this WebApplication app)
    {
        app.UseStatusCodePages(async context =>
        {
            var http = context.HttpContext;
            if (IsDiscoveryPath(http.Request.Path))
            {
                http.Response.Headers.CacheControl = "no-store";
                await http.Response.WriteAsJsonAsync(new { success=false, code=http.Response.StatusCode switch
                {
                    400 => "VALIDATION_FAILED", 405 => "METHOD_NOT_ALLOWED", 415 => "UNSUPPORTED_MEDIA_TYPE",
                    404 => "RESOURCE_NOT_FOUND", _ => "REQUEST_FAILED"
                }, message="Discovery request could not complete.", errors=Array.Empty<string>(), traceId=http.TraceIdentifier });
            }
        });
        app.UseRateLimiter();
        app.MapGet("/api/public/parking-sites", List).RequireRateLimiting("discovery-catalogue");
        app.MapPut("/api/parking-lots/{siteId:guid}/publication",
            (Guid siteId, PublishSite fields, DiscoveryCatalogue catalogue, HttpContext ctx, CancellationToken ct) =>
                Run(ctx, async () => { await catalogue.Publish(siteId, fields, ct); return Results.NoContent(); }));
        app.MapDelete("/api/parking-lots/{siteId:guid}/publication",
            (Guid siteId, DiscoveryCatalogue catalogue, HttpContext ctx, CancellationToken ct) =>
                Run(ctx, async () => { await catalogue.Withdraw(siteId, ct); return Results.NoContent(); }));
    }
    public static bool IsDiscoveryPath(PathString path) => path.StartsWithSegments("/api/public/parking-sites") ||
        path.StartsWithSegments("/api/parking-lots") && path.Value!.EndsWith("/publication", StringComparison.Ordinal);

    private sealed record Cursor(CatalogueFilter Filter, CataloguePosition Position, DateTimeOffset ExpiresAt);

    private static Task<IResult> List(HttpContext ctx, DiscoveryCatalogue catalogue, IDataProtectionProvider protection,
        TimeProvider clock, CancellationToken ct) => Run(ctx, async () =>
    {
        if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
            throw new StructureException("VALIDATION_FAILED", "This read does not accept a body.");
        var q = ctx.Request.Query;
        string? Read(string key) => q.TryGetValue(key, out var v)
            ? v.Count == 1 ? v[0] : throw new StructureException("VALIDATION_FAILED", "Duplicate query parameter.")
            : null;
        static double Number(string? s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            ? n : throw new StructureException("VALIDATION_FAILED", "Invalid numeric bound.");
        var limit = Read("limit");
        var f = new CatalogueFilter(Number(Read("lat")), Number(Read("lon")), Number(Read("radiusKm") ?? "5"),
            Read("vehicleType") ?? "", Read("lotType"), limit == null ? 50 :
                int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out var size) ? size : 0);
        DiscoveryCatalogue.ValidateFilter(f);
        var protector = protection.CreateProtector("Facility.PublicDiscovery.Cursor.v1");
        var token = Read("cursor");
        CataloguePosition? position = null;
        if (token != null)
        {
            try
            {
                if (token.Length is 0 or > 2048) throw new CryptographicException();
                var cursor = JsonSerializer.Deserialize<Cursor>(protector.Unprotect(token));
                if (cursor == null || cursor.Filter != f || cursor.Position == null) throw new CryptographicException();
                if (cursor.ExpiresAt <= clock.GetUtcNow()) throw new StructureException("CURSOR_EXPIRED", "Start a new search.");
                position = cursor.Position;
            }
            catch (Exception e) when (e is JsonException or CryptographicException or FormatException)
            { throw new StructureException("INVALID_CURSOR", "Invalid catalogue cursor."); }
        }
        var slice = await catalogue.List(f, position, ct);
        var items = slice.Items.Take(f.Limit).ToArray();
        var nextCursor = slice.Items.Length > items.Length ? protector.Protect(JsonSerializer.Serialize(
            new Cursor(f, new(slice.CatalogueVersion, items[^1].SiteId), clock.GetUtcNow().AddMinutes(10)))) : null;
        return Results.Ok(new { success=true, data=new { items, nextCursor, slice.CatalogueVersion, slice.AsOf },
            traceId=ctx.TraceIdentifier });
    });

    private static async Task<IResult> Run(HttpContext ctx, Func<Task<IResult>> operation)
    {
        ctx.Response.Headers.CacheControl = "no-store";
        try { return await operation(); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { throw; }
        catch (StructureException e)
        {
            var status = e.Code switch
            {
                "UNAUTHENTICATED" => 401, "FORBIDDEN" => 403, "SITE_NOT_FOUND" => 404,
                "SEARCH_CHANGED" or "CURSOR_EXPIRED" or "STRUCTURE_CONFLICT" => 409,
                "IDENTITY_UNAVAILABLE" or "CATALOGUE_UNAVAILABLE" => 503, _ => 400
            };
            return Error(ctx, status, e.Code, e.Message);
        }
        catch (TimeoutException) { return Error(ctx, 504, "DEPENDENCY_TIMEOUT", "Catalogue request timed out."); }
        catch (Exception e) when (e is NpgsqlException or InvalidOperationException or InvalidCastException)
        { return Error(ctx, 503, "DEPENDENCY_UNAVAILABLE", "Catalogue is unavailable."); }
        catch (Exception) { return Error(ctx, 500, "INTERNAL_ERROR", "Discovery request could not complete."); }
    }
    private static IResult Error(HttpContext ctx, int status, string code, string message) =>
        Results.Json(new { success=false, code, message, errors=Array.Empty<string>(), traceId=ctx.TraceIdentifier }, statusCode:status);
}
