using MediatR;

namespace SmartParking.OperationService.Application.Passes;

public sealed record PreparePassPurchaseCommand(
    Guid SiteId,
    Guid PlanId,
    int PlanRevision,
    Guid FinancialTermId,
    int FinancialTermRevision,
    string ProductType,
    string VehicleType) : IRequest<PreparePassPurchaseResult>;
