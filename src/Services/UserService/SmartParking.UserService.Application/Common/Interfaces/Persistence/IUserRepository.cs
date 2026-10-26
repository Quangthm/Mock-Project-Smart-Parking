using UserService.Application.Common.Interfaces.Persistence;
namespace UserService.Application.Common.Interfaces.Persistence
{
    /// <summary>
    /// Interface User Repository
    /// </summary>
    /// <seealso cref="UserService.Application.Common.Interfaces.Persistence.IRepository&lt;UserService.Domain.Entities.User&gt;" />
    /// <seealso cref="IRepository&lt;User&gt;" />
    public interface IUserRepository : IRepository<SmartParking.UserService.Domain.Entities.User>
    {
        Task<SmartParking.UserService.Domain.Entities.User?> GetByEmailForLoginAsync(
            string email, CancellationToken cancellationToken);

        Task<SmartParking.UserService.Domain.Entities.User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken);

        Task<SmartParking.UserService.Domain.Entities.User?> GetByIdWithRolesAsync(
            Guid userId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Loads the user with a row lock so a concurrent change to the same account cannot
        /// be committed between the current-access check and the write that follows it.
        /// </summary>
        /// <remarks>
        /// The caller must hold a transaction for the duration of the locked read; the lock
        /// is released when that transaction ends.
        /// </remarks>
        Task<SmartParking.UserService.Domain.Entities.User?> GetByIdForUpdateAsync(
            Guid userId,
            CancellationToken cancellationToken);
    }
}
