using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NotificationAuditDbContext _context;

    public UnitOfWork(NotificationAuditDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
