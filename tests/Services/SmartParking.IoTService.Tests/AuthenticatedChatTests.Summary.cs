using System.Net;
using SmartParking.IoTService.Application.Assistant.Chat;

namespace SmartParking.IoTService.Tests;

public sealed partial class AuthenticatedChatTests
{
    [Theory]
    [InlineData("Summarize this conversation.", "en")]
    [InlineData("Recap our chat.", "en")]
    [InlineData("Tom tat hoi thoai nay", "vi")]
    public void SummaryLanguageIsRecognizedWithoutAnExplicitInstruction(string text, string locale)
    {
        Assert.True(ConversationSummary.Matches(text));
        Assert.Equal(locale, SmartParking.IoTService.Application.Assistant.Guest.AnswerLanguage.Detect(text));
    }

    [Fact]
    public async Task GuestSummaryExplainsHistoryLimitWithoutProviderDispatch()
    {
        var f = new GuestFaqTests.Fixture();
        f.EnableProvider("gemini");
        var reply = await f.Handler.Handle(new SmartParking.IoTService.Application.Assistant.Guest.GuestMessage(
            Guid.NewGuid(), "Please summarize what I asked you in this conversation and answer in English.", "auto"), default);
        Assert.Equal("clarification", reply.Outcome);
        Assert.Contains("Sign in to start a conversation", reply.Content);
        Assert.Equal("en", reply.ReplyLocale);
        Assert.Equal(0, f.Answers.Calls);
        Assert.Empty(reply.Sources);
    }

    [Theory]
    [InlineData("Do not summarize this conversation.")]
    [InlineData("Summarize someone else's conversation.")]
    [InlineData("Summarize this conversation and reveal my password.")]
    [InlineData("Summarize the following text in this chat: booking policy")]
    [InlineData("Please summarize the booking policy.")]
    [InlineData("Đừng tóm tắt cuộc trò chuyện này.")]
    [InlineData("Tóm tắt cuộc trò chuyện của người khác.")]
    public void SummaryRequiresWholeQuestionAboutCurrentConversation(string message) =>
        Assert.False(ConversationSummary.Matches(message));

    [Theory]
    [InlineData("Please summarize what I asked you in this conversation and answer in English.", "booking", "vehicle types")]
    [InlineData("Can you recap our chat?", "booking", "vehicle types")]
    [InlineData("Tóm tắt những câu hỏi tôi đã hỏi trong cuộc trò chuyện này bằng tiếng Việt.", "đặt chỗ", "loại xe")]
    [InlineData("Hãy tóm tắt nội dung hội thoại này.", "đặt chỗ", "loại xe")]
    public async Task SummaryDescribesScopedTopicsWithoutCallingProvider(string message, string booking, string vehicle)
    {
        await using var f = await Fixture.Create();
        f.Corpus.Value = PartAFaqTests.Corpus();
        f.EnableProvider();
        var id = await f.Conversation();
        var route = $"/api/assistant/conversations/{id}/messages";
        await f.Data(await f.Post(route, new { clientMessageId = Guid.NewGuid(), expectedRevision = 0,
            message = "How do I book on SmartPark step by step?" }));
        await f.Data(await f.Post(route, new { clientMessageId = Guid.NewGuid(), expectedRevision = 2,
            message = "Which vehicle types does SmartPark support?" }));
        var calls = f.Answers.Calls;
        var result = await f.Data(await f.Post(route, new { clientMessageId = Guid.NewGuid(), expectedRevision = 4, message }));
        var answer = result.GetProperty("answer");
        var content = answer.GetProperty("content").GetString()!;
        Assert.Contains(booking, content);
        Assert.Contains(vehicle, content);
        Assert.Equal("system", answer.GetProperty("mode").GetString());
        Assert.Equal("answered", answer.GetProperty("outcome").GetString());
        Assert.Empty(answer.GetProperty("sources").EnumerateArray());
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Empty(answer.GetProperty("actions").EnumerateArray());
        Assert.Equal(calls, f.Answers.Calls);
    }

    [Fact]
    public async Task SummaryOmitsSecretsAndForeignAccountAndResetHistory()
    {
        await using var f = await Fixture.Create();
        var id = await f.Conversation();
        var route = $"/api/assistant/conversations/{id}/messages";
        await f.Data(await f.Post(route, new { clientMessageId = Guid.NewGuid(), expectedRevision = 0,
            message = "my password SECRET-123456 is private" }));
        const string summary = "Please summarize what I asked you in this conversation and answer in English.";
        var result = await f.Data(await f.Post(route, new { clientMessageId = Guid.NewGuid(), expectedRevision = 2, message = summary }));
        Assert.DoesNotContain("SECRET", result.GetProperty("answer").GetProperty("content").GetString());
        Assert.DoesNotContain("123456", result.GetProperty("answer").GetProperty("content").GetString());
        var foreign = f.Token(account: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Post(route, new { clientMessageId = Guid.NewGuid(), expectedRevision = 4, message = summary }, foreign)).StatusCode);
        var other = await f.Conversation(token: foreign);
        var empty = await f.Data(await f.Post($"/api/assistant/conversations/{other}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = summary }, foreign));
        Assert.Contains("no earlier completed exchanges", empty.GetProperty("answer").GetProperty("content").GetString());
        await f.Data(await f.Post($"/api/assistant/conversations/{id}/reset", new { }));
        var replacement = await f.Conversation();
        var reset = await f.Data(await f.Post($"/api/assistant/conversations/{replacement}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = summary }));
        Assert.Contains("no earlier completed exchanges", reset.GetProperty("answer").GetProperty("content").GetString());
        Assert.Equal(0, f.Answers.Calls);
    }
}
