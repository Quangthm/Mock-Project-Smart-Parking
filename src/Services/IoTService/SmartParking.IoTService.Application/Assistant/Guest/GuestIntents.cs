namespace SmartParking.IoTService.Application.Assistant.Guest;

public static class GuestIntents
{
    internal const string ParkingTimeClarification = "parking-time-clarification";
    internal const string ParkingOverstay = "parking-overstay";
    internal const string LateArrival = "late-arrival";
    internal const string ParkingOverstayQuestion = "Đỗ xe quá giờ đã đặt thì khoản phí phát sinh được xác định thế nào?";

    internal static string? ParkingTimeGuidance(string intent, bool english) => intent switch
    {
        ParkingTimeClarification => english
            ? "Are you asking about arriving after your booking starts, or staying parked after it ends?"
            : "Bạn muốn hỏi về việc đến bãi sau giờ bắt đầu đặt chỗ, hay đỗ xe quá giờ kết thúc?",
        LateArrival => english
            ? "Check the status and terms of your booking. If you are unsure whether your space is still held, contact the parking facility before heading there."
            : "Bạn hãy kiểm tra trạng thái và điều khoản trong chi tiết đặt chỗ. Nếu chưa rõ bãi còn giữ chỗ hay không, hãy liên hệ bãi trước khi đến.",
        _ => null
    };

    internal static string ArticleId(string intent) => intent == ParkingOverstay ? "payment-guide" : intent;

    public static string Classify(string message, IReadOnlyList<PublicArticle>? articles = null)
    {
        var question = PublicFaqRetrieval.Normalize(message);
        if (RestrictedIntent(message, question) is { } restricted) return restricted;
        var text = $" {question} ";
        if (Has(text, "do xe qua gio", "do qua gio", "gui xe qua gio", "de xe qua gio", "lay xe tre", "lay xe muon")) return ParkingOverstay;
        if (question is "den tre" or "den muon" || Has(text, "den bai tre", "den bai muon", "den bai sau gio bat dau")) return LateArrival;
        if (Has(text, "de xe tre", "gui xe tre", "do xe tre", "de xe muon", "gui xe muon", "do xe muon") &&
            !Has(text, "den tre", "den muon", "qua gio", "gio ket thuc", "lay xe")) return ParkingTimeClarification;
        const string brandPrefix = "smartpark ";
        var topicQuestion = question.StartsWith(brandPrefix, StringComparison.Ordinal) ? question[brandPrefix.Length..] : question;
        foreach (var (topic, phrases) in Topics)
            if (phrases.Contains(question, StringComparer.Ordinal) || phrases.Contains(topicQuestion, StringComparer.Ordinal)) return topic;
        return articles is null ? "unknown" : PublicFaqRetrieval.Find(message, articles)?.Id ?? "unknown";
    }

    internal static string? RestrictedIntent(string message, string? normalized = null)
    {
        var text = $" {normalized ?? PublicFaqRetrieval.Normalize(message)} ";
        if (Has(text, "my bookings", "my reservations", "my vehicles", "my booking", "my reservation", "my vehicle", "my car", "my account", "cua toi", "cua minh", "xe toi", "xe minh", "lich dat", "book for me", "dat giup", "tao booking")) return "private";
        if (Has(text, "nearby", "near me", "gan day", "gan toi", "con cho", "cho trong", "available spaces", "availability", "how much", "bao nhieu", "price now", "gia hien tai", "list parking", "danh sach bai")) return "dynamic";
        if (message.Any(char.IsDigit) || message.Contains('@') || Has(text, "password", "mat khau", "otp", "token", "bearer", "secret", "api key")) return "sensitive";
        if (Has(text, "hotline", "address", "dia chi", "partner", "doi tac", "phone number", "so dien thoai",
            "opening hours", "gio lam viec", "founded", "founder", "sang lap")) return "unknown";
        return null;
    }
    private static readonly (string Topic, string[] Phrases)[] Topics =
    [
        ("smartpark-overview", [
            "smartpark", "smartpark la gi", "what is smartpark", "gioi thieu smartpark", "hello", "xin chao",
            "how do smartpark", "smartpark who", "who is smartpark", "how does smartpark work",
            "what does smartpark do", "tell me about smartpark", "smartpark hoat dong nhu the nao",
            "smartpark ho tro gi", "smartpark ho tro nhung gi", "smartpark lam duoc gi",
            "what can smartpark help with", "how can smartpark help me"
        ]),
        ("vehicle-guide", ["loai xe nao", "smartpark phuc vu loai xe nao", "which vehicle types", "which vehicle types does smartpark cover", "vehicle types", "cac loai xe"]),
        ("booking-guide", ["booking", "reservation", "how to book", "how do i book", "booking guidance", "reserve a spot", "prebook parking", "huong dan dat cho", "dat cho nhu the nao", "dat cho nhu nao", "cach dat cho", "dat xe nhu nao", "dat xe nhu the nao", "cach dat xe"]),
        ("discovery-guide", ["how to find parking", "find parking", "search parking", "huong dan tim bai", "cach tim bai", "tim bai nhu the nao"]),
        ("payment-guide", ["payment", "payment guidance", "how to pay", "thanh toan the nao", "nop phi nhu the nao", "huong dan thanh toan", "hoan tien", "refund guidance"]),
        ("support-guide", ["support", "contact support", "can ho tro", "huong dan ho tro", "how to get support", "lien he ho tro"]),
        ("assistant-limits", ["assistant limitations", "gioi han tro ly", "what can the assistant do", "tro ly lam duoc gi"])
    ];
    private static bool Has(string text, params string[] phrases) => phrases.Any(p => text.Contains(" " + p + " ", StringComparison.Ordinal));
}
