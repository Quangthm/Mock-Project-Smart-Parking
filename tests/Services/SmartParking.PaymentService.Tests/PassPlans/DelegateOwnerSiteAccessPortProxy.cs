using System.Reflection;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.PassPlans;

namespace SmartParking.PaymentService.Tests.PassPlans;

internal class DelegateOwnerSiteAccessPortProxy : DispatchProxy
{
    public Func<Guid, CancellationToken, Task<OwnerSiteScopeResult>> ValidateOwnerSiteAsyncFunc { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        nameof(IOwnerSiteAccessPort.ValidateOwnerSiteAsync) =>
            ValidateOwnerSiteAsyncFunc((Guid)args![0]!, (CancellationToken)args[1]!),
        _ => throw new NotImplementedException(targetMethod?.Name)
    };
}
