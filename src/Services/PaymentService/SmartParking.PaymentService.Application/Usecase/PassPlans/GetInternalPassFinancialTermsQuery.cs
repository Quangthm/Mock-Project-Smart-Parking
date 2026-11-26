using MediatR;
using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed record GetInternalPassFinancialTermsQuery(
    Guid SiteId,
    Guid? PlanId = null) : IRequest<IReadOnlyList<PassFinancialTermLookupItemDto>>;
