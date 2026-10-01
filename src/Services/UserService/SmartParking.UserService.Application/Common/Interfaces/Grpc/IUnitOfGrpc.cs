namespace UserService.Application.Common.Interfaces.Grpc
{
    /// <summary>
    /// Interface unit of grpc.
    /// </summary>
    public interface IUnitOfGrpc
    {
        /// <summary>
        /// Gets the demo GRPC client.
        /// </summary>
        /// <value>
        /// The demo GRPC client.
        /// </value>
        IDemoGrpcClient DemoGrpcClient { get; }
    }
}
