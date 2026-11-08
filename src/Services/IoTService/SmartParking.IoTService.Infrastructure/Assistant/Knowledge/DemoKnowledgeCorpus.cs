using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SmartParking.IoTService.Application.Assistant.Knowledge;

namespace SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

// One Vietnamese editing source; search terms are aliases, not translated policies.
internal sealed record DemoArticle(string Id, string Version, string Title, string Content, string[] SearchTerms)
{
    public GroundedSource Source => new(Id, Version, Title, Content[..Math.Min(Content.Length, 1000)]);
}
internal static class DemoKnowledgeCorpus
{
    internal const string FileName = "smartpark-knowledge.md";
    internal const int MaxFileBytes = 256 * 1024;
    internal static bool Identifier(string value) => Regex.IsMatch(value, "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$");

    internal static DemoArticle[] Load(string directory)
    {
        // Read once per question, bounded even if the file grows during a save.
        using var stream = File.OpenRead(Path.Combine(directory, FileName));
        if (stream.Length > MaxFileBytes) throw new DemoGroundedUnavailableException();
        using var bytes = new MemoryStream();
        var buffer = new byte[4096];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (bytes.Length + count > MaxFileBytes) throw new DemoGroundedUnavailableException();
            bytes.Write(buffer, 0, count);
        }
        var text = new UTF8Encoding(false, true).GetString(bytes.ToArray()).TrimStart('\uFEFF').Replace("\r\n", "\n");
        var document = text.Split("\n---\n", 2);
        if (!text.StartsWith("---\n") || document.Length != 2) throw new DemoGroundedUnavailableException();
        string Metadata(string name)
        {
            var values = document[0].Split('\n').Where(line => line.StartsWith(name + ":", StringComparison.Ordinal)).ToArray();
            if (values.Length != 1) throw new DemoGroundedUnavailableException();
            return values[0][(name.Length + 1)..].Trim();
        }
        var version = Metadata("version");
        if (!Identifier(version) || Metadata("locale") != "vi") throw new DemoGroundedUnavailableException();
        var body = document[1];
        var headings = Regex.Matches(body, @"^## \[([a-zA-Z0-9._-]+)\] ([^\n]+)$", RegexOptions.Multiline);
        if (headings.Count is 0 or > 100 || body.Split('\n').Count(line => line.StartsWith("## ", StringComparison.Ordinal)) != headings.Count)
            throw new DemoGroundedUnavailableException();
        var articles = headings.Select((heading, index) =>
        {
            var id = heading.Groups[1].Value;
            var title = heading.Groups[2].Value.Trim();
            var start = heading.Index + heading.Length;
            var end = index + 1 < headings.Count ? headings[index + 1].Index : body.Length;
            var section = body[start..end];
            const string contentHeading = "\n### Nội dung\n";
            var contentStart = section.IndexOf(contentHeading, StringComparison.Ordinal);
            if (contentStart < 0 || section.LastIndexOf(contentHeading, StringComparison.Ordinal) != contentStart)
                throw new DemoGroundedUnavailableException();
            var aliases = section[..contentStart].Split('\n').Where(line => line.StartsWith("Từ khóa:", StringComparison.Ordinal)).ToArray();
            if (aliases.Length != 1 || aliases[0].Length > 2000) throw new DemoGroundedUnavailableException();
            var terms = aliases[0]["Từ khóa:".Length..].Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var content = section[(contentStart + contentHeading.Length)..];
            // References/editorial notes stay in this file, outside model context and citations.
            var notes = content.IndexOf("\n### ", StringComparison.Ordinal);
            if (notes >= 0)
            {
                if (!content[notes..].StartsWith("\n### Đối chiếu — không gửi đến AI\n", StringComparison.Ordinal))
                    throw new DemoGroundedUnavailableException();
                content = content[..notes];
            }
            content = content.Trim();
            if (!Identifier(id) || title.Length is 0 or > 255 || content.Length is 0 or > 16000 || terms.Length is 0 or > 100)
                throw new DemoGroundedUnavailableException();
            return new DemoArticle(id, version, title, content, terms);
        }).ToArray();
        if (articles.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != articles.Length)
            throw new DemoGroundedUnavailableException();
        return articles;
    }

    internal static string Normalize(string value, bool keepDiacritics = false)
    {
        var text = value.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        if (!keepDiacritics) text = string.Concat(text.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Replace('đ', 'd');
        return string.Join(' ', Regex.Matches(text, @"[\p{L}\p{N}]+").Select(m => m.Value));
    }

    private static bool HasVietnameseMarks(string value) => Regex.IsMatch(value.Normalize(NormalizationForm.FormC),
        "[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]", RegexOptions.IgnoreCase);

    internal static string AnswerLanguage(string message)
    {
        // Vietnamese (including unsigned input) is the default. Output language never filters sources.
        if (HasVietnameseMarks(message)) return "vi";
        var words = Normalize(message).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(word => VietnameseCues.Contains(word))) return "vi";
        return words.Any(word => EnglishCues.Contains(word)) ? "en" : "vi";
    }

    internal static DemoArticle[] Retrieve(DemoArticle[] articles, string message)
    {
        // Keep accents when supplied: “báo mất” must not match the alias “bảo mật”.
        // Unsigned input still matches the same Vietnamese topics without needing duplicate policies.
        var keepDiacritics = HasVietnameseMarks(message);
        var normalized = " " + Normalize(message, keepDiacritics) + " ";
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 2 && !StopWords.Contains(Normalize(word))).Distinct().ToArray();
        return articles.Select(article =>
        {
            var title = Normalize(article.Title, keepDiacritics).Split(' ').ToHashSet(StringComparer.Ordinal);
            var content = Normalize(article.Content, keepDiacritics).Split(' ').ToHashSet(StringComparer.Ordinal);
            var score = words.Sum(word => (title.Contains(word) ? 4 : 0) + (content.Contains(word) ? 1 : 0));
            foreach (var term in article.SearchTerms.Select(term => Normalize(term, keepDiacritics)).Where(term => term.Length > 0).Distinct())
            {
                if (normalized.Contains(" " + term + " ", StringComparison.Ordinal))
                    score += StopWords.Contains(Normalize(term)) ? 1 : 8 + term.Split(' ').Length;
            }
            return new { Article = article, Score = score };
        }).Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Article.Id, StringComparer.Ordinal)
            .Take(3).Select(x => x.Article).ToArray();
    }

    private static readonly HashSet<string> StopWords = ["the", "and", "how", "what", "this", "that", "with", "you", "your", "can", "are", "does", "where", "when", "which", "about", "please", "parking", "smartpark", "cho", "toi", "cua", "nhung", "nhu", "nao", "khong", "duoc", "mot", "lam", "sao", "muon", "trong", "cac", "khi", "voi", "nhan",
        "ban", "minh", "chung", "dang", "rat", "qua", "cam", "thay", "nay", "ay", "hom", "nua", "nhe", "nhi", "vay", "giup", "hay", "them", "thi", "have", "has", "was", "been", "feel", "feeling", "really", "very", "just", "today", "friend", "tell", "more"];
    private static readonly HashSet<string> VietnameseCues = ["toi", "dat", "cho", "bai", "phi", "xe", "thanh", "toan", "tai", "khoan", "hoan", "tien", "sao", "nao", "gi", "bao", "nhieu"];
    private static readonly HashSet<string> EnglishCues = ["i", "im", "am", "sleepy", "tired", "thanks", "how", "what", "where", "when", "why", "can", "could", "does", "please", "is", "are", "my", "the", "booking", "bookings", "book", "reserve", "reservation", "cancel", "cancellation", "refund", "refunds", "pay", "payment", "payments", "parking", "price", "prices", "fee", "fees", "cost", "monthly", "yearly", "annual", "pass", "passes", "renewal", "subscription", "account", "security", "password", "vehicle", "vehicles", "support", "contact", "help", "error", "hello", "hi"];
}
