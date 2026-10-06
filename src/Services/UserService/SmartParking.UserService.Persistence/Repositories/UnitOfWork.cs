using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext dbContext;
    private readonly IUserRepository userRepository;

    public UnitOfWork(AppDbContext dbContext, IUserRepository userRepository)
    {
        this.dbContext = dbContext;
        this.userRepository = userRepository;
    }

    public IUserRepository UserRepository => this.userRepository;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}