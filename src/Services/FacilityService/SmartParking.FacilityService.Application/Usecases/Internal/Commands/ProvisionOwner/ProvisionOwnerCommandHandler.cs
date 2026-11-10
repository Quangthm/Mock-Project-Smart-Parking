using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionOwner;

public sealed class ProvisionOwnerCommandHandler(
    IOwnerOnboardingStore store,
    ILogger<ProvisionOwnerCommandHandler> logger)
    : IRequestHandler<ProvisionOwnerCommand>
{
    public async Task Handle(ProvisionOwnerCommand request, CancellationToken ct)
    {
        logger.LogInformation("Provisioning owner for application {ApplicationId}", request.ApplicationId);
        var body = request.Body;
        if (request.ApplicationId == Guid.Empty || body.TenantId != request.ApplicationId ||
            string.IsNullOrWhiteSpace(body.Name) || body.Name.Length > 255 ||
            string.IsNullOrWhiteSpace(body.Email) || body.Email.Length > 255 ||
            string.IsNullOrWhiteSpace(body.Phone) || body.Phone.Length > 20 ||
            body.Lot.ValueKind != JsonValueKind.Object ||
            !body.Lot.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(name.GetString()) || name.GetString()!.Length > 255 ||
            !body.Lot.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(address.GetString()) || address.GetString()!.Length > 2000)
        {
            throw new StructureException("INVALID_ONBOARDING", "Invalid onboarding site.");
        }

        await store.ProvisionAsync(request.ApplicationId, request.Body, ct);
    }
}
