using Microsoft.EntityFrameworkCore;
using UserService.Application.Common.Interfaces.Persistence;
using SmartParking.UserService.Domain.Entities;
using UserService.Persistence.Repositories.BaseRepository;

namespace UserService.Persistence.Repositories;

public class UserRepository
    : GenericRepository<User>,
      IUserRepository
{
    public UserRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await this.dbSet
            .Include(u => u.Accounts)
            .ThenInclude(a => a.AccountRoles)
            .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }
}
