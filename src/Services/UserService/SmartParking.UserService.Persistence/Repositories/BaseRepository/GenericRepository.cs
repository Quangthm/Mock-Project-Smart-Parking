using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Persistence.Repositories.BaseRepository;

public class GenericRepository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    protected readonly List<TEntity> entities;

    public GenericRepository(List<TEntity> entities)
    {
        this.entities = entities;
    }

    public Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken)
    {
        this.entities.Add(entity);

        return Task.CompletedTask;
    }

    public Task AddRangeAsync(
        List<TEntity> entities,
        CancellationToken cancellationToken)
    {
        this.entities.AddRange(entities);

        return Task.CompletedTask;
    }

    public void Update(TEntity entity)
    {
        // Demo implementation.
        // Nothing is required for Login.
    }

    public void Remove(TEntity entity)
    {
        this.entities.Remove(entity);
    }

    public IQueryable<TEntity> Query()
    {
        return this.entities.AsQueryable();
    }

    public IQueryable<TEntity> QueryIncludingDeleted()
    {
        return this.entities.AsQueryable();
    }
}