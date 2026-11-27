using FluentValidation;
using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Usecase.ReservationGate;

namespace SmartParking.OperationService.Application.Usecase.Checkout;

public sealed record CheckMonthlyCoverageQuery(Guid SiteId, string PlateNumber) : IRequest<MonthlyCoverageResult>;
public sealed record MonthlyCoverageResult(bool IsCovered, long? AmountDueVnd);

public sealed class CheckMonthlyCoverageQueryValidator : AbstractValidator<CheckMonthlyCoverageQuery>
{
    public CheckMonthlyCoverageQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.PlateNumber).Must(plate => !string.IsNullOrWhiteSpace(plate) && plate.Length <= 50);
    }
}

public sealed class CheckMonthlyCoverageQueryHandler(
    IOperatorIdentityClient identity,
    ISubscribedVehicleRepository subscriptions,
    TimeProvider clock) : IRequestHandler<CheckMonthlyCoverageQuery, MonthlyCoverageResult>
{
    public async Task<MonthlyCoverageResult> Handle(CheckMonthlyCoverageQuery request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        if (string.IsNullOrWhiteSpace(request.PlateNumber) || request.PlateNumber.Length > 50)
            throw new ReservationGateException(400, "INVALID_PLATE", "Enter a valid vehicle plate.");

        var covered = await subscriptions.IsCoveredAsync(
            scope.TenantId, request.SiteId, request.PlateNumber, clock.GetUtcNow(), ct);
        return new(covered, covered ? 0 : null);
    }
}
