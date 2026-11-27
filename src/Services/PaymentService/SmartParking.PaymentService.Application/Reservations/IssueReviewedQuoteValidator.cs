using FluentValidation;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed class IssueReviewedQuoteValidator : AbstractValidator<IssueReviewedQuote>
{
    public IssueReviewedQuoteValidator()
    {
        RuleFor(x => x.Binding.AccountId).NotEmpty();
        RuleFor(x => x.Binding.TenantId).NotEmpty();
        RuleFor(x => x.Binding.SiteId).NotEmpty();
        RuleFor(x => x.Binding.RequestedSlotId).NotEmpty();
        RuleFor(x => x.Binding.CanonicalPlate).NotEmpty().Matches(@"\A[1-9][0-9][A-Z][0-9]{5}\z");
        RuleFor(x => x.Binding.Start).NotEmpty();
        RuleFor(x => x.Binding.End).GreaterThan(x => x.Binding.Start);
        RuleFor(x => x.Binding.PaymentMethod).Equal("PAYOS");
        RuleFor(x => x.Binding.PlatformPolicyId).NotEmpty();
        RuleFor(x => x.Binding.SitePolicyId).NotEmpty();
        RuleFor(x => x.Binding.PlatformPolicyVersion).GreaterThan(0);
        RuleFor(x => x.Binding.SitePolicyVersion).GreaterThan(0);
        RuleFor(x => x.Binding.Coverage).NotNull();
        When(x => x.Binding.Coverage != null, () => RuleFor(x => x.Binding.Coverage.Version).NotEmpty().MaximumLength(100));
    }
}
