using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.OperationService.API.Controllers.Base;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API.Controllers;

[ApiController]
[Route("api/v1/admin/policies/operations")]
[Authorize(Roles = "admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformPoliciesController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet("catalogue")]
    public async Task<IActionResult> Catalogue(CancellationToken ct) =>
        Ok(new { success = true, data = await Mediator.Send(new GetPlatformPolicyCatalogueQuery(), ct) });

    [HttpGet("effective")]
    public async Task<IActionResult> Effective([FromQuery] Guid? tenantId, [FromQuery] Guid? siteId,
        [FromQuery] DateTimeOffset? at, CancellationToken ct) =>
        Ok(new
        {
            success = true,
            data = await Mediator.Send(new GetEffectivePoliciesQuery(tenantId, siteId, at), ct)
        });

    [HttpGet("revisions")]
    public async Task<IActionResult> Revisions([FromQuery] string status = "ALL", [FromQuery] string scope = "ALL",
        [FromQuery] Guid? tenantId = null, [FromQuery] Guid? siteId = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(new
        {
            success = true,
            data = await Mediator.Send(
                new GetPolicyRevisionsQuery(status, scope, tenantId, siteId, page, pageSize), ct)
        });

    [HttpGet("revisions/{id:guid}")]
    public async Task<IActionResult> GetRevisionById([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetPolicyRevisionByIdQuery(id), ct);
        if (result is null)
            throw new PolicyNotFoundException($"Policy revision '{id}' was not found.");
        return Ok(new { success = true, data = result });
    }

    [HttpPost("draft")]
    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft(CreatePolicyDraftRequestDto request, CancellationToken ct)
    {
        var result = await Mediator.Send(ToCommand(request), ct);
        return StatusCode(StatusCodes.Status201Created, new { success = true, data = result });
    }

    [HttpPut("draft/{id:guid}")]
    [HttpPut("drafts/{id:guid}")]
    public async Task<IActionResult> UpdateDraft([FromRoute] Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch, UpdatePolicyDraftRequestDto request, CancellationToken ct)
    {
        var expectedUpdatedAt = ResolveExpectedUpdatedAt(ifMatch, request.ExpectedUpdatedAt);
        var result = await Mediator.Send(ToCommand(id, expectedUpdatedAt, request), ct);
        return Ok(new { success = true, message = "Platform policy draft updated successfully.", data = result });
    }

    [HttpPost("revisions/{id:guid}/activate")]
    public async Task<IActionResult> Activate([FromRoute] Guid id,
        SchedulePolicyRevisionRequestDto? request, CancellationToken ct) =>
        Ok(new
        {
            success = true,
            data = await Mediator.Send(new SchedulePolicyRevisionCommand(id, request?.EffectiveFrom), ct)
        });

    private static CreatePolicyDraftCommand ToCommand(CreatePolicyDraftRequestDto request) => new(
        request.TenantId, request.SiteId, request.PaymentHoldDurationMinutes, request.MinReservationDurationMinutes,
        request.MaxReservationDurationHours, request.MaxAdvanceBookingDays, request.InterBookingBufferMinutes,
        request.ReservationProtectionWindowHours, request.AllocationLeadTimeMinutes,
        request.FacilityFailureRefundLeadMinutes, request.OccupancyValidityTimespanHours,
        request.ArrivalLateToleranceMinutes, request.RapidCancellationCount, request.RapidCancellationWindowMinutes,
        request.RapidCancellationRestrictionMinutes, request.DailyCancellationLimit, request.ArrivalLateToleranceEnabled,
        request.CancellationTimezone, request.OverrideBounds);

    private static UpdatePolicyDraftCommand ToCommand(Guid id, DateTimeOffset expectedUpdatedAt,
        UpdatePolicyDraftRequestDto request) => new(
        id, expectedUpdatedAt, request.TenantId, request.SiteId, request.PaymentHoldDurationMinutes,
        request.MinReservationDurationMinutes, request.MaxReservationDurationHours, request.MaxAdvanceBookingDays,
        request.InterBookingBufferMinutes, request.ReservationProtectionWindowHours, request.AllocationLeadTimeMinutes,
        request.FacilityFailureRefundLeadMinutes, request.OccupancyValidityTimespanHours,
        request.ArrivalLateToleranceMinutes, request.RapidCancellationCount, request.RapidCancellationWindowMinutes,
        request.RapidCancellationRestrictionMinutes, request.DailyCancellationLimit, request.ArrivalLateToleranceEnabled,
        request.CancellationTimezone, request.OverrideBounds);

    private static DateTimeOffset ResolveExpectedUpdatedAt(string? ifMatch, DateTimeOffset? bodyExpectedUpdatedAt)
    {
        DateTimeOffset? headerExpectedUpdatedAt = null;
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var raw = ifMatch.Trim().Trim('"');
            if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                    out var parsed))
                throw new InvalidRequestException("If-Match header must be an ISO 8601 timestamp.");
            headerExpectedUpdatedAt = parsed;
        }

        if (headerExpectedUpdatedAt is not null && bodyExpectedUpdatedAt is not null &&
            headerExpectedUpdatedAt.Value != bodyExpectedUpdatedAt.Value)
            throw new InvalidRequestException(
                "If-Match header and expectedUpdatedAt body property must match.");

        return headerExpectedUpdatedAt ?? bodyExpectedUpdatedAt ??
            throw new RequestValidationFailedException(
                "Concurrency token expectedUpdatedAt or If-Match header is required for draft updates.");
    }
}