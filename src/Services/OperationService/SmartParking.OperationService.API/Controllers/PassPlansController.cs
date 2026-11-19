using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmartParking.OperationService.API.Authentication;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Queries;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API.Controllers;

[Authorize(Policy = PassPlanOwnerPolicy.Name)]
[Route("api/v1/lots/{siteId:guid}/pass-plans")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PassPlansController(IMediator mediator, ICurrentActor actor) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> GetPlans(Guid siteId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetSitePassPlansQuery(siteId), ct);
        return Ok(new { success = true, data = result });
    }

    [HttpPut("{productType}/draft")]
    public async Task<IActionResult> SaveDraft(
        Guid siteId,
        string productType,
        SavePassPlanDraftRequest request,
        CancellationToken ct)
    {
        var result = await Mediator.Send(
            new SavePassPlanDraftCommand(siteId, productType, actor.UserId, request),
            ct);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("{productType}/publish")]
    public async Task<IActionResult> Publish(
        Guid siteId,
        string productType,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PublishPassPlanRequest? request = null,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new PublishPassPlanCommand(siteId, productType, actor.UserId, idempotencyKey, request),
            ct);
        if (result.IsReplayed)
        {
            Response.Headers.TryAdd("Idempotency-Replayed", "true");
        }

        return Ok(new { success = true, data = result });
    }

    [HttpPatch("{productType}/enablement")]
    public async Task<IActionResult> SetEnablement(
        Guid siteId,
        string productType,
        SetPassPlanEnablementRequest request,
        CancellationToken ct)
    {
        var result = await Mediator.Send(
            new SetPassPlanEnablementCommand(siteId, productType, actor.UserId, request),
            ct);
        return Ok(new { success = true, data = result });
    }
}
