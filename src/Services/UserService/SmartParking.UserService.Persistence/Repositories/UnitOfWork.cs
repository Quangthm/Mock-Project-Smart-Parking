using UserService.Application.Common.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UserService.Application.Common.Models.Exceptions;

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

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new Transaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Transaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await this.dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: "23505", ConstraintName: "uq_active_user_email_normalized" })
        {
            throw new AuthException("EMAIL_EXISTS", "Email is already registered.", 409);
        }
    }
}
