using MediatR;
using SmartParking.PaymentService.Application.DTOs.Payments;

namespace SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;

public record GetPaymentStatusQuery(
    Guid TransactionId,
    Guid AccountId = default,
    bool EnableReviewedVerification = false
) : IRequest<PaymentTransactionStatusDto>;
