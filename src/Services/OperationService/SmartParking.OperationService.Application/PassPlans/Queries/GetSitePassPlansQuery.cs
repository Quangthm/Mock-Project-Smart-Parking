using MediatR;
using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.Application.PassPlans.Queries;

public sealed record GetSitePassPlansQuery(Guid SiteId) : IRequest<SitePassPlansResponseDto>;
