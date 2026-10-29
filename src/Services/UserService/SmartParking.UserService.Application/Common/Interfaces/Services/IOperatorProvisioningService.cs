using UserService.Application.DTOs;

namespace UserService.Application.Common.Interfaces.Services;

public interface IOperatorProvisioningService
{
    Task<OperatorDto> CreateAsync(Guid ownerId, CreateOperatorDto body, CancellationToken ct);
    Task<IReadOnlyList<OperatorDto>> ListAsync(Guid ownerId, CancellationToken ct);
}
public sealed record OperatorAssignment(Guid SiteId, Guid TenantId, string[] Permissions);
