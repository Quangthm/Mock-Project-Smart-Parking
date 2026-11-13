using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;
namespace SmartParking.IoTService.Infrastructure.Assistant.Guest;

// Deployment-owned files; no guest-supplied paths and no publication endpoint.
public sealed class PublicKnowledgeFile(string directory, TimeProvider clock, IMeterFactory? meters = null) : IPublicKnowledge
{
    private sealed record Publication(string Sha256, string Version, string ApprovedBy, string Role, DateTimeOffset PublishedAt, DateTimeOffset? EffectiveTo);
    private sealed record ParsedKnowledge(PublicKnowledge Knowledge, DateTimeOffset? EffectiveFrom,
        DateTimeOffset? EffectiveTo, DateTimeOffset? PublishedAt);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Histogram<double>? duration = meters?.Create("SmartPark.Assistant")
        .CreateHistogram<double>("smartpark.assistant.knowledge.duration", "s");
    private ParsedKnowledge? cached;

    public async Task<PublicKnowledge?> ReadAsync(CancellationToken ct)
    {
        var started = duration?.Enabled == true ? Stopwatch.GetTimestamp() : 0L;
        PublicKnowledge? result = null;
        try
        {
            var bytes = await ReadBounded(DemoKnowledgeCorpus.FileName, ct, DemoKnowledgeCorpus.MaxFileBytes);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var snapshot = Volatile.Read(ref cached);
            if (snapshot?.Knowledge.Fingerprint != hash)
            {
                snapshot = Parse(bytes, hash);
                if (snapshot is null) return null;
                Volatile.Write(ref cached, snapshot);
            }
            var now = clock.GetUtcNow();
            if (snapshot.EffectiveFrom > now || snapshot.EffectiveTo <= now || snapshot.PublishedAt > now) return null;
            var publication = JsonSerializer.Deserialize<Publication>(await ReadBounded("publication.json", ct), Json);
            now = clock.GetUtcNow();
            if (publication is null || publication.Version != snapshot.Knowledge.Version || publication.Role != "admin" ||
                string.IsNullOrWhiteSpace(publication.ApprovedBy) || publication.ApprovedBy.Length > 128 ||
                publication.PublishedAt == default || publication.PublishedAt > now || publication.EffectiveTo <= now ||
                snapshot.EffectiveTo <= now || !string.Equals(hash, publication.Sha256, StringComparison.OrdinalIgnoreCase)) return null;
            if (snapshot.PublishedAt.HasValue && snapshot.PublishedAt != publication.PublishedAt) return null;
            ct.ThrowIfCancellationRequested();
            return result = snapshot.Knowledge;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or DemoGroundedUnavailableException)
        { return null; }
        finally
        {
            if (started != 0)
                duration!.Record(Stopwatch.GetElapsedTime(started).TotalSeconds,
                    new KeyValuePair<string, object?>("smartpark.assistant.outcome",
                        result is not null ? "ready" : ct.IsCancellationRequested ? "cancelled" : "unavailable"));
        }
    }

    private static ParsedKnowledge? Parse(byte[] bytes, string hash)
    {
        var parsed = DemoKnowledgeCorpus.Parse(bytes);
        var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF').Replace("\r\n", "\n");
        var metadataEnd = text.IndexOf("\n---\n", StringComparison.Ordinal);
        var metadata = text[..metadataEnd].Split('\n');
        string Field(string name)
        {
            var values = metadata.Where(line => line.StartsWith(name + ":", StringComparison.Ordinal)).ToArray();
            if (values.Length != 1) throw new DemoGroundedUnavailableException();
            return values[0][(name.Length + 1)..].Trim();
        }
        DateTimeOffset? TimeField(string name)
        {
            var values = metadata.Where(line => line.StartsWith(name + ":", StringComparison.Ordinal)).ToArray();
            if (values.Length == 0) return null;
            var value = Field(name);
            if (value == "null") return null;
            if (value.Length > 64 || !value.Contains('T') ||
                !(value.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(value, @"[+-]\d{2}:\d{2}$")) ||
                !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
                throw new DemoGroundedUnavailableException();
            return instant;
        }
        var version = Field("version");
        var audience = Field("audience");
        var effectiveFrom = TimeField("effective_from");
        var effectiveTo = TimeField("effective_to");
        var publishedAt = TimeField("published_at");
        if (effectiveFrom >= effectiveTo) return null;
        if (Field("publication_state") != "published" || Field("review_status") != "approved" ||
            !audience.StartsWith('[') || !audience.EndsWith(']') ||
            !audience[1..^1].Split(',', StringSplitOptions.TrimEntries).Contains("public", StringComparer.Ordinal) ||
            parsed.Length is 0 or > 30) return null;
        if (parsed.Any(a => !Text(a.Title, 200) || !Text(a.Content, 6000))) return null;
        var articles = parsed.Select(a => new PublicArticle(a.Id, a.Title, a.Title, a.Content, a.Content,
            ["docs/03-design/ai/p1/knowledge/smartpark-knowledge.md"], "vi", Array.AsReadOnly(a.SearchTerms))).ToArray();
        return new(new(version, hash, Array.AsReadOnly(articles)), effectiveFrom, effectiveTo, publishedAt);
    }
    private async Task<byte[]> ReadBounded(string name, CancellationToken ct, int maximum = 128 * 1024)
    {
        await using var stream = new FileStream(Path.Combine(directory, name), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > maximum) throw new IOException();
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > maximum) throw new IOException();
            buffer.Write(chunk, 0, count);
        }
        return buffer.ToArray();
    }
    private static bool Text(string? text, int maximum) => !string.IsNullOrWhiteSpace(text) && text.Length <= maximum && !text.Any(c => char.IsControl(c) && c != '\n');
}

public sealed class GuestAnswerFactory(IHttpClientFactory? clients = null, IMeterFactory? meters = null) : IGuestAnswerFactory
{
    private readonly Histogram<double>? duration = ProviderTransport.CreateDuration(meters);
    public IManagedAnswerProvider Create(PublicKnowledge knowledge, PublicArticle article, string locale) =>
        new Answer(knowledge, article, locale, clients, duration);
    private sealed class Answer(PublicKnowledge knowledge, PublicArticle article, string locale,
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
            var reply = locale == "en" && article.SourceLocale == "vi"
                ? $"The answer is currently available in Vietnamese only.\n\n{source.Excerpt}"
                : source.Excerpt;
            return Task.FromResult(new GroundedReply(reply, [source], null, null, "knowledge"));
        }
    }
}
