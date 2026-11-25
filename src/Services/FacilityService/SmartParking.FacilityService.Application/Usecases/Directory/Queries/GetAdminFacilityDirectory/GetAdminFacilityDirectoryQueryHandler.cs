using MediatR;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Directory.Queries.GetAdminFacilityDirectory;

public sealed class GetAdminFacilityDirectoryQueryHandler(
    ILayoutCallerResolver callers,
    IFacilityDirectoryStore directory)
    : IRequestHandler<GetAdminFacilityDirectoryQuery, AdminFacilityDirectoryDto>
{
    public async Task<AdminFacilityDirectoryDto> Handle(GetAdminFacilityDirectoryQuery request, CancellationToken ct)
    {
        // This is a platform read: no site scope is selected or trusted from client input.
        var caller = await callers.ResolveAsync(Guid.Empty, ct);
        if (caller.Kind == LayoutCallerKind.Anonymous)
            throw new StructureException("UNAUTHENTICATED", "Authentication is required.");
        if (caller.Kind != LayoutCallerKind.Admin)
            throw new StructureException("FORBIDDEN", "Administrator access is required.");

        return await directory.GetAdminDirectoryAsync(ct);
    }
}
