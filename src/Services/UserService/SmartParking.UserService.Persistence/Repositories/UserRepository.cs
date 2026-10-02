using UserService.Application.Common.Interfaces.Persistence;
using SmartParking.UserService.Domain.Entities;
using UserService.Persistence.FakeDatabase;
using UserService.Persistence.Repositories.BaseRepository;

namespace UserService.Persistence.Repositories;

public class UserRepository
    : GenericRepository<User>,
      IUserRepository
{
    public UserRepository()
        : base(InMemoryDatabase.Users)
    {
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Persistence boundary for the demo: read only from memory, without a DB.
        var user = this.entities.FirstOrDefault(user =>
            !user.IsDeleted && string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(user);
    }
}
