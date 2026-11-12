using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Persistence.Repositories;

public class CentralAuditProjectionRepository : ICentralAuditProjectionRepository
{
    private readonly NotificationAuditDbContext _context;

    public CentralAuditProjectionRepository(NotificationAuditDbContext context)
    {
        _context = context;
    }

    public async Task<CentralAuditProjection?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.CentralAuditProjections
            .FirstOrDefaultAsync(a => a.EventId == eventId, cancellationToken);
    }

    public async Task<bool> ExistsByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.CentralAuditProjections
            .AnyAsync(a => a.EventId == eventId, cancellationToken);
    }

    public async Task AddAsync(CentralAuditProjection projection, CancellationToken cancellationToken = default)
    {
        await _context.CentralAuditProjections.AddAsync(projection, cancellationToken);
    }
}
