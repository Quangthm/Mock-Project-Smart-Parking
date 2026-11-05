using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application;

// Input policy is shared by HTTP, background and other application callers.
public sealed class OperatorOperationsService(IOperatorOperationsStore store)
{
    public Task<IReadOnlyList<OperatorTicket>> ListTicketsAsync(Guid siteId, CancellationToken ct) =>
        store.ListTicketsAsync(siteId, ct);
    public Task<OperatorTicket> CreateTicketAsync(Guid tenantId, Guid siteId, Guid actorId, string subject, string message, Guid? bookingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 255 || string.IsNullOrWhiteSpace(message) || message.Trim().Length > 10_000)
            throw new StructureException("INVALID_TICKET", "A subject and message are required.");
        return store.CreateTicketAsync(tenantId, siteId, actorId, subject, message, bookingId, ct);
    }
    public Task<OperatorTicket?> UpdateTicketStatusAsync(Guid tenantId, Guid siteId, Guid actorId, Guid ticketId, string status, string? resolutionNote, CancellationToken ct)
    {
        if (status is not ("open" or "in-progress" or "resolved"))
            throw new StructureException("INVALID_TICKET_STATUS", "Ticket status is invalid.");
        if (resolutionNote?.Length > 2_000)
            throw new StructureException("INVALID_RESOLUTION_NOTE", "Resolution notes must be 2,000 characters or fewer.");
        return store.UpdateTicketStatusAsync(tenantId, siteId, actorId, ticketId, status, resolutionNote, ct);
    }
    public Task<OperatorIncident> CreateIncidentAsync(Guid tenantId, Guid siteId, Guid actorId, string type, string details, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Trim().Length > 80 || string.IsNullOrWhiteSpace(details) || details.Trim().Length > 2_000)
            throw new StructureException("INVALID_INCIDENT", "Incident type and details are required.");
        return store.CreateIncidentAsync(tenantId, siteId, actorId, type, details, ct);
    }
    public Task<OperatorIncident?> ResolveIncidentAsync(Guid tenantId, Guid siteId, Guid incidentId, Guid actorId, CancellationToken ct) =>
        store.ResolveIncidentAsync(tenantId, siteId, incidentId, actorId, ct);
    public Task<IReadOnlyList<OperatorIncident>> ListIncidentsAsync(Guid siteId, CancellationToken ct) =>
        store.ListIncidentsAsync(siteId, ct);
    public Task<IReadOnlyList<OperatorAuditLog>> ListAuditLogsAsync(Guid siteId, CancellationToken ct) =>
        store.ListAuditLogsAsync(siteId, ct);
    public Task RecordAuditAsync(Guid tenantId, Guid siteId, Guid actorId, string action, string details, CancellationToken ct) =>
        store.RecordAuditAsync(tenantId, siteId, actorId, action, details, ct);
    public Task<IReadOnlyList<OperatorNotification>> ListNotificationsAsync(Guid siteId, Guid recipientId, CancellationToken ct) =>
        store.ListNotificationsAsync(siteId, recipientId, ct);
    public Task<bool> MarkNotificationReadAsync(Guid siteId, Guid recipientId, Guid notificationId, CancellationToken ct) =>
        store.MarkNotificationReadAsync(siteId, recipientId, notificationId, ct);
}
