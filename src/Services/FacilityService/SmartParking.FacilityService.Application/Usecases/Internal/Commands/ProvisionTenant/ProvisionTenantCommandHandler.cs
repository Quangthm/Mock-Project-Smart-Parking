using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionTenant;

public sealed class ProvisionTenantCommandHandler(
    IFacilityDirectoryStore directory,
    ILogger<ProvisionTenantCommandHandler> logger)
    : IRequestHandler<ProvisionTenantCommand, bool>
{
    public async Task<bool> Handle(ProvisionTenantCommand request, CancellationToken ct)
    {
        logger.LogInformation("Provisioning tenant {TenantId}", request.TenantId);
        if (request.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Body.Name) || request.Body.Name.Length > 255 ||
            string.IsNullOrWhiteSpace(request.Body.Email) || request.Body.Email.Length > 255 ||
            string.IsNullOrWhiteSpace(request.Body.Phone) || request.Body.Phone.Length > 20)
        {
            throw new StructureException("INVALID_TENANT", "Provide a valid tenant identity and contact.");
        }

        return await directory.ProvisionTenantAsync(request.TenantId, request.Body, ct);
    }
}
