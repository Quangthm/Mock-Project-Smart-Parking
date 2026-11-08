using MediatR;
using SmartParking.IoTService.Application.Assistant.Authorization;

namespace SmartParking.IoTService.Application.Assistant;

public sealed record GetAssistantCapabilitiesQuery : IRequest<AssistantCapabilities>;
public sealed record AssistantFeatures(bool Faq, bool GetMyVehicles, bool SearchParking,
    bool ReadMyReservations, bool ReadMySession, bool ReadMyMoney, bool PrepareReservation, bool CreateSupportTicket);
public sealed record AssistantCapabilities(bool Enabled, string ContractVersion, string[] Locales,
    int MaxMessageLength, AssistantFeatures Features);

public sealed class GetAssistantCapabilitiesHandler(ICurrentActor currentActor)
    : IRequestHandler<GetAssistantCapabilitiesQuery, AssistantCapabilities>
{
    public Task<AssistantCapabilities> Handle(GetAssistantCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var actor = currentActor.Actor ?? throw new AssistantAccessException(false);
        if (actor.Role != "driver") throw new AssistantAccessException(true);
        // R1 has no storage, published corpus or provider. Never advertise the old scaffold as ready.
        return Task.FromResult(new AssistantCapabilities(false, "0.3.0", ["vi", "en"], 4000,
            new(false, false, false, false, false, false, false, false)));
    }
}
