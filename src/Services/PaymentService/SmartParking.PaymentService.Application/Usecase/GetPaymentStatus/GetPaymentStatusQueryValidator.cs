using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;

public class GetPaymentStatusQueryValidator : AbstractValidator<GetPaymentStatusQuery>
{
    public GetPaymentStatusQueryValidator()
    {
        RuleFor(x => x.TransactionId)
            .NotEmpty().WithMessage("Mã giao dịch (transactionId) là bắt buộc.");
    }
}
