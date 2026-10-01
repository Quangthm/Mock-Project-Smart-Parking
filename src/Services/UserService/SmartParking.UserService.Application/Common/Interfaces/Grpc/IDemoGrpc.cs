using UserService.Application.Usecase.ValidateAccessToken;

namespace UserService.Application.Common.Interfaces.Grpc
{
    /// <summary>
    /// Interface demo grpc client.
    /// </summary>
    public interface IDemoGrpcClient
    {
        /// <summary>
        /// Checks the login asynchronous.
        /// </summary>
        /// <param name="validateAccessTokenCommand">The validate access token command.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task<bool> CheckLoginAsync(ValidateAccessTokenCommand validateAccessTokenCommand, CancellationToken cancellationToken);
    }
}
