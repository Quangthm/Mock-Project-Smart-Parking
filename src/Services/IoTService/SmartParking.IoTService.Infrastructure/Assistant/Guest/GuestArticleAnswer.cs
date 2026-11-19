using System.Diagnostics.Metrics;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

namespace SmartParking.IoTService.Infrastructure.Assistant.Guest;

internal sealed class GuestArticleAnswer(PublicKnowledge knowledge, PublicArticle article, string locale,
        IHttpClientFactory? clients, Histogram<double>? duration) : IManagedAnswerProvider
{
    private GroundedSource Source => new(article.Id, knowledge.Version, locale == "en" ? article.TitleEn : article.TitleVi, locale == "en" ? article.En : article.Vi);
    public async Task<GroundedReply> GenerateAsync(string provider, string model, string secret, string message, CancellationToken ct)
    {
        return await ProviderTransport.Generate(new("", provider, model, secret, [Source], locale, article.SourceLocale),
            clients, duration, message, ct);
    }
    public Task<GroundedReply> KnowledgeAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var source = Source;
        var content = article.Id == "booking-guide" ? BookingGuidance.Format(source.Excerpt, message) : source.Excerpt;
        var reply = locale == "en" && article.SourceLocale == "vi"
            ? $"The answer is currently available in Vietnamese only.\n\n{content}"
            : content;
        return Task.FromResult(new GroundedReply(reply, [source], null, null, "knowledge"));
    }
}
