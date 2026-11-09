using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Queries.ReadStructureImpact;

public sealed record ReadStructureImpactQuery(Guid TenantId, Guid SiteId) : IRequest<StructureImpactResponse>;
