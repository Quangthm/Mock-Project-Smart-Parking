using SmartParking.FacilityService.Application;
using Npgsql;

namespace SmartParking.FacilityService.Persistence;

public sealed class OperatorOperationsStore(NpgsqlDataSource source) : IOperatorOperationsStore
{
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS support_tickets (
                id UUID PRIMARY KEY,
                tenant_id UUID NOT NULL,
                site_id UUID NOT NULL,
                user_id UUID NOT NULL,
                subject VARCHAR(255) NOT NULL,
                message TEXT NOT NULL,
                booking_id UUID,
                status VARCHAR(20) NOT NULL DEFAULT 'open'
                    CHECK (status IN ('open', 'in-progress', 'resolved')),
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_support_tickets_site_created
                ON support_tickets (site_id, created_at DESC);

            CREATE TABLE IF NOT EXISTS operator_incident_events (
                id UUID PRIMARY KEY,
                tenant_id UUID NOT NULL,
                site_id UUID NOT NULL,
                actor_id UUID NOT NULL,
                incident_type VARCHAR(80) NOT NULL,
                details TEXT NOT NULL,
                status VARCHAR(20) NOT NULL DEFAULT 'reported'
                    CHECK (status IN ('reported', 'resolved')),
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_operator_incidents_site_created
                ON operator_incident_events (site_id, created_at DESC);

            CREATE TABLE IF NOT EXISTS operator_notifications (
                id UUID PRIMARY KEY,
                tenant_id UUID NOT NULL,
                site_id UUID NOT NULL,
                recipient_id UUID NOT NULL,
                notification_type VARCHAR(80) NOT NULL,
                title VARCHAR(255) NOT NULL,
                message TEXT NOT NULL,
                related_entity_id UUID,
                is_read BOOLEAN NOT NULL DEFAULT FALSE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_operator_notifications_recipient
                ON operator_notifications (recipient_id, created_at DESC);

            CREATE TABLE IF NOT EXISTS operator_audit_logs (
                id UUID PRIMARY KEY,
                tenant_id UUID NOT NULL,
                site_id UUID NOT NULL,
                actor_id UUID,
                actor_role VARCHAR(50) NOT NULL,
                action VARCHAR(100) NOT NULL,
                target_entity VARCHAR(100) NOT NULL,
                target_id UUID,
                reason TEXT NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_operator_audit_logs_site_created
                ON operator_audit_logs (site_id, created_at DESC);
            """, db);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<OperatorTicket>> ListTicketsAsync(Guid siteId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT id, user_id, subject, message, booking_id, status, created_at
            FROM support_tickets
            WHERE site_id = $1
            ORDER BY created_at DESC
            LIMIT 200
            """, db);
        command.Parameters.AddWithValue(siteId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var tickets = new List<OperatorTicket>();
        while (await reader.ReadAsync(ct))
        {
            var userId = reader.GetGuid(1);
            tickets.Add(new(
                reader.GetGuid(0),
                userId.ToString(),
                userId.ToString(),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        }

        return tickets;
    }

    public async Task<OperatorTicket> CreateTicketAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        string subject,
        string message,
        Guid? bookingId,
        CancellationToken ct)
    {
        var ticketId = Guid.NewGuid();
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO support_tickets
                (id, tenant_id, site_id, user_id, subject, message, booking_id)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            RETURNING created_at
            """, db);
        command.Parameters.AddWithValue(ticketId);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(actorId);
        command.Parameters.AddWithValue(subject.Trim());
        command.Parameters.AddWithValue(message.Trim());
        command.Parameters.AddWithValue((object?)bookingId ?? DBNull.Value);
        var createdAt = (DateTimeOffset)(await command.ExecuteScalarAsync(ct)
            ?? throw new InvalidOperationException("Ticket creation did not return a timestamp."));
        return new(ticketId, actorId.ToString(), actorId.ToString(), subject.Trim(), message.Trim(), bookingId, "open", createdAt);
    }

    public async Task<OperatorTicket?> UpdateTicketStatusAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        Guid ticketId,
        string status,
        string? resolutionNote,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE support_tickets
            SET status = $3,
                message = CASE
                    WHEN NULLIF(BTRIM($4), '') IS NULL THEN message
                    ELSE message || E'\n\n[Operator resolution note]: ' || BTRIM($4)
                END,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = $1 AND site_id = $2
              AND status <> 'resolved'
            RETURNING id, user_id, subject, message, booking_id, status, created_at
            """, db, transaction);
        command.Parameters.AddWithValue(ticketId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(resolutionNote ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        var userId = reader.GetGuid(1);
        var ticket = new OperatorTicket(
            reader.GetGuid(0),
            userId.ToString(),
            userId.ToString(),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6));
        await reader.CloseAsync();
        await InsertAuditAsync(db, transaction, tenantId, siteId, actorId, "SUPPORT_TICKET_UPDATED", ticketId, $"Ticket status changed to {status}.", ct);
        await transaction.CommitAsync(ct);
        return ticket;
    }

    public async Task<OperatorIncident> CreateIncidentAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        string type,
        string details,
        CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        await using var db = await source.OpenConnectionAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await using (var command = new NpgsqlCommand("""
            INSERT INTO operator_incident_events
                (id, tenant_id, site_id, actor_id, incident_type, details)
            VALUES ($1, $2, $3, $4, $5, $6)
            """, db, transaction))
        {
            command.Parameters.AddWithValue(id);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(siteId);
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(type.Trim());
            command.Parameters.AddWithValue(details.Trim());
            await command.ExecuteNonQueryAsync(ct);
        }

        await InsertAuditAsync(db, transaction, tenantId, siteId, actorId, "EMERGENCY_ACTION", id, details, ct);
        await transaction.CommitAsync(ct);
        return new(id, siteId, type.Trim(), details.Trim(), "reported", actorId, timestamp);
    }

    public async Task<OperatorIncident?> ResolveIncidentAsync(
        Guid tenantId,
        Guid siteId,
        Guid incidentId,
        Guid actorId,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE operator_incident_events
            SET status = 'resolved'
            WHERE id = $1 AND site_id = $2 AND status = 'reported'
            RETURNING id, site_id, incident_type, details, status, actor_id, created_at
            """, db, transaction);
        command.Parameters.AddWithValue(incidentId);
        command.Parameters.AddWithValue(siteId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        var incident = new OperatorIncident(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetGuid(5),
            reader.GetFieldValue<DateTimeOffset>(6));
        await reader.CloseAsync();
        await InsertAuditAsync(db, transaction, tenantId, siteId, actorId, "INCIDENT_RESOLVED", incidentId, incident.Details, ct);
        await transaction.CommitAsync(ct);
        return incident;
    }

    public async Task<IReadOnlyList<OperatorIncident>> ListIncidentsAsync(Guid siteId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT id, site_id, incident_type, details, status, actor_id, created_at
            FROM operator_incident_events
            WHERE site_id = $1
            ORDER BY created_at DESC
            LIMIT 200
            """, db);
        command.Parameters.AddWithValue(siteId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var incidents = new List<OperatorIncident>();
        while (await reader.ReadAsync(ct))
            incidents.Add(new(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetGuid(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        return incidents;
    }

    public async Task<IReadOnlyList<OperatorAuditLog>> ListAuditLogsAsync(Guid siteId, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT id, actor_id, actor_role, action, COALESCE(reason, ''), created_at
            FROM operator_audit_logs
            WHERE site_id = $1
            ORDER BY created_at DESC
            LIMIT 200
            """, db);
        command.Parameters.AddWithValue(siteId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var entries = new List<OperatorAuditLog>();
        while (await reader.ReadAsync(ct))
            entries.Add(new(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        return entries;
    }

    public async Task RecordAuditAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        string action,
        string details,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await InsertAuditAsync(db, null, tenantId, siteId, actorId, action.Trim(), null, details.Trim(), ct);
    }

    public async Task<IReadOnlyList<OperatorNotification>> ListNotificationsAsync(
        Guid siteId,
        Guid recipientId,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT id, recipient_id, notification_type, title, message, created_at, is_read, related_entity_id
            FROM operator_notifications
            WHERE site_id = $1 AND recipient_id = $2
            ORDER BY created_at DESC
            LIMIT 100
            """, db);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(recipientId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var notifications = new List<OperatorNotification>();
        while (await reader.ReadAsync(ct))
            notifications.Add(new(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetGuid(7)));
        return notifications;
    }

    public async Task<bool> MarkNotificationReadAsync(
        Guid siteId,
        Guid recipientId,
        Guid notificationId,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE operator_notifications
            SET is_read = TRUE
            WHERE id = $1 AND site_id = $2 AND recipient_id = $3
            """, db);
        command.Parameters.AddWithValue(notificationId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(recipientId);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection db,
        NpgsqlTransaction? transaction,
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        string action,
        Guid? targetId,
        string details,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO operator_audit_logs
                (id, tenant_id, site_id, actor_id, actor_role, action, target_entity, target_id, reason)
            VALUES ($1, $2, $3, $4, 'operator', $5, 'OPERATOR_OPERATION', $6, $7)
            """, db, transaction);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(actorId);
        command.Parameters.AddWithValue(action);
        command.Parameters.AddWithValue((object?)targetId ?? DBNull.Value);
        command.Parameters.AddWithValue(details);
        await command.ExecuteNonQueryAsync(ct);
    }
}
