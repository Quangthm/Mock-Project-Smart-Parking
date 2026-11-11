using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.DTOs.Audit;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections;

public class RecordAuditProjectionCommandHandler : IRequestHandler<RecordAuditProjectionCommand, RecordAuditProjectionResult>
{
    private readonly ICentralAuditProjectionRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecordAuditProjectionCommandHandler> _logger;

    public RecordAuditProjectionCommandHandler(
        ICentralAuditProjectionRepository auditRepository,
        IUnitOfWork unitOfWork,
        ILogger<RecordAuditProjectionCommandHandler> logger)
    {
        _auditRepository = auditRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RecordAuditProjectionResult> Handle(RecordAuditProjectionCommand request, CancellationToken cancellationToken)
    {
        // 1. Durable Deduplication Check via Database
        var isDuplicate = await _auditRepository.ExistsByEventIdAsync(request.EventId, cancellationToken);
        if (isDuplicate)
        {
            _logger.LogInformation(
                "Audit event {EventId} from service {OriginatingService} was already projected. Deduplicating and ignoring.",
                request.EventId,
                request.OriginatingService);

            return RecordAuditProjectionResult.Deduplicated(request.EventId);
        }

        // 2. Pure Domain Entity Invariant Creation
        var projection = CentralAuditProjection.Create(
            originatingService: request.OriginatingService,
            eventId: request.EventId,
            actorId: request.ActorId,
            actorRole: request.ActorRole,
            action: request.Action,
            resourceType: request.ResourceType,
            resourceId: request.ResourceId,
            details: request.Details,
            occurredAt: request.OccurredAt);

        // 3. Append-only persistence & local transaction commit
        await _auditRepository.AddAsync(projection, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Structured safe operational logging (no sensitive details payload logged)
        _logger.LogInformation(
            "Successfully recorded central audit projection {ProjectionId} for event {EventId} from service {OriginatingService}. Action: {Action}, Resource: {ResourceType}/{ResourceId}",
            projection.Id,
            projection.EventId,
            projection.OriginatingService,
            projection.Action,
            projection.ResourceType,
            projection.ResourceId);

        return RecordAuditProjectionResult.Success(projection.Id, projection.EventId);
    }
}
