using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.API;

public sealed record OwnReadJwt(string Issuer, string Audience, string PublicKeyPem);
public sealed class HttpReservationReader(IHttpContextAccessor accessor) : IReservationReader
{ public ReservationReader? Actor => accessor.HttpContext?.Items[typeof(ReservationReader)] as ReservationReader; }

// Uses the already installed IdentityModel validator; no handwritten signature verification or new package.
public sealed class OwnReadAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, OwnReadJwt settings, TimeProvider clock) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        try
        {
            if (string.IsNullOrWhiteSpace(settings.Issuer) || string.IsNullOrWhiteSpace(settings.Audience) ||
                !PemEncoding.TryFind(settings.PublicKeyPem, out var pem) || settings.PublicKeyPem[pem.Label] is not ("PUBLIC KEY" or "RSA PUBLIC KEY"))
                return AuthenticateResult.Fail("Unavailable trust configuration.");
            using var rsa = RSA.Create(); rsa.ImportFromPem(settings.PublicKeyPem);
            if (rsa.KeySize < 2048) return AuthenticateResult.Fail("Invalid key.");
            var handler = new JsonWebTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 16384 };
            var result = await handler.ValidateTokenAsync(header[7..], new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = settings.Issuer, ValidateAudience = true, ValidAudience = settings.Audience,
                IgnoreTrailingSlashWhenValidatingAudience = false, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)), RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ValidateLifetime = true, RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero, NameClaimType = "sub", RoleClaimType = "role",
                LifetimeValidator = (nbf, exp, _, _) => exp > clock.GetUtcNow().UtcDateTime && (!nbf.HasValue || nbf <= clock.GetUtcNow().UtcDateTime && nbf < exp)
            });
            if (!result.IsValid || result.SecurityToken is not JsonWebToken token) return AuthenticateResult.Fail("Invalid token.");
            using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
            var fields = payload.RootElement.EnumerateObject().ToArray();
            foreach (var name in new[] { "sub", "account_id", "jti", "role", "exp" })
                if (fields.Count(p => p.Name == name) != 1) return AuthenticateResult.Fail("Invalid identity.");
            foreach (var name in new[] { "sub", "account_id", "jti", "role" })
                if (payload.RootElement.GetProperty(name).ValueKind != JsonValueKind.String) return AuthenticateResult.Fail("Invalid identity.");
            if (!Guid.TryParse(payload.RootElement.GetProperty("account_id").GetString(), out var account) || account == Guid.Empty ||
                !Guid.TryParse(payload.RootElement.GetProperty("sub").GetString(), out var user) || user == Guid.Empty ||
                !Guid.TryParse(payload.RootElement.GetProperty("jti").GetString(), out var jti) || jti == Guid.Empty)
                return AuthenticateResult.Fail("Invalid identity.");
            Context.Items[typeof(ReservationReader)] = new ReservationReader(account, new DateTimeOffset(token.ValidTo, TimeSpan.Zero));
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(result.ClaimsIdentity), Scheme.Name));
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException or JsonException or InvalidOperationException or FormatException)
        { return AuthenticateResult.Fail("Invalid token."); }
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) => OwnReadErrors.Write(Context, 401, "INVALID_TOKEN");
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) => OwnReadErrors.Write(Context, 403, "FORBIDDEN");
}

