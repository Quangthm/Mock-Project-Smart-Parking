using System.Text.RegularExpressions;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Application.Assistant.Guest;

internal sealed partial class PublicFaqQuestion(IManagedAnswerProvider inner, string? question) : IManagedAnswerProvider
{
    private static readonly HashSet<string> QuestionWords = new(StringComparer.Ordinal)
    {
        "a", "an", "are", "about", "and", "can", "could", "do", "does", "for", "here", "how", "i", "is",
        "me", "or", "please", "the", "to", "what", "which", "with", "you", "your", "support", "supports",
        "supported", "accept", "accepts", "explain", "tell", "work", "works", "smartpark", "smartparking",
        "ban", "ben", "cac", "cach", "cho", "co", "cua", "duoc", "gi", "giup", "hay", "ho", "hoi",
        "khong", "la", "lam", "minh", "muon", "nao", "nhung", "nhu", "the", "toi", "tro", "ve", "voi"
    };

    internal static string? Screen(string message, PublicArticle article)
    {
        var text = message.Trim();
        // Restrict egress to ordinary question punctuation and vocabulary present in public material.
        // Preserve the original words for Gemini only after checking every token.
        if (text.Length > 320 || !QuestionText().IsMatch(text) || PersonalContext().IsMatch(text)) return null;
        var allowed = PublicFaqRetrieval.Normalize(string.Join(' ', article.SearchTerms ?? []) +
            " " + article.TitleVi + " " + article.TitleEn + " " + article.Vi + " " + article.En)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var words = PublicFaqRetrieval.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(word => allowed.Contains(word) || QuestionWords.Contains(word)) ? text : null;
    }

    public Task<GroundedReply> GenerateAsync(string provider, string model, string secret, string title, CancellationToken ct) =>
        inner.GenerateAsync(provider, model, secret, provider == "gemini" ? question ?? title : title, ct);

    public Task<GroundedReply> KnowledgeAsync(string title, CancellationToken ct) => inner.KnowledgeAsync(title, ct);

    [GeneratedRegex(@"\A[\p{L}\p{M} ?!]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex QuestionText();

    [GeneratedRegex(@"(?i:\b(?:for|named|tên|cho)\s+\p{L}|\Aan\s+(?:how|what|when|where|which|why)\b)|(?<!\A)\b(?!SmartPark(?:ing)?\b|I\b)\p{Lu}\p{Ll}+", RegexOptions.CultureInvariant)]
    private static partial Regex PersonalContext();
}
