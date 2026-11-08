using MediatR;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Application.Assistant.Guest;

public sealed record PublicArticle(string Id, string TitleVi, string TitleEn, string Vi, string En, string[] Sources,
    string? SourceLocale = null, IReadOnlyList<string>? SearchTerms = null);
public sealed record PublicKnowledge(string Version, string Fingerprint, IReadOnlyList<PublicArticle> Articles);
public interface IPublicKnowledge
{
    Task<PublicKnowledge?> ReadAsync(CancellationToken ct);
}
public interface IGuestAnswerFactory
{
    IManagedAnswerProvider Create(PublicKnowledge knowledge, PublicArticle article, string locale);
}
public sealed record GuestOptions(bool ReleaseEnabled = false, int TimeoutMilliseconds = 20000);
public sealed record GuestStatus(bool Enabled, bool GuestFaq, string[] Locales, string ContractVersion = "0.4.1");
public sealed record GuestSource(string ArticleId, string Version, string Locale, string Title, string Excerpt, string Status = "active");
public sealed record GuestAction(string Type, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid? ReservationId = null);
public sealed record GuestReply(Guid RequestId, string Mode, string Outcome, string Content,
    GuestSource[] Sources, object[] Cards, GuestAction[] Actions)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplyLocale { get; init; }
}
public sealed record GuestMessage(Guid RequestId, string? Message, string? Locale) : IRequest<GuestReply>
{
    public override string ToString() => "[GUEST MESSAGE REDACTED]";
}
public sealed record GetGuestStatus : IRequest<GuestStatus>;
public sealed class GuestFailure(int status, string code) : Exception
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class GuestHandlers(IAssistantManagementStore store, IPublicKnowledge corpus,
    ManagedDemoGenerator generator, IGuestAnswerFactory answers, GuestOptions options, AssistantStageMetrics? metrics = null)
    : IRequestHandler<GetGuestStatus, GuestStatus>, IRequestHandler<GuestMessage, GuestReply>
{
    public async Task<GuestStatus> Handle(GetGuestStatus request, CancellationToken ct)
    {
        if (!options.ReleaseEnabled) return new(false, false, ["vi", "en"]);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(options.TimeoutMilliseconds);
        try
        {
            var state = await State(deadline.Token);
            return new(state.Enabled, state.Enabled && await corpus.ReadAsync(deadline.Token).WaitAsync(deadline.Token) is not null, ["vi", "en"]);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new GuestFailure(503, "DEPENDENCY_UNAVAILABLE"); }
    }

    public async Task<GuestReply> Handle(GuestMessage request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.Locale is not ("vi" or "en" or "auto") ||
            string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 4000)
            throw new GuestFailure(400, "VALIDATION_FAILED");
        if (!options.ReleaseEnabled) throw new GuestFailure(503, "ASSISTANT_DISABLED");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(options.TimeoutMilliseconds);
        var token = deadline.Token;
        try
        {
            var state = await State(token);
            if (!state.Enabled) throw new GuestFailure(503, "ASSISTANT_DISABLED");
            var knowledge = await corpus.ReadAsync(token).WaitAsync(token) ?? throw new GuestFailure(503, "DEPENDENCY_UNAVAILABLE");
            string intent;
            using (metrics?.Start("faq_retrieval"))
                intent = GuestIntents.Classify(request.Message, knowledge.Articles);
            var locale = request.Locale == "auto" ? AnswerLanguage.Detect(request.Message) : request.Locale;
            var en = locale == "en";
            GuestReply reply;
            if (intent is "private" or "dynamic" or "unknown" or "sensitive" or GuestIntents.ParkingTimeClarification or GuestIntents.LateArrival)
            {
                var content = intent switch
                {
                    "private" => en ? "Sign in to use the normal account screens. Guest chat cannot read personal records or create a booking." : "Hãy đăng nhập và dùng màn hình tài khoản thông thường. Chat khách không đọc dữ liệu cá nhân hoặc tạo đặt chỗ.",
                    "dynamic" => en ? "Live parking search, prices and availability are not connected to this assistant yet. I cannot confirm a lot, price or free space." : "Trợ lý chưa kết nối tra cứu bãi, giá và chỗ trống trực tiếp. Mình chưa thể xác nhận bãi, giá hoặc số chỗ còn trống.",
                    "sensitive" => en ? "Please do not send personal details or secrets. Ask a general SmartPark question without those details." : "Vui lòng không gửi thông tin cá nhân hoặc mã bí mật. Hãy hỏi về SmartPark mà không kèm thông tin đó.",
                    _ => GuestIntents.ParkingTimeGuidance(intent, en) ?? (en
                        ? "I do not have an approved source for that question. You can ask about SmartPark, vehicle types, booking guidance, payments or support."
                        : "Mình chưa có nguồn được duyệt cho câu hỏi này. Bạn có thể hỏi về SmartPark, loại xe, hướng dẫn đặt chỗ, thanh toán hoặc hỗ trợ.")
                };
                reply = new(request.RequestId, "system", intent == "private" ? "handoff" : "clarification", content, [], [], intent == "private" ? [new("login")] : []);
            }
            else
            {
                var articleId = GuestIntents.ArticleId(intent);
                var article = knowledge.Articles.SingleOrDefault(a => a.Id == articleId);
                if (article is null) throw new GuestFailure(503, "DEPENDENCY_UNAVAILABLE");
                var adapter = answers.Create(knowledge, article, locale);
                var question = intent == GuestIntents.ParkingOverstay
                    ? GuestIntents.ParkingOverstayQuestion
                    : en ? article.TitleEn : article.TitleVi;
                var screenedQuestion = intent == GuestIntents.ParkingOverstay ? null : PublicFaqQuestion.Screen(request.Message, article);
                var answer = ManagementRules.Candidates(state).Count == 0
                    ? await adapter.KnowledgeAsync(question, token).WaitAsync(token)
                    : await generator.GenerateAsync(question,
                        new PublicFaqQuestion(adapter, screenedQuestion), token).WaitAsync(token);
                if (string.IsNullOrWhiteSpace(answer.Reply) || answer.Reply.Length > 8000 ||
                    answer.Sources.Count > 1 || answer.Sources.Any(s => s.ArticleId != article.Id || s.Version != knowledge.Version))
                    throw new GuestFailure(502, "INVALID_MODEL_OUTPUT");
                var source = new GuestSource(article.Id, knowledge.Version, article.SourceLocale ?? locale,
                    en ? article.TitleEn : article.TitleVi, en ? article.En : article.Vi);
                reply = answer.Sources.Count == 0
                    ? new(request.RequestId, "system", "clarification", en ? "No supported answer is available." : "Chưa có câu trả lời đủ căn cứ.", [], [], [])
                    : new(request.RequestId, answer.Mode == "knowledge" ? "knowledge" : "ai", "answered", answer.Reply, [source], [], []);
            }
            var current = await State(token);
            if (!current.Enabled) throw new GuestFailure(503, "ASSISTANT_DISABLED");
            if (current.Revision != state.Revision || (await corpus.ReadAsync(token).WaitAsync(token))?.Fingerprint != knowledge.Fingerprint)
                throw new GuestFailure(503, "DEPENDENCY_UNAVAILABLE");
            token.ThrowIfCancellationRequested();
            return request.Locale == "auto" ? reply with { ReplyLocale = locale } : reply;
        }
        catch (AssistantMaintenanceException) { throw new GuestFailure(503, "ASSISTANT_DISABLED"); }
        catch (ManagementUnavailableException) { throw new GuestFailure(503, "DEPENDENCY_UNAVAILABLE"); }
        catch (DemoGroundedUnavailableException ex)
        {
            throw ex.Failure switch
            {
                DemoGroundedFailure.Timeout => new GuestFailure(504, "REQUEST_TIMEOUT"),
                DemoGroundedFailure.InvalidResponse or DemoGroundedFailure.ResponseTooLarge => new GuestFailure(502, "INVALID_MODEL_OUTPUT"),
                _ => new GuestFailure(503, "DEPENDENCY_UNAVAILABLE")
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new GuestFailure(504, "REQUEST_TIMEOUT"); }
    }
    private async Task<ManagementState> State(CancellationToken ct)
    {
        if (!store.IsConfigured) throw new GuestFailure(503, "DEPENDENCY_UNAVAILABLE");
        try { return await store.ReadAsync(ct).WaitAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch { throw new GuestFailure(503, "DEPENDENCY_UNAVAILABLE"); }
    }
}
