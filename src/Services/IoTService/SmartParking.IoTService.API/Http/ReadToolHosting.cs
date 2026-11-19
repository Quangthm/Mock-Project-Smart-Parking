using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.IoTService.Application.Assistant.Chat;
using SmartParking.IoTService.Infrastructure.Assistant;

namespace SmartParking.IoTService.API.Http;

public static class ReadToolHosting
{
    public static void AddReadTools(this WebApplicationBuilder builder)
    {
        var vehicles = Register("assistant-user-read", "User");
        var reservations = Register("assistant-operations-read", "Operations");
        var searchConfigured = Register("assistant-parking-search", "Search");
        var age = builder.Configuration.GetValue<int>("Assistant:Reads:Search:MaxAgeSeconds");
        var skew = builder.Configuration.GetValue<int?>("Assistant:Reads:Search:ClockSkewSeconds");
        var search = searchConfigured && age is > 0 and <= 3600 && skew is >= 0 and <= 60;
        builder.Services.TryAddSingleton(new ReadToolOptions(vehicles, reservations, search, age, skew ?? 0));
        builder.Services.TryAddScoped<IOwnerReadTools, OwnerReadClient>();
        builder.Services.AddScoped<ReadTools>();
        bool Register(string name, string owner)
        {
            var enabled = builder.Configuration.GetValue<bool>($"Assistant:Reads:{owner}:Enabled");
            var configured = builder.Configuration[$"Assistant:Reads:{owner}:Origin"];
            Uri? origin = null;
            if (enabled && (!Uri.TryCreate(configured, UriKind.Absolute, out origin) || origin.Scheme != "https" || !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" || origin.Query != "" || origin.Fragment != ""))
                throw new InvalidOperationException("Enabled read tools require a fixed HTTPS owner origin.");
            builder.Services.AddHttpClient(name, client => { client.BaseAddress = origin; client.Timeout = Timeout.InfiniteTimeSpan; })
                .RemoveAllLoggers()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
            return enabled;
        }
    }
}
