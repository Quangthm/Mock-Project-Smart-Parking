using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Guest;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

namespace SmartParking.IoTService.Tests;

public sealed class PartAFaqTests
{
    [Theory]
    [InlineData("gemini", "Hãy trả lời bằng tiếng Anh: SmartPark hỗ trợ những loại xe nào?", "en", "vehicle-guide")]
    [InlineData("openai", "Hãy trả lời bằng tiếng Anh: SmartPark hỗ trợ những loại xe nào?", "en", "vehicle-guide")]
    [InlineData("gemini", "Hướng dẫn tôi các bước đặt chỗ trên SmartPark.", "vi", "booking-guide")]
    [InlineData("openai", "Hướng dẫn tôi các bước đặt chỗ trên SmartPark.", "vi", "booking-guide")]
    public async Task RealProviderAdapterReceivesResolvedLanguageAndCanonicalQuestion(string provider, string question, string locale, string source)
    {
        var f = new GuestFaqTests.Fixture(); f.Corpus.Value = Corpus(); f.EnableProvider(provider);
        if (source == "vehicle-guide")
            f.Corpus.Value = f.Corpus.Value with { Articles = f.Corpus.Value.Articles.Select(a => a.Id == source
                ? a with { TitleEn = a.TitleVi, En = a.Vi, SourceLocale = "vi" } : a).ToArray() };
        using var transport = new PartAProviderTransport();
        var factory = new GuestAnswerFactory(transport);
        var generator = new ManagedDemoGenerator(f.Store, new GuestFaqTests.Protector(), f.Answers,
            TimeProvider.System, NullLogger<ManagedDemoGenerator>.Instance);
        var handler = new GuestHandlers(f.Store, f.Corpus, generator, factory, new(true));
        // An explicit instruction also overrides a conflicting client locale.
        var reply = await handler.Handle(new GuestMessage(Guid.NewGuid(), question, locale == "en" ? "vi" : "auto"), default);
        var sent = Assert.Single(transport.Inputs);
        Assert.Equal(locale, sent.GetProperty("answerLanguage").GetString());
        Assert.Equal(source == "vehicle-guide" ? "vi" : locale, sent.GetProperty("sourceLanguage").GetString());
        Assert.Equal(source, sent.GetProperty("sources")[0].GetProperty("id").GetString());
        Assert.DoesNotContain(question, sent.GetProperty("question").GetString());
        if (source == "booking-guide") Assert.Contains("từng bước có đánh số", sent.GetProperty("question").GetString());
        Assert.Equal("ai", reply.Mode);
        Assert.Equal("answered", reply.Outcome);
        Assert.Equal(source, Assert.Single(reply.Sources).ArticleId);
        Assert.Contains(locale == "en" ? "Synthetic English" : "tiếng Việt giả lập", reply.Content);
    }

    [Theory]
    [InlineData("gemini", "Which vehicle types does SmartPark support?", "en", false)]
    [InlineData("openai", "Which vehicle types does SmartPark support?", "en", false)]
    [InlineData("gemini", "SmartPark hỗ trợ những loại xe nào?", "vi", false)]
    [InlineData("openai", "SmartPark hỗ trợ những loại xe nào?", "vi", false)]
    [InlineData("gemini", "SmartPark hỗ trợ những loại xe nào?", "vi", true)]
    [InlineData("openai", "SmartPark hỗ trợ những loại xe nào?", "vi", true)]
    public async Task VehicleSupportUsesExactPublishedContentWhenTranslationIsAvailable(string provider, string question, string locale, bool legacy)
    {
        var f = new GuestFaqTests.Fixture(); f.Corpus.Value = Corpus(); f.EnableProvider(provider);
        if (legacy)
            f.Corpus.Value = f.Corpus.Value with { Articles = f.Corpus.Value.Articles.Select(a => a.Id == "vehicle-guide"
                ? a with { TitleEn = a.TitleVi, En = a.Vi, SourceLocale = "vi" } : a).ToArray() };
        using var transport = new PartAProviderTransport();
        var generator = new ManagedDemoGenerator(f.Store, new GuestFaqTests.Protector(), f.Answers,
            TimeProvider.System, NullLogger<ManagedDemoGenerator>.Instance);
        var handler = new GuestHandlers(f.Store, f.Corpus, generator, new GuestAnswerFactory(transport), new(true));
        var reply = await handler.Handle(new GuestMessage(Guid.NewGuid(), question, "auto"), default);
        var article = f.Corpus.Value.Articles.Single(a => a.Id == "vehicle-guide");
        Assert.Empty(transport.Inputs);
        Assert.Equal("knowledge", reply.Mode);
        Assert.Equal(locale == "en" ? article.En : article.Vi, reply.Content);
        var citation = Assert.Single(reply.Sources);
        Assert.Equal(locale, citation.Locale);
        Assert.Equal(f.Corpus.Value.Version, citation.Version);
        Assert.Equal(reply.Content, citation.Excerpt);
    }

