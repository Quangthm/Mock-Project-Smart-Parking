using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Guest;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed partial class ReadTools(ICurrentActor current, IOwnerReadTools owner, ReadToolOptions options, TimeProvider clock, IChatStore store)
{
    private const string DetailPattern = @"^(?:reservation details|chi tiet lich dat) ([a-f0-9-]{36})$";
    private const string DatePattern = @"^(?:my reservations|my bookings|lich dat) (\d{4}-\d{2}-\d{2})(?: (?:to|den) (\d{4}-\d{2}-\d{2}))?$";

    private static bool IsVehicles(string text) =>
        Regex.IsMatch(text, @"\A(?:(?:show(?: me)?|list) )?my (?:(?:saved|registered) )?vehicles\z") ||
        Regex.IsMatch(text, @"\A(?:(?:cho(?: (?:toi|minh|tui))? )?(?:xem|coi)(?: giup(?: (?:toi|minh|tui))?)? |danh sach )?" +
            @"xe cua (?:toi|minh|tui)(?: (?:voi|nhe|nha|di))?\z") ||
        text is "toi da dang ky nhung xe nao" or "toi da dang ky xe nao" or "toi co nhung xe nao"
        or "xe toi da dang ky" or "nhung xe toi da dang ky";
    private static bool IsOwnList(string text) => Regex.IsMatch(text,
        @"\A(?:(?:show(?: me)?|list) )?my (?:reservations|bookings)\z|\A(?:(?:cho (?:(?:toi|minh|tui) )?|giup (?:toi|minh|tui) )?(?:xem|coi)(?: giup(?: (?:toi|minh|tui))?)? )?(?:lich dat(?: cho)?|lich giu cho|lich booking) cua (?:toi|minh|tui)(?: (?:voi|nhe|di))?\z");
    private static bool IsTomorrow(string text) => text is "ngay mai toi co lich dat khong" or "lich dat ngay mai"
        or "my reservations tomorrow" or "my bookings tomorrow" or "do i have any bookings tomorrow";
    private static bool IsMoreReservations(string text) => text is "more reservations" or "lich dat tiep theo";
    private static bool IsSearch(string text) => ParkingSearch.IsRequest(text) || text is "more parking" or "tim bai tiep theo";

    internal static bool IsReservationRead(string text) => IsOwnList(text) || IsTomorrow(text) ||
        IsMoreReservations(text) || Regex.IsMatch(text, DetailPattern) || Regex.IsMatch(text, DatePattern);

    internal static bool CanRead(string text) => IsVehicles(text) || IsReservationRead(text) || IsSearch(text);

    internal async Task<GuestReply> Answer(Guid account, Conversation conversation, Guid messageId, string text, Func<CancellationToken, Task> dispatch, CancellationToken ct)
    {
        if (IsSearch(text))
            return await Search(account, conversation, messageId, text, dispatch, ct);
        var vehicles = IsVehicles(text);
        var detail = Regex.Match(text, DetailPattern);
        Require(account);
        var en = conversation.Locale == "en";
        if (vehicles && !options.Vehicles || !vehicles && !options.Reservations)
            return Reply("unavailable", (vehicles, en) switch
            {
                (true, true) => "This read tool is not connected.",
                (true, false) => "Công cụ tra cứu này chưa được kết nối.",
                (false, true) => "Reservation lookup is not connected. On the Driver dashboard, use Current booking for upcoming reservations or Booking history for past ones.",
                _ => "Tra cứu lịch đặt chưa được kết nối. Trên màn hình Driver, chọn Current booking để xem lịch sắp tới hoặc Booking history để xem lịch đã qua."
            });
        try
        {
            if (vehicles)
            {
                await dispatch(ct); Require(account); ct.ThrowIfCancellationRequested();
                var items = await owner.Vehicles(ct); Require(account);
                var vehicleContent = items.Length == 0
                    ? (en ? "No vehicles are available to view for this account." : "Không tìm thấy xe được phép xem cho tài khoản hiện tại.")
                    : (en ? "Vehicles authorized by your account service." : "Danh sách xe do dịch vụ tài khoản cho phép xem.");
                return Reply(items.Length == 0 ? "empty" : "answered", vehicleContent,
                    [new VehicleCard("vehicles", items.Take(20).ToArray(), clock.GetUtcNow(), items.Length > 20)]);
            }
            if (detail.Success)
            {
                if (!Guid.TryParse(detail.Groups[1].Value, out var id) || id == Guid.Empty) return Reply("invalid", "Invalid reservation reference.");
                await dispatch(ct); Require(account); ct.ThrowIfCancellationRequested();
                var fresh = await owner.Detail(id, ct); Require(account);
                return Reply("answered", en ? "Fresh reservation detail." : "Chi tiết lịch đặt vừa được đọc lại.", [new ReservationCard("reservation", fresh.Reservation)]);
            }
            DateTimeOffset? from = null, to = null; string? cursor = null;
            if (IsMoreReservations(text))
            {
                var turns = await store.Context(new ChatScope(account, "driver"), conversation.Id, ct);
                var remaining = 12000;
                var bounded = turns.Take(6).TakeWhile(t => (remaining -= t.Question.Length + t.Answer.Content.Length) >= 0);
                var page = bounded.SelectMany(t => t.Answer.Cards).Select(PageCard).FirstOrDefault(p => p is not null);
                if (page?.NextCursor is null) return Reply("clarification", en ? "No next page is available. Request the date range again." : "Không có trang tiếp theo. Hãy yêu cầu lại khoảng ngày.");
                (from, to, cursor) = (page.From, page.To, page.NextCursor);
            }
            else if (IsTomorrow(text))
            {
                var window = ReservationDayWindow.Tomorrow(clock, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
                (from, to) = (window.From, window.To);
            }
            else if (!IsOwnList(text))
            {
                var dates = Regex.Match(text, DatePattern);
                if (dates.Success && DateOnly.TryParseExact(dates.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date < DateOnly.MaxValue)
                {
                    var last = date;
                    if (dates.Groups[2].Success && (!DateOnly.TryParseExact(dates.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out last) || last < date || last == DateOnly.MaxValue))
                        return Reply("invalid", en ? "Invalid date range." : "Khoảng ngày không hợp lệ.");
                    var window = ReservationDayWindow.ForDate(date, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
                    var end = ReservationDayWindow.ForDate(last, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
                    (from, to) = (window.From, end.To);
                }
                else
                    return Reply("clarification", en ? "Specify a date as YYYY-MM-DD (Asia/Ho_Chi_Minh), or say my reservations tomorrow." : "Hãy ghi ngày YYYY-MM-DD (Asia/Ho_Chi_Minh), hoặc hỏi lịch đặt ngày mai.");
            }
            await dispatch(ct); Require(account); ct.ThrowIfCancellationRequested();
            var result = await owner.Reservations(from, to, cursor, ct); Require(account);
            var cards = result.Items.Select(r => (object)new ReservationCard("reservation", r))
                .Append(new ReservationPageCard("reservation_page", result.NextCursor, from, to, result.AsOf)).ToArray();
            // At most 19 result cards plus paging metadata, five safe actions; each displayed ID remains owner-issued.
            var actions = result.Items.Take(5).Select(r => new GuestAction("view_reservation", r.Id)).ToArray();
            var range = from is null ? "" : $" [{from:O}, {to:O}) UTC; Asia/Ho_Chi_Minh.";
            var content = result.Items.Length == 0
                ? (en ? "No reservations found for this request." : "Không tìm thấy lịch đặt cho yêu cầu này.")
                : (en ? "Reservation records from the owner service." : "Lịch đặt từ dịch vụ quản lý đặt chỗ.");
            return Reply(result.Items.Length == 0 ? "empty" : "answered", content + range, cards, actions);
        }
        catch (OwnerReadFailure error)
        {
            return Reply(error.Code, error.Code switch
            {
                "login_required" => en ? "Please sign in again to continue." : "Bạn hãy đăng nhập lại để tiếp tục tra cứu.",
                "forbidden" => en ? "Your account cannot access this data." : "Tài khoản của bạn không có quyền xem dữ liệu này.",
                "not_found" when detail.Success => en ? "Reservation not found." : "Không tìm thấy lịch đặt này.",
                _ => en ? "The owner service could not complete this read." : "Dịch vụ dữ liệu gặp lỗi khi tra cứu."
            });
        }
    }
    private void Require(Guid account)
    {
        var actor = current.Actor;
        if (actor is null || actor.AccountId != account || actor.ExpiresAt <= clock.GetUtcNow()) throw new ChatFailure(401, "INVALID_TOKEN");
        if (actor.Role != "driver") throw new ChatFailure(403, "ASSISTANT_FORBIDDEN");
    }
    private static GuestReply Reply(string outcome, string content, object[]? cards = null, GuestAction[]? actions = null) => new(Guid.Empty, "system", outcome, content, [], cards ?? [], actions ?? []);
    private static ReservationPageCard? PageCard(object card)
    {
        if (card is ReservationPageCard page) return page;
        if (card is System.Text.Json.JsonElement json)
        {
            try { var p = json.Deserialize<ReservationPageCard>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)); return p?.Type == "reservation_page" ? p : null; }
            catch (System.Text.Json.JsonException) { }
        }
        return null;
    }
    internal static string Normalize(string message) => string.Join(' ', string.Concat(AnswerLanguage.Question(message).Normalize(NormalizationForm.FormD).ToLowerInvariant().Replace('đ', 'd')
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Trim().TrimEnd('?', '.', '!').Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
