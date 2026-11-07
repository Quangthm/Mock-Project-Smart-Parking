using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Lots.Commands.CreateParkingLot;

public sealed class CreateParkingLotCommandHandler(
    IOwnerScopeResolver scopeResolver,
    IOwnerAuthorizer owners,
    IParkingStructureRepository repository,
    ILogger<CreateParkingLotCommandHandler> logger)
    : IRequestHandler<CreateParkingLotCommand, SiteProfile>
{
    public async Task<SiteProfile> Handle(CreateParkingLotCommand request, CancellationToken ct)
    {
        logger.LogInformation("Creating parking lot with Code {SiteCode}", request.Code);
        var owner = await scopeResolver.ResolveAsync(request.TenantId, ct);
        if (!await owners.IsOwnerAsync(owner, ct))
            throw new StructureException("FORBIDDEN", "Current parking management access is required.");

        var structure = new ParkingStructure(
            new SiteProfile(Guid.NewGuid(), owner.TenantId, "", "", "", null, null, true, 0));
        structure.UpdateProfile(request.Code, request.Name, request.Address, request.Latitude, request.Longitude);
        return await repository.CreateAsync(owner, structure.Site, ct);
    }
}
