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

    internal static string ArticleId(string intent) => PublicFaqIntents.Find(intent)?.ArticleId ?? intent;

    internal static string? CanonicalQuestion(string intent) => PublicFaqIntents.Find(intent)?.Question;

    internal static bool UsePublishedAnswer(string intent, PublicArticle article, string locale) =>
        intent == "booking-navigation" ||
        (article.Id == "vehicle-guide" && (article.SourceLocale is null || article.SourceLocale == locale));

    internal static string KnowledgeContent(string intent, string content, bool en)
    {
        var paragraphStart = intent switch
        {
            "booking-advance" or "booking-minimum-duration" or "booking-maximum-duration" =>
                en ? "Published defaults include" : "Các mặc định được công bố",
            "refund-status-guidance" => "Hủy booking, chấp thuận hoàn và thực trả tiền hoàn",
            "lost-parking-ticket" => "Nếu mất vé, cần Operator",
            "parking-entry-confirmation" => "Khi tới cổng, hãy theo hướng dẫn",
            _ => null
        };
        if (paragraphStart is not null)
        {
            var paragraphs = content.Split("\n\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var selected = paragraphs.FirstOrDefault(paragraph => paragraph.StartsWith(paragraphStart, StringComparison.Ordinal));
            if (selected is not null)
            {
                const string languageNotice = "The answer is currently available in Vietnamese only.";
                return paragraphs[0] == languageNotice ? languageNotice + "\n\n" + selected : selected;
            }
        }
        var opening = intent switch
        {
            "assistant-identity" => en ? "I’m SmartPark’s AI assistant." : "Mình là trợ lý AI của SmartPark.",
            "discovery-navigation" or "booking-navigation" =>
                en ? "Open Dashboard and go to Search Parking." : "Mở Dashboard và vào Search Parking.",
            _ => null
        };
        return opening is null ? content : opening + "\n\n" + content;
    }

    public static string Classify(string message, IReadOnlyList<PublicArticle>? articles = null) =>
        Resolve(message, articles) switch
        {
            "outside" or "multiple" or "foreign-account" => "unknown",
            "booking-cancel-action" or "booking-create-action" or BusinessDataHandoff.ParkingPlates or BusinessDataHandoff.ParkingRevenue => "private",
            var intent => intent
        };

    internal static string Resolve(string message, IReadOnlyList<PublicArticle>? articles)
    {
        message = AnswerLanguage.Question(message);
        var question = PublicWorkflowQuestions.NormalizeWording(PublicFaqRetrieval.Normalize(message));
        var workflowQuestion = PublicWorkflowQuestions.Focus(question);
        if (PublicWorkflowQuestions.AsksForAnotherAccountsRecords(question)) return "foreign-account";
        if (PublicWorkflowQuestions.HasMultipleQuestions(message, question)) return "multiple";
        if (PublicWorkflowQuestions.OutsideParking(workflowQuestion)) return "outside";
        if (PublicQuestionPatterns.Match(message) is { } matched)
            return matched == "vehicle-navigation" || articles?.Any(a => a.Id == ArticleId(matched)) == true
                ? matched : "unknown";
        if (PublicFaqIntents.MatchPublicPolicy(message, question, articles) is { } policy) return policy.Id;
        if (PublicWorkflowQuestions.MatchPublicGuidance(workflowQuestion) is { } guidance &&
            (guidance is "reservation-navigation" or "driver-signup-navigation" ||
                articles?.Any(article => article.Id == ArticleId(guidance)) == true)) return guidance;
        if (PublicWorkflowQuestions.MatchAction(question) is { } action) return action;
        if (BusinessDataHandoff.Match(question) is { } business)
        {
            if (PublicWorkflowQuestions.Match(message, workflowQuestion) is "lost-parking-ticket" &&
                articles?.Any(article => article.Id == ArticleId("lost-parking-ticket")) == true)
                return "lost-parking-ticket";
            return business;
        }
        if (PublicWorkflowQuestions.AsksLiveParking(question)) return "dynamic";
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
        if (articles is null) return "unknown";
        if (PublicFaqIntents.MatchParaphrase(message, text, articles) is { } paraphrase) return paraphrase;
        if (PublicWorkflowQuestions.Match(message, workflowQuestion) is { } workflow &&
            articles.Any(article => article.Id == ArticleId(workflow))) return workflow;
        return PublicFaqRetrieval.Find(message, articles)?.Id ?? "unknown";
    }

    internal static GuestReply? LocalReply(string intent, Guid requestId, bool en, string? role = null) => intent switch
    {
        "multiple" => new(requestId, "system", "clarification", en
            ? "Which request would you like help with first?"
            : "Bạn muốn mình hỗ trợ yêu cầu nào trước?", [], [], []),
        "outside" => new(requestId, "system", "clarification", en
            ? "I can help with SmartPark and its parking services."
            : "Mình có thể hỗ trợ về SmartPark và dịch vụ gửi xe trên hệ thống.", [], [], []),
        "foreign-account" => new(requestId, "system", "clarification", en
            ? "Only the account with access can view those records in SmartPark."
            : "Chỉ tài khoản có quyền mới xem được dữ liệu đó trong SmartPark.", [], [], []),
        "driver-signup-navigation" => new(requestId, "system", "answered", en
            ? "On the SmartPark home page, select Sign Up and choose Driver on the Create Account screen."
            : "Trên trang chủ SmartPark, chọn Sign Up rồi chọn Driver ở màn hình Create Account.", [], [], []),
        "reservation-navigation" => new(requestId, "system", "answered", (role, en) switch
        {
            ("driver", true) => "Select Current booking on the Driver dashboard to see your reservations.",
            ("driver", false) => "Chọn Current booking trên màn hình Driver để xem lịch đặt của bạn.",
            (_, true) => "Sign in as a Driver and select Current booking to see that account’s reservations.",
            _ => "Đăng nhập bằng tài khoản Driver và chọn Current booking để xem lịch đặt của tài khoản đó."
        }, [], [], []),
        "booking-cancel-action" or "booking-create-action" => BookingHandoff(intent, requestId, en, role),
        _ => BusinessDataHandoff.LocalReply(intent, requestId, en, role)
    };

    private static GuestReply BookingHandoff(string intent, Guid requestId, bool en, string? role)
    {
        var content = (role, intent, en) switch
        {
            ("driver", "booking-cancel-action", true) => "Select Current booking to review the reservation and its terms. Contact the parking facility for help with a cancellation.",
            ("driver", "booking-cancel-action", false) => "Chọn Current booking để kiểm tra lịch đặt và điều khoản. Liên hệ bãi đỗ để được hướng dẫn hủy.",
            ("driver", _, true) => "Open Search Parking on your dashboard, choose a facility and review the booking terms on the reservation form before confirming.",
            ("driver", _, false) => "Mở Search Parking trên Dashboard, chọn bãi và kiểm tra điều khoản trên form đặt chỗ trước khi xác nhận.",
            (null, _, true) => "Sign in as a Driver to use the booking screens.",
            (null, _, false) => "Đăng nhập bằng tài khoản Driver để sử dụng màn hình đặt chỗ.",
            (_, _, true) => "Use a Driver account to manage its own reservations in the booking screens.",
            _ => "Dùng tài khoản Driver để quản lý lịch đặt của chính tài khoản đó trên màn hình đặt chỗ."
        };
        return new(requestId, "system", "handoff", content, [], [], role is null ? [new("login")] : []);
    }

    internal static string? RestrictedIntent(string message, string? normalized = null)
    {
        var text = $" {normalized ?? PublicFaqRetrieval.Normalize(message)} ";
        if (message.Contains('@') || Has(text, "password", "mat khau", "otp", "token", "bearer", "secret", "api key")) return "sensitive";
        if (Has(text, "my bookings", "my reservations", "my vehicles", "my saved vehicles", "my registered vehicles", "my booking", "my reservation", "my vehicle", "my car", "my account", "cua toi", "cua minh", "cua nguoi khac", "someone else", "another account", "xe toi", "xe minh", "xe tui", "lich dat", "book for me", "dat giup", "tao booking")) return "private";
        if (Has(text, "nearby", "near me", "gan day", "gan toi", "con cho", "cho trong", "available spaces", "availability", "how much", "bao nhieu", "price now", "gia hien tai", "list parking", "danh sach bai")) return "dynamic";
        if (Has(text, "con nhan xe", "accepting vehicles", "accepting cars") &&
            Has(text, "luc nay", "bay gio", "hien tai", "ngay bay gio", "right now", "now")) return "dynamic";
        if (message.Any(char.IsDigit)) return "sensitive";
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
        ("assistant-limits", ["assistant limitations", "gioi han tro ly", "what can the assistant do", "tro ly lam duoc gi", "how can the smartpark assistant help a driver"])
    ];
    private static bool Has(string text, params string[] phrases) => phrases.Any(p => text.Contains(" " + p + " ", StringComparison.Ordinal));
}
