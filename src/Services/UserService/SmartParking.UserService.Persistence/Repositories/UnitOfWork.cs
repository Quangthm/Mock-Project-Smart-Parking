using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly IUserRepository userRepository;

    public UnitOfWork(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    public IUserRepository UserRepository =>
        this.userRepository;

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // No real database exists in this demo.
        return Task.FromResult(0);
    }
}