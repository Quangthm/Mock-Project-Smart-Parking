using System.Reflection;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Policies.DTOs;

namespace SmartParking.OperationService.Tests.PassPlans;

internal class DelegateOwnerIdentityProxy : DispatchProxy
{
    public Func<string, CancellationToken, Task<OwnerIdentityScope>> GetOwnerScopeAsyncFunc { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        nameof(IOwnerIdentityClient.GetOwnerScopeAsync) =>
            GetOwnerScopeAsyncFunc((string)args![0]!, (CancellationToken)args[1]!),
        _ => throw new NotImplementedException(targetMethod?.Name)
    };
}
