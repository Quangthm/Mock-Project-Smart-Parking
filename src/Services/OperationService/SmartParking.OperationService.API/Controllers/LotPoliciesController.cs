using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.Policies.Commands;
using SmartParking.OperationService.Application.Policies.DTOs;
using SmartParking.OperationService.Application.Policies.Queries;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API.Controllers;

[Route("api/v1/lots/{siteId:guid}/policies")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LotPoliciesController(IMediator mediator, ICurrentActor actor) : ApiControllerBase(mediator)
{
    [HttpGet("catalogue")]
    public async Task<IActionResult> GetCatalogue([FromRoute] Guid siteId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetLotPolicyCatalogueQuery(siteId, actor.AccountId ?? actor.UserId), ct);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("draft")]
    public async Task<IActionResult> GetDraft([FromRoute] Guid siteId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetLotPolicyDraftQuery(siteId, actor.AccountId ?? actor.UserId), ct);
        return Ok(new { success = true, data = result });
    }

    [HttpPut("draft")]
    public async Task<IActionResult> SaveDraft(
        [FromRoute] Guid siteId,
        [FromBody] SavePolicyDraftRequest request,
        CancellationToken ct)
    {
        var result = await Mediator.Send(new SaveLotPolicyDraftCommand(siteId, actor.AccountId ?? actor.UserId, request), ct);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish(
        [FromRoute] Guid siteId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] PublishPolicyRequest request,
        CancellationToken ct)
    {
        var result = await Mediator.Send(new PublishLotPolicyCommand(siteId, actor.AccountId ?? actor.UserId, idempotencyKey, request), ct);
        if (result.IsReplayed)
        {
            Response.Headers.TryAdd("Idempotency-Replayed", "true");
        }
        return Ok(new { success = true, data = result });
    }

    [HttpGet("revisions")]
    public async Task<IActionResult> GetRevisions(
        [FromRoute] Guid siteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetLotPolicyRevisionsQuery(siteId, actor.AccountId ?? actor.UserId, page, pageSize), ct);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("revisions/{version:int}")]
    public async Task<IActionResult> GetRevisionSnapshot(
        [FromRoute] Guid siteId,
        [FromRoute] int version,
        CancellationToken ct)
    {
        var result = await Mediator.Send(new GetLotPolicyRevisionSnapshotQuery(siteId, version, actor.AccountId ?? actor.UserId), ct);
        return Ok(new { success = true, data = result });
    }
}
