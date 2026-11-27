using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.GetAuditProjectionById;
using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.SearchAuditProjections;

namespace SmartParking.NotificationAuditService.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/audit-projections")]
[Route("api/audit-projections")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuditProjectionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditProjectionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? actorId,
        [FromQuery] string? actorRole,
        [FromQuery] string? action,
        [FromQuery] string? originatingService,
        [FromQuery] string? resourceType,
        [FromQuery] Guid? resourceId,
        [FromQuery] Guid? lotId,
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchAuditProjectionsQuery(
            ActorId: actorId,
            ActorRole: actorRole,
            Action: action,
            OriginatingService: originatingService,
            ResourceType: resourceType,
            ResourceId: resourceId,
            LotId: lotId,
            Search: search,
            From: from,
            To: to,
            Page: page,
            PageSize: pageSize);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new
        {
            success = true,
            data = result
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAuditProjectionByIdQuery(id), cancellationToken);
        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
