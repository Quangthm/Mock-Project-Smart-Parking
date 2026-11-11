using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Persistence.Repositories;

public class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly NotificationAuditDbContext _context;

    public NotificationTemplateRepository(NotificationAuditDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationTemplate?> GetByCodeAndVersionAsync(string templateCode, int version, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.TemplateCode == templateCode && t.Version == version, cancellationToken);
    }

    public async Task<NotificationTemplate?> GetActiveByCodeAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationTemplates
            .Where(t => t.TemplateCode == templateCode && t.IsActive)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        await _context.NotificationTemplates.AddAsync(template, cancellationToken);
    }
}
