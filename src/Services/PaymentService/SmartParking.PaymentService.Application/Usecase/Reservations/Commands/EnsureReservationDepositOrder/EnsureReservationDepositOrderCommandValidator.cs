using FluentValidation;
namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;

public sealed class EnsureReservationDepositOrderCommandValidator : AbstractValidator<EnsureReservationDepositOrderCommand>
{
    public EnsureReservationDepositOrderCommandValidator()
    {
        RuleFor(x => x.Request.ReservationId).NotEmpty();
        RuleFor(x => x.Request.AccountId).NotEmpty();
        RuleFor(x => x.Request.TenantId).NotEmpty();
        RuleFor(x => x.Request.SiteId).NotEmpty();
        RuleFor(x => x.Request.QuoteId).NotEmpty();
        RuleFor(x => x.Request.RequestedSlotId).NotEmpty();
        RuleFor(x => x.Request.DepositAmount).GreaterThanOrEqualTo(0);
    }
}
