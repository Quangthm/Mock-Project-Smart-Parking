using FluentValidation;

namespace SmartParking.FacilityService.Application.Usecases.Occupancy.Commands.ApplyOccupancyProjection;

public sealed class ApplyOccupancyProjectionCommandValidator : AbstractValidator<ApplyOccupancyProjectionCommand>
{
    public ApplyOccupancyProjectionCommandValidator()
    {
        RuleFor(x => x.Projection).NotNull().WithMessage("Occupancy projection payload is required.");
        RuleFor(x => x.Projection.EventId).NotEqual(Guid.Empty).WithMessage("Occupancy projection eventId is required.");
        RuleFor(x => x.Projection.SiteId).NotEqual(Guid.Empty).WithMessage("Occupancy projection siteId is required.");
        RuleFor(x => x.Projection.EventType).NotEmpty().WithMessage("Occupancy projection eventType is required.");
        RuleFor(x => x.Projection.SequenceNumber).GreaterThan(0).WithMessage("Occupancy projection sequenceNumber must be greater than zero.");
    }
}