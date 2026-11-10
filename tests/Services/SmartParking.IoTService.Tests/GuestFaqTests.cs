using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.IoTService.API;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Guest;

namespace SmartParking.IoTService.Tests;

public sealed class GuestFaqTests
{
    [Fact]
    public async Task RequestMetricsRecordFinalStatusAndOnlyRouteTemplates()
    {
        await using var app = new Fixture().Host();
        var meter = app.Services.GetRequiredService<IMeterFactory>().Create("SmartPark.Assistant");
        var measurements = new List<(double Duration, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter == meter && instrument.Name == "smartpark.assistant.request.duration")
                current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, duration, tags, _) =>
            measurements.Add((duration, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value))));
        listener.Start();
        await app.StartAsync();
        using var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/assistant/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/private-input?origin=private-input")).StatusCode);

        Assert.Collection(measurements,
            entry => Verify(entry, "/api/assistant/status", 200),
            entry => Verify(entry, "unmatched", 404));

        static void Verify((double Duration, Dictionary<string, object?> Tags) entry, string route, int status)
        {
            Assert.True(entry.Duration >= 0);
            Assert.Equal(3, entry.Tags.Count);
            Assert.Equal(route, entry.Tags["http.route"]);
            Assert.Equal(status, entry.Tags["http.response.status_code"]);
            Assert.Equal(false, entry.Tags["smartpark.assistant.request.aborted"]);
        }
    }

    [Theory]
    [InlineData("SmartPark là gì?", "smartpark-overview")]
    [InlineData("What is SmartPark?", "smartpark-overview")]
    [InlineData("Loại xe nào?", "vehicle-guide")]
    [InlineData("Which vehicle types?", "vehicle-guide")]
    [InlineData("Hướng dẫn tìm bãi", "discovery-guide")]
    [InlineData("How to find parking?", "discovery-guide")]
    [InlineData("Hướng dẫn đặt chỗ", "booking-guide")]
    [InlineData("How to book?", "booking-guide")]
    [InlineData("Thanh toán thế nào?", "payment-guide")]
    [InlineData("Payment guidance", "payment-guide")]
    [InlineData("Cần hỗ trợ", "support-guide")]
    [InlineData("Contact support", "support-guide")]
    [InlineData("Giới hạn trợ lý", "assistant-limits")]
    [InlineData("Assistant limitations", "assistant-limits")]
    public async Task TopicsUseApprovedLocalizedSources(string question, string id)
    {
        var f = new Fixture();
        foreach (var locale in new[] { "vi", "en" })
        {
            var reply = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), question, locale), default);
            Assert.Equal("knowledge", reply.Mode);
            Assert.Equal(id, Assert.Single(reply.Sources).ArticleId);
            Assert.Equal(locale, reply.Sources[0].Locale);
            Assert.Equal("active", reply.Sources[0].Status);
            Assert.Empty(reply.Cards);
            Assert.Empty(reply.Actions);
        }
    }

    [Theory]
    [InlineData("how do smartpark", "en")]
    [InlineData("smartpark who", "en")]
    [InlineData("How does SmartPark work?", "en")]
    [InlineData("SmartPark hoạt động như thế nào?", "vi")]
    [InlineData("smartpark hỗ trợ gì", "vi")]
    [InlineData("What can SmartPark help with?", "en")]
    public async Task OverviewVariantsFindSourceAndMatchQuestionLanguage(string question, string locale)
    {
        var fixture = new Fixture();
        var reply = await fixture.Handler.Handle(new GuestMessage(Guid.NewGuid(), question, "auto"), default);
        Assert.Equal("answered", reply.Outcome);
        Assert.Equal(locale, reply.ReplyLocale);
        Assert.Equal("smartpark-overview", Assert.Single(reply.Sources).ArticleId);
    }

    [Theory]
    [InlineData("My bookings tomorrow", "handoff")]
    [InlineData("Lịch đặt của tôi", "handoff")]
    [InlineData("parking nearby", "clarification")]
    [InlineData("bãi còn chỗ không", "clarification")]
    [InlineData("giá bao nhiêu", "clarification")]
    [InlineData("hotline SmartPark", "clarification")]
    [InlineData("SmartPark address", "clarification")]
    [InlineData("Tell me about astronomy", "clarification")]
    [InlineData("Who founded SmartPark?", "clarification")]
    [InlineData("Does SmartPark sell car insurance?", "clarification")]
    [InlineData("SmartPark bán bảo hiểm xe không?", "clarification")]
    [InlineData("Bãi nào còn chỗ lúc 14h?", "clarification")]
    [InlineData("payment email synthetic@example.test", "clarification")]
    [InlineData("biển số 30A12345", "clarification")]
    public async Task PrivateDynamicUnknownAndSensitiveDoNotDispatch(string message, string outcome)
    {
        var f = new Fixture();
        var reply = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), message, "en"), default);
        Assert.Equal("system", reply.Mode);
        Assert.Equal(outcome, reply.Outcome);
        Assert.Empty(reply.Sources);
        Assert.Equal(0, f.Answers.Calls);
        Assert.Equal(outcome == "handoff" ? 1 : 0, reply.Actions.Length);
    }

    [Fact]
    public async Task ColloquialBookingQuestionUsesApprovedBookingGuide()
    {
        var f = new Fixture();
        f.EnableProvider("gemini");
        var reply = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "smartpark đặt xe như nào", "vi"), default);
        Assert.Equal("booking-guide", Assert.Single(reply.Sources).ArticleId);
        Assert.Equal(1, f.Answers.Generations);
    }

    [Theory]
    [InlineData("smartpark nếu để xe trễ như nào")]
    [InlineData("Gửi xe muộn thì sao?")]
    public async Task AmbiguousParkingTimeAsksWhichTimeBeforeUsingProvider(string question)
    {
        var f = new Fixture();
        f.EnableProvider("gemini");
        var reply = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), question, "vi"), default);
        Assert.Equal("system", reply.Mode);
        Assert.Equal("clarification", reply.Outcome);
        Assert.Contains("đến bãi sau giờ bắt đầu", reply.Content);
        Assert.Contains("đỗ xe quá giờ kết thúc", reply.Content);
        Assert.Equal(0, f.Answers.Calls);
    }

    [Fact]
    public async Task ParkingTimeClarificationCanContinueWithoutGuestHistory()
    {
        var f = new Fixture();
        f.EnableProvider("gemini");
        var clarification = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "SmartPark để xe trễ thì phạt sao", "vi"), default);
        Assert.Equal("clarification", clarification.Outcome);

        var overstay = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "đỗ xe quá giờ", "vi"), default);
        Assert.Equal("answered", overstay.Outcome);
        Assert.Equal("payment-guide", Assert.Single(overstay.Sources).ArticleId);
        Assert.Contains("phí phát sinh", f.Answers.LastQuestion);

        var arrival = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "đến bãi trễ", "vi"), default);
        Assert.Contains("trạng thái và điều khoản", arrival.Content);
        Assert.Equal(1, f.Answers.Generations);
    }

    [Fact]
    public async Task OffMissingAndWithdrawnKnowledgeFailClosed()
    {
        var f = new Fixture();
        f.Store.State = f.Store.State with { Enabled = false, MaintenanceMessage = "PRIVATE ADMIN MESSAGE" };
        var status = await f.Handler.Handle(new GetGuestStatus(), default);
        Assert.False(status.Enabled); Assert.False(status.GuestFaq);
        Assert.Equal("ASSISTANT_DISABLED", (await Assert.ThrowsAsync<GuestFailure>(() => f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "booking", "vi"), default))).Code);
        f.Store.State = f.Store.State with { Enabled = true };
        f.Corpus.Value = null;
        Assert.False((await f.Handler.Handle(new GetGuestStatus(), default)).GuestFaq);
        Assert.Equal(503, (await Assert.ThrowsAsync<GuestFailure>(() => f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "booking", "vi"), default))).Status);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("revision")]
    [InlineData("withdraw")]
    public async Task StateAndPublicationAreRecheckedBeforeDelivery(string change)
    {
        var f = new Fixture();
        f.Answers.BeforeAnswer = () =>
        {
            if (change == "off") f.Store.State = f.Store.State with { Enabled = false };
            if (change == "revision") f.Store.State = f.Store.State with { Revision = 2 };
            if (change == "withdraw") f.Corpus.Value = null;
        };
        Assert.Equal(503, (await Assert.ThrowsAsync<GuestFailure>(() => f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "booking", "en"), default))).Status);
    }

    [Fact]
    public async Task ProviderFailuresUsePublicKnowledgeAndNeverRawInput()
    {
        var f = new Fixture(); f.EnableProvider();
        f.Answers.Fail = new DemoGroundedUnavailableException(DemoGroundedFailure.Network);
        var result = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "payment", "en"), default);
        Assert.Equal("knowledge", result.Mode);
        Assert.DoesNotContain("Synthetic Person", f.Answers.LastQuestion);
        Assert.Equal("Payment guidance", f.Answers.LastQuestion);
        Assert.Equal(1, f.Answers.Generations);
    }

    [Theory]
    [InlineData("gemini", "How to pay?", "How to pay?")]
    [InlineData("openai", "How to pay?", "Payment guidance")]
    [InlineData("gemini", "How to pay for An?", "Payment guidance")]
    [InlineData("gemini", "how to pay for an?", "Payment guidance")]
    [InlineData("gemini", "An, how to pay?", "Payment guidance")]
    [InlineData("gemini", "An how to pay?", "Payment guidance")]
    public async Task OnlyGeminiReceivesScreenedPublicQuestion(string provider, string question, string expected)
    {
        var fixture = new Fixture();
        fixture.EnableProvider(provider);
        var knowledge = fixture.Corpus.Value!;
        fixture.Corpus.Value = knowledge with { Articles = knowledge.Articles.Select(article => article.Id == "payment-guide"
            ? article with { SearchTerms = ["pay"] } : article).ToArray() };
        await fixture.Handler.Handle(new GuestMessage(Guid.NewGuid(), question, "en"), default);
        Assert.Equal(expected, fixture.Answers.LastQuestion);
    }

    [Fact]
    public async Task CorrectedFaqUsesTheServerTopicForGemini()
    {
        var fixture = new Fixture();
        fixture.EnableProvider("gemini");
        var knowledge = fixture.Corpus.Value!;
        var vehicle = knowledge.Articles.Single(article => article.Id == "vehicle-guide");
        fixture.Corpus.Value = knowledge with { Articles = [vehicle with { SearchTerms = ["vehicle types"] }] };
        var reply = await fixture.Handler.Handle(new GuestMessage(Guid.NewGuid(), "vehcile types", "auto"), default);
        Assert.Equal("en", reply.ReplyLocale);
        Assert.Equal("vehicle-guide", Assert.Single(reply.Sources).ArticleId);
        Assert.Equal(vehicle.TitleEn, fixture.Answers.LastQuestion);
    }

    [Theory]
    [InlineData("gemini", "Đặt chỗ tối thiểu 15 phút đúng không?", "booking-guide", "Thời lượng đặt chỗ tối thiểu")]
    [InlineData("openai", "Đặt chỗ trước tối đa bao nhiêu ngày?", "booking-guide", "Có thể đặt chỗ trước tối đa")]
    [InlineData("gemini", "Cứ chạy thẳng tới bãi gửi xe được không?", "walk-in-and-entry-guide", "Có thể tới gửi xe trực tiếp")]
    [InlineData("openai", "Can I get a refund if I cancel a monthly pass?", "pass-cancellation-guide", "chỉ tính phần đủ điều kiện hoàn")]
    [InlineData("gemini", "Bãi đóng cửa cả ngày, vậy hủy gói tháng có hoàn tiền?", "pass-cancellation-guide", "khi bãi đóng cửa tạm thời hoặc vĩnh viễn")]
    public async Task RecognizedPublicIntentUsesOnlyTheCanonicalQuestion(string provider, string question, string sourceId, string promptFragment)
    {
        var fixture = new Fixture();
        fixture.EnableProvider(provider);
        var knowledge = fixture.Corpus.Value!;
        if (knowledge.Articles.All(article => article.Id != sourceId))
            fixture.Corpus.Value = knowledge with { Articles = [.. knowledge.Articles, knowledge.Articles[0] with { Id = sourceId }] };

        var reply = await fixture.Handler.Handle(new GuestMessage(Guid.NewGuid(), question, "auto"), default);

        Assert.Equal("answered", reply.Outcome);
        Assert.Equal(sourceId, Assert.Single(reply.Sources).ArticleId);
        Assert.Equal(1, fixture.Answers.Generations);
        Assert.Contains(promptFragment, fixture.Answers.LastQuestion);
        Assert.DoesNotContain(question, fixture.Answers.LastQuestion);
        Assert.DoesNotContain("15", fixture.Answers.LastQuestion);
    }

    [Theory]
    [InlineData("How to pay with 0901234567?")]
    [InlineData("How to pay with me@example.test?")]
    [InlineData("How to pay with my token?")]
    [InlineData("Đặt chỗ tối thiểu 15 phút đúng không? OTP 123456")]
    [InlineData("Đặt chỗ tối thiểu 15 phút đúng không? me@example.test")]
    [InlineData("Đặt chỗ tối thiểu 15@phút đúng không?")]
    [InlineData("Đặt chỗ trước tối đa bao nhiêu ngày ở bãi gần tôi?")]
    [InlineData("Đến bãi còn nhận xe lúc này không?")]
    [InlineData("Is the parking lot accepting cars right now?")]
    [InlineData("What happens if I stay past my reservation AB123?")]
    [InlineData("Đặt chỗ tối thiểu 123456 phút đúng không?")]
    public async Task RestrictedGuestQuestionsNeverCallGemini(string question)
    {
        var fixture = new Fixture();
        fixture.EnableProvider("gemini");
        var reply = await fixture.Handler.Handle(new GuestMessage(Guid.NewGuid(), question, "en"), default);
        Assert.Equal("system", reply.Mode);
        Assert.Equal(0, fixture.Answers.Generations);
    }

    [Theory]
    [InlineData(DemoGroundedFailure.InvalidResponse, 502)]
    [InlineData(DemoGroundedFailure.ResponseTooLarge, 502)]
    [InlineData(DemoGroundedFailure.Knowledge, 503)]
    public async Task NonRecoverableFailuresAreNotRetried(DemoGroundedFailure failure, int status)
    {
        var f = new Fixture(); f.EnableProvider(); f.Answers.Fail = new DemoGroundedUnavailableException(failure);
        Assert.Equal(status, (await Assert.ThrowsAsync<GuestFailure>(() => f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "booking", "vi"), default))).Status);
        Assert.Equal(1, f.Answers.Generations);
    }

    [Fact]
    public async Task DeadlineBoundsUncooperativeWorkAndCancellationIsPreserved()
    {
        var f = new Fixture(40); f.Answers.Stall = true;
        Assert.Equal(504, (await Assert.ThrowsAsync<GuestFailure>(() => f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "booking", "vi"), default))).Status);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "booking", "vi"), cts.Token));
    }

    [Fact]
    public void NumericDynamicQuestionUsesCapabilityResponse()
    {
        Assert.Equal("dynamic", GuestIntents.Classify("Bãi nào còn chỗ lúc 14h?"));
        Assert.Equal("private", GuestIntents.Classify("My bookings at 14h"));
        Assert.Equal("unknown", GuestIntents.Classify("Who founded SmartPark?"));
    }

    [Fact]
    public async Task RealManagementCompositionKeepsGuestOffAndAdminProtected()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows DPAPI composition; Unix certificate setup remains deployment acceptance.
        using var rsa = RSA.Create(2048);
        var directory = Path.Combine(Path.GetTempPath(), "smartpark-s04-di-" + Guid.NewGuid());
        await using var app = AssistantGuestHost.Build(["--environment", "Production"], b =>
        {
            b.Host.UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; });
            b.Configuration.Sources.Clear();
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Assistant:Jwt:Issuer"] = "synthetic-issuer",
                ["Assistant:Jwt:Audience"] = "synthetic-audience",
                ["Assistant:Jwt:PublicKeyPem"] = rsa.ExportRSAPublicKeyPem(),
                ["Assistant:Management:KeyDirectory"] = directory,
                ["Assistant:Guest:KnowledgeDirectory"] = directory,
                ["ConnectionStrings:IoT"] = "Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic;Timeout=1"
            });
            b.WebHost.UseTestServer();
            // S05 recovery is hosted too; keep this real-composition test entirely offline.
            b.Services.AddSingleton<SmartParking.IoTService.Application.Assistant.Chat.IChatStore>(new ChatMemoryStore(TimeProvider.System));
        });
        await app.StartAsync(); using var client = app.GetTestClient();
        Assert.IsType<Infrastructure.Assistant.Guest.PublicKnowledgeFile>(app.Services.GetRequiredService<IPublicKnowledge>());
        Assert.NotNull(app.Services.GetRequiredService<ManagedDemoGenerator>());
        var status = await client.GetFromJsonAsync<JsonElement>("/api/assistant/status");
        Assert.False(status.GetProperty("data").GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/assistant/admin/settings")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/assistant/admin/settings")).StatusCode);
    }

    [Fact]
    public async Task CorpusStallIsBounded()
    {
        var f = new Fixture(40); f.Corpus.Stall = true;
        Assert.Equal(504, (await Assert.ThrowsAsync<GuestFailure>(() => f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "booking", "vi"), default))).Status);
        Assert.Equal(503, (await Assert.ThrowsAsync<GuestFailure>(() => f.Handler.Handle(new GetGuestStatus(), default))).Status);
    }

    [Theory]
    [InlineData("wrong-id")]
    [InlineData("old-version")]
    [InlineData("oversized")]
    public async Task InvalidAnswerCannotBecomePublic(string kind)
    {
        var f = new Fixture(); f.Answers.Invalid = kind;
        Assert.Equal(502, (await Assert.ThrowsAsync<GuestFailure>(() => f.Handler.Handle(new GuestMessage(Guid.NewGuid(), "booking", "vi"), default))).Status);
    }

    [Fact]
    public async Task ProductionHostContractAndAbuseLimits()
    {
        var f = new Fixture();
        await using var app = f.Host(); await app.StartAsync(); using var client = app.GetTestClient();
        var status = await client.GetAsync("/api/assistant/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal("no-store", status.Headers.CacheControl?.ToString());
        Assert.False(status.Headers.Contains("X-Assistant-Mode"));
        var json = await status.Content.ReadAsStringAsync(); Assert.DoesNotContain("provider", json); Assert.DoesNotContain("model", json);
        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/assistant/guest/messages", new { requestId = Guid.NewGuid(), message = "booking", locale = "en", accountId = "foreign", transcript = "PRIVATE", draft = "PRIVATE" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("PRIVATE", await response.Content.ReadAsStringAsync());
        }
        using var extra = new HttpRequestMessage(HttpMethod.Post, "/api/assistant/guest/messages") { Content = JsonContent.Create(new { requestId = Guid.NewGuid(), message = "booking", locale = "vi" }) };
        extra.Headers.Add("X-Forwarded-For", "203.0.113.99");
        var rejected = await client.SendAsync(extra);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode); Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/demo/assistant/capabilities")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/assistant/conversations")).StatusCode);
    }

    [Theory]
    [InlineData("{}", "application/json", 400)]
    [InlineData("{", "application/json", 400)]
    [InlineData("{}", "text/plain", 415)]
    [InlineData("{\"requestId\":\"11111111-1111-4111-8111-111111111111\",\"message\":\"booking\",\"locale\":\"fr\"}", "application/json", 400)]
    public async Task HttpRejectsBadInputs(string body, string media, int status)
    {
        var f = new Fixture(); await using var app = f.Host(); await app.StartAsync(); using var client = app.GetTestClient();
        var response = await client.PostAsync("/api/assistant/guest/messages", new StringContent(body, Encoding.UTF8, media));
        Assert.Equal(status, (int)response.StatusCode); Assert.Contains("\"success\":false", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HttpBodyAndConcurrencyAreBounded()
    {
        var f = new Fixture(); await using var app = f.Host(); await app.StartAsync(); using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/assistant/guest/messages", new StringContent(new string('x', 32769), Encoding.UTF8, "application/json"))).StatusCode);
        f.Answers.Stall = true;
        using var cts = new CancellationTokenSource();
        var one = client.PostAsJsonAsync("/api/assistant/guest/messages", new { requestId = Guid.NewGuid(), message = "booking", locale = "en" }, cts.Token);
        var two = client.PostAsJsonAsync("/api/assistant/guest/messages", new { requestId = Guid.NewGuid(), message = "booking", locale = "en" }, cts.Token);
        await f.Answers.TwoCalls.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/API/assistant/guest/messages/", new { requestId = Guid.NewGuid(), message = "booking", locale = "en" })).StatusCode);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await one);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await two);
    }

    internal sealed class Fixture
    {
        public Store Store = new(); public Corpus Corpus = new(); public Answers Answers = new(); public GuestHandlers Handler;
        private readonly ManagedDemoGenerator generator;
        public Fixture(int timeout = 20000)
        {
            generator = new(Store, new Protector(), Answers, TimeProvider.System, NullLogger<ManagedDemoGenerator>.Instance);
            Handler = new(Store, Corpus, generator, Answers, new(true, timeout));
        }
        public void EnableProvider(string provider = "openai") => Store.State = Store.State with { Providers = [new(provider, "synthetic-model", true, 1)], Keys = [new(Guid.NewGuid(), provider, "synthetic", true, 1, Guid.NewGuid(), DateTimeOffset.UtcNow, null)] };
        public WebApplication Host() => AssistantGuestHost.Build(["--environment", "Production"], b =>
        {
            b.Configuration.Sources.Clear(); b.WebHost.UseTestServer();
            b.Services.AddAuthentication("test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoAuth>("test", _ => { });
            b.Services.AddAuthorization(o => o.AddPolicy("AssistantAdmin", p => p.RequireAuthenticatedUser().RequireRole("admin")));
            b.Services.AddSingleton<IAssistantManagementStore>(Store); b.Services.AddSingleton<IPublicKnowledge>(Corpus);
            b.Services.AddSingleton<IGuestAnswerFactory>(Answers); b.Services.AddSingleton(generator); b.Services.AddSingleton(new GuestOptions(true));
        });
    }
    internal sealed class NoAuth(Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options, Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
    { protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult()); }
    internal sealed class Corpus : IPublicKnowledge
    {
        public PublicKnowledge? Value = new("test-v1", "synthetic-hash", new[] { "smartpark-overview", "vehicle-guide", "discovery-guide", "booking-guide", "payment-guide", "support-guide", "assistant-limits" }
            .Select(id => new PublicArticle(id, "Hướng dẫn", id == "payment-guide" ? "Payment guidance" : "Guidance", "Nội dung tổng hợp.", "Synthetic public guidance.", ["docs/01-requirements/srs/modules/10-ai-assistance.md"])).ToArray());
        public bool Stall;
        public Task<PublicKnowledge?> ReadAsync(CancellationToken ct) => Stall ? new TaskCompletionSource<PublicKnowledge?>().Task : Task.FromResult(Value);
    }
    internal sealed class Store : IAssistantManagementStore
    {
        public ManagementState State = ManagementState.Empty with { Enabled = true, Revision = 1 };
        public bool IsConfigured => true;
        public Task<ManagementState> ReadAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(State); }
        public Task<string?> CredentialAsync(Guid slot, Guid version, CancellationToken ct) => Task.FromResult<string?>("synthetic-key");
        public Task<ManagementState> MutateAsync(Guid actor, AdminMutation input, Func<ManagementState, ManagementWrite> change, CancellationToken ct) => throw new NotSupportedException();
    }
    internal sealed class Protector : IAssistantSecretProtector
    {
        public bool IsConfigured => true;
        public string Protect(Guid version, string secret) => secret;
        public string Unprotect(Guid version, string protectedValue) => protectedValue;
    }
    internal sealed class Answers : IGuestAnswerFactory, IManagedAnswerProvider
    {
        public int Calls; public int Generations; public string LastQuestion = ""; public bool Stall; public string? Invalid; public Exception? Fail; public Action? BeforeAnswer;
        public TaskCompletionSource TwoCalls = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private GroundedSource source = new("booking-guide", "test-v1", "Guidance", "Synthetic public guidance.");
        public IManagedAnswerProvider Create(PublicKnowledge knowledge, PublicArticle article, string locale)
        { source = new(article.Id, knowledge.Version, locale == "en" ? article.TitleEn : article.TitleVi, locale == "en" ? article.En : article.Vi); return this; }
        public Task<GroundedReply> GenerateAsync(string provider, string model, string secret, string message, CancellationToken ct)
        { Generations++; LastQuestion = message; return Fail is null ? KnowledgeAsync(message, ct) : Task.FromException<GroundedReply>(Fail); }
        public Task<GroundedReply> KnowledgeAsync(string message, CancellationToken ct)
        {
            if (Interlocked.Increment(ref Calls) >= 2) TwoCalls.TrySetResult();
            BeforeAnswer?.Invoke();
            if (Stall) return new TaskCompletionSource<GroundedReply>().Task;
            var outputSource = Invalid == "wrong-id" ? source with { ArticleId = "private-draft" } : Invalid == "old-version" ? source with { Version = "old" } : source;
            return Task.FromResult(new GroundedReply(Invalid == "oversized" ? new string('x', 8001) : source.Excerpt, [outputSource], "private-provider", "private-model", "knowledge"));
        }
    }
}
