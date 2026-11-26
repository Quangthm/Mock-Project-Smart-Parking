using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.DTOs.Audit;

namespace SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.GetAuditProjectionById;

public sealed class GetAuditProjectionByIdQueryHandler
    : IRequestHandler<GetAuditProjectionByIdQuery, AuditProjectionDto>
{
    private readonly ICentralAuditProjectionRepository _repository;
    private readonly ILogger<GetAuditProjectionByIdQueryHandler> _logger;

    public GetAuditProjectionByIdQueryHandler(
        ICentralAuditProjectionRepository repository,
        ILogger<GetAuditProjectionByIdQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AuditProjectionDto> Handle(
        GetAuditProjectionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var projection = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (projection is null)
        {
            throw new KeyNotFoundException($"Audit projection '{request.Id}' was not found.");
        }

        _logger.LogInformation(
            "Admin inspected audit projection {ProjectionId} (EventId={EventId}, Action={Action})",
            projection.Id,
            projection.EventId,
            projection.Action);

        return AuditProjectionMapper.ToRedactedDto(projection);
    }
}
