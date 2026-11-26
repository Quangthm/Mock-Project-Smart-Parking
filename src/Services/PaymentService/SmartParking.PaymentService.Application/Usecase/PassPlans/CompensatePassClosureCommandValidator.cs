using FluentValidation;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class CompensatePassClosureCommandValidator : AbstractValidator<CompensatePassClosureCommand>
{
    public CompensatePassClosureCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithErrorCode("INVALID_CLOSURE_INTERVALS");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.IdempotencyKey)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(k => !string.IsNullOrWhiteSpace(k))
                .MaximumLength(128)
                .WithErrorCode("INVALID_CLOSURE_INTERVALS");

            RuleFor(x => x.Request.ClosureId).NotEmpty().WithErrorCode("INVALID_CLOSURE_INTERVALS");
            RuleFor(x => x.Request.SiteId).NotEmpty().WithErrorCode("INVALID_CLOSURE_INTERVALS");
            RuleFor(x => x.Request.TenantId).NotEmpty().WithErrorCode("INVALID_CLOSURE_INTERVALS");
            RuleFor(x => x.Request.PassId).NotEmpty().WithErrorCode("INVALID_CLOSURE_INTERVALS");
            RuleFor(x => x.Request.PaymentOrderId).NotEmpty().WithErrorCode("INVALID_CLOSURE_INTERVALS");

            RuleFor(x => x.Request.ClosureType)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(c => !string.IsNullOrWhiteSpace(c) &&
                           (string.Equals(c.Trim(), "TEMPORARY", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.Trim(), "PERMANENT", StringComparison.OrdinalIgnoreCase)))
                .WithMessage("closureType must be TEMPORARY or PERMANENT.")
                .WithErrorCode("INVALID_CLOSURE_INTERVALS");

            RuleFor(x => x.Request)
                .Must(r => r.PaidPeriodEnd > r.PaidPeriodStart)
                .WithMessage("paidPeriodEnd must be greater than paidPeriodStart.")
                .WithErrorCode("INVALID_CLOSURE_INTERVALS");

            RuleFor(x => x.Request.UnavailableIntervals)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .NotEmpty()
                .WithMessage("unavailableIntervals must contain at least one interval.")
                .WithErrorCode("INVALID_CLOSURE_INTERVALS");

            When(x => x.Request.UnavailableIntervals is not null, () =>
            {
                RuleForEach(x => x.Request.UnavailableIntervals)
                    .Cascade(CascadeMode.Stop)
                    .NotNull()
                    .WithMessage("unavailableIntervals cannot contain null entries.")
                    .WithErrorCode("INVALID_CLOSURE_INTERVALS")
                    .Must(i => i.End > i.Start)
                    .WithMessage("Each unavailable interval must have end > start.")
                    .WithErrorCode("INVALID_CLOSURE_INTERVALS");
            });
        });
    }
}
