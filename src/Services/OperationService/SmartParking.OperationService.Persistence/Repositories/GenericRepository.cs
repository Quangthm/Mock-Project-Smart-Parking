using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Persistence.Repositories;

public class GenericRepository<TEntity>(ReservationDbContext db) : IRepository<TEntity>
    where TEntity : class
{
    protected readonly DbSet<TEntity> Set = db.Set<TEntity>();

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken) =>
        Set.AddAsync(entity, cancellationToken).AsTask();

    public void Update(TEntity entity) => Set.Update(entity);
    public void Remove(TEntity entity) => Set.Remove(entity);
    public IQueryable<TEntity> Query() => Set.AsQueryable();
}
