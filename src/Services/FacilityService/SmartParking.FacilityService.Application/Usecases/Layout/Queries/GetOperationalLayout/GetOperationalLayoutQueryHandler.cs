using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperationalLayout;

public sealed class GetOperationalLayoutQueryHandler(
    ParkingStructureService structureService,
    ILogger<GetOperationalLayoutQueryHandler> logger)
    : IRequestHandler<GetOperationalLayoutQuery, OperationalLayoutProjectionDto>
{
    public async Task<OperationalLayoutProjectionDto> Handle(GetOperationalLayoutQuery request, CancellationToken ct)
    {
        logger.LogInformation("Reading operational layout for site {SiteId} (VehicleType: {VehicleType}, Floor: {Floor})",
            request.SiteId, request.VehicleType, request.Floor);
        return await structureService.ReadOperationalAsync(request.SiteId, request.VehicleType, request.Floor, ct);
    }
}
