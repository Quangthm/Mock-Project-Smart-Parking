using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using SmartParking.OperationService.Application.Discovery;

namespace SmartParking.OperationService.Infrastructure.Clients;

public sealed class DiscoveryOrigins(Uri? facility, Uri? payment, string? paymentKey)
{
    public Uri? Facility { get; } = facility;
    public Uri? Payment { get; } = payment;
    public string? PaymentKey { get; } = paymentKey;
}
public sealed class DiscoveryOwners(IHttpClientFactory clients, DiscoveryOrigins origins) : IDiscoveryOwners
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict
    };
    private sealed record Envelope<T>(bool Success, T Data);

    public async Task<FacilityPage> Sites(SearchFilter filter, string? cursor, CancellationToken ct)
    {
        if (origins.Facility is null) throw DiscoveryFailure.Unavailable();
        var query = new Dictionary<string, string?>
        {
            ["lat"] = filter.Lat.ToString("R", CultureInfo.InvariantCulture),
            ["lon"] = filter.Lon.ToString("R", CultureInfo.InvariantCulture),
            ["radiusKm"] = filter.RadiusKm.ToString("R", CultureInfo.InvariantCulture),
            ["vehicleType"] = filter.VehicleType,
            ["limit"] = "100"
        };
        if (filter.LotType is not null) query["lotType"] = filter.LotType;
        if (cursor is not null) query["cursor"] = cursor;
        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(origins.Facility, QueryHelpers.AddQueryString("/api/public/parking-sites", query)));
        return await Read<FacilityPage>("discovery-facility", request, ct);
    }

    public async Task<PriceBatch> Prices(Guid[] ids, SearchFilter filter, DateTimeOffset at, CancellationToken ct)
    {
        if (origins.Payment is null || string.IsNullOrWhiteSpace(origins.PaymentKey)) throw DiscoveryFailure.Unavailable();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origins.Payment, "/internal/discovery/prices:read"));
        request.Headers.Add("X-Discovery-Service-Key", origins.PaymentKey);
        request.Content = JsonContent.Create(new { siteIds = ids, filter.VehicleType, at, filter.StartAt, filter.EndAt });
        return await Read<PriceBatch>("discovery-payment", request, ct);
    }

    private async Task<T> Read<T>(string name, HttpRequestMessage request, CancellationToken ct) where T : class
    {
        try
        {
            using var client = clients.CreateClient(name);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var status = (int)response.StatusCode;
                if (status is 404 or 409) throw DiscoveryFailure.Changed();
                if (status is 408 or 504) throw new DiscoveryFailure(504, "DEPENDENCY_TIMEOUT");
                if (status == 429)
                {
                    int? seconds = response.Headers.RetryAfter?.Delta is { } delay
                        ? (int)Math.Clamp(Math.Ceiling(delay.TotalSeconds), 1, 300) : null;
                    throw new DiscoveryFailure(429, "RATE_LIMITED", seconds);
                }
                throw DiscoveryFailure.Unavailable();
            }
            const int maxBytes = 512 * 1024;
            if (response.Content.Headers.ContentLength > maxBytes) throw DiscoveryFailure.Unavailable();
            await response.Content.LoadIntoBufferAsync(maxBytes, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length > maxBytes) throw DiscoveryFailure.Unavailable();
            var envelope = JsonSerializer.Deserialize<Envelope<T>>(bytes, Json);
            return envelope is { Success: true, Data: not null } ? envelope.Data : throw DiscoveryFailure.Unavailable();
        }
        catch (Exception e) when (e is HttpRequestException or IOException or JsonException or NotSupportedException)
        {
            throw DiscoveryFailure.Unavailable();
        }
    }
}
