using MediatR;
using UserService.Application.Common.Interfaces.Grpc;

namespace UserService.Application.Usecase.ValidateAccessToken
{
    /// <summary>
    /// Validate access token command handler.
    /// </summary>
    /// <seealso cref="MediatR.IRequestHandler&lt;Template.Application.Usecase.ValidateAccessToken.ValidateAccessTokenCommand, System.Boolean&gt;" />
    public class ValidateAccessTokenCommandHandler : IRequestHandler<ValidateAccessTokenCommand, bool>
    {
        private readonly IUnitOfGrpc unitOfGrpc;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidateAccessTokenCommandHandler"/> class.
        /// </summary>
        /// <param name="unitOfGrpc">The unit of GRPC.</param>
        public ValidateAccessTokenCommandHandler(IUnitOfGrpc unitOfGrpc)
        {
            this.unitOfGrpc = unitOfGrpc;
        }

        /// <summary>
        /// Handles a request
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>
        /// Response from the request
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public async Task<bool> Handle(ValidateAccessTokenCommand request, CancellationToken cancellationToken)
        {
            await this.unitOfGrpc.DemoGrpcClient.CheckLoginAsync(request, cancellationToken).ConfigureAwait(false);

            return true;
        }
    }
}
