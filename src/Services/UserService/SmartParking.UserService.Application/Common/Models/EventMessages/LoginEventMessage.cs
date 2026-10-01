using UserService.Application.Common.Interfaces.MessageBroker;

namespace UserService.Application.Common.Models.EventMessages
{
    /// <summary>
    /// Login event message.
    /// </summary>
    /// <seealso cref="UserService.Application.Common.Interfaces.MessageBroker.IIntegrationEventMessage" />
    public class LoginEventMessage : IIntegrationEventMessage
    {
        /// <summary>
        /// Gets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public Guid Id { get; init; }

        /// <summary>
        /// Gets the event identifier.
        /// </summary>
        /// <value>
        /// The event identifier.
        /// </value>
        public string EventId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the event message.
        /// </summary>
        /// <value>
        /// The event message.
        /// </value>
        public string EventMessage { get; init; } = string.Empty;

        /// <summary>
        /// Gets the date time.
        /// </summary>
        /// <value>
        /// The date time.
        /// </value>
        public DateTimeOffset DateTime { get; init; }

        /// <summary>
        /// Gets the audit detail.
        /// </summary>
        /// <value>
        /// The audit detail.
        /// </value>
        public List<Dictionary<string, string>> AuditDetail { get; init; } = [];
    }
}
