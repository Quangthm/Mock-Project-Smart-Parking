using FluentValidation;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.GetAuditProjectionById;

public sealed class GetAuditProjectionByIdQueryValidator : AbstractValidator<GetAuditProjectionByIdQuery>
{
    public GetAuditProjectionByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Audit projection ID is required.");
    }
}
