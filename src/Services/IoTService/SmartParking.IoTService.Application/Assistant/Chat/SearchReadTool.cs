using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Guest;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed partial class ReadTools
{
    private async Task<GuestReply> Search(Guid account, Conversation conversation, Guid messageId, string text,
        Func<CancellationToken, Task> dispatch, CancellationToken ct)
    {
        Require(account);
        var english = conversation.Locale == "en";
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
            return Reply("clarification", english ? "Enter one origin as latitude,longitude and a vehicle type, e.g. find parking car near 10.77,106.70."
                : "Hãy nhập một điểm xuất phát và loại xe, ví dụ: tìm bãi ô tô gần 10.77,106.70.");
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
                    ? (english ? "No parking sites are proven to match these filters." : "Chưa có bãi được xác nhận đáp ứng các bộ lọc này.")
                    : (english ? "Parking search results from SmartPark." : "Kết quả tìm bãi do SmartPark cung cấp."), cards);
        }
        catch (OwnerReadFailure error)
        {
            return Reply(error.Code, english ? "SmartPark could not complete this search." : "SmartPark chưa hoàn tất tra cứu.");
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
