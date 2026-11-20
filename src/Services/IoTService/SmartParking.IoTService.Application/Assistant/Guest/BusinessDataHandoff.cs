using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Guest;

internal static partial class BusinessDataHandoff
{
    internal const string ParkingPlates = "parking-plate-records";
    internal const string ParkingRevenue = "parking-revenue-records";

    internal static string? Match(string question, bool parkingContext = false)
    {
        if (question.Length > 320) return null;
        if ((parkingContext || ParkingSite().IsMatch(question)) && RevenueLookup().IsMatch(question)) return ParkingRevenue;
        if (OperationalSite().IsMatch(question) && Plate().IsMatch(question) && RecordLookup().IsMatch(question))
            return ParkingPlates;
        return null;
    }

    internal static GuestReply PersonalReservations(bool en) => Reply(Guid.Empty, en
        ? "I can’t look up personal reservations in your current role. To view your own reservations, sign in as a Driver and select Current booking for upcoming reservations or Booking history for past ones."
        : "Trợ lý không thể tra cứu lịch đặt cá nhân ở vai trò hiện tại. Hãy đăng nhập bằng tài khoản Driver và chọn Current booking để xem lịch sắp tới, hoặc Booking history để xem lịch đã qua.");

    internal static GuestReply? LocalReply(string intent, Guid requestId, bool en, string? role) => (intent, role, en) switch
    {
        (ParkingPlates, _, true) => Reply(requestId,
            "I can’t display license plates from parking operations. If your assignment includes Gate Check-Out, select your assigned lot and use the Status filter in Parking History to find the sessions you need."),
        (ParkingPlates, _, false) => Reply(requestId,
            "Trợ lý không hiển thị biển số từ dữ liệu vận hành. Nếu được phân công sử dụng Gate Check-Out, hãy chọn bãi được giao rồi dùng bộ lọc Status trong Parking History để tìm phiên gửi xe cần kiểm tra."),
        (ParkingRevenue, "owner", true) => Reply(requestId,
            "I can’t verify your lot’s live revenue. On the Owner dashboard, open Financial and filter by lot and date to review the figures available on this device."),
        (ParkingRevenue, "owner", false) => Reply(requestId,
            "Trợ lý không thể xác nhận doanh thu hiện tại của bãi. Trên màn hình Owner, mở mục Financial rồi lọc theo bãi và ngày để xem báo cáo từ dữ liệu có trên thiết bị."),
        (ParkingRevenue, _, true) => Reply(requestId,
            "I can’t look up parking lot revenue here. If you have access to financial reports, check them in your assigned workspace."),
        (ParkingRevenue, _, false) => Reply(requestId,
            "Trợ lý không tra cứu doanh thu bãi tại đây. Nếu có quyền xem báo cáo tài chính, hãy kiểm tra trong màn hình được cấp quyền."),
        _ => null
    };

    private static GuestReply Reply(Guid requestId, string content) =>
        new(requestId, "system", "handoff", content, [], [], []);

    [GeneratedRegex(@"\b(?:bai (?:do|xe|gui xe|(?:cua |ben )?(?:toi|minh)|duoc (?:giao|phan cong))|parking (?:lot|facility|garage)|car park|assigned lot|my lot revenue)\b")]
    private static partial Regex ParkingSite();

    private const string Site = @"(?:bai|lot|car park|parking facility|garage)";
    private const string Operation = @"(?:phan cong|duoc giao|phu trach|quan ly|dang truc|dang (?:gui|do|dau)|assigned|on duty|parked|(?:i|we) manage|active sessions?)";

    [GeneratedRegex(@"\b(?:trong bai|ra khoi bai|in (?:the|my|our) (?:lot|car park|parking (?:lot|garage))|" +
        Site + @"\b.*\b" + Operation + @"|" + Operation + @"\b.*\b" + Site + @")\b")]
    private static partial Regex OperationalSite();

    [GeneratedRegex(@"\b(?:bien (?:so|kiem soat)|(?:licen[cs]e |number )?plates?)\b")]
    private static partial Regex Plate();

    [GeneratedRegex(@"\b(?:xem|liet ke|tra cuu|danh sach|bao nhieu|nao|bien (?:so|kiem soat) gi|show|list|view|look up|which|what|how many)\b")]
    private static partial Regex RecordLookup();

    [GeneratedRegex(@"\b(?:doanh thu|revenue|takings)\b.*\b(?:bao nhieu|hom nay|bua nay|hom qua|thang nay|tuan nay|hien tai|today|yesterday|this (?:week|month)|current)\b|" +
        @"\b(?:xem|tra cuu|kiem tra|show|view|check|how much|what(?: s| is))\b.*\b(?:doanh thu|revenue|takings)\b|" +
        @"\bthu duoc (?:bao nhieu|may tien)\b|\bhow much (?:did|has)\b.*\b(?:make|made|earn|earned|take in)\b")]
    private static partial Regex RevenueLookup();
}
