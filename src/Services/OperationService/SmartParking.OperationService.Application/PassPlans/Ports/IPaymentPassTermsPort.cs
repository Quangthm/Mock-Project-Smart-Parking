using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.Application.PassPlans.Ports;

public interface IPaymentPassTermsPort
{
    Task<IReadOnlyList<PaymentPassFinancialTermDto>> GetActiveFinancialTermsAsync(
        Guid siteId,
        Guid? planId,
        CancellationToken ct);
}
