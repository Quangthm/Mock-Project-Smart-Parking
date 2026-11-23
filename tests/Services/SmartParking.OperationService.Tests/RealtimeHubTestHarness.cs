using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Tests;

internal sealed class RealtimeHubTestHarness : IAsyncDisposable
{
    private const string Issuer = "SmartParking.UserService";
    private const string Audience = "SmartParking";
    private const string KeyId = "realtime-test-key-1";

    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly WebApplication _identityApp;
    private readonly WebApplication _operationApp;
    private readonly HttpClient _http;
    private readonly Func<Guid, string, (int StatusCode, Guid UserId, Guid TenantId)> _siteScopeHandler;

    private RealtimeHubTestHarness(
        RSA rsa,
        WebApplication identityApp,
        WebApplication operationApp,
        string operationBaseUrl,
        Func<Guid, string, (int StatusCode, Guid UserId, Guid TenantId)> siteScopeHandler)
    {
        _rsa = rsa;
        _signingKey = new RsaSecurityKey(rsa) { KeyId = KeyId };
        _identityApp = identityApp;
        _operationApp = operationApp;
        _siteScopeHandler = siteScopeHandler;
        _http = new HttpClient { BaseAddress = new Uri(operationBaseUrl) };
    }

    public string OperationBaseUrl => _http.BaseAddress!.ToString().TrimEnd('/');

