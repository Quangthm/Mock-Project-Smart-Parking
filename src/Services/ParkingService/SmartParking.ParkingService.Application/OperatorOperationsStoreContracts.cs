using System.Text.Json;
namespace SmartParking.ParkingService.Application;

public sealed record OperatorTicket(
    Guid Id,
    string UserId,
    string UserName,
    string Subject,
    string Message,
    Guid? BookingId,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record OperatorIncident(
    Guid Id,
    Guid SiteId,
    string Type,
    string Details,
    string Status,
    Guid ActorId,
    DateTimeOffset CreatedAt);

public sealed record OperatorNotification(
    Guid Id,
    Guid RecipientId,
    string Type,
    string Title,
    string Message,
    DateTimeOffset Timestamp,
    bool IsRead,
    Guid? RelatedEntityId);

public sealed record OperatorAuditLog(
    Guid Id,
    Guid? ActorId,
    string ActorRole,
    string Action,
    string Details,
    DateTimeOffset Timestamp);


public interface IOperatorOperationsStore
{
    Task<IReadOnlyList<OperatorTicket>> ListTicketsAsync(Guid siteId, CancellationToken ct);
    Task<OperatorTicket> CreateTicketAsync(Guid tenantId, Guid siteId, Guid actorId, string subject, string message, Guid? bookingId, CancellationToken ct);
    Task<OperatorTicket?> UpdateTicketStatusAsync(Guid tenantId, Guid siteId, Guid actorId, Guid ticketId, string status, string? resolutionNote, CancellationToken ct);
    Task<OperatorIncident> CreateIncidentAsync(Guid tenantId, Guid siteId, Guid actorId, string type, string details, CancellationToken ct);
    Task<OperatorIncident?> ResolveIncidentAsync(Guid tenantId, Guid siteId, Guid incidentId, Guid actorId, CancellationToken ct);
    Task<IReadOnlyList<OperatorIncident>> ListIncidentsAsync(Guid siteId, CancellationToken ct);
    Task<IReadOnlyList<OperatorAuditLog>> ListAuditLogsAsync(Guid siteId, CancellationToken ct);
    Task RecordAuditAsync(Guid tenantId, Guid siteId, Guid actorId, string action, string details, CancellationToken ct);
    Task<IReadOnlyList<OperatorNotification>> ListNotificationsAsync(Guid siteId, Guid recipientId, CancellationToken ct);
    Task<bool> MarkNotificationReadAsync(Guid siteId, Guid recipientId, Guid notificationId, CancellationToken ct);
}
