using Microsoft.AspNetCore.Mvc;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.API.Controllers;

[ApiController]
[Route("api/operator/sites/{siteId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OperatorOperationsController(
    IOperatorSiteAuthorizer identity,
    OperatorOperationsService operations) : ControllerBase
{
    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets(Guid siteId, CancellationToken ct)
    {
        await identity.ScopeAsync(siteId, ct);
        return Ok(new { success = true, data = await operations.ListTicketsAsync(siteId, ct) });
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(Guid siteId, CreateTicketRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Subject) || body.Subject.Trim().Length > 255 ||
            string.IsNullOrWhiteSpace(body.Message) || body.Message.Trim().Length > 10_000)
            return BadRequest(new { success = false, code = "INVALID_TICKET", message = "A subject and message are required." });
        var scope = await identity.ScopeAsync(siteId, ct);
        var ticket = await operations.CreateTicketAsync(scope.TenantId, siteId, scope.UserId,
            body.Subject, body.Message, body.BookingId, ct);
        return Created($"/api/operator/sites/{siteId}/tickets/{ticket.Id}",
            new { success = true, data = ticket });
    }

    [HttpPatch("tickets/{ticketId:guid}")]
    public async Task<IActionResult> UpdateTicket(Guid siteId, Guid ticketId,
        UpdateTicketRequest body, CancellationToken ct)
    {
        if (body.Status is not ("open" or "in-progress" or "resolved"))
            return BadRequest(new { success = false, code = "INVALID_TICKET_STATUS", message = "Ticket status is invalid." });
        var scope = await identity.ScopeAsync(siteId, ct);
        if (body.ResolutionNote?.Length > 2_000)
            return BadRequest(new { success = false, code = "INVALID_RESOLUTION_NOTE", message = "Resolution notes must be 2,000 characters or fewer." });
        var updated = await operations.UpdateTicketStatusAsync(scope.TenantId, siteId, scope.UserId,
            ticketId, body.Status, body.ResolutionNote, ct);
        return updated is null
            ? NotFound(new { success = false, code = "TICKET_NOT_FOUND", message = "Ticket was not found or is already resolved." })
            : Ok(new { success = true, data = updated });
    }

    [HttpGet("incidents")]
    public async Task<IActionResult> Incidents(Guid siteId, CancellationToken ct)
    {
        await identity.ScopeAsync(siteId, ct);
        return Ok(new { success = true, data = await operations.ListIncidentsAsync(siteId, ct) });
    }

    [HttpPost("incidents")]
    public async Task<IActionResult> CreateIncident(Guid siteId, CreateIncidentRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Type) || body.Type.Trim().Length > 80 ||
            string.IsNullOrWhiteSpace(body.Details) || body.Details.Trim().Length > 2_000)
            return BadRequest(new { success = false, code = "INVALID_INCIDENT", message = "Incident type and details are required." });
        var scope = await identity.ScopeAsync(siteId, ct);
        var incident = await operations.CreateIncidentAsync(scope.TenantId, siteId, scope.UserId,
            body.Type, body.Details, ct);
        return Created($"/api/operator/sites/{siteId}/incidents/{incident.Id}",
            new { success = true, data = incident });
    }

    [HttpPatch("incidents/{incidentId:guid}")]
    public async Task<IActionResult> ResolveIncident(Guid siteId, Guid incidentId, CancellationToken ct)
    {
        var scope = await identity.ScopeAsync(siteId, ct);
        var resolved = await operations.ResolveIncidentAsync(scope.TenantId, siteId, incidentId,
            scope.UserId, ct);
        return resolved is null
            ? NotFound(new { success = false, code = "INCIDENT_NOT_FOUND", message = "Incident was not found or already resolved." })
            : Ok(new { success = true, data = resolved });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(Guid siteId, CancellationToken ct)
    {
        await identity.ScopeAsync(siteId, ct);
        return Ok(new { success = true, data = await operations.ListAuditLogsAsync(siteId, ct) });
    }

    [HttpPost("audit")]
    public async Task<IActionResult> CreateAudit(Guid siteId, CreateAuditRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Action) || body.Action.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(body.Details) || body.Details.Trim().Length > 2_000)
            return BadRequest(new { success = false, code = "INVALID_AUDIT_EVENT", message = "Action and details are required." });
        var scope = await identity.ScopeAsync(siteId, ct);
        await operations.RecordAuditAsync(scope.TenantId, siteId, scope.UserId,
            body.Action, body.Details, ct);
        return NoContent();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(Guid siteId, CancellationToken ct)
    {
        var scope = await identity.ScopeAsync(siteId, ct);
        return Ok(new { success = true, data = await operations.ListNotificationsAsync(siteId, scope.UserId, ct) });
    }

    [HttpPatch("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid siteId, Guid notificationId, CancellationToken ct)
    {
        var scope = await identity.ScopeAsync(siteId, ct);
        return await operations.MarkNotificationReadAsync(siteId, scope.UserId, notificationId, ct)
            ? NoContent()
            : NotFound(new { success = false, code = "NOTIFICATION_NOT_FOUND", message = "Notification was not found." });
    }
}
