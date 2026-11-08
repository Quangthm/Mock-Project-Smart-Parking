using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Guest;

public static partial class AnswerLanguage
{
    private static readonly HashSet<string> Vietnamese = new(StringComparer.OrdinalIgnoreCase)
    {
        "xin", "chao", "toi", "ban", "cua", "la", "gi", "cach", "the", "nao", "xe", "bai",
        "dat", "cho", "lich", "thanh", "toan", "khong", "duoc", "may", "ngay", "mai", "tim",
        "bao", "nhieu", "tro", "cam", "noi", "ro", "hon", "giai", "thich", "them",
        "gioi", "han", "ly", "huong", "dan", "hoan", "tien", "loai", "nhu", "gan", "trong", "goi"
    };

    private static readonly HashSet<string> English = new(StringComparer.OrdinalIgnoreCase)
    {
        "hello", "hi", "what", "how", "when", "where", "which", "who", "why", "is", "are", "does", "my", "the", "can",
        "please", "parking", "vehicle", "vehicles", "reservation", "reservations", "booking", "payment",
        "find", "tomorrow", "today", "support", "tell", "more", "explain", "thank", "thanks",
        "refund", "guidance", "assistant", "limitations", "near", "nearby", "availability", "password",
        "types", "lost", "reserve", "prebook"
    };

    public static string Detect(string message, string fallback = "vi")
    {
        var vi = HasVietnameseMarks(message) ? 1 : 0;
        var en = 0;
        foreach (Match word in Word().Matches(message))
        {
            if (Vietnamese.Contains(word.Value)) vi++;
            if (English.Contains(word.Value)) en++;
        }
        return vi == en ? fallback : vi > en ? "vi" : "en";
    }

    internal static bool HasVietnameseMarks(string message) =>
        VietnameseMark().IsMatch(message.Normalize(System.Text.NormalizationForm.FormC));

    [GeneratedRegex("[đăâêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VietnameseMark();

    [GeneratedRegex("[a-zA-Z]+")]
    private static partial Regex Word();
}
