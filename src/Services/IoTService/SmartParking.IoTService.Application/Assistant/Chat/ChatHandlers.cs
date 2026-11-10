using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Application.Assistant.Knowledge;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public interface IChatExecution
{
    IDisposable Admit(Guid account);
    Task<RequestResult> Run(Func<CancellationToken, Task<RequestResult>> work);
}

public sealed class ChatReadiness(ChatOptions options, IChatStore store, IAssistantManagementStore settings,
    IPublicKnowledge knowledge, AssistantStageMetrics? metrics = null)
{
    public async Task<ChatSnapshot> Require(CancellationToken ct)
    {
        using var measurement = metrics?.Start("chat_readiness");
        if (!options.ReleaseEnabled) throw new ChatFailure(503, "ASSISTANT_DISABLED");
        try
        {
            var state = await settings.ReadAsync(ct).WaitAsync(ct);
            if (!state.Enabled) throw new ChatFailure(503, "ASSISTANT_DISABLED");
            if (!await store.Ready(ct)) throw new ChatFailure(503, "STORAGE_UNAVAILABLE");
            var corpus = await knowledge.ReadAsync(ct).WaitAsync(ct) ?? throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
            return new(state.Revision, corpus.Fingerprint, corpus.Version);
        }
        catch (ManagementUnavailableException) { throw new ChatFailure(503, "DEPENDENCY_UNAVAILABLE"); }
    }
}