    internal static PublicKnowledge Corpus()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        // Synthetic publication in a disposable directory exercises the production loader.
        // This never writes to the deployment corpus or approves the editorial source.
        var content = File.ReadAllText(Path.Combine(root.FullName, "docs/03-design/ai/p1/knowledge/smartpark-knowledge.md"))
            .Replace("publication_state: draft", "publication_state: published")
            .Replace("review_status: pending", "review_status: approved");
        var directory = Directory.CreateTempSubdirectory("part-a-corpus-");
        var document = Path.Combine(directory.FullName, "smartpark-knowledge.md");
        var manifest = Path.Combine(directory.FullName, "publication.json");
        try
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            File.WriteAllBytes(document, bytes);
            File.WriteAllText(manifest, JsonSerializer.Serialize(new {
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)), version = "0.4.1",
                approvedBy = "synthetic-test-only", role = "admin", publishedAt = DateTimeOffset.UtcNow.AddDays(-1)
            }));
            return new PublicKnowledgeFile(directory.FullName, TimeProvider.System).ReadAsync(default).GetAwaiter().GetResult()!;
        }
        finally { File.Delete(document); File.Delete(manifest); directory.Delete(); }
    }

    [Theory]
    [InlineData("Which vehicle types does SmartPark support?", "vehicle-guide", "en")]
    [InlineData("Does SmartPark support cars and motorcycles?", "vehicle-guide", "en")]
    [InlineData("What types of vehicles can SmartPark accept?", "vehicle-guide", "en")]
    [InlineData("Which vehicles are supported by SmartPark?", "vehicle-guide", "en")]
    [InlineData("What type of vehicles does SmartPark support?", "vehicle-guide", "en")]
    [InlineData("What kinds of vehicles does SmartPark support?", "vehicle-guide", "en")]
    [InlineData("Can cars and motorcycles park at SmartPark?", "vehicle-guide", "en")]
    [InlineData("Hãy trả lời bằng tiếng Anh: SmartPark hỗ trợ những loại xe nào?", "vehicle-guide", "en")]
    [InlineData("Please answer in Vietnamese: Which vehicle types does SmartPark support?", "vehicle-guide", "vi")]
    [InlineData("SmartPark hỗ trợ những loại xe nào? Please answer in English.", "vehicle-guide", "en")]
    [InlineData("SmartPark hỗ trợ những loại xe nào? Answer in English please.", "vehicle-guide", "en")]
    [InlineData("Does SmartPark support motorcycles? Answer in Vietnamese, please.", "vehicle-guide", "vi")]
    [InlineData("SmartPark hỗ trợ những loại xe nào, trả lời tiếng Anh nhé", "vehicle-guide", "en")]
    [InlineData("SmartPark hỗ trợ những loại xe nào? Answer me in English.", "vehicle-guide", "en")]
    [InlineData("Hướng dẫn tôi các bước đặt chỗ trên SmartPark.", "booking-guide", "vi")]
    [InlineData("Hãy hướng dẫn từng bước đặt chỗ qua SmartPark", "booking-guide", "vi")]
    [InlineData("Please show me the steps to book a parking spot on SmartPark.", "booking-guide", "en")]
    [InlineData("How can I reserve a parking space with SmartPark step by step?", "booking-guide", "en")]
    [InlineData("How do I book on SmartPark step by step?", "booking-guide", "en")]
    [InlineData("What are the steps to book a parking spot?", "booking-guide", "en")]
    [InlineData("What are the steps to book parking on SmartPark?", "booking-guide", "en")]
    [InlineData("Please guide me through the booking process on SmartPark.", "booking-guide", "en")]
    [InlineData("How do I reserve parking with SmartPark?", "booking-guide", "en")]
    [InlineData("Chỉ tôi từng bước đặt chỗ trên SmartPark", "booking-guide", "vi")]
    [InlineData("Cách đặt chỗ?", "booking-guide", "vi")]
    [InlineData("How do I book?", "booking-guide", "en")]
    [InlineData("How do I book parking on SmartPark? Please answer in English?", "booking-guide", "en")]
    public async Task QaQuestionsChooseTheRightSourceAndLanguage(string question, string articleId, string locale)
    {
        var f = new GuestFaqTests.Fixture();
        f.Corpus.Value = Corpus();
        f.EnableProvider("gemini");
        var reply = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), question, "auto"), default);
        Assert.Equal("answered", reply.Outcome);
        Assert.Equal(articleId, Assert.Single(reply.Sources).ArticleId);
        Assert.Equal(locale, reply.ReplyLocale);
        Assert.Equal(articleId == "vehicle-guide" ? 0 : 1, f.Answers.Generations);
        if (articleId == "vehicle-guide")
            Assert.Equal(reply.Sources[0].Excerpt, reply.Content);
        else
        {
            Assert.DoesNotContain(question, f.Answers.LastQuestion);
            Assert.Contains("từng bước có đánh số", f.Answers.LastQuestion);
        }
    }

    [Theory]
    [InlineData("Hãy trả lời bằng tiếng Anh: Xe của tôi", "en", "Xe của tôi")]
    [InlineData("Answer in Vietnamese: What is SmartPark?", "vi", "What is SmartPark?")]
    [InlineData("What is SmartPark? Trả lời bằng tiếng Việt.", "vi", "What is SmartPark")]
    [InlineData("What is SmartPark and reply in Vietnamese", "vi", "What is SmartPark")]
    public void ExplicitLanguageOverridesHeuristicsAndIsRemovedFromTheQuestion(string message, string language, string question)
    {
        Assert.Equal(language, AnswerLanguage.Detect(message));
        Assert.Equal(question, AnswerLanguage.Question(message));
    }

    [Theory]
    [InlineData("Do not answer in English: SmartPark là gì?")]
    [InlineData("What does 'answer in English' mean?")]
    public void MentionsAndNegationsAreNotLanguageInstructions(string message)
    {
        Assert.Null(AnswerLanguage.Requested(message));
        Assert.Equal(message, AnswerLanguage.Question(message));
    }

    [Fact]
    public void DecomposedUnicodeInstructionsBehaveLikeComposedVietnamese()
    {
        const string message = "Hãy trả lời bằng tiếng Anh: SmartPark hỗ trợ những loại xe nào?";
        var decomposed = message.Normalize(NormalizationForm.FormD);
        Assert.Equal("en", AnswerLanguage.Requested(decomposed));
        Assert.Equal(AnswerLanguage.Question(message), AnswerLanguage.Question(decomposed));
        Assert.Equal("vehicle-support", GuestIntents.Classify(decomposed, Corpus().Articles));
    }

    [Theory]
    [InlineData("Tôi có thể xem những xe đã đăng ký của mình ở đâu?")]
    [InlineData("Where can I view my registered vehicles?")]
    [InlineData("Where do I see my vehicles?")]
    [InlineData("Where can I view my registered vehicles, please?")]
    [InlineData("Where can I see the vehicles I have registered?")]
    [InlineData("Xe đã đăng ký của tôi xem ở đâu?")]
    [InlineData("Xem danh sách xe đã đăng ký ở đâu?")]
    public async Task VehicleNavigationDoesNotReadPrivateDataOrDispatchToProvider(string message)
    {
        var f = new GuestFaqTests.Fixture();
        f.Corpus.Value = Corpus(); f.EnableProvider("gemini");
        var reply = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), message, "auto"), default);
        Assert.Equal("system", reply.Mode);
        Assert.Equal("answered", reply.Outcome);
        Assert.Contains("My Vehicles", reply.Content);
        Assert.Empty(reply.Sources);
        Assert.Empty(reply.Cards);
        Assert.Equal(0, f.Answers.Calls);
    }

    [Theory]
    [InlineData("Hãy trả lời bằng tiếng Anh: Hướng dẫn tôi đặt chỗ với OTP 123456")]
    [InlineData("Which vehicle types does SmartPark support? My token is abc")]
    [InlineData("Does SmartPark support cars and motorcycles for my account?")]
    [InlineData("Hướng dẫn tôi các bước đặt chỗ cho xe của người khác")]
    [InlineData("Where can I see my vehicles and someone else's vehicles?")]
    [InlineData("What are the steps to book parking with my token abc?")]
    public async Task ExtraPrivateOrSensitiveQualificationsNeverReachProvider(string message)
    {
        var f = new GuestFaqTests.Fixture();
        f.Corpus.Value = Corpus(); f.EnableProvider("gemini");
        var reply = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), message, "auto"), default);
        Assert.NotEqual("answered", reply.Outcome);
        Assert.Empty(reply.Sources);
        Assert.Equal(0, f.Answers.Calls);
    }

    [Fact]
    public async Task NoModelBookingFallbackNumbersPublishedStepsAndPreservesAllConditions()
    {
        var f = new GuestFaqTests.Fixture(); f.Corpus.Value = Corpus();
        var factory = new GuestAnswerFactory();
        var generator = new ManagedDemoGenerator(f.Store, new GuestFaqTests.Protector(), f.Answers,
            TimeProvider.System, NullLogger<ManagedDemoGenerator>.Instance);
        var handler = new GuestHandlers(f.Store, f.Corpus, generator, factory, new(true));
        var reply = await handler.Handle(new GuestMessage(Guid.NewGuid(), "Hướng dẫn tôi các bước đặt chỗ trên SmartPark.", "auto"), default);
        Assert.Equal("knowledge", reply.Mode);
        Assert.Equal("booking-guide", Assert.Single(reply.Sources).ArticleId);
        Assert.Contains("1. đăng nhập.", reply.Content);
        Assert.Contains("2. chọn loại xe cần gửi.", reply.Content);
        Assert.Contains("7. xác nhận trên form.", reply.Content);
        var article = f.Corpus.Value!.Articles.Single(a => a.Id == "booking-guide");
        Assert.EndsWith(article.Vi[article.Vi.IndexOf("\n\n", StringComparison.Ordinal)..], reply.Content);
        Assert.Equal(article.Vi, reply.Sources[0].Excerpt);
        Assert.Empty(reply.Actions);
    }

    [Theory]
    [InlineData("Does SmartPark support car insurance?")]
    [InlineData("How do I book a flight on SmartPark?")]
    [InlineData("Where can I see someone else's registered vehicles?")]
    public async Task BroaderGrammarDoesNotTurnUnrelatedRequestsIntoSuccessfulFaq(string message)
    {
        var f = new GuestFaqTests.Fixture(); f.Corpus.Value = Corpus(); f.EnableProvider("gemini");
        var reply = await f.Handler.Handle(new GuestMessage(Guid.NewGuid(), message, "auto"), default);
        Assert.NotEqual("answered", reply.Outcome);
        Assert.Empty(reply.Sources);
        Assert.Equal(0, f.Answers.Generations);
    }
}
