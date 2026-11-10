using MediatR;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Guest;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed record ChatOptions(bool ReleaseEnabled = false, int GenerationMilliseconds = 20000, int DeadlineMilliseconds = 25000);
public sealed class ChatFailure(int status, string code) : Exception
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed record Conversation(Guid Id, string Title, string Locale, long Revision, DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt, DateTimeOffset? ExpiresAt);
public sealed record ChatMessage(Guid Id, long Sequence, string Role, string Content, DateTimeOffset CreatedAt,
    Guid ClientMessageId, string? Outcome, GuestSource[] Sources, object[] Actions, string Mode, object[] Cards);
public sealed record RequestError(string Code, string Message, bool Retryable);
public sealed record RequestResult(Guid RequestId, Guid ClientMessageId, Guid ConversationId, string State,
    long Revision, Guid UserMessageId, ChatMessage? Answer, RequestError? RequestError,
    DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt);
public sealed record ChatPage<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record History(Conversation Conversation, IReadOnlyList<ChatMessage> Items, string? NextCursor, Guid? ActiveRequestClientMessageId);
public sealed record CreateConversation(Guid ClientConversationId, string? Locale) : IRequest<CreatedConversation>;
public readonly record struct ChatScope(Guid AccountId, string Role)
{
    public static bool Supports(string role) => role is "driver" or "admin" or "owner";
    public static ChatScope From(CurrentActor actor) => new(actor.AccountId, actor.Role);
}
public sealed record ResetConversation(Guid ConversationId) : IRequest<bool>;
public sealed record CreatedConversation(Conversation Conversation, bool Created);
public sealed record SendChat(Guid ConversationId, Guid ClientMessageId, long? ExpectedRevision, string? Message) : IRequest<RequestResult>
{
    public override string ToString() => "[CHAT MESSAGE REDACTED]";
}
public sealed record ReadChat(Guid ConversationId, Guid? ClientMessageId = null, bool Cancel = false) : IRequest<object>;
public sealed record ListChat(Guid? ConversationId, int Limit, string? Cursor) : IRequest<object>;
public sealed record ChatCursor(Guid AccountId, Guid? ConversationId, int Limit, DateTimeOffset Snapshot,
    DateTimeOffset? Activity, Guid? Id, long UpperSequence, long BeforeSequence, string Role = "driver");
public interface IChatCursors
{
    string Encode(ChatCursor cursor);
    ChatCursor Decode(string cursor);
}
public sealed record Admission(RequestResult Result, bool Created);
public sealed record ContextTurn(string Question, ChatMessage Answer);
public sealed record ChatSnapshot(long SettingsRevision, string CorpusFingerprint, string CorpusVersion);
public interface IChatStore
{
    Task<bool> Ready(CancellationToken ct);
    Task<CreatedConversation?> FindCreation(ChatScope scope, Guid key, string locale, CancellationToken ct);
    Task<CreatedConversation> Create(CurrentActor actor, Guid key, string locale, CancellationToken ct);
    Task<Conversation> Get(ChatScope scope, Guid conversation, CancellationToken ct);
    Task<IReadOnlyList<Conversation>> List(ChatScope scope, ChatCursor cursor, CancellationToken ct);
    Task<(Conversation Conversation, IReadOnlyList<ChatMessage> Messages, Guid? Active)> Messages(ChatScope scope, Guid conversation, ChatCursor cursor, CancellationToken ct);
    Task<RequestResult?> Lookup(ChatScope scope, Guid conversation, Guid key, string? fingerprint, CancellationToken ct);
    Task<Admission> Admit(ChatScope scope, SendChat input, string fingerprint, ChatSnapshot snapshot, int deadlineMs, CancellationToken ct);
    Task<IReadOnlyList<ContextTurn>> Context(ChatScope scope, Guid conversation, CancellationToken ct);
    Task<bool> DispatchAllowed(ChatScope scope, Guid conversation, Guid key, CancellationToken ct);
    Task<RequestResult> Finish(ChatScope scope, Guid conversation, Guid key, GuestReply? answer, string? error, bool cancel, CancellationToken ct);
    Task Reset(ChatScope scope, Guid conversation, CancellationToken ct);
    Task Recover(CancellationToken ct);
    Task Purge(CancellationToken ct);
}

// Transport lifecycle, not a business workflow or reservation state machine.
public static class ChatLifecycle
{
    public const long MaxRevision = 9007199254740991;
    public static void Admission(Conversation conversation, bool running, long expected)
    {
        if (running) throw new ChatFailure(409, "CONVERSATION_BUSY");
        if (conversation.Revision != expected || expected > MaxRevision - 2) throw new ChatFailure(409, "REVISION_CONFLICT");
    }
    public static bool CanFinish(string state) => state == "running";
    public static RequestError Error(string code) => new(code, "The answer could not be completed. Retrieve this request before trying a new message.", code != "INVALID_MODEL_OUTPUT");
}
