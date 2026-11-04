using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionTenant;

public sealed class ProvisionTenantCommandHandler(
    FacilityDirectoryService directory,
    ILogger<ProvisionTenantCommandHandler> logger)
    : IRequestHandler<ProvisionTenantCommand, bool>
{
    public async Task<bool> Handle(ProvisionTenantCommand request, CancellationToken ct)
    {
        logger.LogInformation("Provisioning tenant {TenantId}", request.TenantId);
        return await directory.ProvisionTenantAsync(request.TenantId, request.Body, ct);
    }
}
