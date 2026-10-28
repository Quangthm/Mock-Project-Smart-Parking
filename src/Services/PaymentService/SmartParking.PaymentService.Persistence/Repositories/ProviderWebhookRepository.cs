using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

public class ProviderWebhookRepository : IProviderWebhookRepository
{
    private readonly PaymentDbContext _dbContext;

    public ProviderWebhookRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProviderWebhook?> GetByProviderAndEventIdAsync(string provider, string eventId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProviderWebhooks
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderEventId == eventId, cancellationToken);
    }

    public async Task AddAsync(ProviderWebhook webhook, CancellationToken cancellationToken = default)
    {
        await _dbContext.ProviderWebhooks.AddAsync(webhook, cancellationToken);
    }

    public void Update(ProviderWebhook webhook)
    {
        _dbContext.ProviderWebhooks.Update(webhook);
    }
}
