using MediatR;
using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed record QuotePassFinancialTermsCommand(QuotePassFinancialTermsRequest Request)
    : IRequest<PassFinancialQuoteSnapshotDto>;
