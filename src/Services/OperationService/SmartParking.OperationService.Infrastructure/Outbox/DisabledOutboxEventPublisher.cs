using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Infrastructure;

internal sealed class DisabledOutboxEventPublisher(ILogger<DisabledOutboxEventPublisher> logger) : IOutboxEventPublisher
{
    private static bool _warned;

    public string Name => "disabled";

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!_warned)
        {
            _warned = true;
            logger.LogWarning("Occupancy outbox publisher disabled: RabbitMq:ConnectionString is not configured. Events remain unpublished.");
        }

        return Task.CompletedTask;
    }
}
