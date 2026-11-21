using FluentValidation;
namespace SmartParking.OperationService.Application.Reservations;

public sealed class RecordReservationDepositValidator : AbstractValidator<RecordReservationDepositCommand>
{
    public RecordReservationDepositValidator()
    {
        RuleFor(x => x.Evidence).NotNull().DependentRules(() =>
        {
            RuleFor(x => x.Evidence.EventId).NotEmpty();
            RuleFor(x => x.Evidence.CollectionId).NotEmpty();
            RuleFor(x => x.Evidence.PaymentTransactionId).NotEmpty();
            RuleFor(x => x.Evidence.PaymentOrderId).NotEmpty();
            RuleFor(x => x.Evidence.OperationId).NotEmpty();
            RuleFor(x => x.Evidence.ReservationId).NotEmpty();
            RuleFor(x => x.Evidence.QuoteId).NotEmpty();
            RuleFor(x => x.Evidence.QuoteVersion).NotEmpty();
            RuleFor(x => x.Evidence.AccountId).NotEmpty();
            RuleFor(x => x.Evidence.TenantId).NotEmpty();
            RuleFor(x => x.Evidence.SiteId).NotEmpty();
            RuleFor(x => x.Evidence.Amount).GreaterThan(0);
            RuleFor(x => x.Evidence.Currency).NotEmpty();
        });
    }
}
