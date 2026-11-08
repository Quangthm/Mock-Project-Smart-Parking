using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Chat;

namespace SmartParking.IoTService.Infrastructure.Assistant;

public sealed partial class OwnerReadClient(IHttpClientFactory clients, IHttpContextAccessor http, ICurrentActor current, TimeProvider clock, ReadToolOptions options) : IOwnerReadTools
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<VehicleSummary[]> Vehicles(CancellationToken ct)
    {
        var data = await Read("assistant-user-read", "/api/vehicles", ct);
        try
        {
            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > 1000) throw new JsonException();
            return data.EnumerateArray().Select(v =>
            {
                var id = v.GetProperty("id").GetGuid(); var type = v.GetProperty("vehicleType").GetString(); var label = v.GetProperty("rawPlate").GetString();
                if (id == Guid.Empty || type is not ("CAR" or "MOTORCYCLE") || string.IsNullOrWhiteSpace(label) || label.Length > 100) throw new JsonException();
                return new VehicleSummary(id, type, label);
            }).ToArray();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { throw new OwnerReadFailure("dependency_unavailable"); }
    }
    public async Task<ReservationPage> Reservations(DateTimeOffset? from, DateTimeOffset? to, string? cursor, CancellationToken ct)
    {
        var path = "/api/me/reservations?limit=19";
        if (from is not null) path += "&from=" + Uri.EscapeDataString(from.Value.ToString("O")) + "&to=" + Uri.EscapeDataString(to!.Value.ToString("O"));
        if (cursor is not null) path += "&cursor=" + Uri.EscapeDataString(cursor);
        var data = await Read("assistant-operations-read", path, ct);
        try
        {
            var page = data.Deserialize<ReservationPage>(Json) ?? throw new JsonException();
            if (page.Items is null || page.Items.Length > 19 || page.NextCursor?.Length > 2048 || page.AsOf == default || page.Items.Any(r => !Valid(r) || from is not null && !(r.StartAt < to && r.EndAt > from))) throw new JsonException();
            return page;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { throw new OwnerReadFailure("dependency_unavailable"); }
    }
    public async Task<ReservationDetail> Detail(Guid id, CancellationToken ct)
    {
        var data = await Read("assistant-operations-read", "/api/me/reservations/" + id.ToString("D"), ct);
        try
        {
            var detail = data.Deserialize<ReservationDetail>(Json) ?? throw new JsonException();
            if (!Valid(detail.Reservation) || detail.Reservation.Id != id || detail.AsOf == default) throw new JsonException();
            return detail;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { throw new OwnerReadFailure("dependency_unavailable"); }
    }
    private static bool Valid(ReservationSummary? r) => r is not null && r.Id != Guid.Empty && r.SiteId != Guid.Empty && r.EndAt > r.StartAt && r.AsOf != default &&
        !string.IsNullOrWhiteSpace(r.Status) && r.Status.Length <= 100 && r.Vehicle is not null && r.Vehicle.VehicleType is "CAR" or "MOTORCYCLE" &&
        !string.IsNullOrWhiteSpace(r.Vehicle.DisplayLabel) && r.Vehicle.DisplayLabel.Length <= 100;
    private async Task<JsonElement> Read(string client, string path, CancellationToken ct)
    {
        var actor = current.Actor;
        if (actor is null || actor.Role != "driver" || actor.ExpiresAt <= clock.GetUtcNow()) throw new OwnerReadFailure("login_required");
        AuthenticationHeaderValue? auth = null;
        if (client != SearchClient)
        {
            var bearer = http.HttpContext?.Request.Headers.Authorization.ToString();
            if (!AuthenticationHeaderValue.TryParse(bearer, out auth) || !auth.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(auth.Parameter))
                throw new OwnerReadFailure("login_required");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Authorization = auth;
            using var response = await clients.CreateClient(client).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new OwnerReadFailure(client == SearchClient && (int)response.StatusCode is 401 or 403 ? "dependency_unavailable" : (int)response.StatusCode switch
            { 400 => "invalid", 401 => "login_required", 403 => "forbidden", 404 => "not_found", 409 => "conflict", 429 => "rate_limit", 408 or 504 => "timeout", _ => "dependency_unavailable" });
            using var stream = await response.Content.ReadAsStreamAsync(deadline.Token); using var bytes = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, deadline.Token)) > 0)
            { if (bytes.Length + count > 262144) throw new OwnerReadFailure("dependency_unavailable"); bytes.Write(buffer, 0, count); }
            using var document = JsonDocument.Parse(bytes.ToArray());
            if (!document.RootElement.GetProperty("success").GetBoolean()) throw new OwnerReadFailure("dependency_unavailable");
            if (current.Actor?.AccountId != actor.AccountId || actor.ExpiresAt <= clock.GetUtcNow()) throw new OwnerReadFailure("login_required");
            return document.RootElement.GetProperty("data").Clone();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new OwnerReadFailure("timeout"); }
        catch (HttpRequestException) { throw new OwnerReadFailure("dependency_unavailable"); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { throw new OwnerReadFailure("dependency_unavailable"); }
    }
}
