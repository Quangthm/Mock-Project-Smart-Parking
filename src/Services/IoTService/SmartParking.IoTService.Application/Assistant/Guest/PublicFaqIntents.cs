using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Guest;

internal static partial class PublicFaqIntents
{
    internal sealed record Route(string Id, string ArticleId, string Question);

    private static readonly Route Advance = new("booking-advance", "booking-guide",
        "Có thể đặt chỗ trước tối đa bao lâu theo mặc định và cần kiểm tra điều khoản hiệu lực ở đâu?");
    private static readonly Route MinimumDuration = new("booking-minimum-duration", "booking-guide",
        "Thời lượng đặt chỗ tối thiểu mặc định là bao nhiêu và người lái nên kiểm tra chính sách hiệu lực ở đâu?");
    private static readonly Route MaximumDuration = new("booking-maximum-duration", "booking-guide",
        "Thời lượng đặt chỗ tối đa mặc định là bao nhiêu và người lái nên kiểm tra chính sách hiệu lực ở đâu?");
    private static readonly Route Refund = new("booking-refund", "cancellation-refund-guide",
        "Khi tự nguyện hủy đặt chỗ, hãy nêu các mốc và mức hoàn mặc định, điều kiện tiền đủ điều kiện đã thực thu và cách xác định mức thực tế.");
    private static readonly Route Overstay = new(GuestIntents.ParkingOverstay, "payment-guide",
        GuestIntents.ParkingOverstayQuestion);
    private static readonly Route WalkIn = new("parking-walk-in", "walk-in-and-entry-guide",
        "Có thể tới gửi xe trực tiếp mà không đặt trước không? Nêu điều kiện bãi hỗ trợ, tiếp nhận và việc đến trực tiếp không giữ chỗ.");
    private static readonly Route Arrival = new("parking-arrive-late", "arrival-and-no-show-guide",
        "Nếu đến sau giờ bắt đầu đặt chỗ thì kiểm tra hạn đến ở đâu, dung sai hoạt động thế nào và khi nào bị tính vắng mặt?");
    private static readonly Route PassRefund = new("pass-refund", "pass-cancellation-guide",
        "Nếu tự nguyện hủy gói đỗ xe, cần xử lý các đặt chỗ phụ thuộc thế nào? Nêu quy tắc hoàn trước, trong và sau kỳ, chỉ tính phần đủ điều kiện hoàn và phân biệt gói tháng với gói năm.");
    private static readonly Route PassClosure = new("pass-closure", "pass-cancellation-guide",
        "Gói đỗ xe và khoản hoàn được xử lý thế nào khi bãi đóng cửa tạm thời hoặc vĩnh viễn? Nêu điều kiện phần thời gian không phục vụ hoặc giá trị chưa sử dụng đủ điều kiện.");
    private static readonly Route StartBooking = new("booking-start", "booking-guide",
        "Người lái cần làm những bước nào để chuẩn bị và xác nhận đặt chỗ?");
    private static readonly Route Exit = new("parking-exit-blocked", "checkout-and-departure-guide",
        "Khi cổng ra chưa mở, người lái cần làm gì? Nếu đã thanh toán, cần xác minh gì trước khi trả thêm?");
    private static readonly Route Notice = new("parking-notice-missing", "notification-guide",
        "Nếu không nhận được thông báo, làm sao kiểm tra hệ thống đã ghi nhận đặt chỗ hoặc giao dịch?");

    private static readonly Route[] Routes =
        [Advance, MinimumDuration, MaximumDuration, Refund, Overstay, WalkIn, Arrival, PassRefund, PassClosure, StartBooking, Exit, Notice];

    internal static Route? Find(string intent) => Routes.FirstOrDefault(route => route.Id == intent);

    // Recognize only complete public questions before the broad number and personal-context checks.
    // The model receives the fixed question above. No substring of the caller's message is forwarded.
    internal static Route? MatchPublicPolicy(string message, string question, IReadOnlyList<PublicArticle>? articles)
    {
        if (articles is null || message.Length > 160 || !PublicQuestionCharacters().IsMatch(message.Trim()))
            return null;
        var duration = DurationQuestion().Match(question);
        var durationRoute = duration.Success ? (duration.Groups["minimum"].Success ? MinimumDuration : MaximumDuration) : null;
        var route = AdvanceQuestion().IsMatch(question) ? Advance
            : RefundTimingQuestion().IsMatch(question) ? Refund
            : GenericOverstayQuestion().IsMatch(question) ? Overstay
            : durationRoute;
        return route is not null && articles.Any(article => article.Id == route.ArticleId) ? route : null;
    }

