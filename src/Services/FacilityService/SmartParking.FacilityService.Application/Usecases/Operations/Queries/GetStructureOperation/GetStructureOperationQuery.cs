using MediatR;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.Application.Usecases.Operations.Queries.GetStructureOperation;

public sealed record GetStructureOperationQuery(
    Guid SiteId,
    Guid OperationId,
    Guid? TenantId = null) : IRequest<StructureOperation>;
