using SmartParking.ParkingService.Infrastructure;
using SmartParking.ParkingService.Persistence;

namespace SmartParking.ParkingService.API;

internal static class OperatorOperationsEndpoints
{
    public static async Task MapOperatorOperationsAsync(this WebApplication app)
    {
        app.MapGet("/api/operator/sites/{siteId:guid}/tickets", async (
            Guid siteId,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            await identity.ScopeAsync(siteId, ct);
            return Results.Ok(new { success = true, data = await store.ListTicketsAsync(siteId, ct) });
        });
        app.MapPost("/api/operator/sites/{siteId:guid}/tickets", async (
            Guid siteId,
            CreateTicketRequest body,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Subject) || body.Subject.Trim().Length > 255 ||
                string.IsNullOrWhiteSpace(body.Message) || body.Message.Trim().Length > 10_000)
                return Results.BadRequest(new { success = false, code = "INVALID_TICKET", message = "A subject and message are required." });
            var scope = await identity.ScopeAsync(siteId, ct);
            var ticket = await store.CreateTicketAsync(
                scope.TenantId, siteId, scope.UserId, body.Subject, body.Message, body.BookingId, ct);
            return Results.Created($"/api/operator/sites/{siteId}/tickets/{ticket.Id}", new { success = true, data = ticket });
        });
        app.MapPatch("/api/operator/sites/{siteId:guid}/tickets/{ticketId:guid}", async (
            Guid siteId,
            Guid ticketId,
            UpdateTicketRequest body,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            if (body.Status is not ("open" or "in-progress" or "resolved"))
                return Results.BadRequest(new { success = false, code = "INVALID_TICKET_STATUS", message = "Ticket status is invalid." });
            var scope = await identity.ScopeAsync(siteId, ct);
            if (body.ResolutionNote?.Length > 2_000)
                return Results.BadRequest(new { success = false, code = "INVALID_RESOLUTION_NOTE", message = "Resolution notes must be 2,000 characters or fewer." });
            var updated = await store.UpdateTicketStatusAsync(
                scope.TenantId, siteId, scope.UserId, ticketId, body.Status, body.ResolutionNote, ct);
            return updated is null
                ? Results.NotFound(new { success = false, code = "TICKET_NOT_FOUND", message = "Ticket was not found or is already resolved." })
                : Results.Ok(new { success = true, data = updated });
        });
        app.MapGet("/api/operator/sites/{siteId:guid}/incidents", async (
            Guid siteId,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            await identity.ScopeAsync(siteId, ct);
            return Results.Ok(new { success = true, data = await store.ListIncidentsAsync(siteId, ct) });
        });
        app.MapPost("/api/operator/sites/{siteId:guid}/incidents", async (
            Guid siteId,
            CreateIncidentRequest body,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Type) || body.Type.Trim().Length > 80 ||
                string.IsNullOrWhiteSpace(body.Details) || body.Details.Trim().Length > 2_000)
                return Results.BadRequest(new { success = false, code = "INVALID_INCIDENT", message = "Incident type and details are required." });
            var scope = await identity.ScopeAsync(siteId, ct);
            var incident = await store.CreateIncidentAsync(
                scope.TenantId, siteId, scope.UserId, body.Type, body.Details, ct);
            return Results.Created($"/api/operator/sites/{siteId}/incidents/{incident.Id}", new { success = true, data = incident });
        });
        app.MapPatch("/api/operator/sites/{siteId:guid}/incidents/{incidentId:guid}", async (
            Guid siteId,
            Guid incidentId,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            var scope = await identity.ScopeAsync(siteId, ct);
            var resolved = await store.ResolveIncidentAsync(scope.TenantId, siteId, incidentId, scope.UserId, ct);
            return resolved is null
                ? Results.NotFound(new { success = false, code = "INCIDENT_NOT_FOUND", message = "Incident was not found or already resolved." })
                : Results.Ok(new { success = true, data = resolved });
        });
        app.MapGet("/api/operator/sites/{siteId:guid}/audit", async (
            Guid siteId,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            await identity.ScopeAsync(siteId, ct);
            return Results.Ok(new { success = true, data = await store.ListAuditLogsAsync(siteId, ct) });
        });
        app.MapPost("/api/operator/sites/{siteId:guid}/audit", async (
            Guid siteId,
            CreateAuditRequest body,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Action) || body.Action.Trim().Length > 100 ||
                string.IsNullOrWhiteSpace(body.Details) || body.Details.Trim().Length > 2_000)
                return Results.BadRequest(new { success = false, code = "INVALID_AUDIT_EVENT", message = "Action and details are required." });
            var scope = await identity.ScopeAsync(siteId, ct);
            await store.RecordAuditAsync(scope.TenantId, siteId, scope.UserId, body.Action, body.Details, ct);
            return Results.NoContent();
        });
        app.MapGet("/api/operator/sites/{siteId:guid}/notifications", async (
            Guid siteId,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            var scope = await identity.ScopeAsync(siteId, ct);
            return Results.Ok(new
            {
                success = true,
                data = await store.ListNotificationsAsync(siteId, scope.UserId, ct)
            });
        });
        app.MapPatch("/api/operator/sites/{siteId:guid}/notifications/{notificationId:guid}/read", async (
            Guid siteId,
            Guid notificationId,
            OperatorSiteClient identity,
            OperatorOperationsStore store,
            CancellationToken ct) =>
        {
            var scope = await identity.ScopeAsync(siteId, ct);
            return await store.MarkNotificationReadAsync(siteId, scope.UserId, notificationId, ct)
                ? Results.NoContent()
                : Results.NotFound(new { success = false, code = "NOTIFICATION_NOT_FOUND", message = "Notification was not found." });
        });

        using var serviceScope = app.Services.CreateScope();
        await serviceScope.ServiceProvider.GetRequiredService<OperatorOperationsStore>()
            .EnsureSchemaAsync(CancellationToken.None);
    }
}

public sealed record CreateTicketRequest(string Subject, string Message, Guid? BookingId);
public sealed record UpdateTicketRequest(string Status, string? ResolutionNote);
public sealed record CreateIncidentRequest(string Type, string Details);
public sealed record CreateAuditRequest(string Action, string Details);
