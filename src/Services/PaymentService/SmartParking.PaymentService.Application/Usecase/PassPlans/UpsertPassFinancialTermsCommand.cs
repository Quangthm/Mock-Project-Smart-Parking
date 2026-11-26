using MediatR;
using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed record UpsertPassFinancialTermsCommand(
    Guid SiteId,
    Guid PlanId,
    string? IdempotencyKey,
    UpsertPassFinancialTermsRequest Request) : IRequest<UpsertPassFinancialTermsResponseDto>;
