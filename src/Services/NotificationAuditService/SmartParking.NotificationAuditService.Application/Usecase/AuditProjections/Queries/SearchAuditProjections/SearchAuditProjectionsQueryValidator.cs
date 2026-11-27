using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.SearchAuditProjections;

public sealed class SearchAuditProjectionsQueryValidator : AbstractValidator<SearchAuditProjectionsQuery>
{
    public SearchAuditProjectionsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.ActorRole)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.ActorRole));

        RuleFor(x => x.Action)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Action));

        RuleFor(x => x.OriginatingService)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.OriginatingService));

        RuleFor(x => x.ResourceType)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.ResourceType));

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Search));

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From.Value <= x.To.Value)
            .WithMessage("'from' timestamp must be less than or equal to 'to' timestamp.");
    }
}
