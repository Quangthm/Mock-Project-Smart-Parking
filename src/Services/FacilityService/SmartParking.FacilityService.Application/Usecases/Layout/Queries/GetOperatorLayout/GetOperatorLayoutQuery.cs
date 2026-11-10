using MediatR;
using SmartParking.FacilityService.Application.DTOs;

namespace SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperatorLayout;

public sealed record GetOperatorLayoutQuery(Guid SiteId) : IRequest<OperatorStructureResponse>;
