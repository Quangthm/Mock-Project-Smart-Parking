using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Guest;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed partial class ReadTools
{
    private async Task<GuestReply> Search(Guid account, Conversation conversation, Guid messageId, string text,
        Func<CancellationToken, Task> dispatch, CancellationToken ct)
    {
        Require(account);
        var english = conversation.Locale == "en" && !ParkingSearch.IsVietnameseRequest(text);
        if (!options.Search)
            return Reply("unavailable", english ? "Live parking search is unavailable." : "Tra cứu bãi đỗ hiện chưa khả dụng.");
        string? cursor = null;
        var original = messageId;
        if (text is "more parking" or "tim bai tiep theo")
        {
            var remaining = 12000;
            var context = (await store.Context(new ChatScope(account, "driver"), conversation.Id, ct)).Take(6)
                .TakeWhile(t => (remaining -= t.Question.Length + t.Answer.Content.Length) >= 0).ToArray();
            var last = context.FirstOrDefault(t => ParkingSearch.IsRequest(Normalize(t.Question)) ||
                Normalize(t.Question) is "more parking" or "tim bai tiep theo");
            var metadata = last?.Answer.Cards.Select(SearchMetadata).FirstOrDefault(m => m is not null);
            var initial = context.FirstOrDefault(t => t.Answer.ClientMessageId == metadata?.SearchMessageId);
            if (metadata?.NextCursor is null || initial is null)
                return Reply("clarification", english ? "Start a new search with your origin." : "Hãy tìm lại với điểm xuất phát.");
            cursor = metadata.NextCursor;
            original = metadata.SearchMessageId;
            text = Normalize(initial.Question);
        }
        var query = ParkingSearch.Parse(text);
        if (query is null)
            return Reply("clarification", ParkingSearch.Clarification(text, english));
        try
        {
            await dispatch(ct);
            Require(account);
            ct.ThrowIfCancellationRequested();
            var page = await owner.Search(query, cursor, ct);
            Require(account);
            var cards = page.Items.Select(site => (object)new ParkingCard("parking", site, page.AsOf))
                .Append(new ParkingPageCard("parking_page", page.NextCursor, original, page.AsOf)).ToArray();
            return Reply(page.Items.Length == 0 ? "empty" : "answered",
                page.Items.Length == 0
                    ? (english ? "No parking sites were confirmed to match these filters." : "Không tìm thấy bãi nào được xác nhận phù hợp với bộ lọc đã gửi.")
                    : (english ? "Parking search results from SmartPark." : "Kết quả tìm bãi do SmartPark cung cấp."), cards);
        }
        catch (OwnerReadFailure error)
        {
            return Reply(error.Code, error.Code switch
            {
                "invalid" => english ? "The parking search API rejected the filters. Check the location, filters and time range." : "API tìm bãi từ chối bộ lọc. Hãy kiểm tra tọa độ, bộ lọc và khung giờ.",
                "timeout" => english ? "The parking search API timed out. Please try again." : "API tìm bãi phản hồi quá thời gian chờ. Bạn có thể thử lại.",
                _ => english ? "The parking search API could not return results. Please try again." : "API tìm bãi gặp lỗi khi trả kết quả. Bạn có thể thử lại."
            });
        }
    }

    private static ParkingPageCard? SearchMetadata(object card)
    {
        if (card is ParkingPageCard page) return page;
        if (card is JsonElement { ValueKind: JsonValueKind.Object } json &&
            (json.TryGetProperty("type", out var type) || json.TryGetProperty("Type", out type)) && type.GetString() == "parking_page")
            return json.Deserialize<ParkingPageCard>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return null;
    }
}
