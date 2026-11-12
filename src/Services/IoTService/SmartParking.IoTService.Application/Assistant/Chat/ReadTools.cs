using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Authorization;
using SmartParking.IoTService.Application.Assistant.Guest;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed record ReadToolOptions(bool Vehicles = false, bool Reservations = false, bool Search = false, int SearchMaxAgeSeconds = 0, int SearchSkewSeconds = 0);
public sealed class OwnerReadFailure(string code) : Exception { public string Code { get; } = code; }
public sealed record VehicleSummary(Guid Id, string VehicleType, string DisplayLabel);
public sealed record ReservationVehicle(string VehicleType, string? DisplayLabel);
public sealed record ReservationSummary(Guid Id, Guid SiteId, ReservationVehicle? Vehicle, DateTimeOffset StartAt,
    DateTimeOffset EndAt, string Status, DateTimeOffset AsOf);
public sealed record ReservationPage(ReservationSummary[] Items, string? NextCursor, DateTimeOffset AsOf);
public sealed record ReservationDetail(ReservationSummary Reservation, DateTimeOffset AsOf);
public sealed record VehicleCard(string Type, VehicleSummary[] Items, DateTimeOffset AsOf, bool Truncated);
public sealed record ReservationCard(string Type, ReservationSummary Reservation);
public sealed record ReservationPageCard(string Type, string? NextCursor, DateTimeOffset? From, DateTimeOffset? To, DateTimeOffset AsOf);
public interface IOwnerReadTools
{
    Task<ParkingSearchPage> Search(ParkingSearch query, string? cursor, CancellationToken ct);
    Task<VehicleSummary[]> Vehicles(CancellationToken ct);
    Task<ReservationPage> Reservations(DateTimeOffset? from, DateTimeOffset? to, string? cursor, CancellationToken ct);
    Task<ReservationDetail> Detail(Guid id, CancellationToken ct);
}

public sealed partial class ReadTools(ICurrentActor current, IOwnerReadTools owner, ReadToolOptions options, TimeProvider clock, IChatStore store)
{
    public async Task<GuestReply?> TryAnswer(Guid account, Conversation conversation, Guid messageId, string message, Func<CancellationToken, Task> dispatch, CancellationToken ct)
    {
        var text = Normalize(message);
        var vehicles = text is "my vehicles" or "show my vehicles" or "xe cua toi" or "danh sach xe cua toi";
        var reservations = text.Contains("lich dat", StringComparison.Ordinal) || text.Contains("my reservations", StringComparison.Ordinal) || text.Contains("my bookings", StringComparison.Ordinal) || text == "do i have any bookings tomorrow";
        var detail = Regex.Match(text, @"^(?:reservation details|chi tiet lich dat) ([a-f0-9-]{36})$");
        var next = text is "more reservations" or "lich dat tiep theo";
        if (ParkingSearch.IsRequest(text) || text is "more parking" or "tim bai tiep theo")
            return await Search(account, conversation, messageId, text, dispatch, ct);
        if (!vehicles && !reservations && !detail.Success && !next) return null;
        Require(account);
        var en = conversation.Locale == "en";
        if (vehicles && !options.Vehicles || !vehicles && !options.Reservations)
            return Reply("unavailable", en ? "This read tool is not connected." : "Công cụ tra cứu này chưa được kết nối.");
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
            if (next)
            {
                var turns = await store.Context(new ChatScope(account, "driver"), conversation.Id, ct);
                var remaining = 12000;
                var bounded = turns.Take(6).TakeWhile(t => (remaining -= t.Question.Length + t.Answer.Content.Length) >= 0);
                var page = bounded.SelectMany(t => t.Answer.Cards).Select(PageCard).FirstOrDefault(p => p is not null);
                if (page?.NextCursor is null) return Reply("clarification", en ? "No next page is available. Request the date range again." : "Không có trang tiếp theo. Hãy yêu cầu lại khoảng ngày.");
                (from, to, cursor) = (page.From, page.To, page.NextCursor);
            }
            else if (text is "ngay mai toi co lich dat khong" or "lich dat ngay mai" or "my reservations tomorrow" or "my bookings tomorrow" or "do i have any bookings tomorrow")
            {
                var window = ReservationDayWindow.Tomorrow(clock, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
                (from, to) = (window.From, window.To);
            }
            else
            {
                var dates = Regex.Match(text, @"^(?:my reservations|my bookings|lich dat) (\d{4}-\d{2}-\d{2})(?: (?:to|den) (\d{4}-\d{2}-\d{2}))?$");
                if (dates.Success && DateOnly.TryParseExact(dates.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date < DateOnly.MaxValue)
                {
                    var last = date;
                    if (dates.Groups[2].Success && (!DateOnly.TryParseExact(dates.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out last) || last < date || last == DateOnly.MaxValue))
                        return Reply("invalid", en ? "Invalid date range." : "Khoảng ngày không hợp lệ.");
                    var window = ReservationDayWindow.ForDate(date, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
                    var end = ReservationDayWindow.ForDate(last, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
                    (from, to) = (window.From, end.To);
                }
                else if (text is not ("my reservations" or "my bookings" or "lich dat cua toi"))
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
    private static string Normalize(string message) => string.Join(' ', string.Concat(message.Normalize(NormalizationForm.FormD).ToLowerInvariant().Replace('đ', 'd')
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Trim().TrimEnd('?', '.', '!').Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
