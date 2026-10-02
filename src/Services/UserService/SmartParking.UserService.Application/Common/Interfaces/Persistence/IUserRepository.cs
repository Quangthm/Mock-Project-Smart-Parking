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
        Task<SmartParking.UserService.Domain.Entities.User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken);
    }
}
