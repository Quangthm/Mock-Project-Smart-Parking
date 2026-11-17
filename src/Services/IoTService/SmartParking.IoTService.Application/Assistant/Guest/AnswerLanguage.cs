using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Guest;

public static partial class AnswerLanguage
{
    private static readonly HashSet<string> Vietnamese = new(StringComparer.OrdinalIgnoreCase)
    {
        "xin", "chao", "toi", "ban", "cua", "la", "gi", "cach", "the", "nao", "xe", "bai",
        "dat", "cho", "lich", "thanh", "toan", "khong", "duoc", "may", "ngay", "mai", "tim",
        "bao", "nhieu", "tro", "cam", "noi", "ro", "hon", "giai", "thich", "them",
        "tom", "tat", "gioi", "han", "ly", "huong", "dan", "hoan", "tien", "loai", "nhu", "gan", "trong", "goi"
    };

    private static readonly HashSet<string> English = new(StringComparer.OrdinalIgnoreCase)
    {
        "hello", "hi", "what", "how", "when", "where", "which", "who", "why", "is", "are", "does", "my", "the", "can",
        "please", "parking", "vehicle", "vehicles", "reservation", "reservations", "booking", "payment",
        "find", "tomorrow", "today", "support", "tell", "more", "explain", "thank", "thanks",
        "refund", "guidance", "assistant", "limitations", "near", "nearby", "availability", "password",
        "types", "lost", "reserve", "prebook", "summarize", "summarise", "recap", "summary"
    };

    public static string Detect(string message, string fallback = "vi")
    {
        if (Requested(message) is { } requested) return requested;
        var vi = HasVietnameseMarks(message) ? 1 : 0;
        var en = 0;
        foreach (Match word in Word().Matches(message))
        {
            if (Vietnamese.Contains(word.Value)) vi++;
            if (English.Contains(word.Value)) en++;
        }
        return vi == en ? fallback : vi > en ? "vi" : "en";
    }

    // Only standalone language instructions at the edges are removed. The question remains
    // subject to the same personal-data, intent and provider-egress checks as ordinary input.
    public static string? Requested(string message)
    {
        message = message.Normalize(System.Text.NormalizationForm.FormC);
        var match = PrefixInstruction().Match(message);
        if (!match.Success) match = SuffixInstruction().Match(message);
        return !match.Success ? null : PublicFaqRetrieval.Normalize(match.Groups["language"].Value)
            is "english" or "tieng anh" ? "en" : "vi";
    }

    public static string Question(string message)
    {
        message = message.Normalize(System.Text.NormalizationForm.FormC);
        var prefix = PrefixInstruction().Match(message);
        if (prefix.Success) return message[prefix.Length..].Trim();
        var suffix = SuffixInstruction().Match(message);
        return suffix.Success ? message[..suffix.Index].Trim() : message;
    }

    private const string Instruction = @"(?:(?:please|hãy|hay|vui lòng|vui long)\s+)?(?:(?:answer|respond|reply)(?:\s+me)?\s+in|(?:trả lời|tra loi)(?:\s+(?:bằng|bang))?)\s+(?<language>english|vietnamese|tiếng anh|tieng anh|tiếng việt|tieng viet)(?:\s*,?\s*(?:please|nhé|nhe))?";

    [GeneratedRegex(@"\A\s*" + Instruction + @"(?:\s*[:;,.]\s*|\s+|\z)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrefixInstruction();

    [GeneratedRegex(@"(?:[?!.;,]\s*|\s+(?:and|và|va)\s+)" + Instruction + @"\s*[.!?]?\s*\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SuffixInstruction();

    internal static bool HasVietnameseMarks(string message) =>
        VietnameseMark().IsMatch(message.Normalize(System.Text.NormalizationForm.FormC));

    [GeneratedRegex("[đăâêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VietnameseMark();

    [GeneratedRegex("[a-zA-Z]+")]
    private static partial Regex Word();
}
