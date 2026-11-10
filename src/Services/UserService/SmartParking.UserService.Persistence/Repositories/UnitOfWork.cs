using UserService.Application.Common.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.UserService.Domain.Entities;
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
        foreach (var entry in dbContext.ChangeTracker.Entries<RegisteredVehicle>().Where(e => e.State == EntityState.Modified))
        {
            var identityChanged = new[] { nameof(RegisteredVehicle.AccountId), nameof(RegisteredVehicle.CanonicalPlate),
                nameof(RegisteredVehicle.VehicleType), nameof(RegisteredVehicle.DeletedAt) }
                .Any(name => !Equals(entry.Property(name).OriginalValue, entry.Property(name).CurrentValue));
            if (identityChanged)
            {
                var revision = entry.Property(v => v.BindingRevision);
                revision.CurrentValue = checked(revision.OriginalValue + 1);
            }
        }
        try { return await this.dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: "23505", ConstraintName: "uq_active_user_email_normalized" })
        {
            throw new AuthException("EMAIL_EXISTS", "Email is already registered.", 409);
        }
    }
}
