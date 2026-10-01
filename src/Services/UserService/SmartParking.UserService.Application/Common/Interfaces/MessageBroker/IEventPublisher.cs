namespace UserService.Application.Common.Interfaces.MessageBroker
{
    /// <summary>
    /// Interface event bus.
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes the asynchronous.
        /// </summary>
        /// <typeparam name="TMessage">The type of the message.</typeparam>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
            where TMessage : IIntegrationEventMessage;

        /// <summary>
        /// Publishes the many asynchronous.
        /// </summary>
        /// <typeparam name="TMessage">The type of the message.</typeparam>
        /// <param name="messages">The messages.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task PublishManyAsync<TMessage>(IEnumerable<TMessage> messages, CancellationToken cancellationToken)
            where TMessage : IIntegrationEventMessage;
    }
}
