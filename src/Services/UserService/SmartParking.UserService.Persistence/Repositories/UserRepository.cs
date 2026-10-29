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
            .ThenInclude(a => a.Role)
            .FirstOrDefaultAsync(user => user.Email == email && user.DeletedOn == null, cancellationToken);
    }

    public async Task<User?> GetByEmailForLoginAsync(string email, CancellationToken cancellationToken)
    {
        // The caller holds a transaction; row locking serializes failures across API instances.
        var user = await this.dbSet
            .FromSqlInterpolated($"SELECT * FROM users WHERE (lower(email) = lower({email}) OR phone = {email}) AND deleted_at IS NULL FOR UPDATE")
            .Include(u => u.Accounts).ThenInclude(a => a.Role)
            .FirstOrDefaultAsync(cancellationToken);
        if (user is not null) await this.dbContext.Entry(user).ReloadAsync(cancellationToken);
        return user;
    }

    public async Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await this.dbSet
            .AsNoTracking()
            .Include(u => u.Accounts)
            .ThenInclude(a => a.Role)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }
}
