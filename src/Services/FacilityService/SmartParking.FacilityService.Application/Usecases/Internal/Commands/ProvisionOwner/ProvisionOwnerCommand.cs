using MediatR;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionOwner;

public sealed record ProvisionOwnerCommand(
    Guid ApplicationId,
    OwnerOnboardingBody Body) : IRequest;
