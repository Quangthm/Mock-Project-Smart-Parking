using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.API.Hubs;

namespace SmartParking.OperationService.API.Realtime;

public sealed class SignalRLayoutRealtimeBroadcaster(IHubContext<ParkingLotLayoutHub> hub) : IRealtimeLayoutBroadcaster
{
    public async Task BroadcastAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var siteId = message.SiteId;
        var envelope = new
        {
            eventId = message.EventId,
            eventType = message.EventType,
            occurredAt = message.OccurredAt,
            sequenceNumber = message.SequenceNumber,
            siteId = message.SiteId,
            payload = ParsePayload(message.Payload)
        };
        await hub.Clients.Group($"site:{siteId}:operators")
            .SendAsync(message.EventType, envelope, cancellationToken);
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

public sealed class SignalRCheckoutNotifier(IHubContext<ParkingLotLayoutHub> hub) : IRealtimeCheckoutNotifier
{
    public Task CheckoutCompletedAsync(
        Guid siteId, Guid sessionId, Guid? reservationId, Guid paymentOrderId, CancellationToken ct) =>
        hub.Clients.Group($"site:{siteId}:operators").SendAsync("CheckoutCompleted", new
        {
            siteId,
            sessionId,
            reservationId,
            referenceId = reservationId ?? sessionId,
            paymentOrderId,
            status = "COMPLETED"
        }, ct);
}
