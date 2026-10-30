using Microsoft.EntityFrameworkCore;

namespace SmartParking.FacilityService.Persistence;

public sealed record OperatorTicket(Guid Id, string UserId, string UserName, string Subject, string Message,
    Guid? BookingId, string Status, DateTimeOffset CreatedAt);
public sealed record OperatorIncident(Guid Id, Guid SiteId, string Type, string Details, string Status, Guid ActorId, DateTimeOffset CreatedAt);
public sealed record OperatorNotification(Guid Id, Guid RecipientId, string Type, string Title, string Message,
    DateTimeOffset Timestamp, bool IsRead, Guid? RelatedEntityId);
public sealed record OperatorAuditLog(Guid Id, Guid? ActorId, string ActorRole, string Action, string Details, DateTimeOffset Timestamp);

public sealed class OperatorOperationsStore(ParkingDbContext db)
{
    private static OperatorTicket Ticket(SupportTicketEntity ticket) =>
        new(ticket.Id, ticket.UserId.ToString(), ticket.UserId.ToString(), ticket.Subject, ticket.Message,
            ticket.BookingId, ticket.Status, ticket.CreatedAt);

    private static OperatorIncident Incident(OperatorIncidentEntity incident) =>
        new(incident.Id, incident.SiteId, incident.Type, incident.Details, incident.Status,
            incident.ActorId, incident.CreatedAt);

    private static async Task AddAuditAsync(ParkingDbContext db, Guid tenantId, Guid siteId, Guid actorId,
        string action, Guid? targetId, string details, CancellationToken ct)
    {
        db.OperatorAuditLogs.Add(new OperatorAuditLogEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, ActorId = actorId,
            Action = action, TargetId = targetId, Reason = details,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<IReadOnlyList<OperatorTicket>> ListTicketsAsync(Guid siteId, CancellationToken ct) =>
        (await db.SupportTickets.AsNoTracking().Where(x => x.SiteId == siteId)
            .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct)).Select(Ticket).ToList();

    public async Task<OperatorTicket> CreateTicketAsync(Guid tenantId, Guid siteId, Guid actorId,
        string subject, string message, Guid? bookingId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new SupportTicketEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, UserId = actorId,
            Subject = subject.Trim(), Message = message.Trim(), BookingId = bookingId,
            Status = "open", CreatedAt = now, UpdatedAt = now
        };
        db.SupportTickets.Add(entity);
        await db.SaveChangesAsync(ct);
        return Ticket(entity);
    }

    public async Task<OperatorTicket?> UpdateTicketStatusAsync(Guid tenantId, Guid siteId, Guid actorId,
        Guid ticketId, string status, string? resolutionNote, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(x =>
            x.Id == ticketId && x.SiteId == siteId && x.Status != "resolved", ct);
        if (ticket is null) return null;
        var note = resolutionNote?.Trim();
        if (!string.IsNullOrWhiteSpace(note))
            ticket.Message += "\n\n[Operator resolution note]: " + note;
        ticket.Status = status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await AddAuditAsync(db, tenantId, siteId, actorId, "SUPPORT_TICKET_UPDATED",
            ticketId, $"Ticket status changed to {status}.", ct);
        await transaction.CommitAsync(ct);
        return Ticket(ticket);
    }

    public async Task<OperatorIncident> CreateIncidentAsync(Guid tenantId, Guid siteId, Guid actorId,
        string type, string details, CancellationToken ct)
    {
        var entity = new OperatorIncidentEntity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, ActorId = actorId,
            Type = type.Trim(), Details = details.Trim(), Status = "reported",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.OperatorIncidents.Add(entity);
        await db.SaveChangesAsync(ct);
        await AddAuditAsync(db, tenantId, siteId, actorId, "EMERGENCY_ACTION", entity.Id, entity.Details, ct);
        await transaction.CommitAsync(ct);
        return Incident(entity);
    }

    public async Task<OperatorIncident?> ResolveIncidentAsync(Guid tenantId, Guid siteId, Guid incidentId,
        Guid actorId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var entity = await db.OperatorIncidents.FirstOrDefaultAsync(x =>
            x.Id == incidentId && x.SiteId == siteId && x.Status == "reported", ct);
        if (entity is null) return null;
        entity.Status = "resolved";
        await db.SaveChangesAsync(ct);
        await AddAuditAsync(db, tenantId, siteId, actorId, "INCIDENT_RESOLVED", incidentId, entity.Details, ct);
        await transaction.CommitAsync(ct);
        return Incident(entity);
    }

    public async Task<IReadOnlyList<OperatorIncident>> ListIncidentsAsync(Guid siteId, CancellationToken ct) =>
        (await db.OperatorIncidents.AsNoTracking().Where(x => x.SiteId == siteId)
            .OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct)).Select(Incident).ToList();

    public async Task<IReadOnlyList<OperatorAuditLog>> ListAuditLogsAsync(Guid siteId, CancellationToken ct) =>
        await db.OperatorAuditLogs.AsNoTracking().Where(x => x.SiteId == siteId)
            .OrderByDescending(x => x.CreatedAt).Take(200)
            .Select(x => new OperatorAuditLog(x.Id, x.ActorId, x.ActorRole, x.Action, x.Reason, x.CreatedAt))
            .ToListAsync(ct);

    public Task RecordAuditAsync(Guid tenantId, Guid siteId, Guid actorId, string action,
        string details, CancellationToken ct) =>
        AddAuditAsync(db, tenantId, siteId, actorId, action.Trim(), null, details.Trim(), ct);

    public async Task<IReadOnlyList<OperatorNotification>> ListNotificationsAsync(Guid siteId,
        Guid recipientId, CancellationToken ct) =>
        await db.OperatorNotifications.AsNoTracking()
            .Where(x => x.SiteId == siteId && x.RecipientId == recipientId)
            .OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new OperatorNotification(x.Id, x.RecipientId, x.Type, x.Title, x.Message,
                x.CreatedAt, x.IsRead, x.RelatedEntityId)).ToListAsync(ct);

    public async Task<bool> MarkNotificationReadAsync(Guid siteId, Guid recipientId,
        Guid notificationId, CancellationToken ct)
    {
        var notification = await db.OperatorNotifications.FirstOrDefaultAsync(x =>
            x.Id == notificationId && x.SiteId == siteId && x.RecipientId == recipientId, ct);
        if (notification is null) return false;
        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
