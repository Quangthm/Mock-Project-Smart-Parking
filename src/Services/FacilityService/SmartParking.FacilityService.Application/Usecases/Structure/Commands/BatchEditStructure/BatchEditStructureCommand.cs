using MediatR;
using SmartParking.FacilityService.Application.DTOs;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.BatchEditStructure;

public sealed record BatchEditStructureCommand(
    Guid SiteId,
    Guid IdempotencyKey,
    IReadOnlyList<BatchItemBody>? Items,
    Guid? TenantId = null) : IRequest<StructureOperation>;
