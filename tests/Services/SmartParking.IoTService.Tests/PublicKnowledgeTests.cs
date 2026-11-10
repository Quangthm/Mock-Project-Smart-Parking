using System.Net;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Guest;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;
using Xunit.Abstractions;
namespace SmartParking.IoTService.Tests;

public sealed class PublicKnowledgeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("draft", false)]
    [InlineData("private", false)]
    [InlineData("owner", false)]
    [InlineData("duplicate-metadata", false)]
    [InlineData("missing-content", false)]
    [InlineData("edited", false)]
    [InlineData("expired", false)]
    [InlineData("future", false)]
    [InlineData("missing-approval", false)]
    [InlineData("duplicate-article", false)]
    [InlineData("pending-review", false)]
    [InlineData("oversized", false)]
    [InlineData("source-expired", false)]
    [InlineData("source-future", false)]
    [InlineData("source-malformed-date", false)]
    [InlineData("source-inverted-window", false)]
    [InlineData("source-future-publication", false)]
    [InlineData("source-publication-mismatch", false)]
    public async Task PublicationRequiresApprovedExactPublicBytes(string variant, bool ready)
    {
        var dir = Path.Combine(Path.GetTempPath(), "smartpark-s04-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try
        {
            var section = "## [booking-guide] Hướng dẫn\n\nTừ khóa: booking; đặt chỗ\n\n### Nội dung\n\nHướng dẫn gốc.\n\n### Đối chiếu — không gửi đến AI\nEDITORIAL_PRIVATE_ONLY\n";
            if (variant == "missing-content") section = section.Replace("### Nội dung", "### Sai cấu trúc");
            if (variant == "duplicate-article") section += section;
            if (variant == "oversized") section = section.Replace("Hướng dẫn gốc.", new string('a',6001));
            var sourceDates = variant switch {
                "source-expired" => "\neffective_to: " + DateTimeOffset.UtcNow.AddDays(-1).ToString("O"),
                "source-future" => "\neffective_from: " + DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
                "source-malformed-date" => "\neffective_to: not-a-date",
                "source-inverted-window" => "\neffective_from: 2020-02-01T00:00:00Z\neffective_to: 2020-01-01T00:00:00Z",
                "source-future-publication" => "\npublished_at: " + DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
                "source-publication-mismatch" => "\npublished_at: 2020-01-01T00:00:00Z",
                _ => "\neffective_from: null\neffective_to: null\npublished_at: null" };
            var markdown = "---\nversion: test-v1\nlocale: vi\naudience: " + (variant == "private" ? "[driver]" : "[public, driver]") +
                "\npublication_state: " + (variant == "draft" ? "draft" : "published") +
                "\nreview_status: " + (variant == "pending-review" ? "pending" : "approved") +
                (variant == "duplicate-metadata" ? "\naudience: [public]" : "") + sourceDates + "\n---\n# Kiến thức\n\n" + section;
            var document = Encoding.UTF8.GetBytes(markdown);
            File.WriteAllBytes(Path.Combine(dir, "smartpark-knowledge.md"), document);
            if (variant != "missing-approval") File.WriteAllText(Path.Combine(dir, "publication.json"), JsonSerializer.Serialize(new
            {
                sha256 = Convert.ToHexString(SHA256.HashData(document)), version = "test-v1", approvedBy = "synthetic-admin", role = variant == "owner" ? "owner" : "admin",
                publishedAt = DateTimeOffset.UtcNow.AddDays(variant == "future" ? 1 : -1), effectiveTo = variant == "expired" ? DateTimeOffset.UtcNow.AddHours(-1) : (DateTimeOffset?)null
            }));
            if (variant == "edited") File.AppendAllText(Path.Combine(dir, "smartpark-knowledge.md"), " ");
            var knowledge = await new PublicKnowledgeFile(dir, TimeProvider.System).ReadAsync(default);
            Assert.Equal(ready, knowledge is not null);
            if (knowledge is not null)
            {
                var article = Assert.Single(knowledge.Articles);
                Assert.Equal("vi", article.SourceLocale);
                Assert.Equal(article.Vi, article.En);
                Assert.DoesNotContain("EDITORIAL_PRIVATE_ONLY", article.Vi);
            }
        }
        finally { foreach (var file in Directory.GetFiles(dir)) File.Delete(file); Directory.Delete(dir); }
    }

    [Fact]
    public async Task FullVietnamesePolicyIsRetainedAndEnglishFallbackIsLabelled()
    {
        var content = new string('a', 1200) + " Điều kiện cuối không được bỏ.";
        var article = new PublicArticle("booking-guide", "Hướng dẫn", "Hướng dẫn", content, content, [], "vi");
        var knowledge = new PublicKnowledge("v1", "hash", [article]);
        var answer = await new GuestAnswerFactory().Create(knowledge, article, "en").KnowledgeAsync("Hướng dẫn", default);
        Assert.StartsWith("The answer is currently available in Vietnamese only.", answer.Reply);
        Assert.Contains("Điều kiện cuối không được bỏ.", answer.Reply);
        Assert.Equal(content, Assert.Single(answer.Sources).Excerpt);
        var vietnamese = await new GuestAnswerFactory().Create(knowledge, article, "vi").KnowledgeAsync("Hướng dẫn", default);
        Assert.Equal(content, vietnamese.Reply);
    }

    [Fact]
    public async Task LegacyJsonIsNeverUsedAsFallback()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smartpark-md-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "public-faq.json"), "{}");
            Assert.Null(await new PublicKnowledgeFile(dir, TimeProvider.System).ReadAsync(default));
        }
        finally { File.Delete(Path.Combine(dir, "public-faq.json")); Directory.Delete(dir); }
    }

    [Fact]
    public async Task CachedKnowledgeStillChecksFreshBytesManifestAndExpiry()
    {
        var directory = Directory.CreateTempSubdirectory("assistant-public-cache-");
        var now = DateTimeOffset.UtcNow;
        var clock = new AuthenticatedChatTests.Clock { Now = now };
        var document = Path.Combine(directory.FullName, "smartpark-knowledge.md");
        var manifest = Path.Combine(directory.FullName, "publication.json");
        var markdown = $"""
            ---
            version: cache-v1
            locale: vi
            audience: [public, driver]
            publication_state: published
            review_status: approved
            effective_to: {now.AddHours(1):O}
            ---
            ## [booking-guide] Hướng dẫn

            Từ khóa: đặt chỗ

            ### Nội dung

            Hướng dẫn gốc.
            """;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(markdown);
            await File.WriteAllBytesAsync(document, bytes);
            var publication = JsonSerializer.Serialize(new
            {
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)), version = "cache-v1",
                approvedBy = "synthetic-test-only", role = "admin", publishedAt = now.AddDays(-1)
            });
            await File.WriteAllTextAsync(manifest, publication);
            var knowledge = new PublicKnowledgeFile(directory.FullName, clock);
            var initial = Assert.IsType<PublicKnowledge>(await knowledge.ReadAsync(default));
            Assert.Same(initial, await knowledge.ReadAsync(default));
            await File.WriteAllTextAsync(manifest, publication.Replace("admin", "owner"));
            Assert.Null(await knowledge.ReadAsync(default));
            await File.WriteAllTextAsync(manifest, publication);
            var timestamp = File.GetLastWriteTimeUtc(document);
            await File.WriteAllTextAsync(document, markdown.Replace("gốc", "mới"));
            File.SetLastWriteTimeUtc(document, timestamp);
            Assert.Null(await knowledge.ReadAsync(default));
            await File.WriteAllBytesAsync(document, bytes);
            Assert.NotNull(await knowledge.ReadAsync(default));
            clock.Now = now.AddHours(1);
            Assert.Null(await knowledge.ReadAsync(default));
        }
        finally
        {
            foreach (var file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("gemini")]
    public async Task ProviderReceivesOnlySuppliedPublicSnapshot(string provider)
    {
        var handler = new Http(); using var client = new HttpClient(handler);
        var sources = new[] { new GroundedSource("public-topic", "v1", "Public title", "Approved synthetic FAQ.") };
        using var generator = new DemoGroundedGenerator(new("missing-draft-directory", provider, "synthetic-model", "synthetic-key", sources, "en", "vi"), client);
        handler.Provider = provider;
        var result = await generator.GenerateAsync("Public title", default);
        Assert.Equal("public-topic", Assert.Single(result.Sources).ArticleId);
        Assert.Contains("Approved synthetic FAQ", handler.Body);
        Assert.DoesNotContain("missing-draft-directory", handler.Body);
        Assert.Contains("en", handler.Body);
        using var body = JsonDocument.Parse(handler.Body);
        var input = provider == "openai" ? body.RootElement.GetProperty("input").GetString()!
            : body.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        using var snapshot = JsonDocument.Parse(input);
        Assert.Equal("en", snapshot.RootElement.GetProperty("answerLanguage").GetString());
        Assert.Equal("vi", snapshot.RootElement.GetProperty("sourceLanguage").GetString());
        Assert.Contains("answerLanguage is authoritative", handler.Body);
    }

    [Fact]
    public async Task GuestGenerationReusesNamedHandlerAndEmitsOnlyOutcomeMetrics()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddAssistantProviderTransport();
        var handlerCount = 0;
        var handler = new Http { Provider = "gemini" };
        services.AddHttpClient(ProviderTransport.ClientName).ConfigurePrimaryHttpMessageHandler(() =>
        {
            handlerCount++;
            return handler;
        });
        using var provider = services.BuildServiceProvider();
        var meters = provider.GetRequiredService<IMeterFactory>();
        var meter = meters.Create("SmartPark.Assistant");
        using var listener = new MeterListener();
        var outcomes = new List<string>();
        listener.InstrumentPublished = (instrument, active) =>
        {
            if (instrument.Meter == meter && instrument.Name == "smartpark.assistant.provider.attempt.duration")
                active.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, duration, tags, _) =>
        {
            Assert.True(duration >= 0);
            var tag = Assert.Single(tags.ToArray());
            Assert.Equal("smartpark.assistant.outcome", tag.Key);
            outcomes.Add(Assert.IsType<string>(tag.Value));
        });
        listener.Start();

        var clients = provider.GetRequiredService<IHttpClientFactory>();
        var factory = new GuestAnswerFactory(clients, meters);
        var article = new PublicArticle("public-topic", "FAQ", "FAQ", "Approved", "Approved", []);
        var knowledge = new PublicKnowledge("v1", "hash", [article]);
        foreach (var secret in new[] { "synthetic-first", "synthetic-second" })
        {
            var answer = await factory.Create(knowledge, article, "en")
                .GenerateAsync("gemini", "synthetic-model", secret, "FAQ", default);
            Assert.Equal("public-topic", Assert.Single(answer.Sources).ArticleId);
            Assert.Equal(secret, handler.Key);
        }
        Assert.Equal(1, handlerCount);
        Assert.Equal(new[] { "completed", "completed" }, outcomes);
    }

    [Fact]
    public async Task RepositoryMarkdownSuppliesEveryGuestTopicWithoutEditorialNotes()
    {
        const string relative = "docs/03-design/ai/p1/knowledge/smartpark-knowledge.md";
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, relative))) root = root.Parent;
        Assert.NotNull(root);
        var markdown = File.ReadAllText(Path.Combine(root.FullName, relative))
            .Replace("publication_state: draft", "publication_state: published")
            .Replace("review_status: pending", "review_status: approved");
        var version = System.Text.RegularExpressions.Regex.Match(markdown, @"(?m)^version: ([^\r\n]+)").Groups[1].Value;
        var dir = Path.Combine(Path.GetTempPath(), "assistant-repo-md-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(markdown);
            File.WriteAllBytes(Path.Combine(dir, "smartpark-knowledge.md"), bytes);
            File.WriteAllText(Path.Combine(dir, "publication.json"), JsonSerializer.Serialize(new {
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)), version, approvedBy = "synthetic-test-only",
                role = "admin", publishedAt = DateTimeOffset.UtcNow.AddDays(-1), effectiveTo = (DateTimeOffset?)null }));
            var knowledge = await new PublicKnowledgeFile(dir, TimeProvider.System).ReadAsync(default);
            Assert.NotNull(knowledge);
            foreach (var id in new[] { "smartpark-overview", "vehicle-guide", "discovery-guide", "booking-guide", "payment-guide", "support-guide", "assistant-limits" })
                Assert.Single(knowledge.Articles, a => a.Id == id);
            Assert.All(knowledge.Articles, a => {
                Assert.Equal("vi", a.SourceLocale);
                Assert.DoesNotContain("Đối chiếu", a.Vi);
                Assert.DoesNotContain("Baseline:", a.Vi);
                Assert.DoesNotContain("Từ khóa:", a.Vi);
            });
            await MeasureKnowledgeReads(dir);
            foreach (var (question, expected) in new (string, string)[] {
                ("SmartPark có loại xe nào?", "vehicle-guide"),
                ("SmartPark hỗ trợ loại xe nào?", "vehicle-guide"),
                ("smartpark hỗ trợ vehicle gì", "vehicle-guide"),
                ("smartpark support car", "vehicle-guide"),
                ("smartpark support bike", "vehicle-guide"),
                ("SmartPark support ticket", "support-guide"),
                ("smartpark hỗ trợ xe nào", "vehicle-guide"),
                ("SmartPark phục vụ xe gì?", "vehicle-guide"),
                ("Cho tôi hỏi bên bạn phục vụ ô tô hay xe máy?", "vehicle-guide"),
                ("Could you explain the vehicle types supported here?", "vehicle-guide"),
                ("Tôi muốn tìm hiểu về gói tháng cho người lái", "monthly-yearly-guide"),
                ("Tôi muốn báo mất vé", "parking-rules-guide"),
                ("SmartPark có dự báo thời tiết không?", "unknown"),
                ("Do you sell a car or motorcycle?", "unknown"),
                ("Vehicle types and weather forecast", "unknown"),
                ("SmartPark hỗ trợ sửa xe không?", "unknown"),
                ("SmartPark hỗ trợ xe của tôi?", "private"),
                ("SmartPark hỗ trợ xe tôi không?", "private"),
                ("Who founded SmartPark and what vehicle types do you support?", "unknown"),
                ("Loại xe của tôi", "private"),
                ("Tìm bãi nào còn chỗ", "dynamic"),
                ("Loại xe và API key", "sensitive")
            })
                Assert.Equal(expected, GuestIntents.Classify(question, knowledge.Articles));

            var evaluation = new (string Question, string Expected)[]
            {
                ("SmartPark phuc vu loai xe nao?", "vehicle-guide"),
                ("Tôi muốn tìm hiểu về gói tháng cho người lái", "monthly-yearly-guide"),
                ("vehcile types", "vehicle-guide"),
                ("lost tickte", "parking-rules-guide"),
                ("goi thnag", "monthly-yearly-guide"),
                ("đặt trướcc", "booking-guide"),
                ("reserve a spot", "booking-guide"),
                ("prebook parking", "booking-guide"),
                ("nộp phí như thế nào", "payment-guide"),
                ("gửi xe muộn thì sao", "parking-time-clarification"),
                ("tell me more", "unknown"),
                ("vehcile types and weather forecast", "unknown"),
                ("Do you sell vehcile insurance?", "unknown"),
                ("lost tickte and flight refunds", "unknown"),
                ("vehicle tyres", "unknown"),
                ("lost wicket", "unknown"),
                ("availble spaces", "unknown"),
                ("manage my vehcles", "unknown"),
                ("vehcile types in my account", "private"),
                ("vehcile types near me", "dynamic"),
                ("vehcile types token", "sensitive")
            };
            var mismatches = evaluation.Select(test =>
                (test.Question, test.Expected, Actual: GuestIntents.Classify(test.Question, knowledge.Articles)))
                .Where(result => result.Actual != result.Expected)
                .Select(result => $"{result.Question}: expected {result.Expected}, got {result.Actual}").ToArray();
            Assert.True(mismatches.Length == 0, string.Join(Environment.NewLine, mismatches));

            var paraphrases = new (string Question, string Expected)[] {
                ("Không đặt trước có gửi xe được không?", "walk-in-and-entry-guide"),
                ("khong dat truoc co gui xe duoc khong", "walk-in-and-entry-guide"),
                ("Cứ chạy thẳng tới bãi gửi xe được không?", "walk-in-and-entry-guide"),
                ("Can I park without a reservation?", "walk-in-and-entry-guide"),
                ("Có phải book trước mới được vào bãi không?", "walk-in-and-entry-guide"),
                ("Tới trễ thì sao?", "arrival-and-no-show-guide|late-arrival"),
                ("Đến bãi trễ thì sao?", "arrival-and-no-show-guide|late-arrival"),
                ("Lỡ tới bãi sau giờ hẹn thì sao?", "arrival-and-no-show-guide|late-arrival"),
                ("What happens if I arrive late?", "arrival-and-no-show-guide|late-arrival"),
                ("toi tre thi sao", "arrival-and-no-show-guide|late-arrival"),
                ("Lấy xe muộn thì tính phí thế nào?", "payment-guide|parking-overstay"),
                ("Nếu đỗ lâu hơn giờ đã đặt thì sao?", "payment-guide|parking-overstay"),
                ("Lỡ để xe qua giờ hẹn có bị phạt không?", "payment-guide|parking-overstay"),
                ("What happens if I stay past my reservation?", "payment-guide|parking-overstay"),
                ("Đỗ xe quá giờ thì sao?", "payment-guide|parking-overstay"),
                ("Hoàn tiền gói tháng", "pass-cancellation-guide"),
                ("huy goi thang", "pass-cancellation-guide"),
                ("Còn nửa kỳ mà thôi dùng vé tháng thì có được trả lại tiền không?", "pass-cancellation-guide"),
                ("Can I get a refund if I cancel a monthly pass?", "pass-cancellation-guide"),
                ("Bãi đóng cửa khi còn gói", "pass-cancellation-guide"),
                ("Hủy đặt chỗ thì có được hoàn tiền không?", "cancellation-refund-guide"),
                ("Hủy trước 30 phút có được hoàn tiền không?", "cancellation-refund-guide"),
                ("What is a no-show?", "cancellation-refund-guide"),
                ("Bỏ chỗ đã đặt thì có lấy lại tiền không?", "cancellation-refund-guide"),
                ("Cách đặt chỗ?", "booking-guide"),
                ("Đặt chỗ trước tối đa mấy ngày?", "booking-guide"),
                ("Đặt chỗ trước tối đa bao nhiêu ngày?", "booking-guide"),
                ("Đặt chỗ tối thiểu 15 phút đúng không?", "booking-guide"),
                ("Toi muon giu mot cho gui xe thi lam sao", "booking-guide"),
                ("Cổng không mở", "checkout-and-departure-guide"),
                ("Đã trả tiền mà thanh chắn không nhấc thì làm gì?", "checkout-and-departure-guide"),
                ("What if the exit gate doesn't open?", "checkout-and-departure-guide"),
                ("Không nhận được thông báo phải làm sao?", "notification-guide"),
                ("Điện thoại im re, làm sao biết hệ thống đã nhận đơn?", "notification-guide"),
                ("Bãi gần đây còn chỗ không?", "dynamic"),
                ("Cho xem booking của tôi", "private"),
                ("SmartPark có dự báo thời tiết không?", "unknown"),
                ("Gửi xe muộn thì sao?", "parking-time-clarification")
            };
            var store = new GuestFaqTests.Store();
            var answers = new GuestAnswerFactory();
            var corpus = new GuestFaqTests.Corpus { Value = knowledge };
            var generator = new ManagedDemoGenerator(store, new GuestFaqTests.Protector(),
                new GuestFaqTests.Answers(), TimeProvider.System, NullLogger<ManagedDemoGenerator>.Instance);
            var handler = new GuestHandlers(store, corpus, generator, answers, new GuestOptions(true));
            foreach (var (question, expected) in paraphrases)
            {
                var reply = await handler.Handle(new GuestMessage(Guid.NewGuid(), question, "auto"), default);
                var intent = GuestIntents.Classify(question, knowledge.Articles);
                var source = reply.Sources.SingleOrDefault()?.ArticleId;
                var accepted = expected.Split('|');
                Assert.True(source is null ? accepted.Contains(intent) : accepted.Contains(source),
                    $"{question}: expected {expected}, got {intent} ({source ?? "no source"})");
            }

            foreach (var (question, articleId) in new (string, string?)[]
            {
                ("Di thang toi bai do xe duoc khong?", "walk-in-and-entry-guide"),
                ("Nếu đến cổng sau giờ hẹn thì sao?", "arrival-and-no-show-guide"),
                ("Nếu gửi xe lâu hơn giờ kết thúc thì sao?", "payment-guide"),
                ("Do I get a refund if I cancel 45 minutes before the reservation starts?", "cancellation-refund-guide"),
                ("Thời lượng đặt chỗ tối đa 24 giờ phải không?", "booking-guide"),
                ("Tôi có thể đặt chỗ trước tối đa bao nhiêu ngày?", "booking-guide"),
                ("Nếu hủy đặt chỗ trước 24 giờ thì có được hoàn tiền không?", "cancellation-refund-guide"),
                ("Hủy chỗ trước nửa tiếng có lấy lại tiền không?", "cancellation-refund-guide"),
                ("How many days ahead can I book a parking spot?", "booking-guide"),
                ("Can I just show up at the car park without booking?", "walk-in-and-entry-guide"),
                ("Tôi vẫn có thể gửi xe trực tiếp không?", "walk-in-and-entry-guide"),
                ("Đã thanh toán mà thanh chắn không mở", "checkout-and-departure-guide"),
                ("Không thấy thông báo xác nhận giao dịch", "notification-guide"),
                ("Cổng không mở ở trường thì sao?", null),
                ("Tôi trẻ và muốn dự báo thời tiết", null),
                ("Can I park without a reservation and buy insurance?", null),
                ("Không nhận được thông báo tuyển dụng thì sao?", null),
                ("Tôi muốn hủy vé tháng xe buýt", null),
                ("Không đặt trước có gửi xe đạp được không?", null),
                ("Can I park without a reservation in a van?", null),
                ("Cứ chạy thẳng tới bãi gửi xe được không? Dự báo thời tiết?", null),
                ("Does parking without booking include car insurance?", null)
            })
            {
                var reply = await handler.Handle(new GuestMessage(Guid.NewGuid(), question, "auto"), default);
                var actual = reply.Sources.SingleOrDefault()?.ArticleId;
                Assert.True(actual == articleId, $"{question}: expected {articleId ?? "no source"}, got {actual ?? "no source"}");
                Assert.Equal(articleId is null ? "clarification" : "answered", reply.Outcome);
            }

            foreach (var (question, locale, articleId, outcome, expectedText) in new[] {
                ("What is SmartPark?", "en", "smartpark-overview", "answered", "available in Vietnamese only"),
                ("Các loại xe?", "vi", "vehicle-guide", "answered", "xe máy"),
                ("SmartPark support bike", "en", "vehicle-guide", "answered", "xe đạp"),
                ("Can I park a motorcycle?", "en", "vehicle-guide", "answered", "xe máy"),
                ("vehcile types", "en", "vehicle-guide", "answered", "available in Vietnamese only"),
                ("lost tickte", "en", "parking-rules-guide", "answered", "available in Vietnamese only"),
                ("goi thnag", "vi", "monthly-yearly-guide", "answered", "gói tháng"),
                ("reserve a spot", "en", "booking-guide", "answered", "available in Vietnamese only"),
                ("Gửi xe muộn thì sao?", "vi", "", "clarification", "đỗ xe quá giờ kết thúc"),
                ("Is parking available near me?", "en", "", "clarification", ""),
                ("Show my vehicles", "en", "", "handoff", "Sign in"),
                ("Xe của mình đâu?", "vi", "", "handoff", "đăng nhập"),
                ("Lịch đặt của tôi", "vi", "", "handoff", "đăng nhập"),
                ("My bookings tomorrow", "en", "", "handoff", "Sign in"),
                ("Does SmartPark sell car insurance?", "en", "", "clarification", ""),
                ("Can I park a motorcycle and buy insurance?", "en", "", "clarification", ""),
                ("SmartPark có dự báo thời tiết không?", "vi", "", "clarification", "")
            })
            {
                var reply = await handler.Handle(new GuestMessage(Guid.NewGuid(), question, "auto"), default);
                Assert.True(outcome == reply.Outcome, $"{question}: expected {outcome}, got {reply.Outcome}");
                Assert.True(locale == reply.ReplyLocale, $"{question}: expected {locale}, got {reply.ReplyLocale}");
                Assert.False(string.IsNullOrWhiteSpace(reply.Content));
                if (expectedText.Length > 0) Assert.Contains(expectedText, reply.Content);
                Assert.Empty(reply.Cards);
                if (articleId.Length > 0)
                {
                    Assert.Equal("knowledge", reply.Mode);
                    var source = Assert.Single(reply.Sources);
                    Assert.Equal(articleId, source.ArticleId);
                    Assert.Equal(knowledge.Version, source.Version);
                    Assert.Equal("vi", source.Locale);
                    Assert.Equal(knowledge.Articles.Single(article => article.Id == articleId).Vi, source.Excerpt);
                    Assert.Contains(source.Excerpt, reply.Content);
                }
                else
                {
                    Assert.Equal("system", reply.Mode);
                    Assert.Empty(reply.Sources);
                }
                if (outcome == "handoff") Assert.Equal("login", Assert.Single(reply.Actions).Type);
                else Assert.Empty(reply.Actions);
            }
        }
        finally { foreach (var file in Directory.GetFiles(dir)) File.Delete(file); Directory.Delete(dir); }
    }

    private async Task MeasureKnowledgeReads(string directory)
    {
        const int samples = 80;
        var reusable = new PublicKnowledgeFile(directory, TimeProvider.System);
        Assert.NotNull(await reusable.ReadAsync(default));
        var cold = new double[samples];
        var warm = new double[samples];
        for (var index = 0; index < samples; index++)
        {
            cold[index] = await Measure(new PublicKnowledgeFile(directory, TimeProvider.System));
            warm[index] = await Measure(reusable);
        }
        Array.Sort(cold);
        Array.Sort(warm);
        output.WriteLine(FormattableString.Invariant(
            $"knowledge_read n={samples} parse_every_read_p50_ms={cold[39]:F3} p95_ms={cold[75]:F3} reused_parse_p50_ms={warm[39]:F3} p95_ms={warm[75]:F3}"));

        static async Task<double> Measure(PublicKnowledgeFile corpus)
        {
            var started = Stopwatch.GetTimestamp();
            var result = await corpus.ReadAsync(default);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Assert.NotNull(result);
            return elapsed;
        }
    }
    private sealed class Http : HttpMessageHandler
    {
        public string Body = ""; public string Provider = ""; public string? Key;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            Key = request.Headers.TryGetValues("x-goog-api-key", out var keys) ? keys.Single() : null;
            var answer = JsonSerializer.Serialize(new { reply = "Synthetic answer.", sourceIds = new[] { "public-topic" } });
            object envelope = Provider == "openai"
                ? new { status = "completed", output = new[] { new { type = "message", role = "assistant", content = new[] { new { type = "output_text", text = answer } } } } }
                : new { candidates = new[] { new { finishReason = "STOP", content = new { role = "model", parts = new[] { new { text = answer } } } } } };
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(envelope), Encoding.UTF8, "application/json") };
        }
    }
}
