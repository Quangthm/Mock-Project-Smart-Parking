using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Infrastructure.Assistant.Guest;

namespace SmartParking.IoTService.Tests;

public sealed partial class AuthenticatedChatTests
{
    [Theory]
    [InlineData("Hủy lịch đặt thế nào?", "cancellation-refund-guide", "yêu cầu hủy đặt chỗ qua đâu")]
    [InlineData("Hủy đặt chỗ gửi xe ở đâu?", "cancellation-refund-guide", "yêu cầu hủy đặt chỗ qua đâu")]
    [InlineData("Muốn hủy đặt chỗ gửi xe ở đâu?", "cancellation-refund-guide", "yêu cầu hủy đặt chỗ qua đâu")]
    [InlineData("Tìm bãi trên bản đồ thế nào?", "discovery-guide", "thông tin trên bản đồ")]
    [InlineData("Muốn giữ chỗ thì làm thế nào?", "booking-guide", "từng bước có đánh số")]
    [InlineData("Đặt chỗ xe máy sao?", "booking-guide", "từng bước có đánh số")]
    [InlineData("Đặt chỗ gửi xe khi đi xem phim", "booking-guide", "từng bước có đánh số")]
    [InlineData("Đặt chỗ gửi xe khi đi xem phim CGV thì bấm đâu?", "booking-guide", "từng bước có đánh số")]
    [InlineData("Sao chưa thấy mail xác nhận sau khi book chỗ gửi xe?", "notification-guide", "kiểm tra hệ thống")]
    [InlineData("Mình đã book chỗ gửi xe và chưa thấy thông báo.", "notification-guide", "kiểm tra hệ thống")]
    [InlineData("Hệ thống này là gì?", "smartpark-overview", "Giới thiệu SmartPark")]
    [InlineData("Bạn là gì?", "smartpark-overview", "bạn là trợ lý AI")]
    public async Task PublicGuidanceKeepsItsIntentWithOwnerReadsEnabled(string message, string article, string canonical)
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner, search: true);
        f.Corpus.Value = await RoutingPublication();
        f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message }));

        var answer = result.GetProperty("answer");
        Assert.Equal("answered", answer.GetProperty("outcome").GetString());
        Assert.Equal(article, Assert.Single(answer.GetProperty("sources").EnumerateArray()).GetProperty("articleId").GetString());
        Assert.Contains(canonical, Assert.Single(f.Answers.Inputs));
        Assert.Empty(owner.Paths);
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
    }

    [Theory]
    [InlineData(false, "Đặt chỗ ở đâu?", "Mở Dashboard và vào Search Parking.")]
    [InlineData(true, "Đặt chỗ ở đâu?", "Mở Dashboard và vào Search Parking.")]
    [InlineData(false, "Where do I start to book a parking spot?", "Open Dashboard and go to Search Parking.")]
    [InlineData(true, "Where do I start to book a parking spot?", "Open Dashboard and go to Search Parking.")]
    [InlineData(false, "Where do I book parking?", "Open Dashboard and go to Search Parking.")]
    [InlineData(false, "Where can I reserve a spot for my motorcycle in SmartPark?", "Open Dashboard and go to Search Parking.")]
    [InlineData(false, "Where can I reserve a spot for my car in SmartPark?", "Open Dashboard and go to Search Parking.")]
    [InlineData(true, "Where should I book a space for a car on SmartPark?", "Open Dashboard and go to Search Parking.")]
    public async Task BookingLocationUsesThePublishedGuideWithAStableOpening(bool guest, string message, string opening)
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner, search: true, publicFaq: true);
        f.Corpus.Value = await RoutingPublication();
        f.EnableProvider();
        var answer = guest
            ? await f.Data(await f.Post("/api/assistant/guest/messages",
                new { requestId = Guid.NewGuid(), locale = "auto", message }, ""))
            : (await f.Data(await f.Post($"/api/assistant/conversations/{await f.Conversation()}/messages",
                new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message }))).GetProperty("answer");

        Assert.Equal("answered", answer.GetProperty("outcome").GetString());
        Assert.StartsWith(opening, answer.GetProperty("content").GetString());
        Assert.Equal("booking-guide", Assert.Single(answer.GetProperty("sources").EnumerateArray()).GetProperty("articleId").GetString());
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Empty(answer.GetProperty("actions").EnumerateArray());
        Assert.Empty(owner.Paths);
        Assert.Equal(0, f.Answers.Generations);
    }

    [Theory]
    [InlineData("driver", "Hủy lịch đặt của tôi ngay", "handoff", "Current booking")]
    [InlineData("driver", "Cancel giúp tui cái booking của tui với", "handoff", "Current booking")]
    [InlineData("driver", "Đặt giúp mình chỗ gửi xe", "handoff", "Search Parking")]
    [InlineData("driver", "Bãi gần tôi còn chỗ không?", "clarification", "Search Parking")]
    [InlineData("driver", "Có bãi nào đang nhận xe máy lúc này không?", "clarification", "Search Parking")]
    [InlineData("driver", "Phí gửi xe của bãi này hiện tại trả mấy đồng?", "clarification", "Search Parking")]
    [InlineData("driver", "Coi giá tiền bãi này ngay", "clarification", "chưa xác nhận được")]
    [InlineData("driver", "Coi giá tiền bãi này ngay với hướng dẫn hủy lịch đặt đi", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Hướng dẫn hủy lịch đặt với xem phí của bãi đó lúc này", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Cho tôi coi giá tiền của bãi này ngay với hướng dẫn hủy lịch đặt nha", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Cho hỏi giá tiền bãi này với hướng dẫn hủy lịch đặt?", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Đặt chỗ gửi xe sao và sao chưa thấy mail xác nhận sau khi book chỗ gửi xe?", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Xem giúp các booking đã thanh toán của vợ tôi", "clarification", "tài khoản có quyền")]
    [InlineData("driver", "Cho xem xe của vợ tôi", "clarification", "tài khoản có quyền")]
    [InlineData("driver", "Cho xem xe của tôi và chỉ cách hủy đặt chỗ", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Show my saved vehicles and my reservations?", "clarification", "first")]
    [InlineData("driver", "Cho xem xe của tôi ở đâu?", "answered", "My Vehicles")]
    [InlineData("driver", "Hủy đặt chỗ giùm tớ rồi cấp lại vé gửi xe", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Cách đặt chỗ? Cho xem lịch đặt của tôi.", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Cho xem lịch đặt của tôi, hiện bãi còn chỗ không", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "How do I book parking. Can I see my vehicles?", "clarification", "first")]
    [InlineData("driver", "Hủy lịch đặt và xem lịch đặt của tôi", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Cho tôi coi xe của tôi với chỉ cách hủy đặt chỗ nha.", "clarification", "yêu cầu nào trước")]
    [InlineData("driver", "Giữ chỗ học đàn rồi trả tiền sau", "clarification", "dịch vụ gửi xe")]
    [InlineData("driver", "Giữ chỗ cắt tóc rồi thanh toán sau được không?", "clarification", "dịch vụ gửi xe")]
    [InlineData("driver", "Cho hỏi cách báo thu sai tiền internet?", "clarification", "dịch vụ gửi xe")]
    [InlineData("driver", "Thanh toán tiền nước qua ứng dụng kiểu gì?", "clarification", "dịch vụ gửi xe")]
    [InlineData("driver", "Đặt chỗ tại rạp phim", "clarification", "dịch vụ gửi xe")]
    [InlineData("driver", "Where can I reserve a spot for dinner at a restaurant?", "clarification", "parking services")]
    [InlineData("driver", "Sao chưa thấy mail xác nhận sau khi book chỗ ăn tối?", "clarification", "dịch vụ gửi xe")]
    [InlineData("driver", "Mình chưa thấy email xác nhận sau khi đặt chỗ khám bệnh.", "clarification", "SmartPark")]
    [InlineData("admin", "Cho xem lịch đặt của tôi", "handoff", "Driver và chọn Current booking")]
    [InlineData("admin", "Show me my reservations", "handoff", "current role")]
    [InlineData("admin", "Cho xem xe của tôi", "clarification", "SmartPark")]
    [InlineData("owner", "Cho tui coi lịch giữ chỗ của tui với", "handoff", "Driver và chọn Current booking")]
    [InlineData("owner", "Xem giúp lịch booking của tui", "handoff", "Driver và chọn Current booking")]
    [InlineData("owner", "Ngày mai tôi có lịch đặt không?", "handoff", "Current booking")]
    [InlineData("owner", "Lịch đặt 2026-11-01", "handoff", "Booking history")]
    [InlineData("owner", "Doanh thu bãi của tôi hôm nay bao nhiêu?", "handoff", "mục Financial")]
    [InlineData("owner", "What's the revenue for my parking lot today?", "handoff", "Financial")]
    [InlineData("owner", "How do I check my lot revenue today?", "handoff", "Financial")]
    [InlineData("owner", "Bãi mình thu được bao nhiêu?", "handoff", "mục Financial")]
    [InlineData("owner", "Doanh thu bai ben minh bua nay", "handoff", "mục Financial")]
    [InlineData("owner", "How much did my car park make today?", "handoff", "Financial")]
    [InlineData("owner", "Doanh thu bãi tôi hôm nay bao nhiêu và cách đặt chỗ?", "clarification", "yêu cầu nào trước")]
    [InlineData("owner", "Cho xem lịch đặt của tôi và doanh thu bãi đỗ hôm nay", "clarification", "yêu cầu nào trước")]
    [InlineData("owner", "Cho xem lịch đặt của tôi. Doanh thu bãi tôi hôm nay bao nhiêu?", "clarification", "yêu cầu nào trước")]
    [InlineData("owner", "Doanh thu bãi tôi hôm nay bao nhiêu và bãi gần tôi còn chỗ không?", "clarification", "yêu cầu nào trước")]
    [InlineData("owner", "Cho xem biển số xe trong bãi tôi đang trực và doanh thu hôm nay", "clarification", "yêu cầu nào trước")]
    [InlineData("owner", "Doanh thu nhà hàng hôm nay bao nhiêu?", "clarification", "dịch vụ gửi xe")]
    [InlineData("admin", "Doanh thu bãi đỗ hôm nay bao nhiêu?", "handoff", "màn hình được cấp quyền")]
    [InlineData("owner", "Hủy lịch đặt của tôi ngay", "handoff", "Driver")]
    public async Task SystemRoutesDoNotDispatchToOwnerOrProvider(string role, string message, string outcome, string content)
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner, search: true);
        f.Corpus.Value = await RoutingPublication();
        f.EnableProvider();
        var token = f.Token(role: role);
        var id = await f.Conversation(token: token);
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message }, token));

        var answer = result.GetProperty("answer");
        Assert.Equal(outcome, answer.GetProperty("outcome").GetString());
        Assert.Contains(content, answer.GetProperty("content").GetString());
        Assert.Empty(answer.GetProperty("sources").EnumerateArray());
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Empty(answer.GetProperty("actions").EnumerateArray());
        Assert.Empty(owner.Paths);
        Assert.Empty(f.Answers.Inputs);
    }

    [Theory]
    [InlineData("Cho xem biển số xe trong bãi tôi được phân công", "handoff", "Gate Check-Out")]
    [InlineData("Bãi tôi đang trực có những biển số nào?", "handoff", "Gate Check-Out")]
    [InlineData("Cho tui coi biển số các xe đang đậu trong bãi với", "handoff", "Gate Check-Out")]
    [InlineData("Cho xem danh sách biển số các xe trong bãi", "handoff", "Gate Check-Out")]
    [InlineData("Biển số xe ra khỏi bãi xem lại ở đâu?", "handoff", "Parking History")]
    [InlineData("Cho tui xem biển số ở bãi mình phụ trách", "handoff", "Parking History")]
    [InlineData("Bãi được giao có biển số gì?", "handoff", "Parking History")]
    [InlineData("Show me the plates of the vehicles in my assigned lot", "handoff", "Gate Check-Out")]
    [InlineData("Can you list plates for vehicles in my assigned garage?", "handoff", "Gate Check-Out")]
    [InlineData("Show what plates are currently in the lot I manage", "handoff", "Gate Check-Out")]
    [InlineData("Cho xem biển số xe trong bãi tôi được phân công và gửi xe thanh toán sao?", "clarification", "yêu cầu nào trước")]
    [InlineData("Cho xem biển số xe trong bãi tôi đang trực và hệ thống này là gì", "clarification", "yêu cầu nào trước")]
    [InlineData("Cho xem biển số xe ở bãi của người khác", "clarification", "tài khoản có quyền")]
    [InlineData("Doanh thu bãi đỗ hôm nay bao nhiêu?", "handoff", "màn hình được cấp quyền")]
    public async Task OperatorWidgetPublicEndpointProvidesOnlyWorkspaceGuidance(string message, string outcome, string content)
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner, search: true, publicFaq: true);
        f.Corpus.Value = await RoutingPublication();
        f.EnableProvider();
        var answer = await f.Data(await f.Post("/api/assistant/guest/messages",
            new { requestId = Guid.NewGuid(), locale = "auto", message, role = "owner" }, ""));

        Assert.Equal("system", answer.GetProperty("mode").GetString());
        Assert.Equal(outcome, answer.GetProperty("outcome").GetString());
        Assert.Contains(content, answer.GetProperty("content").GetString());
        Assert.DoesNotContain("mục Financial", answer.GetProperty("content").GetString());
        Assert.Empty(answer.GetProperty("sources").EnumerateArray());
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Empty(answer.GetProperty("actions").EnumerateArray());
        Assert.Empty(owner.Paths);
        Assert.Empty(f.Answers.Inputs);
    }

    [Fact]
    public async Task UnconnectedReservationReadPointsDriverToTheBookingScreens()
    {
        await using var f = await Fixture.Create();
        f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "Cho xem lịch đặt của tôi" }));

        var answer = result.GetProperty("answer");
        Assert.Equal("unavailable", answer.GetProperty("outcome").GetString());
        Assert.Contains("Current booking", answer.GetProperty("content").GetString());
        Assert.Contains("Booking history", answer.GetProperty("content").GetString());
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Empty(f.Answers.Inputs);
    }

    [Theory]
    [InlineData("Cho xem lịch đặt của tôi")]
    [InlineData("Cho tui coi lịch giữ chỗ của tui với")]
    [InlineData("Xem giúp lịch booking của tui")]
    public async Task NaturalReservationReadUsesTheRequestingAccount(string message)
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner);
        var account = Guid.NewGuid();
        var token = f.Token(account: account);
        var id = await f.Conversation(token: token);
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message, accountId = f.Account }, token));

        var answer = result.GetProperty("answer");
        Assert.Equal("answered", answer.GetProperty("outcome").GetString());
        Assert.Equal("/api/me/reservations", Assert.Single(owner.Paths).AbsolutePath);
        Assert.Equal(account, Assert.Single(owner.Accounts));
        Assert.Equal(owner.Reservation, answer.GetProperty("cards")[0].GetProperty("reservation").GetProperty("id").GetGuid());
        Assert.Empty(f.Answers.Inputs);
    }

    [Theory]
    [InlineData("Cho xem xe của tôi")]
    [InlineData("Cho tui coi xe của tui với")]
    [InlineData("cho minh xem xe cua minh nhe")]
    [InlineData("Coi xe của mình nha")]
    [InlineData("Xem giúp xe của tôi")]
    public async Task NaturalVehicleReadUsesTheRequestingAccount(string message)
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner);
        f.EnableProvider();
        var account = Guid.NewGuid();
        var token = f.Token(account: account);
        var id = await f.Conversation(token: token);
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message, accountId = f.Account }, token));

        var answer = result.GetProperty("answer");
        Assert.Equal("answered", answer.GetProperty("outcome").GetString());
        Assert.Equal("/api/vehicles", Assert.Single(owner.Paths).AbsolutePath);
        Assert.Equal(account, Assert.Single(owner.Accounts));
        var card = Assert.Single(answer.GetProperty("cards").EnumerateArray());
        Assert.Equal(owner.Vehicle, Assert.Single(card.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Empty(f.Answers.Inputs);
    }

    [Fact]
    public async Task SearchPaginationReusesTheQuestionWithAnExplicitLanguageInstruction()
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner, search: true);
        f.Corpus.Value = await RoutingPublication();
        var id = await f.Conversation();
        var route = $"/api/assistant/conversations/{id}/messages";
        var first = await f.Data(await f.Post(route, new
        {
            clientMessageId = Guid.NewGuid(), expectedRevision = 0,
            message = "Answer in English: tìm bãi ô tô gần 10,105 radius=4"
        }));
        Assert.Equal("answered", first.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.StartsWith("Parking", first.GetProperty("answer").GetProperty("content").GetString());

        var guidance = await f.Data(await f.Post(route,
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 2, message = "Tìm bãi trên bản đồ thế nào?" }));
        Assert.Equal("discovery-guide", guidance.GetProperty("answer").GetProperty("sources")[0].GetProperty("articleId").GetString());
        var next = await f.Data(await f.Post(route,
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 4, message = "more parking" }));
        Assert.Equal("empty", next.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Equal(2, owner.Paths.Count);
        Assert.Contains("cursor=opaque-test-page", owner.Paths[1].Query);
        Assert.Contains("radiusKm=4", owner.Paths[1].Query);
    }

    [Theory]
    [InlineData("Bạn là gì?", "smartpark-overview", "Mình là trợ lý AI của SmartPark.")]
    [InlineData("Hệ thống này là gì?", "smartpark-overview", "SmartPark hỗ trợ người lái")]
    [InlineData("Hủy lịch đặt thế nào?", "cancellation-refund-guide", "màn hình thông thường")]
    [InlineData("Tìm bãi trên bản đồ thế nào?", "discovery-guide", "Search Parking")]
    [InlineData("How do I use the map to find a nearby garage? Reply in English.", "discovery-guide", "Search Parking")]
    [InlineData("Thanh toán phí gửi xe rồi và muốn ra cổng, giờ làm sao?", "checkout-and-departure-guide", "xe thực sự rời bãi")]
    [InlineData("Đặt chỗ ở đâu?", "booking-guide", "xác nhận trên form")]
    [InlineData("Muốn reserve một ô cho ô tô thì bắt đầu ở màn hình nào?", "booking-guide", "Search Parking")]
    [InlineData("Tôi muốn book trước một chỗ đỗ xe để đi ăn tối, bấm ở đâu?", "booking-guide", "Search Parking")]
    [InlineData("Chỗ gửi xe để đi coi phim book kiểu chi?", "booking-guide", "xác nhận trên form")]
    [InlineData("Vé gửi xe lỡ làm rơi, nhờ ai xác minh?", "checkout-and-departure-guide", "Operator")]
    [InlineData("Mất vé gửi xe thì tra cứu biển số xe đang đỗ trong bãi có đủ để ra cổng không?", "checkout-and-departure-guide", "Nếu mất vé, cần Operator")]
    [InlineData("Bien lai ghi cao hon phi gui xe, bao cho ai kiem tra", "incident-and-appeal-guide", "Operator")]
    [InlineData("Vừa đặt chỗ xong mà chưa thấy email nhắc, vô mô coi lại?", "notification-guide", "kiểm tra trạng thái")]
    [InlineData("Sao chưa thấy mail xác nhận sau khi book chỗ gửi xe?", "notification-guide", "kiểm tra trạng thái")]
    [InlineData("Chưa có mail xác nhận sau khi book chỗ gửi xe, làm sao?", "notification-guide", "kiểm tra trạng thái")]
    [InlineData("Chưa thấy thông báo sau khi book chỗ gửi xe, kiểm tra ở đâu?", "notification-guide", "kiểm tra trạng thái")]
    public async Task GuestPublicFallbackUsesTheRequestedTopic(string message, string article, string content)
    {
        await using var f = await Fixture.Create(measure: true);
        f.Corpus.Value = await RoutingPublication();
        var guest = await f.Data(await f.Post("/api/assistant/guest/messages",
            new { requestId = Guid.NewGuid(), locale = "auto", message }, ""));
        Assert.Equal("knowledge", guest.GetProperty("mode").GetString());
        Assert.Contains(content, guest.GetProperty("content").GetString());
        Assert.Equal(article, Assert.Single(guest.GetProperty("sources").EnumerateArray()).GetProperty("articleId").GetString());
        Assert.Empty(guest.GetProperty("cards").EnumerateArray());
    }

    [Theory]
    [InlineData("Đặt chỗ trước tối đa mấy ngày?", "booking-guide", "Các mặc định được công bố", "điều khoản hiệu lực")]
    [InlineData("Tôi thấy app báo hoàn rồi, vậy tiền đã về chưa?", "cancellation-refund-guide", "Hủy booking, chấp thuận hoàn", "trạng thái thực tế")]
    [InlineData("Mất vé giữ xe thì lúc ra cổng phải làm sao?", "checkout-and-departure-guide", "Nếu mất vé, cần Operator", "đã được công bố và có hiệu lực")]
    public async Task GuestFallbackLeadsWithThePublishedParagraphAndKeepsItsConditions(
        string message, string article, string opening, string condition)
    {
        await using var f = await Fixture.Create(measure: true);
        f.Corpus.Value = await RoutingPublication();
        var guest = await f.Data(await f.Post("/api/assistant/guest/messages",
            new { requestId = Guid.NewGuid(), locale = "vi", message }, ""));
        var answer = guest.GetProperty("content").GetString()!;
        var source = Assert.Single(guest.GetProperty("sources").EnumerateArray());
        Assert.Equal(article, source.GetProperty("articleId").GetString());
        Assert.StartsWith(opening, answer);
        Assert.Contains(condition, answer);
        Assert.Contains(answer, source.GetProperty("excerpt").GetString());
    }

    [Fact]
    public async Task DriverSignUpQuestionUsesThePublicNavigationWithoutCitingOwnerOnboarding()
    {
        await using var f = await Fixture.Create(measure: true);
        f.Corpus.Value = await RoutingPublication();
        var guest = await f.Data(await f.Post("/api/assistant/guest/messages", new
        {
            requestId = Guid.NewGuid(), locale = "vi",
            message = "Đăng ký làm tài xế trên ứng dụng SmartPark cần bắt đầu từ đâu?"
        }, ""));
        Assert.Equal("system", guest.GetProperty("mode").GetString());
        Assert.Equal("answered", guest.GetProperty("outcome").GetString());
        Assert.Contains("Sign Up rồi chọn Driver", guest.GetProperty("content").GetString());
        Assert.Empty(guest.GetProperty("sources").EnumerateArray());
        Assert.Empty(guest.GetProperty("actions").EnumerateArray());
    }

    private static async Task<PublicKnowledge> RoutingPublication()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "docs/03-design/ai/smartparkdocker/knowledge/0.4.1");
        return await new PublicKnowledgeFile(path, TimeProvider.System).ReadAsync(default)
            ?? throw new InvalidOperationException("The published 0.4.1 corpus failed validation.");
    }
}
