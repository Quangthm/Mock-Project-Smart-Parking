using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Chat;

namespace SmartParking.IoTService.Infrastructure.Assistant;

public sealed partial class OwnerReadClient(IHttpClientFactory clients, IHttpContextAccessor http, ICurrentActor current, TimeProvider clock, ReadToolOptions options, IDataProtectionProvider protection) : IOwnerReadTools
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
    private sealed record BookingCursor(Guid Account, DateTimeOffset? From, DateTimeOffset? To, DateTimeOffset CreatedAt, Guid Id);
    private IDataProtector CursorProtector => protection.CreateProtector("Assistant.BookingCursor.v1");
    public async Task<ReservationPage> Reservations(DateTimeOffset? from, DateTimeOffset? to, string? cursor, CancellationToken ct)
    {
        if ((from is null) != (to is null) || from is not null && to <= from) throw new OwnerReadFailure("invalid");
        var actor = current.Actor ?? throw new OwnerReadFailure("login_required");
        BookingCursor? before = null;
        if (cursor is not null)
        {
            try
            {
                if (cursor.Length > 2048) throw new FormatException();
                before = JsonSerializer.Deserialize<BookingCursor>(CursorProtector.Unprotect(cursor), Json);
                if (before is null || before.Account != actor.AccountId || before.From != from || before.To != to ||
                    before.Id == Guid.Empty || before.CreatedAt == default) throw new FormatException();
            }
            catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
            { throw new OwnerReadFailure("invalid"); }
        }
        var path = "/api/me/reservations?pageSize=20";
        if (from is not null) path += "&from=" + Uri.EscapeDataString(from.Value.ToString("O")) + "&to=" + Uri.EscapeDataString(to!.Value.ToString("O"));
        if (before is not null) path += "&before=" + Uri.EscapeDataString(before.CreatedAt.ToString("O")) + "&beforeId=" + before.Id;
        var data = await Read("assistant-operations-read", path, ct);
        try
        {
            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > 20) throw new JsonException();
            var rows = data.Deserialize<BookingRead[]>(Json) ?? throw new JsonException();
            var observedAt = clock.GetUtcNow();
            var items = rows.Select(row => Summary(row, observedAt)).ToArray();
            for (var i = 0; i < rows.Length; i++)
            {
                var priorAt = i == 0 ? before?.CreatedAt : rows[i - 1].CreatedAt;
                var priorId = i == 0 ? before?.Id : rows[i - 1].ReservationId;
                if (priorAt is not null && (rows[i].CreatedAt > priorAt || rows[i].CreatedAt == priorAt && rows[i].ReservationId.CompareTo(priorId!.Value) >= 0)) throw new JsonException();
                if (from is not null && !(rows[i].Start < to && rows[i].End > from)) throw new JsonException();
            }
            if (rows.Select(r => r.ReservationId).Distinct().Count() != rows.Length) throw new JsonException();
            var next = rows.Length == 20 ? CursorProtector.Protect(JsonSerializer.Serialize(
                new BookingCursor(actor.AccountId, from, to, rows[18].CreatedAt, rows[18].ReservationId), Json)) : null;
            return new(items.Take(19).ToArray(), next, observedAt);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { throw new OwnerReadFailure("dependency_unavailable"); }
    }
    private static ReservationSummary Summary(BookingRead? row, DateTimeOffset observedAt)
    {
        if (row is null || row.CreatedAt == default) throw new JsonException();
        var summary = new ReservationSummary(row.ReservationId, row.SiteId,
            row.Vehicle is null ? null : new(row.Vehicle.VehicleType, row.Vehicle.DisplayLabel),
            row.Start, row.End, row.State, observedAt);
        if (!Valid(summary)) throw new JsonException();
        return summary;
    }
    private sealed record BookingRead(Guid ReservationId, string State, Guid SiteId, DateTimeOffset Start, DateTimeOffset End,
        DateTimeOffset CreatedAt, BookingVehicleRead? Vehicle);
    private sealed record BookingVehicleRead(string VehicleType, string? DisplayLabel);
    public async Task<ReservationDetail> Detail(Guid id, CancellationToken ct)
    {
        var data = await Read("assistant-operations-read", "/api/me/reservations/" + id.ToString("D"), ct);
        try
        {
            var observedAt = clock.GetUtcNow();
            var summary = Summary(data.Deserialize<BookingRead>(Json), observedAt);
            if (summary.Id != id) throw new JsonException();
            return new(summary, observedAt);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { throw new OwnerReadFailure("dependency_unavailable"); }
    }
    private static bool Valid(ReservationSummary? r) => r is not null && r.Id != Guid.Empty && r.SiteId != Guid.Empty && r.EndAt > r.StartAt && r.AsOf != default &&
        !string.IsNullOrWhiteSpace(r.Status) && r.Status.Length <= 100 && (r.Vehicle is null || (r.Vehicle.VehicleType is "CAR" or "MOTORCYCLE") &&
        (r.Vehicle.DisplayLabel is null || !string.IsNullOrWhiteSpace(r.Vehicle.DisplayLabel) && r.Vehicle.DisplayLabel.Length <= 100));
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
