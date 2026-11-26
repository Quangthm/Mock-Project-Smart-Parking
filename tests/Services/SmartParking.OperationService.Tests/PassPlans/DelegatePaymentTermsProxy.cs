using System.Reflection;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Ports;

namespace SmartParking.OperationService.Tests.PassPlans;

internal class DelegatePaymentTermsProxy : DispatchProxy
{
    public Func<Guid, Guid?, CancellationToken, Task<IReadOnlyList<PaymentPassFinancialTermDto>>> GetActiveFinancialTermsFunc { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        nameof(IPaymentPassTermsPort.GetActiveFinancialTermsAsync) =>
            GetActiveFinancialTermsFunc((Guid)args![0]!, (Guid?)args[1], (CancellationToken)args[2]!),
        _ => throw new NotImplementedException(targetMethod?.Name)
    };
}