public sealed class ChatHandlers(ICurrentActor current, IChatStore store, IChatCursors cursors,
    ChatReadiness readiness, IChatExecution execution, ChatOptions options, TimeProvider clock,
    IPublicKnowledge knowledge, IAssistantManagementStore settings, IGuestAnswerFactory answers, ManagedDemoGenerator generator,
    ChatBudget budget, ReadTools reads, AssistantStageMetrics? metrics = null)
    : IRequestHandler<CreateConversation, CreatedConversation>, IRequestHandler<SendChat, RequestResult>,
      IRequestHandler<ReadChat, object>, IRequestHandler<ListChat, object>, IRequestHandler<ResetConversation, bool>
{
    private CurrentActor Actor()
    {
        var actor = current.Actor ?? throw new ChatFailure(401, "INVALID_TOKEN");
        if (actor.ExpiresAt <= clock.GetUtcNow()) throw new ChatFailure(401, "INVALID_TOKEN");
        if (!ChatScope.Supports(actor.Role)) throw new ChatFailure(403, "ASSISTANT_FORBIDDEN");
        return actor;
    }
    private T Deliver<T>(T result) { Actor(); return result; }
    private async Task<RequestResult> DeliverSaved(ChatScope scope, RequestResult result, CancellationToken ct)
    {
        var annotated = await Annotate(result, ct);
        await store.Get(scope, result.ConversationId, ct);
        return Deliver(annotated);
    }
    public async Task<CreatedConversation> Handle(CreateConversation request, CancellationToken ct)
    {
        var actor = Actor();
        if (request.ClientConversationId == Guid.Empty || request.Locale is not ("vi" or "en")) throw new ChatFailure(400, "VALIDATION_FAILED");
        var replay = await store.FindCreation(ChatScope.From(actor), request.ClientConversationId, request.Locale, ct);
        if (replay is not null) return Deliver(replay);
        await readiness.Require(ct);
        return Deliver(await store.Create(actor, request.ClientConversationId, request.Locale, ct));
    }
    public async Task<RequestResult> Handle(SendChat request, CancellationToken ct)
    {
        var actor = Actor();
        var scope = ChatScope.From(actor);
        if (request.ConversationId == Guid.Empty || request.ClientMessageId == Guid.Empty || request.ExpectedRevision is null or < 0 or > ChatLifecycle.MaxRevision ||
            string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 4000) throw new ChatFailure(400, "VALIDATION_FAILED");
        request = request with { Message = request.Message.Trim() };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { request.Message, request.ExpectedRevision }))));
        var replay = await store.Lookup(scope, request.ConversationId, request.ClientMessageId, fingerprint, ct);
        if (replay is not null) return await DeliverSaved(scope, replay, ct);
        var snapshot = await readiness.Require(ct);
        using var permit = execution.Admit(actor.AccountId);
        Admission admission;
        using (metrics?.Start("chat_admission"))
            admission = await store.Admit(scope, request, fingerprint, snapshot, (int)budget.Remaining.TotalMilliseconds, ct);
        if (!admission.Created) return await DeliverSaved(scope, admission.Result, ct);
        // Work is registered with host lifetime. Socket abort does not abandon admitted work.
        var result = await execution.Run(token => Generate(scope, request, snapshot, token));
        // A fresh answer is not delivered after off/publication change; lookup remains available.
        if (await readiness.Require(ct) != snapshot) throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
        await store.Get(scope, request.ConversationId, ct);
        return Deliver(result);
    }
    private async Task<RequestResult> Generate(ChatScope scope, SendChat input, ChatSnapshot snapshot, CancellationToken stopping)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(options.GenerationMilliseconds, budget.Remaining.TotalMilliseconds - 5000))));
        GuestReply? reply = null; string? error = null; var cancel = false;
        try
        {
            var conversation = await store.Get(scope, input.ConversationId, deadline.Token);
            if (!await store.DispatchAllowed(scope, input.ConversationId, input.ClientMessageId, deadline.Token))
                return (await store.Lookup(scope, input.ConversationId, input.ClientMessageId, null, deadline.Token))!;
            var active = await readiness.Require(deadline.Token);
            if (active != snapshot) throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
            var pending = Answer(scope, conversation, input, snapshot, deadline.Token);
            // Observe failures from adapters that ignore cancellation without accepting late completion.
            _ = pending.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            while (!pending.IsCompleted)
            {
                await Task.WhenAny(pending, Task.Delay(100, deadline.Token));
                deadline.Token.ThrowIfCancellationRequested();
                if (!await store.DispatchAllowed(scope, input.ConversationId, input.ClientMessageId, deadline.Token))
                { cancel = true; break; }
                if (await readiness.Require(deadline.Token) != snapshot) throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
            }
            if (!cancel)
            {
                reply = await pending.WaitAsync(deadline.Token);
                if (await readiness.Require(deadline.Token) != snapshot) throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
            }
        }
        catch (ChatFailure ex) { cancel = ex.Code == "ASSISTANT_DISABLED"; error = cancel ? null : "RETRIEVAL_UNAVAILABLE"; }
        catch (DemoGroundedUnavailableException ex) { error = ex.Failure is DemoGroundedFailure.InvalidResponse or DemoGroundedFailure.ResponseTooLarge ? "INVALID_MODEL_OUTPUT" : ex.Failure == DemoGroundedFailure.Timeout ? "MODEL_TIMEOUT" : "MODEL_UNAVAILABLE"; }
        catch (OperationCanceledException) { error = stopping.IsCancellationRequested ? "REQUEST_INTERRUPTED" : "MODEL_TIMEOUT"; }
        catch { error = "INTERNAL_ERROR"; }
        finally { await deadline.CancelAsync(); }
        using var finish = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Min(5000, budget.Remaining.TotalMilliseconds)));
        using var measurement = metrics?.Start("chat_finish");
        return await store.Finish(scope, input.ConversationId, input.ClientMessageId, error is null && !cancel ? reply : null, error, cancel, finish.Token);
    }
    private async Task<GuestReply> Answer(ChatScope scope, Conversation conversation, SendChat input, ChatSnapshot snapshot, CancellationToken ct)
    {
        var locale = AnswerLanguage.Detect(input.Message!, conversation.Locale);
        var read = scope.Role == "driver" ? await reads.TryAnswer(scope.AccountId, conversation with { Locale = locale }, input.ClientMessageId, input.Message!, async token =>
        {
            if (!await store.DispatchAllowed(scope, conversation.Id, input.ClientMessageId, token)) throw new OperationCanceledException(token);
            if (await readiness.Require(token) != snapshot) throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
            if (!await store.DispatchAllowed(scope, conversation.Id, input.ClientMessageId, token)) throw new OperationCanceledException(token);
            // Readiness may have awaited publication after reading settings; close that preparation window.
            var freshSettings = await settings.ReadAsync(token).WaitAsync(token);
            if (!freshSettings.Enabled) throw new ChatFailure(503, "ASSISTANT_DISABLED");
            if (freshSettings.Revision != snapshot.SettingsRevision) throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
            token.ThrowIfCancellationRequested();
        }, ct) : null;
        if (read is not null) return read;
        var corpus = await knowledge.ReadAsync(ct).WaitAsync(ct) ?? throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
        string intent;
        using (metrics?.Start("faq_retrieval"))
            intent = GuestIntents.Classify(input.Message!, corpus.Articles);
        if (input.Message!.Trim().ToLowerInvariant() is "tell me more" or "explain more" or "nói rõ hơn" or "giải thích thêm")
        {
            var turns = await store.Context(scope, conversation.Id, ct);
            var remaining = 12000;
            foreach (var turn in turns.Take(6))
            {
                remaining -= turn.Question.Length + turn.Answer.Content.Length;
                if (remaining < 0) break;
                if (turn.Answer.Sources.FirstOrDefault() is { } source && source.Version == corpus.Version)
                { intent = source.ArticleId; break; }
            }
        }
        var en = locale == "en";
        if (GuestIntents.ParkingTimeGuidance(intent, en) is { } guidance)
            return new(Guid.Empty, "system", "clarification", guidance, [], [], []);
        var articleId = GuestIntents.ArticleId(intent);
        var article = corpus.Articles.SingleOrDefault(a => a.Id == articleId);
        if (article is null)
            return new(Guid.Empty, "system", "clarification", en
                ? "I can explain published SmartPark guidance. Try asking how to use SmartPark or which vehicle types it supports. Please omit personal details and secrets."
                : "Mình có thể giải thích hướng dẫn SmartPark đã công bố. Bạn có thể hỏi về cách sử dụng SmartPark hoặc các loại xe được hỗ trợ. Vui lòng tránh gửi thông tin riêng tư hoặc bí mật.", [], [], []);
        var adapter = new DispatchGuard(answers.Create(corpus, article, locale), async token =>
        {
            if (!await store.DispatchAllowed(scope, conversation.Id, input.ClientMessageId, token)) throw new OperationCanceledException(token);
            var fresh = await readiness.Require(token);
            if (fresh != snapshot || fresh.CorpusFingerprint != corpus.Fingerprint) throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
            if (!await store.DispatchAllowed(scope, conversation.Id, input.ClientMessageId, token)) throw new OperationCanceledException(token);
            token.ThrowIfCancellationRequested();
        });
        var title = en ? article.TitleEn : article.TitleVi;
        var question = GuestIntents.CanonicalQuestion(intent) ?? title;
        var state = await settings.ReadAsync(ct).WaitAsync(ct);
        var answer = ManagementRules.Candidates(state).Count == 0
            ? await adapter.KnowledgeAsync(question, ct).WaitAsync(ct)
            : await generator.GenerateAsync(question, adapter, ct).WaitAsync(ct);
        if (string.IsNullOrWhiteSpace(answer.Reply) || answer.Reply.Length > 8000 || answer.Sources.Count > 1 ||
            answer.Sources.Any(s => s.ArticleId != article.Id || s.Version != corpus.Version))
            throw new DemoGroundedUnavailableException(DemoGroundedFailure.InvalidResponse);
        return answer.Sources.Count == 0
            ? new(Guid.Empty, "system", "clarification", en ? "No supported answer is available." : "Chưa có câu trả lời đủ căn cứ.", [], [], [])
            : new(Guid.Empty, answer.Mode == "knowledge" ? "knowledge" : "ai", "answered", answer.Reply,
                [new(article.Id, corpus.Version, article.SourceLocale ?? locale, title, en ? article.En : article.Vi)], [], []);
    }
    public async Task<bool> Handle(ResetConversation request, CancellationToken ct)
    {
        var actor = Actor();
        if (request.ConversationId == Guid.Empty) throw new ChatFailure(400, "VALIDATION_FAILED");
        await store.Reset(ChatScope.From(actor), request.ConversationId, ct);
        return true;
    }

    public async Task<object> Handle(ReadChat request, CancellationToken ct)
    {
        var actor = Actor();
        var scope = ChatScope.From(actor);
        if (request.ConversationId == Guid.Empty || request.ClientMessageId == Guid.Empty) throw new ChatFailure(400, "VALIDATION_FAILED");
        if (request.ClientMessageId is not { } key) return Deliver(await store.Get(scope, request.ConversationId, ct));
        var result = request.Cancel
            ? await store.Finish(scope, request.ConversationId, key, null, null, true, ct)
            : await store.Lookup(scope, request.ConversationId, key, null, ct) ?? throw new ChatFailure(404, "RESOURCE_NOT_FOUND");
        return await DeliverSaved(scope, result, ct);
    }
    public async Task<object> Handle(ListChat request, CancellationToken ct)
    {
        var actor = Actor();
        if (request.ConversationId == Guid.Empty || request.Limit < 1 || request.Limit > (request.ConversationId is null ? 50 : 100)) throw new ChatFailure(400, "VALIDATION_FAILED");
        var cursor = request.Cursor is null ? new ChatCursor(actor.AccountId, request.ConversationId, request.Limit, clock.GetUtcNow(), null, null, long.MaxValue, long.MaxValue, actor.Role) : cursors.Decode(request.Cursor);
        if (cursor.AccountId != actor.AccountId || cursor.Role != actor.Role || cursor.ConversationId != request.ConversationId || cursor.Limit != request.Limit) throw new ChatFailure(400, "INVALID_CURSOR");
        if (request.ConversationId is not { } id)
        {
            var rows = await store.List(ChatScope.From(actor), cursor, ct);
            var page = rows.Take(request.Limit).ToArray();
            return Deliver(new ChatPage<Conversation>(page, rows.Count > request.Limit ? cursors.Encode(cursor with { Activity = page[^1].LastMessageAt ?? page[^1].CreatedAt, Id = page[^1].Id }) : null));
        }
        var (conversation, messages, active) = await store.Messages(ChatScope.From(actor), id, cursor, ct);
        var selected = messages.Take(request.Limit).OrderBy(x => x.Sequence).ToArray();
        var corpus = await AnnotationCorpus(ct);
        var history = new History(conversation, selected.Select(m => Annotate(m, corpus)).ToArray(), messages.Count > request.Limit
            ? cursors.Encode(cursor with { UpperSequence = Math.Min(cursor.UpperSequence, messages[0].Sequence), BeforeSequence = selected[0].Sequence }) : null, active);
        await store.Get(ChatScope.From(actor), id, ct);
        return Deliver(history);
    }
    private async Task<PublicKnowledge?> AnnotationCorpus(CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(500, budget.Remaining.TotalMilliseconds)));
        try { return await knowledge.ReadAsync(deadline.Token).WaitAsync(deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (Exception) when (!ct.IsCancellationRequested) { return null; }
    }
    private sealed class DispatchGuard(IManagedAnswerProvider inner, Func<CancellationToken, Task> guard) : IManagedAnswerProvider
    {
        public async Task<GroundedReply> GenerateAsync(string provider, string model, string secret, string message, CancellationToken ct)
        { await guard(ct); return await inner.GenerateAsync(provider, model, secret, message, ct); }
        public async Task<GroundedReply> KnowledgeAsync(string message, CancellationToken ct)
        { await guard(ct); return await inner.KnowledgeAsync(message, ct); }
    }
    private static ChatMessage Annotate(ChatMessage message, PublicKnowledge? corpus) => message with
    { Sources = message.Sources.Select(s => s with { Status = corpus is null || !corpus.Articles.Any(a => a.Id == s.ArticleId) ? "withdrawn" : corpus.Version != s.Version ? "outdated" : "active" }).ToArray() };
    private async Task<RequestResult> Annotate(RequestResult result, CancellationToken ct) => result.Answer is null ? result
        : result with { Answer = Annotate(result.Answer, await AnnotationCorpus(ct)) };
}
