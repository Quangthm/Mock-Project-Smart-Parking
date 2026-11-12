namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public interface IOutboxEventPublisher
{
    string Name { get; }

    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
