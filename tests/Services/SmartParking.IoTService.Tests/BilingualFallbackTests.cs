using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Guest;

namespace SmartParking.IoTService.Tests;

public sealed class BilingualFallbackTests
{
    [Theory]
    [InlineData("none", "vehicle-guide", "en")]
    [InlineData("timeout", "vehicle-guide", "en")]
    [InlineData("unavailable", "vehicle-guide", "en")]
    [InlineData("none", "booking-guide", "en")]
    [InlineData("timeout", "booking-guide", "en")]
    [InlineData("unavailable", "booking-guide", "en")]
    [InlineData("none", "vehicle-guide", "vi")]
    [InlineData("timeout", "vehicle-guide", "vi")]
    [InlineData("unavailable", "vehicle-guide", "vi")]
    [InlineData("none", "booking-guide", "vi")]
    [InlineData("timeout", "booking-guide", "vi")]
    [InlineData("unavailable", "booking-guide", "vi")]
    public async Task PublishedTranslationSurvivesProviderFailure(string failure, string id, string locale)
    {
        var f = new GuestFaqTests.Fixture();
        f.Corpus.Value = PartAFaqTests.Corpus();
        if (failure != "none") f.EnableProvider("gemini");
        using var transport = new PartAProviderTransport(failure);
        var factory = new GuestAnswerFactory(transport);
        var generator = new ManagedDemoGenerator(f.Store, new GuestFaqTests.Protector(), f.Answers,
            TimeProvider.System, NullLogger<ManagedDemoGenerator>.Instance,
            new ManagedFailoverOptions { AttemptTimeout = TimeSpan.FromMilliseconds(100) });
        var handler = new GuestHandlers(f.Store, f.Corpus, generator, factory, new(true));
        var question = id == "vehicle-guide" ? "Which vehicle types does SmartPark support?" : "How do I book on SmartPark step by step?";
        var reply = await handler.Handle(new GuestMessage(Guid.NewGuid(),
            (locale == "en" ? "Please answer in English: " : "Please answer in Vietnamese: ") + question, "auto"), default);
        Assert.Equal("knowledge", reply.Mode);
        Assert.Equal("answered", reply.Outcome);
        Assert.Equal(locale, reply.ReplyLocale);
        Assert.DoesNotContain("Vietnamese only", reply.Content);
        var source = Assert.Single(reply.Sources);
        Assert.Equal(id, source.ArticleId);
        Assert.Equal("0.4.1", source.Version);
        Assert.Equal(locale, source.Locale);
        var article = f.Corpus.Value.Articles.Single(a => a.Id == id);
        Assert.Equal(locale == "en" ? article.En : article.Vi, source.Excerpt);
        if (locale == "en")
        {
            Assert.Equal(article.En, reply.Content);
            Assert.DoesNotContain("Đối chiếu", reply.Content);
            if (id == "booking-guide")
            {
                Assert.Contains("1. Sign in.", reply.Content);
                Assert.Contains("7. Confirm on the form.", reply.Content);
                Assert.Contains("does not submit the booking", reply.Content);
                Assert.Contains("not fixed limits", reply.Content);
            }
        }
        Assert.Empty(reply.Cards);
        Assert.Empty(reply.Actions);
    }
}
