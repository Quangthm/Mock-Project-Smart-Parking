using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Contracts.Audit;
using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections;

namespace SmartParking.NotificationAuditService.Infrastructure.Consumers;

public class AuditProjectionConsumer : IConsumer<AuditProjectionEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuditProjectionConsumer> _logger;

    public AuditProjectionConsumer(
        IMediator mediator,
        ILogger<AuditProjectionConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuditProjectionEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Consuming audit event {EventId} from originating service {OriginatingService}. Action: {Action}, Resource: {ResourceType}/{ResourceId}",
            message.EventId,
            message.OriginatingService,
            message.Action,
            message.ResourceType,
            message.ResourceId);

        var command = new RecordAuditProjectionCommand
        {
            EventId = message.EventId,
            OriginatingService = message.OriginatingService,
            ActorId = message.ActorId,
            ActorRole = message.ActorRole,
            Action = message.Action,
            ResourceType = message.ResourceType,
            ResourceId = message.ResourceId,
            Details = message.Details,
            OccurredAt = message.OccurredAt
        };

        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.IsDuplicate)
        {
            _logger.LogInformation(
                "Consumer acknowledged duplicate audit event {EventId} without error.",
                message.EventId);
        }
        else if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Consumer successfully processed audit projection for event {EventId} (Projection ID: {ProjectionId}).",
                message.EventId,
                result.ProjectionId);
        }
        else
        {
            _logger.LogError(
                "Failed to record audit projection for event {EventId}: {ErrorMessage}",
                message.EventId,
                result.Message);

            throw new InvalidOperationException($"Failed to record audit projection for event {message.EventId}: {result.Message}");
        }
    }
}