    public static async Task<RealtimeHubTestHarness> StartAsync(
        Func<Guid, string, (int StatusCode, Guid UserId, Guid TenantId)>? siteScopeHandler = null,
        bool configureJwt = true,
        int clockSkewSeconds = 30,
        Func<string, (int StatusCode, Guid UserId, Guid[] TenantIds, (Guid SiteId, Guid TenantId)[] FacilitySites)>? ownerScopeHandler = null,
        Func<Guid, string, int>? driverLayoutHandler = null,
        bool enableSharedHub = false,
        CancellationToken ct = default)
    {
        var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(false);
        var jwksJson = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!)
                }
            }
        });

        var scopeCallback = siteScopeHandler ?? ((_, _) => (StatusCodes.Status403Forbidden, Guid.Empty, Guid.Empty));
        var ownerCallback = ownerScopeHandler ?? (_ => (StatusCodes.Status403Forbidden, Guid.Empty, [], []));
        var driverCallback = driverLayoutHandler ?? ((_, _) => StatusCodes.Status404NotFound);

        var identityBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        identityBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        var identityApp = identityBuilder.Build();
        identityApp.MapGet("/.well-known/jwks.json", () => Results.Text(jwksJson, "application/json"));
        identityApp.MapGet("/api/operators/site-scope", (HttpContext context, Guid siteId) =>
        {
            var auth = context.Request.Headers.Authorization.ToString();
            var outcome = scopeCallback(siteId, auth);
            if (outcome.StatusCode == StatusCodes.Status200OK)
            {
                return Results.Ok(new { userId = outcome.UserId, tenantIds = new[] { outcome.TenantId } });
            }
            return Results.StatusCode(outcome.StatusCode);
        });
        identityApp.MapGet("/api/auth/owner-scope", (HttpContext context) =>
        {
            var auth = context.Request.Headers.Authorization.ToString();
            var outcome = ownerCallback(auth);
            if (outcome.StatusCode == StatusCodes.Status200OK)
            {
                return Results.Ok(new
                {
                    userId = outcome.UserId,
                    tenantIds = outcome.TenantIds
                });
            }
            return Results.StatusCode(outcome.StatusCode);
        });
        identityApp.MapGet("/api/parking-lots", (HttpContext context) =>
        {
            var auth = context.Request.Headers.Authorization.ToString();
            var outcome = ownerCallback(auth);
            if (outcome.StatusCode == StatusCodes.Status200OK)
            {
                return Results.Ok(new
                {
                    success = true,
                    data = outcome.FacilitySites.Select(s => new { id = s.SiteId, tenantId = s.TenantId })
                });
            }
            return Results.StatusCode(outcome.StatusCode);
        });
        identityApp.MapGet("/api/parking-lots/{siteId:guid}/operational-layout", (HttpContext context, Guid siteId) =>
        {
            var auth = context.Request.Headers.Authorization.ToString();
            var status = driverCallback(siteId, auth);
            return status == StatusCodes.Status200OK
                ? Results.Ok(new { success = true, data = new { siteId } })
                : Results.StatusCode(status);
        });
        await identityApp.StartAsync(ct);
        var identityBaseUrl = identityApp.Urls.First().TrimEnd('/');

        var operationApp = OperationHost.Build(["--environment", "Testing"], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var settings = new Dictionary<string, string?>
            {
                ["Services:Key"] = "realtime-test-service-key-at-least-32-chars",
                ["Services:User"] = identityBaseUrl + "/",
                ["Services:Facility"] = identityBaseUrl + "/",
                ["Services:Parking"] = identityBaseUrl + "/",
                ["ConnectionStrings:Reservation"] = "Host=127.0.0.1;Port=1;Database=unused_realtime_test;Username=unused;Password=unused",
                ["ReservationAdmission:Enabled"] = "false",
                ["Messaging:Enabled"] = "false",
                ["Messaging:AuditEnabled"] = "false",
                ["Realtime:SharedMultiSiteHubEnabled"] = enableSharedHub ? "true" : "false",
                ["JwtValidation:ClockSkewSeconds"] = clockSkewSeconds.ToString()
            };
            if (configureJwt)
            {
                settings["JwtValidation:Issuer"] = Issuer;
                settings["JwtValidation:Audience"] = Audience;
                settings["JwtValidation:JwksUri"] = $"{identityBaseUrl}/.well-known/jwks.json";
                settings["JwtValidation:AllowInsecureTransport"] = "true";
            }
            else
            {
                settings["JwtValidation:Issuer"] = null;
                settings["JwtValidation:Audience"] = null;
                settings["JwtValidation:JwksUri"] = null;
                settings["JwtValidation:AllowInsecureTransport"] = "false";
            }
            builder.Configuration.AddInMemoryCollection(settings);
        });
        await operationApp.StartAsync(ct);
        var operationBaseUrl = operationApp.Urls.First().TrimEnd('/');

        return new RealtimeHubTestHarness(rsa, identityApp, operationApp, operationBaseUrl, scopeCallback);
    }

    public string CreateToken(Guid userId, Guid accountId, string role, TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;
        var validFor = lifetime ?? TimeSpan.FromMinutes(10);
        var notBefore = validFor < TimeSpan.Zero ? now.Add(validFor).AddMinutes(-5) : now.AddMinutes(-1);
        var expires = now.Add(validFor);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("account_id", accountId.ToString()),
                new Claim("role", role)
            ],
            notBefore: notBefore,
            expires: expires,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(HttpStatusCode StatusCode, string ConnectionToken, string RawBody)> NegotiateAsync(
        string siteSegment,
        string? bearerToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/ws/parking-lots/{Uri.EscapeDataString(siteSegment)}/layout/negotiate?negotiateVersion=1");
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return (response.StatusCode, string.Empty, body);

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.TryGetProperty("connectionToken", out var prop)
            ? prop.GetString() ?? string.Empty
            : string.Empty;
        return (response.StatusCode, token, body);
    }

    public async Task<(HttpStatusCode StatusCode, string ConnectionToken, string RawBody)> NegotiateSharedAsync(
        string? bearerToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/ws/parking-lots/layout/negotiate?negotiateVersion=1");
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return (response.StatusCode, string.Empty, body);

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.TryGetProperty("connectionToken", out var prop)
            ? prop.GetString() ?? string.Empty
            : string.Empty;
        return (response.StatusCode, token, body);
    }

    public async Task<ClientWebSocket> ConnectSharedWebSocketAsync(
        string? connectionToken,
        string? queryAccessToken,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(connectionToken))
            query.Add($"id={Uri.EscapeDataString(connectionToken)}");
        if (!string.IsNullOrWhiteSpace(queryAccessToken))
            query.Add($"access_token={Uri.EscapeDataString(queryAccessToken)}");

        var wsBase = OperationBaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
        var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        var uri = new Uri($"{wsBase}/ws/parking-lots/layout{suffix}");

        var ws = new ClientWebSocket();
        await ws.ConnectAsync(uri, ct);
        var handshake = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
        await ws.SendAsync(handshake, WebSocketMessageType.Text, true, ct);
        return ws;
    }

    public async Task InvokeHubAsync(
        ClientWebSocket ws,
        string invocationId,
        string target,
        object?[] arguments,
        CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = 1,
            invocationId,
            target,
            arguments
        }) + "\u001e";
        await ws.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, ct);
    }

    public async Task<ClientWebSocket> ConnectWebSocketAsync(
        string siteSegment,
        string? connectionToken,
        string? queryAccessToken,
        string? headerBearerToken = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(connectionToken))
            query.Add($"id={Uri.EscapeDataString(connectionToken)}");
        if (!string.IsNullOrWhiteSpace(queryAccessToken))
            query.Add($"access_token={Uri.EscapeDataString(queryAccessToken)}");

        var wsBase = OperationBaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);
        var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        var uri = new Uri($"{wsBase}/ws/parking-lots/{Uri.EscapeDataString(siteSegment)}/layout{suffix}");

        var ws = new ClientWebSocket();
        if (!string.IsNullOrWhiteSpace(headerBearerToken))
            ws.Options.SetRequestHeader("Authorization", $"Bearer {headerBearerToken}");

        await ws.ConnectAsync(uri, ct);
        var handshake = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
        await ws.SendAsync(handshake, WebSocketMessageType.Text, true, ct);
        return ws;
    }

    public async Task<IReadOnlyList<JsonDocument>> ReadSignalRFramesAsync(
        ClientWebSocket ws,
        int maxFrames = 10,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var results = new List<JsonDocument>();
        var buffer = new byte[16 * 1024];
        var pending = new StringBuilder();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(3));

        try
        {
            while (results.Count < maxFrames && ws.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                var message = new MemoryStream();
                WebSocketReceiveResult received;
                do
                {
                    received = await ws.ReceiveAsync(segment, cts.Token);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        FlushFrames(pending, results);
                        return results;
                    }
                    message.Write(buffer, 0, received.Count);
                }
                while (!received.EndOfMessage);

                pending.Append(Encoding.UTF8.GetString(message.ToArray()));
                FlushFrames(pending, results);
                if (results.Any(doc => doc.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 7))
                    return results;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Expected timeout when draining idle frames.
        }

        return results;
    }

    public async Task<IReadOnlyList<JsonDocument>> ReadUntilCompletionAsync(
        ClientWebSocket ws,
        string invocationId,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        var results = new List<JsonDocument>();
        var buffer = new byte[16 * 1024];
        var pending = new StringBuilder();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(5));

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                var message = new MemoryStream();
                WebSocketReceiveResult received;
                do
                {
                    received = await ws.ReceiveAsync(segment, cts.Token);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        FlushFrames(pending, results);
                        return results;
                    }
                    message.Write(buffer, 0, received.Count);
                }
                while (!received.EndOfMessage);

                pending.Append(Encoding.UTF8.GetString(message.ToArray()));
                FlushFrames(pending, results);
                if (results.Any(doc =>
                    (doc.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 7) ||
                    (doc.RootElement.TryGetProperty("type", out var ctType) && ctType.GetInt32() == 3 &&
                     doc.RootElement.TryGetProperty("invocationId", out var idProp) && idProp.GetString() == invocationId)))
                {
                    return results;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timed out waiting for completion frame.
        }

        return results;
    }

    public async Task<(HttpStatusCode FirstGetStatus, HttpStatusCode PostStatus, string PollBody)> LongPollExchangeAsync(
        string siteSegment,
        string connectionToken,
        string bearerToken,
        CancellationToken ct = default)
    {
        var path = $"/ws/parking-lots/{Uri.EscapeDataString(siteSegment)}/layout?id={Uri.EscapeDataString(connectionToken)}";

        using var firstGet = new HttpRequestMessage(HttpMethod.Get, path);
        firstGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var firstResponse = await _http.SendAsync(firstGet, ct);
        if (!firstResponse.IsSuccessStatusCode)
            return (firstResponse.StatusCode, 0, await firstResponse.Content.ReadAsStringAsync(ct));

        using var handshakePost = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("{\"protocol\":\"json\",\"version\":1}\u001e", Encoding.UTF8, "text/plain")
        };
        handshakePost.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var postResponse = await _http.SendAsync(handshakePost, ct);

        var bodyBuilder = new StringBuilder();
        for (var poll = 0; poll < 4; poll++)
        {
            using var nextGet = new HttpRequestMessage(HttpMethod.Get, path);
            nextGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var nextResponse = await _http.SendAsync(nextGet, ct);
            if (!nextResponse.IsSuccessStatusCode)
            {
                bodyBuilder.Append(await nextResponse.Content.ReadAsStringAsync(ct));
                break;
            }
            var chunk = await nextResponse.Content.ReadAsStringAsync(ct);
            bodyBuilder.Append(chunk);
            var accumulated = bodyBuilder.ToString();
            if (accumulated.Contains("SnapshotRequired", StringComparison.Ordinal) ||
                accumulated.Contains("\"type\":7", StringComparison.Ordinal))
                break;
        }

        return (firstResponse.StatusCode, postResponse.StatusCode, bodyBuilder.ToString());
    }

    public Task BroadcastAsync(OutboxMessage message, CancellationToken ct = default) =>
        _operationApp.Services.GetRequiredService<IRealtimeLayoutBroadcaster>().BroadcastAsync(message, ct);

    private static void FlushFrames(StringBuilder pending, List<JsonDocument> results)
    {
        var text = pending.ToString();
        var index = text.IndexOf('\u001e');
        if (index < 0) return;

        var start = 0;
        while (index >= 0)
        {
            var raw = text[start..index];
            if (!string.IsNullOrWhiteSpace(raw))
                results.Add(JsonDocument.Parse(raw));
            start = index + 1;
            index = text.IndexOf('\u001e', start);
        }

        pending.Clear();
        if (start < text.Length)
            pending.Append(text[start..]);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _operationApp.StopAsync();
        await _operationApp.DisposeAsync();
        await _identityApp.StopAsync();
        await _identityApp.DisposeAsync();
        _rsa.Dispose();
    }
}
