using MediatR;
using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed record GetPassRevenueSummaryQuery(
    Guid SiteId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? ProductType = null) : IRequest<PassRevenueSummaryDto>;