    internal static string? MatchParaphrase(string message, string text, IReadOnlyList<PublicArticle> articles)
    {
        var matches = Paraphrases(text).Where(route => articles.Any(article => article.Id == route.ArticleId))
            .Take(2).ToArray();
        if (matches.Length == 0) return null;
        if (matches.Length > 1) return "unknown";

        var match = matches[0];
        // The fixed question would discard these qualifications. Ask for a narrower question instead.
        if (message.AsSpan().TrimEnd().TrimEnd("?!.".AsSpan()).IndexOfAny('?', '!', '.') >= 0 ||
            Has(text, "bao hiem", "insurance", "rua xe", "car wash") ||
            ((match == WalkIn || match == PassRefund || match == PassClosure) &&
             Has(text, "xe dap", "xe buyt", "xe tai", "bicycle", "bus", "truck", "a van", "my van", "xe van")))
            return "unknown";

        // A connective can join a separate question. Require lexical support for the whole message in that case.
        if (Has(text, "va", "hay", "and", "or") && PublicFaqRetrieval.Find(message, articles)?.Id != match.ArticleId)
            return "unknown";
        return match.Id;
    }

    private static IEnumerable<Route> Paraphrases(string text)
    {
        if (Has(text, "gui xe truc tiep", "gui xe vang lai", "parking without booking", "park without a reservation",
                "khong dat truoc co gui xe", "khong co tai khoan thi co gui xe") ||
            (Has(text, "show up") && Has(text, "car park", "parking lot") && Has(text, "without booking", "without a reservation")) ||
            (Has(text, "chay thang", "di thang", "toi thang", "den thang") && Has(text, "bai", "cong") &&
             Has(text, "gui xe", "do xe", "vao bai")) ||
            (Has(text, "dat truoc", "book truoc") && Has(text, "vao bai") && Has(text, "co phai", "co can")))
            yield return WalkIn;
        if (Has(text, "den bai", "toi bai", "den cong", "toi cong") &&
            Has(text, "sau gio hen", "qua gio hen", "sau gio bat dau"))
            yield return Arrival;
        if (Has(text, "do lau hon", "gui xe lau hon", "stay longer", "stay past") &&
            Has(text, "gio da dat", "gio ket thuc", "gio hen", "booking", "reservation"))
            yield return Overstay;
        if (Has(text, "goi thang", "goi nam", "ve thang", "ve nam", "monthly pass", "yearly pass", "con goi"))
        {
            if (Has(text, "bai dong cua", "bai tam dong", "parking lot closes", "lot closure"))
                yield return PassClosure;
            else if (Has(text, "huy", "thoi dung", "hoan tien", "tra lai tien", "lay lai tien", "refund", "cancel"))
                yield return PassRefund;
        }
        if (Has(text, "bo cho da dat", "huy dat cho", "huy booking", "cancel a reservation") &&
            Has(text, "hoan tien", "tra lai tien", "lay lai tien", "refund"))
            yield return Refund;
        if (Has(text, "giu mot cho", "giu cho gui xe", "giu cho do xe") &&
            Has(text, "lam sao", "the nao", "cach nao"))
            yield return StartBooking;
        if ((Has(text, "cong khong mo", "thanh chan khong nhac", "thanh chan khong mo") &&
             Has(text, "ra bai", "lay xe", "da tra tien", "da thanh toan")) ||
            text == " what if the exit gate doesn t open ")
            yield return Exit;
        if (text == " khong nhan duoc thong bao phai lam sao " ||
            (Has(text, "dien thoai im re", "khong thay tin nhan", "khong thay thong bao", "khong nhan duoc thong bao") &&
             Has(text, "nhan don", "dat cho", "thanh toan", "giao dich")))
            yield return Notice;
    }

    private static bool Has(string text, params string[] phrases) =>
        phrases.Any(phrase => text.Contains($" {phrase} ", StringComparison.Ordinal));

    [GeneratedRegex(@"\A(?:(?:toi |minh )?co the )?(?:dat cho|dat xe) truoc toi da (?:(?:bao nhieu|may) ngay|[0-9]{1,3} ngay(?: dung khong| phai khong)?)\z|\Ahow many days (?:in advance|ahead) can i (?:book(?: a parking spot)?|reserve parking)\z", RegexOptions.CultureInvariant)]
    private static partial Regex AdvanceQuestion();

    [GeneratedRegex(@"\A(?:dat cho|thoi luong dat cho) toi (?:(?<minimum>thieu)|da) (?:(?:bao nhieu|may) (?:phut|gio)|[0-9]{1,3} (?:phut|gio)(?: dung khong| phai khong)?)\z|\A(?:is the )?(?:(?<minimum>minimum)|maximum) (?:booking|reservation) duration(?: [0-9]{1,3} (?:minutes|hours))?\z", RegexOptions.CultureInvariant)]
    private static partial Regex DurationQuestion();

    [GeneratedRegex(@"\A[\p{L}\p{M}0-9 ?!]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex PublicQuestionCharacters();

    [GeneratedRegex(@"\A(?:neu )?huy(?: dat cho| booking| cho)? truoc (?:[0-9]{1,3} (?:phut|gio)|nua tieng)(?: thi)? co (?:duoc hoan tien|lay lai tien) khong\z|\A(?:do i get a refund if i cancel|can i get a refund if i cancel) [0-9]{1,3} (?:minutes|hours) before (?:booking start|the reservation starts)\z", RegexOptions.CultureInvariant)]
    private static partial Regex RefundTimingQuestion();

    [GeneratedRegex(@"\A(?:what happens|what do i do) if i stay past my reservation\z", RegexOptions.CultureInvariant)]
    private static partial Regex GenericOverstayQuestion();
}
