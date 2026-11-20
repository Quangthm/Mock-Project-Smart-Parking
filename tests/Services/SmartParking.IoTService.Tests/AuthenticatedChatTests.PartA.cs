using SmartParking.IoTService.Application.Assistant.Guest;

namespace SmartParking.IoTService.Tests;

public sealed partial class AuthenticatedChatTests
{
    [Theory]
    [InlineData("driver", "unavailable", "booking-guide", "How do I book on SmartPark step by step?", false)]
    [InlineData("admin", "unavailable", "booking-guide", "How do I book on SmartPark step by step?", false)]
    [InlineData("admin", null, "vehicle-guide", "Hãy trả lời bằng tiếng Anh: SmartPark hỗ trợ những loại xe nào?", false)]
    [InlineData("driver", null, "vehicle-guide", "Which vehicle types does SmartPark support?", true)]
    public async Task AuthenticatedEnglishReplyRetainsPublishedCitation(string role, string? failure, string articleId, string message, bool legacy)
    {
        using var transport = new PartAProviderTransport(failure);
        await using var f = await Fixture.Create(measure: true, providerTransport: transport);
        f.Corpus.Value = PartAFaqTests.Corpus();
        if (legacy)
            f.Corpus.Value = f.Corpus.Value with { Articles = f.Corpus.Value.Articles.Select(a => a.Id == articleId
                ? a with { TitleEn = a.TitleVi, En = a.Vi, SourceLocale = "vi" } : a).ToArray() };
        f.EnableProvider();
        var token = f.Token(role: role);
        var id = await f.Conversation(token: token);
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message }, token));
        var answer = result.GetProperty("answer");
        var deterministic = articleId == "vehicle-guide" && !legacy;
        Assert.Equal(deterministic || failure is not null ? "knowledge" : "ai", answer.GetProperty("mode").GetString());
        var source = Assert.Single(answer.GetProperty("sources").EnumerateArray());
        Assert.Equal(articleId, source.GetProperty("articleId").GetString());
        Assert.Equal(legacy ? "vi" : "en", source.GetProperty("locale").GetString());
        var published = f.Corpus.Value.Articles.Single(a => a.Id == articleId).En;
        Assert.Equal(published, source.GetProperty("excerpt").GetString());
        if (failure is not null || deterministic) Assert.Equal(published, answer.GetProperty("content").GetString());
        if (deterministic) Assert.Empty(transport.Inputs);
        if (legacy)
        {
            var sent = Assert.Single(transport.Inputs);
            Assert.Equal("en", sent.GetProperty("answerLanguage").GetString());
            Assert.Equal("vi", sent.GetProperty("sourceLanguage").GetString());
        }
    }

    [Theory]
    [InlineData("Hãy trả lời bằng tiếng Anh: SmartPark hỗ trợ những loại xe nào?", true, false)]
    [InlineData("Please answer in Vietnamese: Which vehicle types does SmartPark support?", false, false)]
    [InlineData("Please answer in Vietnamese: Which vehicle types does SmartPark support?", false, true)]
    public async Task PartAAuthenticatedFaqPassesLanguageToRealAnswerFactory(string message, bool english, bool legacy)
    {
        using var transport = new PartAProviderTransport();
        await using var f = await Fixture.Create(measure: true, providerTransport: transport);
        f.Corpus.Value = PartAFaqTests.Corpus();
        if (legacy)
            f.Corpus.Value = f.Corpus.Value with { Articles = f.Corpus.Value.Articles.Select(a => a.Id == "vehicle-guide"
                ? a with { TitleEn = a.TitleVi, En = a.Vi, SourceLocale = "vi" } : a).ToArray() };
        f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message }));
        var answer = result.GetProperty("answer");
        Assert.Equal("knowledge", answer.GetProperty("mode").GetString());
        Assert.Equal("vehicle-guide", answer.GetProperty("sources")[0].GetProperty("articleId").GetString());
        var content = answer.GetProperty("content").GetString()!;
        Assert.DoesNotContain("Vietnamese only", content);
        Assert.Contains(english ? "SmartPark distinguishes cars and motorcycles" : "Thiết kế SmartPark", content);
        Assert.Equal(english ? "en" : "vi", answer.GetProperty("sources")[0].GetProperty("locale").GetString());
        Assert.Empty(transport.Inputs);
        var article = f.Corpus.Value.Articles.Single(a => a.Id == "vehicle-guide");
        Assert.Equal(english ? article.En : article.Vi, content);
    }

    [Theory]
    [InlineData("driver", "Which vehicle types does SmartPark support?", "vehicle-guide")]
    [InlineData("admin", "Does SmartPark support cars and motorcycles?", "vehicle-guide")]
    [InlineData("owner", "Hãy trả lời bằng tiếng Anh: SmartPark hỗ trợ những loại xe nào?", "vehicle-guide")]
    [InlineData("driver", "Hướng dẫn tôi các bước đặt chỗ trên SmartPark.", "booking-guide")]
    [InlineData("driver", "đặt chỗ sao", "booking-guide")]
    [InlineData("owner", "đặt chỗ sao", "booking-guide")]
    [InlineData("admin", "Please show me the steps to book a parking spot on SmartPark.", "booking-guide")]
    public async Task PartAQaPromptsReachCanonicalPublicSourceAcrossRoles(string role, string question, string source)
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner);
        f.Corpus.Value = PartAFaqTests.Corpus(); f.EnableProvider();
        var credential = f.Token(role: role);
        var id = await f.Conversation(token: credential);
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = question }, credential));
        var answer = result.GetProperty("answer");
        Assert.Equal("answered", answer.GetProperty("outcome").GetString());
        Assert.Equal(source, answer.GetProperty("sources")[0].GetProperty("articleId").GetString());
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Equal(0, owner.Calls);
        Assert.Equal(source == "vehicle-guide" ? 0 : 1, f.Answers.Generations);
        Assert.Contains(source == "booking-guide" ? "từng bước có đánh số" : "SmartPark hỗ trợ", Assert.Single(f.Answers.Inputs));
    }

    [Theory]
    [InlineData("driver", "Tui hủy chỗ đã đặt, lấy lại tiền được hông?", "cancellation-refund-guide")]
    [InlineData("admin", "Quy trình Admin xét duyệt hồ sơ bãi đỗ thế nào?", "roles-and-partner-guide")]
    [InlineData("owner", "Tôi muốn đăng ký làm chủ bãi.", "roles-and-partner-guide")]
    public async Task RoleWorkflowFaqUsesPublishedGuidanceWithoutReadingBusinessRecords(string role, string question, string article)
    {
        var owner = new ReadOwner();
        await using var fixture = await Fixture.Create(owner: owner);
        fixture.Corpus.Value = PartAFaqTests.Corpus();
        fixture.EnableProvider();
        var token = fixture.Token(role: role);
        var conversation = await fixture.Conversation(token: token);

        var data = await fixture.Data(await fixture.Post($"/api/assistant/conversations/{conversation}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = question }, token));

        var answer = data.GetProperty("answer");
        Assert.Equal("answered", answer.GetProperty("outcome").GetString());
        var source = Assert.Single(answer.GetProperty("sources").EnumerateArray());
        Assert.Equal(article, source.GetProperty("articleId").GetString());
        Assert.Equal("0.4.1", source.GetProperty("version").GetString());
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Equal(0, owner.Calls);
    }

    [Theory]
    [InlineData("Tôi có thể xem những xe đã đăng ký của mình ở đâu?")]
    [InlineData("Where can I view my registered vehicles?")]
    public async Task PartAVehicleNavigationNeverFallsIntoRolesGuideOrReadsVehicles(string question)
    {
        var owner = new ReadOwner(); await using var f = await Fixture.Create(owner: owner);
        f.Corpus.Value = PartAFaqTests.Corpus(); f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = question }));
        var answer = result.GetProperty("answer");
        Assert.Contains("My Vehicles", answer.GetProperty("content").GetString());
        Assert.Empty(answer.GetProperty("sources").EnumerateArray());
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Equal(0, owner.Calls); Assert.Equal(0, f.Answers.Generations);
    }

    [Fact]
    public async Task PartALanguageInstructionPreservesScopedVehicleRead()
    {
        var owner = new ReadOwner(); await using var f = await Fixture.Create(owner: owner);
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0,
                message = "Hãy trả lời bằng tiếng Anh: Xe của tôi" }));
        var answer = result.GetProperty("answer");
        Assert.Equal("Vehicles authorized by your account service.", answer.GetProperty("content").GetString());
        Assert.Equal(owner.Vehicle, answer.GetProperty("cards")[0].GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(new[] { f.Account }, owner.Accounts);
        Assert.Equal(0, f.Answers.Generations);
    }
}
