using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionOwner;

public sealed class ProvisionOwnerCommandHandler(
    OwnerOnboardingService onboarding,
    ILogger<ProvisionOwnerCommandHandler> logger)
    : IRequestHandler<ProvisionOwnerCommand>
{
    public async Task Handle(ProvisionOwnerCommand request, CancellationToken ct)
    {
        logger.LogInformation("Provisioning owner for application {ApplicationId}", request.ApplicationId);
        await onboarding.ProvisionAsync(request.ApplicationId, request.Body, ct);
    }
}
