using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Infrastructure.Assistant.Guest;

internal static class BookingGuidance
{
    // Format the published preparation paragraph, not a second copy of booking policy.
    // Retain every remaining paragraph (conditions, timing and payment caveats) unchanged.
    internal static string Format(string content, string question)
    {
        const string introduction = "Để chuẩn bị đặt chỗ, ";
        if (!question.Contains("từng bước có đánh số", StringComparison.Ordinal) ||
            !content.StartsWith(introduction, StringComparison.Ordinal)) return content;
        var end = content.IndexOf("\n\n", StringComparison.Ordinal);
        if (end < 0) end = content.Length;
        var paragraph = content[introduction.Length..end];
        var steps = Regex.Split(paragraph, @" rồi |, (?=tìm |chọn |xem )|\. Sau đó | và (?=xác nhận trên form)");
        if (steps.Length < 2) return content;
        return "Để chuẩn bị đặt chỗ:\n\n" + string.Join("\n", steps.Select((step, index) =>
            $"{index + 1}. {step.Trim().TrimEnd('.')}.")) + content[end..];
    }
}
