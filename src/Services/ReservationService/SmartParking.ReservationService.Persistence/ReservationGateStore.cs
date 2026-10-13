using System.Data;
using Npgsql;

namespace SmartParking.ReservationService.Persistence;

public sealed record ReservationGateEntry(
    Guid Id,
    Guid AccountId,
    Guid SiteId,
    string SiteName,
    Guid? SlotId,
    string SlotNumber,
    string LicensePlate,
    string Status,
    string? PaymentStatus,
    string? PaymentMethod,
    decimal? Amount,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset? EntryTime,
    DateTimeOffset? ExitTime,
    DateTimeOffset CreatedAt);

public sealed record WalkInParkingSession(
    Guid Id,
    Guid SiteId,
    Guid SlotId,
    string SlotNumber,
    string LicensePlate,
    string VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset? ExitTime);

public sealed class ReservationGateStore(NpgsqlDataSource source, IOperatorGateSlotClaims slotClaims)
{
    public async Task<IReadOnlyList<ReservationGateEntry>> ListAsync(
        Guid tenantId,
        Guid siteId,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = CreateEntryQuery(db, null);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var entries = new List<ReservationGateEntry>();
        while (await reader.ReadAsync(ct))
            entries.Add(ReadEntry(reader));
        return entries;
    }

    public async Task<ReservationGateEntry?> FindAsync(
        Guid tenantId,
        Guid siteId,
        string query,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = CreateEntryQuery(db, query);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(Guid.TryParse(query, out var id) ? id : Guid.Empty);
        command.Parameters.AddWithValue(NormalizePlate(query));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadEntry(reader) : null;
    }

