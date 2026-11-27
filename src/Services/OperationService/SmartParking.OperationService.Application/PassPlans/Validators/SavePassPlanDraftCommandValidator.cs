using FluentValidation;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Validators;

public sealed class SavePassPlanDraftCommandValidator : AbstractValidator<Commands.SavePassPlanDraftCommand>
{
    public SavePassPlanDraftCommandValidator()
    {
        RuleFor(x => x.SiteId)
            .NotEmpty()
            .WithErrorCode("VALIDATION_ERROR");

        RuleFor(x => x.ProductType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(pt => !string.IsNullOrWhiteSpace(pt) &&
                        Enum.TryParse<PassProductType>(pt.Trim(), ignoreCase: true, out var parsed) &&
                        Enum.IsDefined(parsed))
            .WithMessage("productType must be MONTHLY or YEARLY.")
            .WithErrorCode("VALIDATION_ERROR");

        RuleFor(x => x.Request)
            .NotNull()
            .WithErrorCode("VALIDATION_ERROR");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.IsAvailableOnly)
                .Equal(true)
                .WithMessage("isAvailableOnly must be true (WHEN_SPACE_AVAILABLE).")
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(x => x.Request.ZoneId)
                .Must(z => !z.HasValue || z.Value != Guid.Empty)
                .WithMessage("zoneId cannot be an empty GUID.")
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(x => x.Request.VehicleTypes)
                .NotNull()
                .NotEmpty()
                .WithMessage("vehicleTypes must contain at least one vehicle type.")
                .WithErrorCode("VALIDATION_ERROR");

            RuleForEach(x => x.Request.VehicleTypes!)
                .Must(v => !string.IsNullOrWhiteSpace(v) &&
                           Enum.TryParse<PassVehicleType>(v.Trim(), ignoreCase: true, out var parsed) &&
                           Enum.IsDefined(parsed))
                .WithMessage("Each vehicleType must be CAR, MOTORCYCLE, or OVERSIZED.")
                .WithErrorCode("VALIDATION_ERROR");

            RuleFor(x => x.Request.OrdinaryCoverageRules)
                .NotNull()
                .WithMessage("ordinaryCoverageRules is required.")
                .WithErrorCode("VALIDATION_ERROR");

            When(x => x.Request.OrdinaryCoverageRules is not null, () =>
            {
                RuleFor(x => x.Request.OrdinaryCoverageRules!)
                    .Must(r => r.DayCovered || r.NightCovered)
                    .WithMessage("At least one of dayCovered or nightCovered must be true.")
                    .WithErrorCode("VALIDATION_ERROR");

                RuleFor(x => x.Request.OrdinaryCoverageRules!.ExcludedZoneIds)
                    .Must(ids => ids is null || ids.All(id => id != Guid.Empty))
                    .WithMessage("excludedZoneIds cannot contain empty GUIDs.")
                    .WithErrorCode("VALIDATION_ERROR");

                RuleFor(x => x.Request)
                    .Must(req => !req.ZoneId.HasValue ||
                                 req.OrdinaryCoverageRules!.ExcludedZoneIds is null ||
                                 !req.OrdinaryCoverageRules.ExcludedZoneIds.Contains(req.ZoneId.Value))
                    .WithMessage("zoneId cannot also appear in excludedZoneIds.")
                    .WithErrorCode("VALIDATION_ERROR");
            });
        });
    }
}
