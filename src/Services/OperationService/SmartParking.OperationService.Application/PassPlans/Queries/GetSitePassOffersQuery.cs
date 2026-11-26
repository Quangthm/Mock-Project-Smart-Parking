using MediatR;
using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.Application.PassPlans.Queries;

public sealed record GetSitePassOffersQuery(Guid SiteId, string? VehicleType = null)
    : IRequest<SitePassOffersResponseDto>;
