using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IProviderWebhookRepository
{
    Task<ProviderWebhook?> GetByProviderAndEventIdAsync(string provider, string eventId, CancellationToken cancellationToken = default);
    Task AddAsync(ProviderWebhook webhook, CancellationToken cancellationToken = default);
    void Update(ProviderWebhook webhook);
}
