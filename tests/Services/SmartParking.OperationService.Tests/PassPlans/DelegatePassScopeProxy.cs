using System.Reflection;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Tests.PassPlans;

internal class DelegatePassScopeProxy : DispatchProxy
{
    public Func<Guid, CancellationToken, Task<OwnerSiteScopeResult>> ValidateOwnerSiteAsyncFunc { get; set; } = null!;
    public Func<Guid, Guid?, IReadOnlyList<PassVehicleType>, IReadOnlyList<Guid>, CancellationToken, Task<OwnerSiteScopeResult>> ValidateOwnerDraftScopeAsyncFunc { get; set; } = null!;
    public Func<Guid, CancellationToken, Task<bool>> SiteExistsAsyncFunc { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        nameof(IPassPlanFacilityScopePort.ValidateOwnerSiteAsync) =>
            ValidateOwnerSiteAsyncFunc((Guid)args![0]!, (CancellationToken)args[1]!),
        nameof(IPassPlanFacilityScopePort.ValidateOwnerDraftScopeAsync) =>
            ValidateOwnerDraftScopeAsyncFunc(
                (Guid)args![0]!,
                (Guid?)args[1],
                (IReadOnlyList<PassVehicleType>)args[2]!,
                (IReadOnlyList<Guid>)args[3]!,
                (CancellationToken)args[4]!),
        nameof(IPassPlanFacilityScopePort.SiteExistsAsync) =>
            SiteExistsAsyncFunc((Guid)args![0]!, (CancellationToken)args[1]!),
        _ => throw new NotImplementedException(targetMethod?.Name)
    };
}
