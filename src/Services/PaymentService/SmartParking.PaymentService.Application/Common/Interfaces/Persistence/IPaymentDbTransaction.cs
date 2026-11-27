namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IPaymentDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}
