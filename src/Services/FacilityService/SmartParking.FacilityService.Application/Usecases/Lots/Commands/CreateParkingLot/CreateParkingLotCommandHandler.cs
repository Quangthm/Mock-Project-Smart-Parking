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

        var maxFloors = ResolveMaxFloors(request.LotType, request.ModelFileName, request.MaxFloors);
        var lotType = string.IsNullOrWhiteSpace(request.LotType) ? null : request.LotType.Trim();
        var structure = new ParkingStructure(
            new SiteProfile(Guid.NewGuid(), owner.TenantId, "", "", "", null, null, true, 0,
                LotType: lotType, ModelFileName: request.ModelFileName, MaxFloors: maxFloors));
        structure.UpdateProfile(request.Code, request.Name, request.Address, request.Latitude, request.Longitude);
        return await repository.CreateAsync(owner, structure.Site, ct);
    }

    // Known parking models map to a fixed number of floors. When a template is supplied we derive
    // the ceiling from it; when the caller also supplies MaxFloors it may not exceed the template.
    private static int? ResolveMaxFloors(string? lotType, string? modelFileName, int? requested)
    {
        var ceiling = ParkingModelFloorCeilings.Resolve(lotType, modelFileName);
        if (requested is not { } value) return ceiling;
        if (value < 1) throw new StructureException("INVALID_STRUCTURE", "Max floors must be at least 1.");
        if (ceiling is { } limit && value > limit)
            throw new StructureException("INVALID_STRUCTURE", $"The selected parking model supports at most {limit} floor(s).");
        return value;
    }
}
