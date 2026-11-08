using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Guest;

internal static class PublicFaqRetrieval
{
    private static readonly HashSet<string> CommonWords = new(StringComparer.Ordinal)
    {
        "smartpark", "smartparking", "a", "an", "the", "and", "do", "does", "is", "are", "can", "could",
        "what", "which", "how", "to", "of", "for", "me", "you", "i", "please", "about", "with",
        "explain", "supported", "here",
        "co", "nao", "gi", "nhung", "cac", "la", "duoc", "khong", "cho", "toi", "minh", "ban", "cua",
        "cach", "voi", "thi", "nhu", "the", "biet", "giup", "ho", "tro", "hoi", "ben", "hay",
        "muon", "hieu", "bao"
    };

    internal static string Normalize(string value, bool keepAccents = false)
    {
        var text = value.ToLowerInvariant().Normalize(keepAccents ? NormalizationForm.FormC : NormalizationForm.FormD);
        if (!keepAccents)
            text = string.Concat(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Replace('đ', 'd');
        return string.Join(' ', Regex.Matches(text, @"[\p{L}\p{N}]+").Select(match => match.Value));
    }

    private static string[] Terms(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(word => word != "về" && (word == "chỗ" || !CommonWords.Contains(Normalize(word))))
        .Distinct(StringComparer.Ordinal).ToArray();

    internal static PublicArticle? Find(string message, IReadOnlyList<PublicArticle> articles)
    {
        var keepAccents = AnswerLanguage.HasVietnameseMarks(message);
        var question = Normalize(message, keepAccents);
        // In a brand-led question, “support” before an object is a verb, not the support desk.
        question = Regex.Replace(question, @"\A(?:smartpark|smartparking) supports? (?=\p{L})", "");
        var words = Terms(question);
        if (words.Length == 0) return null;

        var paddedQuestion = $" {question} ";
        var ranked = articles.Select(article =>
        {
            var aliases = (article.SearchTerms ?? []).Select(term => Normalize(term, keepAccents)).Distinct(StringComparer.Ordinal).ToArray();
            var aliasTerms = aliases.Select(Terms).ToArray();
            var titleWords = Terms(Normalize(article.TitleVi + " " + article.TitleEn, keepAccents)).ToHashSet(StringComparer.Ordinal);
            var aliasWords = aliasTerms.SelectMany(terms => terms).ToHashSet(StringComparer.Ordinal);
            var anchors = words.Count(word => titleWords.Contains(word) || aliasWords.Contains(word));
            var phrase = aliases.Where(alias => paddedQuestion.Contains($" {alias} ", StringComparison.Ordinal))
                .Select(alias => Terms(alias).Length).DefaultIfEmpty().Max();
            // A short question may have one topic word. Require independent evidence from the title and aliases.
            var shortTopic = words.Length == 1 && (phrase == 1 || (titleWords.Contains(words[0]) &&
                aliasTerms.Count(terms => terms.Contains(words[0])) >= 2));
            if (anchors < 2 && phrase < 2 && !shortTopic)
                return (Article: article, Score: 0);

            var content = Terms(Normalize(article.Vi + " " + article.En, keepAccents)).ToHashSet(StringComparer.Ordinal);
            var covered = words.Count(word => titleWords.Contains(word) || aliasWords.Contains(word) || content.Contains(word));
            // The provider receives a topic title, so reject queries whose remaining words point outside the source.
            if (covered * 3 <= words.Length * 2) return (Article: article, Score: 0);
            var score = phrase * 12 + anchors * 5 + words.Count(titleWords.Contains) * 2 + words.Count(content.Contains);
            return (Article: article, Score: score);
        }).Where(item => item.Score > 0).OrderByDescending(item => item.Score).Take(2).ToArray();

        if (ranked.Length > 0)
            return ranked.Length == 2 && ranked[0].Score == ranked[1].Score ? null : ranked[0].Article;

        // Recover one typo in a short alias only when the rest of the question matches that
        // alias exactly. Extra words and ambiguous matches still require clarification.
        var tokens = question.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is < 2 or > 4 || words.Length < 2) return null;
        var corrected = articles.Where(article => (article.SearchTerms ?? []).Any(term =>
            MatchesAliasWithTypo(tokens, Normalize(term, keepAccents)) &&
            GuestIntents.RestrictedIntent(term) is null)).Take(2).ToArray();
        return corrected.Length == 1 ? corrected[0] : null;
    }

    private static bool MatchesAliasWithTypo(string[] question, string alias)
    {
        var expected = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (question.Length != expected.Length || Terms(alias).Length < 2) return false;
        var corrected = false;
        for (var index = 0; index < expected.Length; index++)
        {
            if (question[index] == expected[index]) continue;
            if (corrected || expected[index].Length < 5 || !OneTypo(question[index], expected[index])) return false;
            corrected = true;
        }
        return corrected;
    }

    private static bool OneTypo(ReadOnlySpan<char> input, ReadOnlySpan<char> expected)
    {
        if (Math.Abs(input.Length - expected.Length) > 1) return false;
        while (!input.IsEmpty && !expected.IsEmpty && input[0] == expected[0])
        {
            input = input[1..];
            expected = expected[1..];
        }
        if (input.Length > expected.Length) return input[1..].SequenceEqual(expected);
        if (expected.Length > input.Length) return input.SequenceEqual(expected[1..]);
        return input.Length >= 2 && input[0] == expected[1] && input[1] == expected[0] &&
            input[2..].SequenceEqual(expected[2..]);
    }
}