    public async Task<WalkInParkingSession?> FindActiveWalkInAsync(
        Guid tenantId,
        Guid siteId,
        string plate,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT session.id, session.site_id, session.current_slot_id,
                   COALESCE(allocation.slot_id::text, session.current_slot_id::text),
                   COALESCE(session.original_plate, session.normalized_plate, ''),
                   session.vehicle_type, session.entry_time, session.exit_time
            FROM parking_sessions session
            LEFT JOIN LATERAL (
                SELECT slot_id
                FROM slot_allocations
                WHERE parking_session_id = session.id AND allocation_status = 'OCCUPIED'
                ORDER BY created_at DESC
                LIMIT 1
            ) allocation ON TRUE
            WHERE session.tenant_id = $1 AND session.site_id = $2
              AND session.reservation_id IS NULL AND session.session_status = 'ACTIVE'
              AND session.normalized_plate = $3
            ORDER BY session.entry_time DESC
            LIMIT 1
            """, db);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(NormalizePlate(plate));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct) || reader.IsDBNull(2))
            return null;
        return new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.IsDBNull(3) ? reader.GetGuid(2).ToString() : reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
    }

    public async Task<WalkInParkingSession> CreateWalkInAsync(
        Guid tenantId,
        Guid siteId,
        Guid sessionId,
        Guid slotId,
        string slotNumber,
        string vehicleType,
        string plate,
        CancellationToken ct)
    {
        var normalizedPlate = NormalizePlate(plate);
        if (normalizedPlate.Length is < 2 or > 50)
            throw new ReservationGateException(400, "INVALID_PLATE", "Enter a valid vehicle plate.");

        var now = DateTimeOffset.UtcNow;
        await using var db = await source.OpenConnectionAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended($1, 0))",
            db,
            transaction))
        {
            lockCommand.Parameters.AddWithValue($"{siteId:N}:{normalizedPlate}");
            await lockCommand.ExecuteNonQueryAsync(ct);
        }
        await using (var duplicateCommand = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM parking_sessions
                WHERE tenant_id = $1 AND site_id = $2 AND normalized_plate = $3
                  AND exit_time IS NULL
            )
            """, db, transaction))
        {
            duplicateCommand.Parameters.AddWithValue(tenantId);
            duplicateCommand.Parameters.AddWithValue(siteId);
            duplicateCommand.Parameters.AddWithValue(normalizedPlate);
            if ((bool)(await duplicateCommand.ExecuteScalarAsync(ct))!)
                throw new ReservationGateException(409, "VEHICLE_ALREADY_INSIDE", "An active parking session already exists for this plate.");
        }

        await using (var command = new NpgsqlCommand("""
            INSERT INTO parking_sessions
                (id, tenant_id, site_id, current_slot_id, original_plate, normalized_plate,
                 vehicle_type, ticket_reference, entry_time, session_status)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, 'ACTIVE')
            """, db, transaction))
        {
            command.Parameters.AddWithValue(sessionId);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(siteId);
            command.Parameters.AddWithValue(slotId);
            command.Parameters.AddWithValue(plate.Trim());
            command.Parameters.AddWithValue(normalizedPlate);
            command.Parameters.AddWithValue(vehicleType);
            command.Parameters.AddWithValue($"WALKIN-{sessionId:N}");
            command.Parameters.AddWithValue(now);
            await command.ExecuteNonQueryAsync(ct);
        }

        await using (var command = new NpgsqlCommand("""
            INSERT INTO slot_allocations
                (id, parking_session_id, tenant_id, site_id, slot_id,
                 allocated_start_time, allocated_end_time, allocation_status)
            VALUES ($1, $2, $3, $4, $5, $6, 'infinity', 'OCCUPIED')
            """, db, transaction))
        {
            command.Parameters.AddWithValue(Guid.NewGuid());
            command.Parameters.AddWithValue(sessionId);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(siteId);
            command.Parameters.AddWithValue(slotId);
            command.Parameters.AddWithValue(now);
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return new(sessionId, siteId, slotId, slotNumber, plate.Trim(), vehicleType, now, null);
    }

    public async Task<WalkInParkingSession> CheckOutWalkInAsync(
        Guid tenantId,
        Guid siteId,
        Guid sessionId,
        string plate,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        WalkInParkingSession session;
        await using (var command = new NpgsqlCommand("""
            SELECT id, site_id, current_slot_id, COALESCE(original_plate, normalized_plate, ''),
                   vehicle_type, entry_time, exit_time
            FROM parking_sessions
            WHERE id = $1 AND tenant_id = $2 AND site_id = $3
              AND reservation_id IS NULL
            FOR UPDATE
            """, db, transaction))
        {
            command.Parameters.AddWithValue(sessionId);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(siteId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.IsDBNull(2))
                throw new ReservationGateException(404, "WALK_IN_SESSION_NOT_FOUND", "Active walk-in session was not found.");
            var storedPlate = reader.GetString(3);
            if (!string.Equals(NormalizePlate(storedPlate), NormalizePlate(plate), StringComparison.Ordinal))
                throw new ReservationGateException(409, "PLATE_MISMATCH", "The scanned plate does not match this parking session.");
            if (reader.GetString(4) is not ("CAR" or "MOTORCYCLE"))
                throw new ReservationGateException(409, "INVALID_VEHICLE_TYPE", "This parking session has an unsupported vehicle type.");
            session = new(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(2).ToString(),
                storedPlate,
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
        }

        if (session.ExitTime is null)
        {
            var exitTime = DateTimeOffset.UtcNow;
            await using (var command = new NpgsqlCommand("""
                UPDATE parking_sessions
                SET exit_time = $1, session_status = 'COMPLETED'
                WHERE id = $2 AND session_status = 'ACTIVE'
                """, db, transaction))
            {
                command.Parameters.AddWithValue(exitTime);
                command.Parameters.AddWithValue(sessionId);
                await command.ExecuteNonQueryAsync(ct);
            }
            await using (var command = new NpgsqlCommand("""
                UPDATE slot_allocations
                SET allocation_status = 'RELEASED', allocated_end_time = $1
                WHERE parking_session_id = $2 AND allocation_status = 'OCCUPIED'
                """, db, transaction))
            {
                command.Parameters.AddWithValue(exitTime);
                command.Parameters.AddWithValue(sessionId);
                await command.ExecuteNonQueryAsync(ct);
            }
            session = session with { ExitTime = exitTime };
        }

        await transaction.CommitAsync(ct);
        return session;
    }

    public Task<ReservationGateEntry> CheckInAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        Guid reservationId,
        string? recognizedPlate,
        CancellationToken ct) =>
        TransitionAsync(
            tenantId,
            siteId,
            actorId,
            reservationId,
            recognizedPlate,
            checkIn: true,
            ct);

    public Task<ReservationGateEntry> CheckOutAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        Guid reservationId,
        string? recognizedPlate,
        CancellationToken ct) =>
        TransitionAsync(
            tenantId,
            siteId,
            actorId,
            reservationId,
            recognizedPlate,
            checkIn: false,
            ct);

    private async Task<ReservationGateEntry> TransitionAsync(
        Guid tenantId,
        Guid siteId,
        Guid actorId,
        Guid reservationId,
        string? recognizedPlate,
        bool checkIn,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        Guid accountId;
        Guid? targetSlotId;
        Guid? targetUnitId;
        string plate;
        string vehicleType;
        string status;
        DateTimeOffset startTime;
        DateTimeOffset endTime;

        await using (var command = new NpgsqlCommand("""
            SELECT account_id, target_slot_id, target_spatial_unit_id,
                   COALESCE(original_plate, normalized_plate, ''),
                   vehicle_type, status,
                   expected_start_time, expected_end_time
            FROM reservations
            WHERE id = $1 AND tenant_id = $2 AND site_id = $3
            FOR UPDATE
            """, db, transaction))
        {
            command.Parameters.AddWithValue(reservationId);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(siteId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found at this site.");

            accountId = reader.GetGuid(0);
            targetSlotId = reader.IsDBNull(1) ? null : reader.GetGuid(1);
            targetUnitId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
            plate = reader.GetString(3);
            vehicleType = reader.GetString(4);
            status = reader.GetString(5);
            startTime = reader.GetFieldValue<DateTimeOffset>(6);
            endTime = reader.GetFieldValue<DateTimeOffset>(7);
        }

        var expectedStatus = checkIn ? status is "CONFIRMED" or "ALLOCATED" : status == "PARKING";
        if (!expectedStatus)
            throw new ReservationGateException(
                409,
                "INVALID_RESERVATION_STATE",
                checkIn
                    ? "Only confirmed reservations can check in."
                    : "Only checked-in reservations can check out.");

        if (!string.IsNullOrWhiteSpace(recognizedPlate) &&
            !string.Equals(NormalizePlate(plate), NormalizePlate(recognizedPlate), StringComparison.Ordinal))
            throw new ReservationGateException(409, "PLATE_MISMATCH", "The scanned plate does not match this reservation.");

        Guid? slotId = null;
        Guid? sessionId = checkIn ? Guid.NewGuid() : null;
        DateTimeOffset? entryTime = null;
        DateTimeOffset? exitTime = null;
        var now = DateTimeOffset.UtcNow;

        var reservedSlotId = checkIn
            ? await FindReservedSlotAsync(db, transaction, tenantId, siteId, reservationId, ct)
            : null;
        await using var slotClaim = checkIn
            ? await slotClaims.ClaimAvailableAsync(
                tenantId,
                siteId,
                vehicleType,
                reservedSlotId ?? targetSlotId,
                targetUnitId,
                allowReservedSlot: true,
                ct)
            : null;

        if (checkIn)
        {
            var activeSessionId = sessionId ?? throw new InvalidOperationException("A check-in session ID is required.");
            var activeSlotId = slotClaim!.Slot.Id;
            slotId = activeSlotId;
            entryTime = now;

            await using (var command = new NpgsqlCommand("""
                INSERT INTO parking_sessions
                    (id, reservation_id, tenant_id, site_id, account_id, current_slot_id,
                     original_plate, normalized_plate, vehicle_type, ticket_reference,
                     entry_time, session_status)
                VALUES
                    ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, 'ACTIVE')
                """, db, transaction))
            {
                command.Parameters.AddWithValue(activeSessionId);
                command.Parameters.AddWithValue(reservationId);
                command.Parameters.AddWithValue(tenantId);
                command.Parameters.AddWithValue(siteId);
                command.Parameters.AddWithValue(accountId);
                command.Parameters.AddWithValue(activeSlotId);
                command.Parameters.AddWithValue(plate);
                command.Parameters.AddWithValue(NormalizePlate(plate));
                command.Parameters.AddWithValue(vehicleType);
                command.Parameters.AddWithValue($"GATE-{activeSessionId:N}");
                command.Parameters.AddWithValue(now);
                await command.ExecuteNonQueryAsync(ct);
            }

            await using (var command = new NpgsqlCommand("""
                UPDATE slot_allocations
                SET allocation_status = 'OCCUPIED', parking_session_id = $1
                WHERE reservation_id = $2 AND tenant_id = $3 AND site_id = $4
                  AND slot_id = $5 AND allocation_status = 'RESERVED'
                """, db, transaction))
            {
                command.Parameters.AddWithValue(activeSessionId);
                command.Parameters.AddWithValue(reservationId);
                command.Parameters.AddWithValue(tenantId);
                command.Parameters.AddWithValue(siteId);
                command.Parameters.AddWithValue(activeSlotId);
                if (await command.ExecuteNonQueryAsync(ct) == 0)
                {
                    await using var insert = new NpgsqlCommand("""
                        INSERT INTO slot_allocations
                            (id, reservation_id, parking_session_id, tenant_id, site_id, slot_id,
                             allocated_start_time, allocated_end_time, allocation_status)
                        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, 'OCCUPIED')
                        """, db, transaction);
                    insert.Parameters.AddWithValue(Guid.NewGuid());
                    insert.Parameters.AddWithValue(reservationId);
                    insert.Parameters.AddWithValue(activeSessionId);
                    insert.Parameters.AddWithValue(tenantId);
                    insert.Parameters.AddWithValue(siteId);
                    insert.Parameters.AddWithValue(activeSlotId);
                    insert.Parameters.AddWithValue(startTime);
                    insert.Parameters.AddWithValue(endTime);
                    await insert.ExecuteNonQueryAsync(ct);
                }
            }

            await using (var command = new NpgsqlCommand(
                "UPDATE reservations SET status = 'PARKING' WHERE id = $1 AND tenant_id = $2 AND site_id = $3",
                db,
                transaction))
            {
                command.Parameters.AddWithValue(reservationId);
                command.Parameters.AddWithValue(tenantId);
                command.Parameters.AddWithValue(siteId);
                await command.ExecuteNonQueryAsync(ct);
            }
        }
        else
        {
            await using (var command = new NpgsqlCommand("""
                SELECT id, current_slot_id, entry_time
                FROM parking_sessions
                WHERE reservation_id = $1 AND tenant_id = $2 AND site_id = $3 AND session_status = 'ACTIVE'
                FOR UPDATE
                """, db, transaction))
            {
                command.Parameters.AddWithValue(reservationId);
                command.Parameters.AddWithValue(tenantId);
                command.Parameters.AddWithValue(siteId);
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new ReservationGateException(409, "ACTIVE_SESSION_NOT_FOUND", "No active parking session exists for this reservation.");
                sessionId = reader.GetGuid(0);
                slotId = reader.IsDBNull(1) ? null : reader.GetGuid(1);
                entryTime = reader.GetFieldValue<DateTimeOffset>(2);
            }

            exitTime = now;
            await using (var command = new NpgsqlCommand("""
                UPDATE parking_sessions
                SET exit_time = $1, session_status = 'COMPLETED'
                WHERE id = $2 AND session_status = 'ACTIVE'
                """, db, transaction))
            {
                command.Parameters.AddWithValue(now);
                command.Parameters.AddWithValue(sessionId.Value);
                await command.ExecuteNonQueryAsync(ct);
            }

            await using (var command = new NpgsqlCommand(
                "UPDATE reservations SET status = 'COMPLETED' WHERE id = $1 AND tenant_id = $2 AND site_id = $3",
                db,
                transaction))
            {
                command.Parameters.AddWithValue(reservationId);
                command.Parameters.AddWithValue(tenantId);
                command.Parameters.AddWithValue(siteId);
                await command.ExecuteNonQueryAsync(ct);
            }

            await using (var command = new NpgsqlCommand("""
                UPDATE slot_allocations
                SET allocation_status = 'RELEASED', allocated_end_time = $2
                WHERE parking_session_id = $1 AND allocation_status = 'OCCUPIED'
                """, db, transaction))
            {
                command.Parameters.AddWithValue(sessionId.Value);
                command.Parameters.AddWithValue(now);
                await command.ExecuteNonQueryAsync(ct);
            }
        }

        await using (var command = new NpgsqlCommand("""
            INSERT INTO audit_logs
                (id, tenant_id, site_id, actor_id, actor_role, action, target_entity, target_id, reason)
            VALUES ($1, $2, $3, $4, 'operator', $5, 'RESERVATION', $6, $7)
            """, db, transaction))
        {
            command.Parameters.AddWithValue(Guid.NewGuid());
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue(siteId);
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(checkIn ? "VEHICLE_CHECK_IN" : "VEHICLE_CHECK_OUT");
            command.Parameters.AddWithValue(reservationId);
            command.Parameters.AddWithValue($"Gate action for plate {plate}.");
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        if (checkIn)
            slotClaim!.Commit();
        else if (slotId is { } releasedSlot)
            await slotClaims.ReleaseAsync(tenantId, siteId, releasedSlot, ct);
        return await FindByIdAsync(tenantId, siteId, reservationId, ct)
            ?? throw new InvalidOperationException("Reservation disappeared after a successful gate transition.");
    }

    private static async Task<Guid?> FindReservedSlotAsync(
        NpgsqlConnection db,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid siteId,
        Guid reservationId,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT slot_id FROM slot_allocations
            WHERE reservation_id = $1 AND tenant_id = $2 AND site_id = $3
              AND allocation_status = 'RESERVED'
            ORDER BY created_at DESC
            LIMIT 1 FOR UPDATE
            """, db, transaction);
        command.Parameters.AddWithValue(reservationId);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        var value = await command.ExecuteScalarAsync(ct);
        return value is Guid id ? id : null;
    }

    private async Task<ReservationGateEntry?> FindByIdAsync(
        Guid tenantId,
        Guid siteId,
        Guid reservationId,
        CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var command = CreateEntryQuery(db, null, "r.id = $3");
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(siteId);
        command.Parameters.AddWithValue(reservationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadEntry(reader) : null;
    }

    private static NpgsqlCommand CreateEntryQuery(
        NpgsqlConnection db,
        string? query,
        string? idPredicate = null)
    {
        var predicate = idPredicate ?? (query is null
            ? "TRUE"
            : "(r.id = $3 OR r.normalized_plate = $4)");
        return new NpgsqlCommand($"""
            SELECT
                r.id, r.account_id, r.site_id, r.site_id::text,
                COALESCE(session.current_slot_id, allocation.slot_id, r.target_slot_id),
                COALESCE(COALESCE(session.current_slot_id, allocation.slot_id, r.target_slot_id)::text, 'Unassigned'),
                COALESCE(r.original_plate, r.normalized_plate, ''),
                r.status,
                NULL::text,
                NULL::text,
                NULL::numeric,
                r.expected_start_time, r.expected_end_time,
                session.entry_time, session.exit_time, r.created_at
            FROM reservations r
            LEFT JOIN LATERAL (
                SELECT current_slot_id, entry_time, exit_time
                FROM parking_sessions
                WHERE reservation_id = r.id
                ORDER BY created_at DESC
                LIMIT 1
            ) session ON TRUE
            LEFT JOIN LATERAL (
                SELECT slot_id
                FROM slot_allocations
                WHERE reservation_id = r.id AND allocation_status IN ('RESERVED', 'OCCUPIED')
                ORDER BY created_at DESC
                LIMIT 1
            ) allocation ON TRUE
            WHERE r.tenant_id = $1 AND r.site_id = $2 AND {predicate}
            ORDER BY r.created_at DESC
            """, db);
    }

    private static ReservationGateEntry ReadEntry(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.GetString(5),
            reader.GetString(6),
            MapStatus(reader.GetString(7)),
            reader.IsDBNull(8) ? null : MapPaymentStatus(reader.GetString(8)),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetDecimal(10),
            reader.GetFieldValue<DateTimeOffset>(11),
            reader.GetFieldValue<DateTimeOffset>(12),
            reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
            reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
            reader.GetFieldValue<DateTimeOffset>(15));

    private static string MapStatus(string status) => status switch
    {
        "CONFIRMED" or "ALLOCATED" => "confirmed",
        "PARKING" => "checked-in",
        "COMPLETED" => "completed",
        "PENDING_PAYMENT" => "pending",
        "CANCELLED" or "EXPIRED" or "NO_SHOW" or "UNFULFILLABLE" => "cancelled",
        _ => status.ToLowerInvariant()
    };

    private static string MapPaymentStatus(string status) => status switch
    {
        "PAID" => "paid",
        "FAILED" => "failed",
        _ => "pending"
    };

    private static string NormalizePlate(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}

public sealed class ReservationGateException(int statusCode, string code, string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