public sealed class ReservationReadCursors(IDataProtectionProvider protection) : IReservationReadCursors
{
    private readonly IDataProtector protector = protection.CreateProtector("Operations.OwnReservations.v1");
    public string Encode(ReservationCursor cursor) => protector.Protect(JsonSerializer.Serialize(cursor));
    public ReservationCursor Decode(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > 2048) throw new OwnReadFailure(400, "INVALID_CURSOR");
        try { return JsonSerializer.Deserialize<ReservationCursor>(protector.Unprotect(cursor)) ?? throw new OwnReadFailure(400, "INVALID_CURSOR"); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { throw new OwnReadFailure(400, "INVALID_CURSOR"); }
    }
}
public sealed class OwnReadErrors : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (!context.Request.Path.StartsWithSegments("/api/me/reservations") || context.Response.HasStarted) return false;
        var (status, code) = exception switch
        {
            OwnReadFailure e => (e.Status, e.Code),
            OperationCanceledException => (504, "DEPENDENCY_TIMEOUT"),
            System.Data.Common.DbException => (503, "DEPENDENCY_UNAVAILABLE"),
            _ => (500, "INTERNAL_ERROR")
        };
        await Write(context, status, code); return true;
    }
    public static Task Write(HttpContext ctx, int status, string code)
    {
        ctx.Response.StatusCode = status; ctx.Response.Headers.CacheControl = "no-store";
        if (status == 401) ctx.Response.Headers.WWWAuthenticate = "Bearer";
        return ctx.Response.WriteAsJsonAsync(new { success = false, code, message = "Reservation read could not be completed.", errors = Array.Empty<object>(), traceId = ctx.TraceIdentifier }, ctx.RequestAborted);
    }
}
public static class OwnReservationHosting
{
    public static void AddOwnReservationReads(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton(new OwnReadJwt(builder.Configuration["OwnReservationReads:Issuer"] ?? "",
            builder.Configuration["OwnReservationReads:Audience"] ?? "", builder.Configuration["OwnReservationReads:PublicKeyPem"] ?? ""));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IReservationReader, HttpReservationReader>();
        builder.Services.TryAddScoped<IOwnReservationStore, OwnReservationStore>();
        builder.Services.AddDataProtection();
        builder.Services.TryAddSingleton<IReservationReadCursors, ReservationReadCursors>();
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, OwnReadAuthentication>("OwnRead", _ => { });
        builder.Services.AddAuthorization(o => o.AddPolicy("OwnReservationDriver", p => p.AddAuthenticationSchemes("OwnRead").RequireAuthenticatedUser().RequireRole("driver")));
        builder.Services.AddExceptionHandler<OwnReadErrors>();
    }
    public static void UseOwnReservationReads(this WebApplication app)
    {
        app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api/me/reservations"), branch =>
        {
            branch.UseExceptionHandler(_ => { });
            branch.Use(async (ctx, next) => { ctx.Response.Headers.CacheControl = "no-store"; await next(ctx); });
            branch.UseStatusCodePages(async ctx => await OwnReadErrors.Write(ctx.HttpContext, ctx.HttpContext.Response.StatusCode,
                ctx.HttpContext.Response.StatusCode == 404 ? "RESOURCE_NOT_FOUND" : "VALIDATION_FAILED"));
        });
        app.UseAuthentication(); app.UseAuthorization();
    }
    public static void MapOwnReservationReads(this WebApplication app)
    {
        var group = app.MapGroup("/api/me/reservations").RequireAuthorization("OwnReservationDriver");
        group.MapGet("", async (HttpContext ctx, IMediator mediator) =>
        {
            CheckQuery(ctx, ["from", "to", "limit", "cursor"]);
            var limit = ctx.Request.Query.TryGetValue("limit", out var raw) ? int.TryParse(raw, out var n) ? n : 0 : 20;
            var result = await mediator.Send(new ListOwnReservations(Date(ctx, "from"), Date(ctx, "to"), limit,
                ctx.Request.Query.TryGetValue("cursor", out var cursor) ? cursor.ToString() : null), ctx.RequestAborted);
            return Results.Json(new { success = true, data = result, traceId = ctx.TraceIdentifier });
        });
        group.MapGet("/{id}", async (string id, HttpContext ctx, IMediator mediator) =>
        {
            CheckQuery(ctx, []);
            if (!Guid.TryParse(id, out var key)) throw new OwnReadFailure(400, "VALIDATION_FAILED");
            var result = await mediator.Send(new GetOwnReservation(key), ctx.RequestAborted);
            return Results.Json(new { success = true, data = result, traceId = ctx.TraceIdentifier });
        });
    }
    private static void CheckQuery(HttpContext ctx, string[] allowed)
    {
        if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.TransferEncoding.Count > 0 || ctx.Request.Query.Any(q => !allowed.Contains(q.Key) || q.Value.Count != 1))
            throw new OwnReadFailure(400, "VALIDATION_FAILED");
    }
    private static DateTimeOffset? Date(HttpContext ctx, string name)
    {
        if (!ctx.Request.Query.TryGetValue(name, out var raw)) return null;
        var value = raw.ToString();
        if (!(value.EndsWith('Z') || value.Length >= 6 && value[^3] == ':' && value[^6] is '+' or '-') ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw new OwnReadFailure(400, "VALIDATION_FAILED");
        return date.ToUniversalTime();
    }
}
