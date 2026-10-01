using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Application.Common.Interfaces.Persistence
{
    /// <summary>
    /// Interface Unit of Work.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Gets the user repository.
        /// </summary>
        /// <value>
        /// The user repository.
        /// </value>
        IUserRepository UserRepository { get; }

        /// <summary>
        /// Saves the changes asynchronous.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
