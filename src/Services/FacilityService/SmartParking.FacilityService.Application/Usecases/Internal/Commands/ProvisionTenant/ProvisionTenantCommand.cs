using MediatR;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Commands.ProvisionTenant;

public sealed record ProvisionTenantCommand(
    Guid TenantId,
    TenantBody Body) : IRequest<bool>;
