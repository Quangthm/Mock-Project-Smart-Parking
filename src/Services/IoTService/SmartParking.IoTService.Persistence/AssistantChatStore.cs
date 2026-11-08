using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Chat;
using SmartParking.IoTService.Application.Assistant.Guest;

namespace SmartParking.IoTService.Persistence;

// Every request writer locks the owning conversation first. No external I/O runs under these locks.
public sealed class AssistantChatStore(NpgsqlDataSource source) : IChatStore
{
    private const string Columns = "id,title,locale,revision,created_at,last_message_at,expires_at";
    private static Conversation Row(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt64(3), r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.GetFieldValue<DateTimeOffset>(6));
    private static T Json<T>(string value) => JsonSerializer.Deserialize<T>(value) ?? throw new ChatFailure(503, "STORAGE_UNAVAILABLE");
    private static NpgsqlCommand Command(NpgsqlConnection db, NpgsqlTransaction? tx, string sql, params object?[] args)
    {
        var cmd = new NpgsqlCommand(sql, db, tx) { CommandTimeout = 5 };
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg ?? DBNull.Value);
        return cmd;
    }
    private async Task<T> Db<T>(Func<NpgsqlConnection, Task<T>> work, CancellationToken ct)
    {
        try { await using var db = await source.OpenConnectionAsync(ct); return await work(db); }
        catch (NpgsqlException) { throw new ChatFailure(503, "STORAGE_UNAVAILABLE"); }
    }
    private static async Task<DateTimeOffset> Now(NpgsqlConnection db, NpgsqlTransaction? tx, CancellationToken ct)
    { await using var cmd = Command(db, tx, "SELECT clock_timestamp()"); return (DateTimeOffset)(DateTime) (await cmd.ExecuteScalarAsync(ct))!; }
    private static async Task<Conversation> Owned(NpgsqlConnection db, NpgsqlTransaction? tx, Guid account, Guid id, bool locked, CancellationToken ct)
    {
        await using var cmd = Command(db, tx, $"SELECT {Columns} FROM assistant_conversations WHERE id=$1 AND owner_account_id=$2 AND deleted_at IS NULL" + (locked ? " FOR UPDATE" : ""), id, account);
        Conversation row;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        { if (!await r.ReadAsync(ct)) throw new ChatFailure(404, "RESOURCE_NOT_FOUND"); row = Row(r); }
        if (row.ExpiresAt <= await Now(db, tx, ct)) throw new ChatFailure(404, "RESOURCE_NOT_FOUND");
        return row;
    }
    private static async Task<RequestResult?> Request(NpgsqlConnection db, NpgsqlTransaction? tx, Conversation conversation, Guid key, string? fingerprint, CancellationToken ct)
    {
        await using var cmd = Command(db, tx, "SELECT id,client_message_id,state,user_message_id,answer::text,error_code,created_at,finished_at,fingerprint FROM assistant_requests WHERE conversation_id=$1 AND client_message_id=$2", conversation.Id, key);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        if (fingerprint is not null && fingerprint != r.GetString(8)) throw new ChatFailure(409, "IDEMPOTENCY_CONFLICT");
        return new(r.GetGuid(0), r.GetGuid(1), conversation.Id, r.GetString(2), conversation.Revision, r.GetGuid(3), r.IsDBNull(4) ? null : Json<ChatMessage>(r.GetString(4)), r.IsDBNull(5) ? null : ChatLifecycle.Error(r.GetString(5)), r.GetFieldValue<DateTimeOffset>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7));
    }
    public Task<bool> Ready(CancellationToken ct) => Db(async db =>
    { await using var cmd = Command(db, null, "SELECT id FROM assistant_requests LIMIT 0"); await cmd.ExecuteNonQueryAsync(ct); return true; }, ct);
    public Task<CreatedConversation?> FindCreation(Guid account, Guid key, string locale, CancellationToken ct) => Db(async db =>
    {
        await using var cmd = Command(db, null, $"SELECT {Columns} FROM assistant_conversations WHERE owner_account_id=$1 AND client_conversation_id=$2 AND deleted_at IS NULL", account, key);
        Conversation row;
        await using (var r = await cmd.ExecuteReaderAsync(ct)) { if (!await r.ReadAsync(ct)) return null; row = Row(r); }
        if (row.ExpiresAt <= await Now(db, null, ct)) throw new ChatFailure(404, "RESOURCE_NOT_FOUND");
        if (row.Locale != locale) throw new ChatFailure(409, "IDEMPOTENCY_CONFLICT");
        return new CreatedConversation(row, false);
    }, ct);
    public Task<CreatedConversation> Create(CurrentActor actor, Guid key, string locale, CancellationToken ct) => Db<CreatedConversation>(async db =>
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        var id = Guid.NewGuid();
        await using var insert = Command(db, tx, "INSERT INTO assistant_conversations(id,user_id,owner_account_id,client_conversation_id,title,locale,revision,created_at,updated_at,expires_at) VALUES($1,$2,$3,$4,$5,$6,0,clock_timestamp(),clock_timestamp(),clock_timestamp()+interval '30 days') ON CONFLICT(owner_account_id,client_conversation_id) DO NOTHING", id, actor.UserId, actor.AccountId, key, locale == "en" ? "New conversation" : "Hội thoại mới", locale);
        var created = await insert.ExecuteNonQueryAsync(ct) == 1;
        await using var select = Command(db, tx, $"SELECT {Columns} FROM assistant_conversations WHERE owner_account_id=$1 AND client_conversation_id=$2 FOR UPDATE", actor.AccountId, key);
        Conversation row;
        await using (var r = await select.ExecuteReaderAsync(ct)) { await r.ReadAsync(ct); row = Row(r); }
        if (row.ExpiresAt <= await Now(db, tx, ct)) throw new ChatFailure(404, "RESOURCE_NOT_FOUND");
        row = await Owned(db, tx, actor.AccountId, row.Id, true, ct);
        if (row.Locale != locale) throw new ChatFailure(409, "IDEMPOTENCY_CONFLICT");
        await tx.CommitAsync(ct); return new(row, created);
    }, ct);
    public Task<Conversation> Get(Guid account, Guid conversation, CancellationToken ct) => Db(db => Owned(db, null, account, conversation, false, ct), ct);
    public Task<IReadOnlyList<Conversation>> List(Guid account, ChatCursor cursor, CancellationToken ct) => Db<IReadOnlyList<Conversation>>(async db =>
    {
        var sql = $"SELECT {Columns} FROM assistant_conversations WHERE owner_account_id=$1 AND deleted_at IS NULL AND expires_at>clock_timestamp() AND COALESCE(last_message_at,created_at)<=$2";
        var args = new List<object?> { account, cursor.Snapshot };
        if (cursor.Activity is not null) { sql += " AND (COALESCE(last_message_at,created_at),id)<($3,$4)"; args.Add(cursor.Activity); args.Add(cursor.Id); }
        sql += $" ORDER BY COALESCE(last_message_at,created_at) DESC,id DESC LIMIT {cursor.Limit + 1}";
        await using var cmd = Command(db, null, sql, args.ToArray()); await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Conversation>(); while (await r.ReadAsync(ct)) rows.Add(Row(r)); return rows;
    }, ct);
    public Task<(IReadOnlyList<ChatMessage> Messages, Guid? Active)> Messages(Guid account, Guid conversation, ChatCursor cursor, CancellationToken ct) => Db<(IReadOnlyList<ChatMessage>, Guid?)>(async db =>
    {
        await Owned(db, null, account, conversation, false, ct);
        await using var cmd = Command(db, null, "SELECT response::text FROM assistant_messages WHERE conversation_id=$1 AND sequence<=$2 AND sequence<$3 AND created_at<=$4 ORDER BY sequence DESC LIMIT $5", conversation, cursor.UpperSequence, cursor.BeforeSequence, cursor.Snapshot, cursor.Limit + 1);
        var rows = new List<ChatMessage>();
        await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) rows.Add(Json<ChatMessage>(r.GetString(0)));
        await using var active = Command(db, null, "SELECT client_message_id FROM assistant_requests WHERE conversation_id=$1 AND state='running'", conversation);
        var key = await active.ExecuteScalarAsync(ct);
        return (rows, key is Guid id ? id : null);
    }, ct);
    private static async Task<Conversation> RecoverLocked(NpgsqlConnection db, NpgsqlTransaction tx, Conversation conversation, CancellationToken ct)
    {
        await using var cmd = Command(db, tx, "UPDATE assistant_requests SET state='failed',error_code='REQUEST_INTERRUPTED',finished_at=clock_timestamp() WHERE conversation_id=$1 AND state='running' AND deadline<=clock_timestamp()", conversation.Id);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) return conversation;
        await using var update = Command(db, tx, "UPDATE assistant_conversations SET revision=revision+1 WHERE id=$1", conversation.Id); await update.ExecuteNonQueryAsync(ct);
        return conversation with { Revision = conversation.Revision + 1 };
    }
    public Task<RequestResult?> Lookup(Guid account, Guid conversation, Guid key, string? fingerprint, CancellationToken ct) => Db(async db =>
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        var row = await Owned(db, tx, account, conversation, true, ct);
        row = await RecoverLocked(db, tx, row, ct);
        var result = await Request(db, tx, row, key, fingerprint, ct);
        await tx.CommitAsync(ct); return result;
    }, ct);
    private static async Task InsertMessage(NpgsqlConnection db, NpgsqlTransaction tx, Guid conversation, ChatMessage message, CancellationToken ct)
    {
        await using var cmd = Command(db, tx, "INSERT INTO assistant_messages(id,conversation_id,sender_role,content,created_at,sequence,client_message_id,response) VALUES($1,$2,$3,$4,$5,$6,$7,$8::jsonb)", message.Id, conversation, message.Role.ToUpperInvariant(), message.Content, message.CreatedAt, message.Sequence, message.ClientMessageId, JsonSerializer.Serialize(message));
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public Task<Admission> Admit(Guid account, SendChat input, string fingerprint, ChatSnapshot snapshot, int deadlineMs, CancellationToken ct)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        return Db(async db =>
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        var row = await Owned(db, tx, account, input.ConversationId, true, ct);
        row = await RecoverLocked(db, tx, row, ct);
        var old = await Request(db, tx, row, input.ClientMessageId, fingerprint, ct);
        if (old is not null) { await tx.CommitAsync(ct); return new Admission(old, false); }
        await using var busy = Command(db, tx, "SELECT EXISTS(SELECT 1 FROM assistant_requests WHERE conversation_id=$1 AND state='running')", row.Id);
        ChatLifecycle.Admission(row, (bool)(await busy.ExecuteScalarAsync(ct))!, input.ExpectedRevision!.Value);
        var now = await Now(db, tx, ct);
        var remaining = deadlineMs - elapsed.ElapsedMilliseconds;
        if (remaining <= 0) throw new ChatFailure(504, "REQUEST_TIMEOUT");
        var message = new ChatMessage(Guid.NewGuid(), row.Revision + 1, "user", input.Message!, now, input.ClientMessageId, null, [], [], "system", []);
        await InsertMessage(db, tx, row.Id, message, ct);
        var requestId = Guid.NewGuid();
        await using var insert = Command(db, tx, "INSERT INTO assistant_requests(id,conversation_id,owner_account_id,client_message_id,fingerprint,state,deadline,created_at,user_message_id,snapshot) VALUES($1,$2,$3,$4,$5,'running',$6,$7,$8,$9::jsonb)", requestId, row.Id, account, input.ClientMessageId, fingerprint, now.AddMilliseconds(remaining), now, message.Id, JsonSerializer.Serialize(snapshot));
        await insert.ExecuteNonQueryAsync(ct);
        await using var update = Command(db, tx, "UPDATE assistant_conversations SET revision=revision+1,last_message_at=$2,updated_at=$2 WHERE id=$1", row.Id, now); await update.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return new Admission(new(requestId, input.ClientMessageId, row.Id, "running", row.Revision + 1, message.Id, null, null, now, null), true);
    }, ct);
    }
    public Task<IReadOnlyList<ContextTurn>> Context(Guid account, Guid conversation, CancellationToken ct) => Db<IReadOnlyList<ContextTurn>>(async db =>
    {
        await Owned(db, null, account, conversation, false, ct);
        await using var cmd = Command(db, null, "SELECT m.content,r.answer::text FROM assistant_requests r JOIN assistant_messages m ON m.id=r.user_message_id WHERE r.conversation_id=$1 AND r.state='completed' ORDER BY m.sequence DESC LIMIT 6", conversation);
        await using var r = await cmd.ExecuteReaderAsync(ct); var rows = new List<ContextTurn>(); var remaining = 12000;
        while (await r.ReadAsync(ct)) { var question = r.GetString(0); var answer = Json<ChatMessage>(r.GetString(1)); remaining -= question.Length + answer.Content.Length; if (remaining < 0) break; rows.Add(new(question, answer)); }
        return rows;
    }, ct);
    public Task<bool> DispatchAllowed(Guid account, Guid conversation, Guid key, CancellationToken ct) => Db(async db =>
    {
        await using var cmd = Command(db, null, "SELECT EXISTS(SELECT 1 FROM assistant_requests r JOIN assistant_conversations c ON c.id=r.conversation_id WHERE c.owner_account_id=$1 AND c.id=$2 AND c.deleted_at IS NULL AND c.expires_at>clock_timestamp() AND r.client_message_id=$3 AND r.state='running' AND r.deadline>clock_timestamp())", account, conversation, key);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }, ct);
    public Task<RequestResult> Finish(Guid account, Guid conversation, Guid key, GuestReply? answer, string? error, bool cancel, CancellationToken ct) => Db(async db =>
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        var row = await Owned(db, tx, account, conversation, true, ct);
        row = await RecoverLocked(db, tx, row, ct);
        var old = await Request(db, tx, row, key, null, ct) ?? throw new ChatFailure(404, "RESOURCE_NOT_FOUND");
        if (!ChatLifecycle.CanFinish(old.State)) { await tx.CommitAsync(ct); return old; }
        var now = await Now(db, tx, ct);
        var state = cancel ? "cancelled" : error is not null || answer is null ? "failed" : "completed";
        ChatMessage? message = state == "completed" ? new(Guid.NewGuid(), row.Revision + 1, "assistant", answer!.Content, now, key, answer.Outcome, answer.Sources, answer.Actions.Cast<object>().ToArray(), answer.Mode, answer.Cards) : null;
        // S01 cancel does not change transcript/revision. Failed and completed retain P0 revision semantics.
        var revision = row.Revision + (cancel ? 0 : 1);
        await using var update = Command(db, tx, "UPDATE assistant_requests SET state=$3,finished_at=$4,answer=$5::jsonb,error_code=$6 WHERE conversation_id=$1 AND client_message_id=$2 AND state='running' AND deadline>clock_timestamp()", conversation, key, state, now, message is null ? null : JsonSerializer.Serialize(message), state == "failed" ? error ?? "INTERNAL_ERROR" : null);
        // Explicit null types keep Npgsql independent of inference for terminal null fields.
        update.Parameters[4].NpgsqlDbType = NpgsqlDbType.Text; update.Parameters[5].NpgsqlDbType = NpgsqlDbType.Text;
        if (await update.ExecuteNonQueryAsync(ct) == 0)
        {
            row = await RecoverLocked(db, tx, row, ct);
            var expired = await Request(db, tx, row, key, null, ct) ?? throw new ChatFailure(404, "RESOURCE_NOT_FOUND");
            await tx.CommitAsync(ct); return expired;
        }
        if (message is not null) await InsertMessage(db, tx, conversation, message, ct);
        await using var conv = Command(db, tx, "UPDATE assistant_conversations SET revision=$2,last_message_at=CASE WHEN $3 THEN $4 ELSE last_message_at END,updated_at=$4 WHERE id=$1", conversation, revision, message is not null, now); await conv.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return old with { State = state, Revision = revision, Answer = message, RequestError = state == "failed" ? ChatLifecycle.Error(error ?? "INTERNAL_ERROR") : null, FinishedAt = now };
    }, ct);
    public Task Recover(CancellationToken ct) => Db(async db =>
    {
        await using var tx = await db.BeginTransactionAsync(ct);
        await using var recover = Command(db, tx, """
            WITH locked AS MATERIALIZED (
                SELECT c.id FROM assistant_conversations c
                WHERE EXISTS (SELECT 1 FROM assistant_requests r WHERE r.conversation_id=c.id AND r.state='running' AND r.deadline<=clock_timestamp())
                ORDER BY c.id FOR UPDATE OF c SKIP LOCKED LIMIT 1000
            ), terminal AS (
                UPDATE assistant_requests r SET state='failed',error_code='REQUEST_INTERRUPTED',finished_at=clock_timestamp()
                FROM locked WHERE r.conversation_id=locked.id AND r.state='running' AND r.deadline<=clock_timestamp()
                RETURNING r.conversation_id
            ) UPDATE assistant_conversations c SET revision=c.revision+1 FROM terminal WHERE c.id=terminal.conversation_id
            """);
        await recover.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct); return true;
    }, ct);
    public Task Purge(CancellationToken ct) => Db(async db =>
    { await using var cmd = Command(db, null, "DELETE FROM assistant_conversations WHERE expires_at<=clock_timestamp() AND NOT retention_hold"); await cmd.ExecuteNonQueryAsync(ct); return true; }, ct);
}
