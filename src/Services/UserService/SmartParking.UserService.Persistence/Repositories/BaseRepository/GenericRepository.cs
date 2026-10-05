using Microsoft.EntityFrameworkCore;
using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Persistence.Repositories.BaseRepository;

public class GenericRepository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    protected readonly AppDbContext dbContext;
    protected readonly DbSet<TEntity> dbSet;

    public GenericRepository(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
        this.dbSet = dbContext.Set<TEntity>();
    }

    public async Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken)
    {
        await this.dbSet.AddAsync(entity, cancellationToken);
    }

    public async Task AddRangeAsync(
        List<TEntity> entities,
        CancellationToken cancellationToken)
    {
        await this.dbSet.AddRangeAsync(entities, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        this.dbSet.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        this.dbSet.Remove(entity);
    }

    public IQueryable<TEntity> Query()
    {
        return this.dbSet.AsQueryable();
    }

    public IQueryable<TEntity> QueryIncludingDeleted()
    {
        return this.dbSet.AsQueryable();
    }
}