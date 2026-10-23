using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartParking.ReservationService.API.Controllers.Base;
using SmartParking.ReservationService.Application;
using SmartParking.ReservationService.Application.Usecase.Structure;
using SmartParking.ReservationService.Domain;

namespace SmartParking.ReservationService.API.Controllers;

[Route("internal/structure")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StructureController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpPut("{siteId:guid}/{token:guid}")]
    public async Task<IActionResult> Prepare(
        [FromRoute] Guid siteId,
        [FromRoute] Guid token,
        [FromBody] HoldRequest body,
        CancellationToken ct) =>
        Ok(await Mediator.Send(new PrepareStructureEditCommand(body.TenantId, siteId, token), ct));

    [HttpPost("{siteId:guid}/impact")]
    public async Task<IActionResult> Impact(
        [FromRoute] Guid siteId,
        [FromBody] HoldRequest body,
        CancellationToken ct) =>
        Ok(await Mediator.Send(new ReadStructureImpactQuery(body.TenantId, siteId), ct));

    [HttpPost("{siteId:guid}/{token:guid}/release")]
    public async Task<IActionResult> Release(
        [FromRoute] Guid siteId,
        [FromRoute] Guid token,
        [FromBody] ReleaseRequest body,
        CancellationToken ct)
    {
        await Mediator.Send(new CompleteStructureEditCommand(body.TenantId, siteId, token, body.Outcome), ct);
        return NoContent();
    }

    [HttpPost("{siteId:guid}/snapshot")]
    public async Task<IActionResult> Snapshot(
        [FromRoute] Guid siteId,
        [FromBody] HoldRequest body,
        CancellationToken ct) =>
        Ok(await Mediator.Send(new ReadStructureSnapshotQuery(body.TenantId, siteId), ct));
}

public sealed record HoldRequest(Guid TenantId);
public sealed record ReleaseRequest(Guid TenantId, EditOutcome? Outcome);
