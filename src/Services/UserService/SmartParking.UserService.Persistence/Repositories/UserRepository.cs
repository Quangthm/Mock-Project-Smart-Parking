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
            .FirstOrDefaultAsync(user => user.Email == email && user.DeletedOn == null, cancellationToken);
    }

    public async Task<User?> GetByEmailForLoginAsync(string email, CancellationToken cancellationToken)
    {
        // The caller holds a transaction; row locking serializes failures across API instances.
        return await this.dbSet
            .FromSqlInterpolated($"SELECT * FROM users WHERE email = {email} AND deleted_at IS NULL FOR UPDATE")
            .Include(u => u.Accounts).ThenInclude(a => a.AccountRoles)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await this.dbSet
            .Include(u => u.Accounts)
            .ThenInclude(a => a.AccountRoles)
            .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }
}
