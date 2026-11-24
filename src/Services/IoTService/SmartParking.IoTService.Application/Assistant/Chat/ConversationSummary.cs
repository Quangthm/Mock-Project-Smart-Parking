using SmartParking.IoTService.Application.Assistant.Guest;
using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public static class ConversationSummary
{
    public static bool Matches(string message)
    {
        var text = PublicFaqRetrieval.Normalize(AnswerLanguage.Question(message));
        return Regex.IsMatch(text,
            @"\A(?:(?:please|can you|could you|would you|can you please|could you please) )?(?:(?:summarize|summarise|recap) (?:this conversation|our conversation|the conversation|our chat|this chat|my questions|what i asked(?: you)?(?: in this conversation)?|what we discussed|what we talked about)|(?:give me|provide) a summary of (?:this conversation|our chat|my questions))(?: please)?\z",
            RegexOptions.CultureInvariant) || Regex.IsMatch(text,
            @"\A(?:(?:hay|vui long|ban hay|ban co the) )?tom tat (?:nhung cau hoi toi da hoi(?: trong cuoc tro chuyen nay)?|nhung gi (?:toi|minh) da hoi|(?:noi dung )?(?:cuoc tro chuyen|hoi thoai) nay|cac cau hoi cua (?:toi|minh)|nhung gi chung ta da trao doi)(?: bang tieng viet)?(?: nhe)?\z",
            RegexOptions.CultureInvariant);
    }

    public static GuestReply Reply(IReadOnlyList<ContextTurn> turns, string locale)
    {
        var english = locale == "en";
        // Reuse the existing scoped six-turn context. Emit only fixed topic descriptions,
        // never copy questions, answer text, identifiers, cards or historical policy claims.
        var topics = turns.Take(6).Reverse().Where(turn => !Matches(turn.Question))
            .Select(turn => Topic(turn, english)).Distinct(StringComparer.Ordinal).ToArray();
        var content = topics.Length == 0
            ? english ? "There are no earlier completed exchanges with topics to summarize in the available recent context."
                : "Ngữ cảnh gần đây hiện có chưa có chủ đề từ lượt trao đổi hoàn tất trước đó để tóm tắt."
            : (english ? "From the recent completed exchanges in this conversation, you asked about\n\n"
                : "Trong các lượt trao đổi gần đây đã hoàn tất của cuộc trò chuyện này, bạn đã hỏi về\n\n") +
                string.Join("\n", topics.Select(topic => "- " + topic));
        return new(Guid.Empty, "system", "answered", content, [], [], []);
    }

    private static string Topic(ContextTurn turn, bool english)
    {
        var topic = turn.Answer.Sources.FirstOrDefault()?.ArticleId;
        if (topic is null)
            topic = PublicQuestionPatterns.Match(AnswerLanguage.Question(turn.Question)) ?? GuestIntents.Classify(turn.Question);
        topic = GuestIntents.ArticleId(topic);
        return topic switch
        {
            "vehicle-guide" => english ? "The vehicle types SmartPark supports."
                : "Các loại xe SmartPark hỗ trợ.",
            "booking-guide" => english ? "Parking booking guidance and conditions."
                : "Hướng dẫn và điều kiện đặt chỗ.",
            "cancellation-refund-guide" => english ? "Booking cancellation and refund guidance." : "Hướng dẫn hủy đặt chỗ và hoàn tiền.",
            "walk-in-and-entry-guide" => english ? "Parking without a booking and entering the facility." : "Gửi xe không đặt trước và vào bãi.",
            "arrival-and-no-show-guide" => english ? "Arrival timing and missed bookings." : "Thời điểm đến và không đến theo booking.",
            "pass-cancellation-guide" => english ? "Cancelling parking passes." : "Hủy gói gửi xe.",
            "checkout-and-departure-guide" => english ? "Checking out and leaving the facility." : "Thanh toán ra bãi và rời bãi.",
            "notification-guide" => english ? "Booking and transaction notifications." : "Thông báo đặt chỗ và giao dịch.",
            "vehicle-navigation" => english ? "Where to view your registered vehicles in My Vehicles."
                : "Nơi xem xe đã đăng ký trong mục My Vehicles.",
            "payment-guide" => english ? "Payment and refund guidance." : "Hướng dẫn thanh toán và hoàn tiền.",
            "discovery-guide" => english ? "How to find parking." : "Cách tìm bãi đỗ xe.",
            "smartpark-overview" => english ? "What SmartPark does and how to use it." : "SmartPark là gì và cách sử dụng.",
            "support-guide" => english ? "How to get support." : "Cách nhận hỗ trợ.",
            "assistant-limits" => english ? "The assistant's capabilities and limitations." : "Khả năng và giới hạn của trợ lý.",
            "private" => english ? "Account-specific information or actions." : "Thông tin hoặc thao tác riêng của tài khoản.",
            "dynamic" => english ? "Current parking information, prices or availability." : "Thông tin bãi, giá hoặc chỗ trống hiện tại.",
            "sensitive" => english ? "A question containing sensitive details, which are omitted here." : "Một câu hỏi có thông tin nhạy cảm được lược bỏ ở đây.",
            "parking-time-clarification" or "late-arrival" or "parking-overstay" => english
                ? "Arrival timing or parking beyond the booked time." : "Thời điểm đến bãi hoặc đỗ xe quá giờ đã đặt.",
            _ => english ? "Other questions in this conversation." : "Các câu hỏi khác trong cuộc trò chuyện này."
        };
    }
}
