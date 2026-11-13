using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Chat;
using SmartParking.IoTService.Application.Assistant.Guest;
namespace SmartParking.IoTService.Tests;

// Synthetic port fixture only. This does NOT prove PostgreSQL locking, constraints or restart durability.
internal sealed class ChatMemoryStore(TimeProvider clock) : IChatStore
{
    internal sealed class Entry(ChatScope owner, Guid key, Conversation conversation)
    {
        public ChatScope Owner = owner; public Guid Key = key; public Conversation Conversation = conversation;
        public bool Deleted;
        public List<ChatMessage> Messages = [];
        public Dictionary<Guid, (string Fingerprint, RequestResult Result, DateTimeOffset Deadline)> Requests = [];
    }
    private readonly object gate = new();
    internal Dictionary<Guid, Entry> Entries = [];
    public bool Available = true;
    public IReadOnlyList<ContextTurn>? ContextOverride;
    private Entry Own(ChatScope scope, Guid id)
    {
        if (!Available) throw new ChatFailure(503, "STORAGE_UNAVAILABLE");
        if (!Entries.TryGetValue(id, out var e) || e.Owner != scope || e.Deleted || e.Conversation.ExpiresAt <= clock.GetUtcNow()) throw new ChatFailure(404, "RESOURCE_NOT_FOUND");
        return e;
    }
    public Task<bool> Ready(CancellationToken ct) => Task.FromResult(Available);
    public Task<CreatedConversation?> FindCreation(ChatScope scope, Guid key, string locale, CancellationToken ct)
    {
        lock (gate)
        {
            var e = Entries.Values.SingleOrDefault(e => e.Owner == scope && e.Key == key);
            if (e is null) return Task.FromResult<CreatedConversation?>(null);
            Own(scope, e.Conversation.Id);
            if (e.Conversation.Locale != locale) throw new ChatFailure(409, "IDEMPOTENCY_CONFLICT");
            return Task.FromResult<CreatedConversation?>(new(e.Conversation, false));
        }
    }
    public Task<CreatedConversation> Create(CurrentActor actor, Guid key, string locale, CancellationToken ct)
    {
        lock (gate)
        {
            var old = FindCreation(ChatScope.From(actor), key, locale, ct).Result; if (old is not null) return Task.FromResult(old);
            if (Entries.Values.Any(e => e.Owner == ChatScope.From(actor) && !e.Deleted)) throw new ChatFailure(409, "CONVERSATION_EXISTS");
            var c = new Conversation(Guid.NewGuid(), "New conversation", locale, 0, clock.GetUtcNow(), null, null);
            Entries[c.Id] = new(ChatScope.From(actor), key, c); return Task.FromResult(new CreatedConversation(c, true));
        }
    }
    public Task<Conversation> Get(ChatScope scope, Guid id, CancellationToken ct) { lock(gate) return Task.FromResult(Own(scope,id).Conversation); }
    public Task<IReadOnlyList<Conversation>> List(ChatScope scope, ChatCursor cursor, CancellationToken ct)
    {
        lock(gate) return Task.FromResult<IReadOnlyList<Conversation>>(Entries.Values.Where(e => e.Owner == scope && !e.Deleted && (e.Conversation.ExpiresAt is null || e.Conversation.ExpiresAt > clock.GetUtcNow())).Select(e => e.Conversation)
            .Where(c => (c.LastMessageAt ?? c.CreatedAt) <= cursor.Snapshot && (cursor.Activity is null || (c.LastMessageAt ?? c.CreatedAt) < cursor.Activity || (c.LastMessageAt ?? c.CreatedAt) == cursor.Activity && c.Id.CompareTo(cursor.Id!.Value) < 0))
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt).ThenByDescending(c => c.Id).Take(cursor.Limit+1).ToArray());
    }
    public Task<(Conversation Conversation, IReadOnlyList<ChatMessage> Messages, Guid? Active)> Messages(ChatScope scope, Guid id, ChatCursor cursor, CancellationToken ct)
    {
        lock(gate)
        {
            var e = Own(scope,id);
            var messages = e.Messages.Where(m => m.Sequence <= cursor.UpperSequence && m.Sequence < cursor.BeforeSequence)
                .OrderByDescending(m => m.Sequence).Take(cursor.Limit + 1).ToArray();
            var active = e.Requests.Values.Select(r => r.Result).FirstOrDefault(r => r.State == "running")?.ClientMessageId;
            return Task.FromResult<(Conversation, IReadOnlyList<ChatMessage>, Guid?)>((e.Conversation, messages, active));
        }
    }
    public Task<RequestResult?> Lookup(ChatScope scope, Guid id, Guid key, string? fingerprint, CancellationToken ct)
    {
        lock(gate)
        {
            var e=Own(scope,id); Recover(default);
            if(!e.Requests.TryGetValue(key,out var request)) return Task.FromResult<RequestResult?>(null);
            if(fingerprint is not null && fingerprint!=request.Fingerprint) throw new ChatFailure(409,"IDEMPOTENCY_CONFLICT");
            return Task.FromResult<RequestResult?>(request.Result with {Revision=e.Conversation.Revision});
        }
    }
    public Task<Admission> Admit(ChatScope scope, SendChat input, string fingerprint, ChatSnapshot snapshot, int deadlineMs, CancellationToken ct)
    {
        lock(gate)
        {
            var e=Own(scope,input.ConversationId); var old=Lookup(scope,input.ConversationId,input.ClientMessageId,fingerprint,ct).Result;
            if(old is not null) return Task.FromResult(new Admission(old,false));
            ChatLifecycle.Admission(e.Conversation,e.Requests.Values.Any(r=>r.Result.State=="running"),input.ExpectedRevision!.Value);
            var now=clock.GetUtcNow(); var msg=new ChatMessage(Guid.NewGuid(),e.Conversation.Revision+1,"user",input.Message!,now,input.ClientMessageId,null,[],[],"system",[]);
            e.Messages.Add(msg); e.Conversation=e.Conversation with {Revision=e.Conversation.Revision+1,LastMessageAt=now};
            var result=new RequestResult(Guid.NewGuid(),input.ClientMessageId,input.ConversationId,"running",e.Conversation.Revision,msg.Id,null,null,now,null);
            e.Requests[input.ClientMessageId]=(fingerprint,result,now.AddMilliseconds(deadlineMs)); return Task.FromResult(new Admission(result,true));
        }
    }
    public Task<IReadOnlyList<ContextTurn>> Context(ChatScope scope,Guid id,CancellationToken ct)
    {
        if(ContextOverride is not null)return Task.FromResult(ContextOverride);
        lock(gate)
        {
            var e=Own(scope,id);var left=12000;var turns=new List<ContextTurn>();
            foreach(var r in e.Requests.Values.Select(r=>r.Result).Where(r=>r.State=="completed").OrderByDescending(r=>r.Answer!.Sequence).Take(6))
            {var q=e.Messages.Single(m=>m.Id==r.UserMessageId).Content;left-=q.Length+r.Answer!.Content.Length;if(left<0)break;turns.Add(new(q,r.Answer));}
            return Task.FromResult<IReadOnlyList<ContextTurn>>(turns);
        }
    }
    public Task<bool> DispatchAllowed(ChatScope scope,Guid id,Guid key,CancellationToken ct)
    {lock(gate) { var e=Own(scope,id); return Task.FromResult(e.Requests.TryGetValue(key,out var r)&&r.Result.State=="running"&&r.Deadline>clock.GetUtcNow()); }}
    public Task<RequestResult> Finish(ChatScope scope,Guid id,Guid key,GuestReply? answer,string? error,bool cancel,CancellationToken ct)
    {
        lock(gate)
        {
            var e=Own(scope,id);var old=Lookup(scope,id,key,null,ct).Result??throw new ChatFailure(404,"RESOURCE_NOT_FOUND");
            if(old.State!="running")return Task.FromResult(old);
            var now=clock.GetUtcNow();var state=cancel?"cancelled":error is not null||answer is null?"failed":"completed";
            ChatMessage? msg=state=="completed"?new(Guid.NewGuid(),e.Conversation.Revision+1,"assistant",answer!.Content,now,key,answer.Outcome,answer.Sources,answer.Actions.Cast<object>().ToArray(),answer.Mode,answer.Cards):null;
            if(msg is not null)e.Messages.Add(msg);
            e.Conversation=e.Conversation with{Revision=e.Conversation.Revision+(cancel?0:1),LastMessageAt=msg is null?e.Conversation.LastMessageAt:now};
            var result=old with{State=state,Revision=e.Conversation.Revision,Answer=msg,RequestError=state=="failed"?ChatLifecycle.Error(error??"INTERNAL_ERROR"):null,FinishedAt=now};
            var r=e.Requests[key]; e.Requests[key]=(r.Fingerprint,result,r.Deadline);return Task.FromResult(result);
        }
    }
    public Task Reset(ChatScope scope, Guid id, CancellationToken ct)
    {
        lock (gate)
        {
            if (!Entries.TryGetValue(id, out var entry) || entry.Owner != scope)
                throw new ChatFailure(404, "RESOURCE_NOT_FOUND");
            if (!entry.Deleted)
            {
                entry.Deleted = true;
                entry.Conversation = entry.Conversation with { ExpiresAt = clock.GetUtcNow().AddDays(7) };
            }
        }
        return Task.CompletedTask;
    }
    public Task Recover(CancellationToken ct)
    {
        lock(gate)foreach(var e in Entries.Values)foreach(var key in e.Requests.Keys.ToArray())
        {
            var r=e.Requests[key];if(r.Result.State!="running"||r.Deadline>clock.GetUtcNow())continue;
            e.Conversation=e.Conversation with{Revision=e.Conversation.Revision+1};
            e.Requests[key]=(r.Fingerprint,r.Result with{State="failed",RequestError=ChatLifecycle.Error("REQUEST_INTERRUPTED"),FinishedAt=clock.GetUtcNow(),Revision=e.Conversation.Revision},r.Deadline);
        }
        return Task.CompletedTask;
    }
    public Task Purge(CancellationToken ct) => Task.CompletedTask;
}
