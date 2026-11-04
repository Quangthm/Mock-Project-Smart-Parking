using MediatR;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.Application.Usecases.Operations.Commands.ExecuteStructureOperation;

public sealed record ExecuteStructureOperationCommand(
    Guid SiteId,
    Guid IdempotencyKey,
    StructureEdit Edit,
    Guid? TenantId = null) : IRequest<StructureOperation>;
