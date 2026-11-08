using System.Globalization;
using System.Text;
using SmartParking.IoTService.Application.Assistant.Demo;

namespace SmartParking.IoTService.Infrastructure.Assistant.Demo;

// Deterministic canned replies for transport/UI demonstration, never a live FAQ knowledge source.
public sealed class DemoReplyGenerator : IDemoReplyGenerator
{
    public Task<string> ReplyAsync(string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = string.Concat(message.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant().Replace('đ', 'd');
        var reply = normalized switch
        {
            var text when text.Contains("doanh thu") || text.Contains("revenue") =>
                "Bản demo chưa đọc doanh thu hoặc dữ liệu kinh doanh. Tôi không có số liệu thực tế để trả lời câu hỏi này.",
            var text when text.Contains("thanh toan") || text.Contains("payment") || text.Contains("hoan tien") =>
                "Ví dụ mô phỏng: với yêu cầu thanh toán, trợ lý có thể hướng dẫn bạn mở màn hình thanh toán thông thường. Bản demo này không tạo giao dịch, thu tiền hay hoàn tiền.",
            var text when text.Contains("dat cho") || text.Contains("booking") || text.Contains("reservation") =>
                "Ví dụ mô phỏng: bạn có thể bắt đầu bằng việc chọn bãi và thời gian cần đỗ xe. Bản demo chưa kiểm tra chỗ trống hoặc tạo đặt chỗ; chưa có thao tác nào được thực hiện.",
            var text when text.Contains("bai do") || text.Contains("parking") || text.Contains("tim bai") =>
                "Ví dụ mô phỏng: trợ lý có thể hỏi khu vực và thời gian bạn muốn đỗ xe để hỗ trợ tìm bãi. Bản demo không tra cứu bãi, giá hoặc chỗ trống thực tế.",
            _ => "Tôi đang trả lời bằng nội dung mẫu cố định, chưa kết nối mô hình AI. Bạn có thể thử hỏi về tìm bãi đỗ xe, đặt chỗ hoặc thanh toán để xem luồng hội thoại."
        };
        return Task.FromResult(reply);
    }
}
