using System.Diagnostics.Metrics;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

namespace SmartParking.IoTService.Infrastructure.Assistant.Guest;

public sealed class GuestAnswerFactory(IHttpClientFactory? clients = null, IMeterFactory? meters = null) : IGuestAnswerFactory
{
    private readonly Histogram<double>? duration = ProviderTransport.CreateDuration(meters);
    public IManagedAnswerProvider Create(PublicKnowledge knowledge, PublicArticle article, string locale) =>
        new GuestArticleAnswer(knowledge, article, locale, clients, duration);
}
