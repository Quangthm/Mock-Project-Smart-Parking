using MediatR;
using SmartParking.IoTService.Application.Assistant.Authorization;

namespace SmartParking.IoTService.Application.Assistant;

public sealed record GetAssistantCapabilitiesQuery : IRequest<AssistantCapabilities>;
public sealed record AssistantFeatures(bool Faq, bool GetMyVehicles, bool SearchParking,
    bool ReadMyReservations, bool ReadMySession, bool ReadMyMoney, bool PrepareReservation, bool CreateSupportTicket);
public sealed record AssistantCapabilities(bool Enabled, string ContractVersion, string[] Locales,
    int MaxMessageLength, AssistantFeatures Features);

public sealed class GetAssistantCapabilitiesHandler(ICurrentActor currentActor, Chat.ChatReadiness? readiness = null, TimeProvider? clock = null, Chat.ReadToolOptions? reads = null)
    : IRequestHandler<GetAssistantCapabilitiesQuery, AssistantCapabilities>
{
    public async Task<AssistantCapabilities> Handle(GetAssistantCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var actor = currentActor.Actor ?? throw new AssistantAccessException(false);
        if (actor.Role != "driver") throw new AssistantAccessException(true);
        var enabled = false;
        if (readiness is not null)
        {
            try { await readiness.Require(cancellationToken); enabled = true; }
            catch (Chat.ChatFailure ex) when (ex.Code is "ASSISTANT_DISABLED" or "RETRIEVAL_UNAVAILABLE") { }
        }
        if (clock is not null && actor.ExpiresAt <= clock.GetUtcNow()) throw new AssistantAccessException(false);
        return new AssistantCapabilities(enabled, readiness is null ? "0.3.0" : "0.4.1", ["vi", "en"], 4000,
            new(enabled, enabled && reads?.Vehicles == true, enabled && reads?.Search == true, enabled && reads?.Reservations == true, false, false, false, false));
    }
}
