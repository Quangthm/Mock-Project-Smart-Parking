using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.API.Hubs;

namespace SmartParking.OperationService.API.Realtime;

public sealed class SignalRLayoutRealtimeBroadcaster(
    IHubContext<ParkingLotLayoutHub> hub,
    IConfiguration? configuration = null) : IRealtimeLayoutBroadcaster
{
    public async Task BroadcastAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var siteId = message.SiteId;
        var parsedPayload = ParsePayload(message.Payload);

        var operatorEnvelope = new
        {
            eventId = message.EventId,
            eventType = message.EventType,
            occurredAt = message.OccurredAt,
            sequenceNumber = message.SequenceNumber,
            siteId,
            payload = parsedPayload
        };
        await hub.Clients.Group($"site:{siteId}:operators")
            .SendAsync(message.EventType, operatorEnvelope, cancellationToken);

        if (configuration?.GetValue<bool>("Realtime:SharedMultiSiteHubEnabled") != true ||
            IsOperationsOnlyBookingsEvent(message.Payload))
            return;

        var ownerEnvelope = new
        {
            eventId = message.EventId,
            eventType = message.EventType,
            schemaVersion = 1,
            occurredAt = message.OccurredAt,
            sequenceNumber = message.SequenceNumber,
            siteId,
            payload = parsedPayload
        };
        await hub.Clients.Group($"site:{siteId}:owners")
            .SendAsync(message.EventType, ownerEnvelope, cancellationToken);

        // Redacted Driver envelope: omit tenantId, slotId, sessionId, licensePlate, and customer fields.
        var driverEnvelope = new
        {
            eventId = message.EventId,
            eventType = "SnapshotRequired",
            schemaVersion = 1,
            occurredAt = message.OccurredAt,
            siteId,
            payload = new
            {
                schemaVersion = 1,
                affectedResources = new[] { "capacity-summary", "sanitized-detail" }
            }
        };
        await hub.Clients.Group($"site:{siteId}:drivers")
            .SendAsync("SnapshotRequired", driverEnvelope, cancellationToken);
    }

    private static bool IsOperationsOnlyBookingsEvent(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return false;
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var prop in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(prop.Name, "affectedResources", StringComparison.OrdinalIgnoreCase) ||
                    prop.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var items = prop.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToArray();

                return items.Length > 0 &&
                       items.All(item => string.Equals(item!.Trim(), "bookings", StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static object? ParsePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return payload;
        }
    }
}
