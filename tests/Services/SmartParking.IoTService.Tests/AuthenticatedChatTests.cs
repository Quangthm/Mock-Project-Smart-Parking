using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using SmartParking.IoTService.API;
using SmartParking.IoTService.API.Authentication;
using SmartParking.IoTService.Application.Assistant.Chat;
using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Infrastructure.Assistant.Guest;

namespace SmartParking.IoTService.Tests;

public sealed partial class AuthenticatedChatTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrOffDuringOwnerPreparationPreventsDispatch(bool off)
    {
        var owner = new ReadOwner(); await using var f = await Fixture.Create(owner: owner);
        var id = await f.Conversation(); var key = Guid.NewGuid(); var route = $"/api/assistant/conversations/{id}";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = f.Corpus.Reads + 3;
        f.Corpus.Hook = async n => { if (n == target) { entered.TrySetResult(); await release.Task; } };
        var pending = f.Post(route + "/messages", new { clientMessageId = key, expectedRevision = 0, message = "my vehicles" });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (off) f.Settings.State = f.Settings.State with { Enabled = false };
        else await f.Data(await f.Post(route + $"/requests/{key}/cancel", new { }));
        release.TrySetResult(); await pending;
        Assert.Equal(0, owner.Calls); Assert.Equal(0, f.Answers.Generations);
    }

    [Fact]
    public async Task SameUserDifferentAccountsKeepOwnerVehicleMembershipButIsolateChat()
    {
        var owner = new ReadOwner(); await using var f = await Fixture.Create(owner: owner);
        var first = await f.Conversation(); var other = Guid.NewGuid(); var token = f.Token(other, f.User);
        var second = (await f.Data(await f.Post("/api/assistant/conversations", new { clientConversationId = Guid.NewGuid(), locale = "en" }, token))).GetProperty("id").GetGuid();
        foreach (var (conversation, credential) in new[] { (first, f.Token()), (second, token) })
        {
            var result = await f.Data(await f.Post($"/api/assistant/conversations/{conversation}/messages", new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "my vehicles" }, credential));
            Assert.Equal(owner.Vehicle, result.GetProperty("answer").GetProperty("cards")[0].GetProperty("items")[0].GetProperty("id").GetGuid());
        }
        Assert.Equal(new[] { f.Account, other }, owner.Accounts);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Get($"/api/assistant/conversations/{first}/messages", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Get($"/api/assistant/conversations/{second}/messages", f.Token())).StatusCode);
    }

    [Theory]
    [InlineData("timeout", "timeout")]
    [InlineData("deadline", "timeout")]
    [InlineData("malformed", "dependency_unavailable")]
    public async Task TransportFailureAndMalformedSuccessAreNotEmpty(string mode, string outcome)
    {
        var owner = new ReadOwner { Mode = mode }; await using var f = await Fixture.Create(owner: owner);
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages", new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "my vehicles" }));
        Assert.Equal(outcome, result.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Empty(result.GetProperty("answer").GetProperty("cards").EnumerateArray());
    }

    [Fact]
    public async Task ExpiryDuringOwnerReadDoesNotDeliverPrivateCards()
    {
        var owner = new ReadOwner { Block = true }; await using var f = await Fixture.Create(owner: owner);
        var id = await f.Conversation(); var key = Guid.NewGuid(); var route = $"/api/assistant/conversations/{id}";
        var pending = f.Post(route + "/messages", new { clientMessageId = key, expectedRevision = 0, message = "my vehicles" });
        await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); f.Clock.Now = f.Clock.Now.AddSeconds(3); owner.Release.TrySetResult();
        Assert.Equal(HttpStatusCode.Unauthorized, (await pending).StatusCode);
        var recovered = await f.Data(await f.Get(route + $"/requests/{key}", f.Token()));
        Assert.Empty(recovered.GetProperty("answer").GetProperty("cards").EnumerateArray());
        Assert.Equal("login_required", recovered.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Equal(1, owner.Calls);
    }

    [Theory]
    [InlineData("do i have any bookings tomorrow")]
    [InlineData("my reservations 2032-02-28 to 2032-03-01")]
    [InlineData("lich dat 2032-02-28 den 2032-03-01")]
    public async Task ExplicitRangeAndEnglishTomorrowReachOwner(string message)
    {
        var owner = new ReadOwner(); await using var f = await Fixture.Create(owner: owner); var id = await f.Conversation();
        await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages", new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message }));
        Assert.Equal(1, owner.Calls);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(owner.Paths[0].Query);
        if (message.Contains("2032"))
        {
            Assert.Equal(DateTimeOffset.Parse("2032-02-27T17:00:00Z"), DateTimeOffset.Parse(query["from"].ToString()));
            Assert.Equal(DateTimeOffset.Parse("2032-03-01T17:00:00Z"), DateTimeOffset.Parse(query["to"].ToString()));
        }
    }

    [Theory]
    [InlineData(401, "login_required")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "not_found")]
    [InlineData(409, "conflict")]
    [InlineData(429, "rate_limit")]
    [InlineData(500, "dependency_unavailable")]
    [InlineData(503, "dependency_unavailable")]
    [InlineData(504, "timeout")]
    [InlineData(400, "invalid")]
    [InlineData(302, "dependency_unavailable")]
    public async Task OwnerErrorsRemainDistinctAndNeverBecomeEmpty(int status, string outcome)
    {
        var owner = new ReadOwner { Status = status };
        await using var f = await Fixture.Create(owner: owner); f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages", new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "my vehicles" }));
        Assert.Equal(outcome, result.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Empty(result.GetProperty("answer").GetProperty("cards").EnumerateArray());
        Assert.Equal(1, owner.Calls); Assert.Equal(0, f.Answers.Generations);
        Assert.DoesNotContain("PRIVATE_UPSTREAM", result.ToString());
    }

    [Fact]
    public async Task OwnerCardsPagingFreshDetailAndPrivacyUseRealHttpAdapter()
    {
        var owner = new ReadOwner(); await using var f = await Fixture.Create(owner: owner); f.EnableProvider();
        var id = await f.Conversation(); var route = $"/api/assistant/conversations/{id}/messages";
        async Task<JsonElement> Ask(string message, int revision) => await f.Data(await f.Post(route, new { clientMessageId = Guid.NewGuid(), expectedRevision = revision, message, accountId = Guid.NewGuid(), userId = Guid.NewGuid() }));
        var vehicles = await Ask("my vehicles", 0);
        Assert.Equal(owner.Vehicle, vehicles.GetProperty("answer").GetProperty("cards")[0].GetProperty("items")[0].GetProperty("id").GetGuid());
        var bookings = await Ask("my reservations tomorrow", 2);
        var card = bookings.GetProperty("answer").GetProperty("cards")[0].GetProperty("reservation");
        Assert.Equal(owner.Reservation, card.GetProperty("id").GetGuid());
        Assert.Equal(owner.Reservation, bookings.GetProperty("answer").GetProperty("actions")[0].GetProperty("reservationId").GetGuid());
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(owner.Paths[1].Query);
        var expected = ReservationDayWindow.Tomorrow(f.Clock, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        Assert.Equal(expected.From, DateTimeOffset.Parse(query["from"].ToString()));
        Assert.Equal(expected.To, DateTimeOffset.Parse(query["to"].ToString()));
        var more = await Ask("more reservations", 4);
        Assert.Equal("empty", more.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Contains("cursor=synthetic-cursor", owner.Paths[2].Query);
        owner.Status = 404;
        var detail = await Ask("reservation details " + owner.Reservation, 6);
        Assert.Equal("not_found", detail.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Equal("/api/me/reservations/" + owner.Reservation, owner.Paths[3].AbsolutePath);
        Assert.Equal(0, f.Answers.Generations); Assert.Empty(f.Answers.Inputs);
        Assert.All(owner.Accounts, a => Assert.Equal(f.Account, a));
        var capabilities = await f.Data(await f.Get("/api/assistant/capabilities"));
        Assert.True(capabilities.GetProperty("features").GetProperty("getMyVehicles").GetBoolean());
        Assert.True(capabilities.GetProperty("features").GetProperty("readMyReservations").GetBoolean());
        Assert.False(capabilities.GetProperty("features").GetProperty("searchParking").GetBoolean());
        f.Settings.State = f.Settings.State with { Enabled = false };
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Post(route, new { clientMessageId = Guid.NewGuid(), expectedRevision = 8, message = "my vehicles" })).StatusCode);
        Assert.Equal(4, owner.Calls);
    }

    [Fact]
    public async Task ReadsRejectForeignConversationGuestRoleExpiredAndForgedHeadersBeforeDispatch()
    {
        var owner = new ReadOwner(); await using var f = await Fixture.Create(owner: owner); var id = await f.Conversation();
        var route = $"/api/assistant/conversations/{id}/messages";
        foreach (var token in new[] { f.Token(Guid.NewGuid(), f.User), f.Token(Guid.NewGuid(), Guid.NewGuid()), "", f.Token(role: "admin"), f.Token(expired: true) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "my vehicles", accountId = f.Account }) };
            if (token != "") request.Headers.Authorization = new("Bearer", token);
            request.Headers.Add("X-Account-Id", f.Account.ToString()); request.Headers.Add("X-User-Id", f.User.ToString());
            var response = await f.Client.SendAsync(request); Assert.Contains((int)response.StatusCode, new[] { 401, 403, 404 });
        }
        Assert.Equal(0, owner.Calls);
    }

    [Fact]
    public async Task CancelReadFencesLateCardAndPreservesLedger()
    {
        var owner = new ReadOwner { Block = true }; await using var f = await Fixture.Create(owner: owner); var id = await f.Conversation(); var key = Guid.NewGuid();
        var route = $"/api/assistant/conversations/{id}";
        var pending = f.Post(route + "/messages", new { clientMessageId = key, expectedRevision = 0, message = "my vehicles" });
        await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Data(await f.Post(route + $"/requests/{key}/cancel", new { }));
        owner.Release.TrySetResult(); await pending;
        var result = await f.Data(await f.Get(route + $"/requests/{key}"));
        Assert.Equal("cancelled", result.GetProperty("state").GetString()); Assert.Equal(JsonValueKind.Null, result.GetProperty("answer").ValueKind);
        Assert.Equal(0, f.Answers.Generations);
    }

    [Fact]
    public async Task SearchRequiresOneExplicitOriginAndPreservesPublicOwnerCardsOnReplay()
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner, search: true);
        f.EnableProvider();
        var caps = await f.Data(await f.Get("/api/assistant/capabilities"));
        Assert.True(caps.GetProperty("features").GetProperty("searchParking").GetBoolean());
        var route = $"/api/assistant/conversations/{await f.Conversation()}";
        async Task<JsonElement> Ask(string message, int revision, Guid key) =>
            (await f.Data(await f.Post(route + "/messages", new { clientMessageId = key, expectedRevision = revision, message }))).GetProperty("answer");
        Assert.Equal("clarification", (await Ask("find parking near A or B", 0, Guid.NewGuid())).GetProperty("outcome").GetString());
        Assert.Empty(owner.Paths);
        var key = Guid.NewGuid();
        var answer = await Ask("tìm bãi ô tô gần 10.0001,105.0002 radius=4 lot=outdoor", 2, key);
        var card = answer.GetProperty("cards")[0].GetProperty("parking");
        Assert.Equal(owner.Site, card.GetProperty("siteId").GetGuid());
        Assert.Equal(JsonValueKind.Null, card.GetProperty("availability").GetProperty("availableCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, card.GetProperty("price").ValueKind);
        Assert.Contains("radiusKm=4", owner.Paths.Single().Query);
        var replay = await f.Data(await f.Get(route + $"/requests/{key}"));
        Assert.Equal(answer.GetRawText(), replay.GetProperty("answer").GetRawText());
        Assert.Single(owner.Paths);
        Assert.Equal("empty", (await Ask("more parking", 4, Guid.NewGuid())).GetProperty("outcome").GetString());
        Assert.Contains("cursor=opaque-test-page", owner.Paths.Last().Query);
        owner.Status = 503;
        Assert.Equal("dependency_unavailable", (await Ask("find parking car near 10,105", 6, Guid.NewGuid())).GetProperty("outcome").GetString());
        Assert.Empty(f.Answers.Inputs);
    }

    [Theory]
    [InlineData(400, "invalid")]
    [InlineData(401, "dependency_unavailable")]
    [InlineData(403, "dependency_unavailable")]
    [InlineData(404, "not_found")]
    [InlineData(409, "conflict")]
    [InlineData(429, "rate_limit")]
    [InlineData(500, "dependency_unavailable")]
    [InlineData(503, "dependency_unavailable")]
    [InlineData(504, "timeout")]
    [InlineData(302, "dependency_unavailable")]
    public async Task SearchOwnerErrorsNeverBecomeEmptyOrProviderInput(int status, string outcome)
    {
        var owner = new ReadOwner { Status = status };
        await using var f = await Fixture.Create(owner: owner, search: true);
        f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "find parking car near 10,105" }));
        Assert.Equal(outcome, result.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Empty(result.GetProperty("answer").GetProperty("cards").EnumerateArray());
        Assert.Empty(f.Answers.Inputs);
        Assert.Single(owner.Paths);
    }

    [Theory]
    [InlineData("find parking")]
    [InlineData("find parking car near home")]
    [InlineData("find parking car near 91,105")]
    [InlineData("find parking car near 10,105 radius=51")]
    [InlineData("find parking car near 10,105 max=100")]
    [InlineData("find parking car near 10,105 radius=3 radius=4")]
    [InlineData("find parking car near 10,105 from=2026-11-06")]
    public async Task InvalidOrAmbiguousSearchClarifiesLocally(string message)
    {
        var owner = new ReadOwner();
        await using var f = await Fixture.Create(owner: owner, search: true);
        f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message }));
        Assert.Equal("clarification", result.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Empty(f.Answers.Inputs);
        Assert.Empty(owner.Paths);
    }

    [Theory]
    [InlineData("timeout", "timeout")]
    [InlineData("malformed", "dependency_unavailable")]
    public async Task SearchMalformedAndTimeoutStayOutsideModel(string mode, string outcome)
    {
        var owner = new ReadOwner { Mode = mode };
        await using var f = await Fixture.Create(owner: owner, search: true);
        f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "find parking car near 10,105" }));
        Assert.Equal(outcome, result.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Empty(f.Answers.Inputs);
    }

    [Fact]
    public async Task SearchCancellationAndOtherConversationCannotReuseOrigin()
    {
        var owner = new ReadOwner { Block = true };
        await using var f = await Fixture.Create(owner: owner, search: true);
        var route = $"/api/assistant/conversations/{await f.Conversation()}";
        var key = Guid.NewGuid();
        var pending = f.Post(route + "/messages", new { clientMessageId = key, expectedRevision = 0, message = "find parking car near 10,105" });
        await owner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Data(await f.Post(route + $"/requests/{key}/cancel", new {}));
        owner.Release.TrySetResult();
        await pending;
        var lookup = await f.Data(await f.Get(route + $"/requests/{key}"));
        Assert.Equal("cancelled", lookup.GetProperty("state").GetString());
        var other = await f.Conversation();
        var next = await f.Data(await f.Post($"/api/assistant/conversations/{other}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "more parking" }));
        Assert.Equal("clarification", next.GetProperty("answer").GetProperty("outcome").GetString());
        Assert.Single(owner.Paths);
    }

    private sealed class ReadOwner : HttpMessageHandler
    {
        public int Status = 200, Calls; public bool Block; public string? Mode;
        public Guid Vehicle = Guid.NewGuid(), Reservation = Guid.NewGuid(), Site = Guid.NewGuid();
        public List<Uri> Paths = []; public List<Guid> Accounts = [];
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Paths.Add(request.RequestUri!);
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Null(request.Content);
            Assert.Equal("https", request.RequestUri!.Scheme);
            Assert.DoesNotContain(request.Headers, h => h.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase));
            if (request.RequestUri.AbsolutePath == "/api/parking-search")
            {
                Assert.Null(request.Headers.Authorization);
                Assert.Equal("synthetic-search.invalid", request.RequestUri.Host);
                Entered.TrySetResult();
                if (Block) await Release.Task.WaitAsync(ct);
                if (Mode == "timeout") throw new TaskCanceledException();
                if (Mode == "malformed") return new(HttpStatusCode.OK) { Content = new StringContent("{\"success\":true,\"data\":{}}") };
                if (Status != 200) return new((HttpStatusCode)Status);
                var now = DateTimeOffset.UtcNow;
                var row = new ParkingSummary(Site, "Synthetic lot", "Synthetic address", 10m, 105m,
                    40, "outdoor", ["CAR"], new("current", null, null, "unknown"), null,
                    new(now, null, now), new(new("fresh", null), new("unknown", "unknown"), new("unknown", "not_configured")));
                var next = request.RequestUri.Query.Contains("cursor=", StringComparison.Ordinal);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true,
                    data = new ParkingSearchPage(next ? [] : [row], next ? null : "opaque-test-page", now) }) };
            }
            var token = request.Headers.Authorization!.Parameter!;
            using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.Split('.')[1]));
            Accounts.Add(payload.RootElement.GetProperty("account_id").GetGuid());
            Entered.TrySetResult(); if (Block) await Release.Task.WaitAsync(ct);
            if (Mode == "timeout") throw new TaskCanceledException();
            if (Mode == "deadline") await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            if (Mode == "malformed") return new(HttpStatusCode.OK) { Content = new StringContent("{\"success\":true,\"data\":{}}") };
            if (Status != 200) return new((HttpStatusCode)Status) { Content = new StringContent("PRIVATE_UPSTREAM") };
            object data;
            if (request.RequestUri.AbsolutePath == "/api/vehicles") data = new[] { new { id = Vehicle, vehicleType = "CAR", rawPlate = "SYNTHETIC-PLATE", accountId = Guid.NewGuid(), imageReference = "PRIVATE_IMAGE" } };
            else
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri.Query);
                var start = query.ContainsKey("from") ? DateTimeOffset.Parse(query["from"].ToString()).AddHours(1) : DateTimeOffset.UtcNow;
                var row = new ReservationSummary(Reservation, Site, new("CAR", "SYNTHETIC-PLATE"), start, start.AddHours(1), "CONFIRMED", DateTimeOffset.UtcNow);
                data = request.RequestUri.AbsolutePath.EndsWith(Reservation.ToString()) ? new ReservationDetail(row, row.AsOf)
                    : new ReservationPage(query.ContainsKey("cursor") ? [] : [row], query.ContainsKey("cursor") ? null : "synthetic-cursor", row.AsOf);
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, data }) };
        }
    }
    [Theory]
    [InlineData("Ngày mai tôi có lịch đặt không?")]
    [InlineData("my vehicles 51A-12345 account_id=forged Bearer PRIVATE_TOKEN")]
    [InlineData("parking nearby my contact private@example.invalid")]
    public async Task UnconnectedReadRequestsNeverAdvertiseResultsOrReachProvider(string message)
    {
        await using var f = await Fixture.Create();
        f.EnableProvider();
        var id = await f.Conversation();
        var key = Guid.NewGuid();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = key, expectedRevision = 0, message, accountId = Guid.NewGuid(), userId = Guid.NewGuid(), tool = "get_my_reservations" }));
        var answer = result.GetProperty("answer");
        Assert.Equal("system", answer.GetProperty("mode").GetString());
        Assert.Contains(answer.GetProperty("outcome").GetString(), new[] { "clarification", "unavailable" });
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
        Assert.Empty(answer.GetProperty("actions").EnumerateArray());
        Assert.Equal(0, f.Answers.Generations);
        Assert.Empty(f.Answers.Inputs);
        var capabilities = await f.Data(await f.Get("/api/assistant/capabilities"));
        Assert.All(capabilities.GetProperty("features").EnumerateObject().Where(x => x.Name != "faq"), x => Assert.False(x.Value.GetBoolean()));
        var replay = await f.Data(await f.Get($"/api/assistant/conversations/{id}/requests/{key}"));
        Assert.Equal(answer.GetRawText(), replay.GetProperty("answer").GetRawText());
        Assert.Equal(0, f.Answers.Generations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownQuestionDoesNotMakeClaimsAboutSearch(bool search)
    {
        await using var f = await Fixture.Create(owner: new ReadOwner(), search: search);
        var capabilities = await f.Data(await f.Get("/api/assistant/capabilities"));
        Assert.Equal(search, capabilities.GetProperty("features").GetProperty("searchParking").GetBoolean());
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = "Tell me about astronomy" }));
        var answer = result.GetProperty("answer");
        Assert.Equal("clarification", answer.GetProperty("outcome").GetString());
        Assert.DoesNotContain("parking search", answer.GetProperty("content").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("find parking", answer.GetProperty("content").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(answer.GetProperty("cards").EnumerateArray());
    }

    [Theory]
    [InlineData("Đỗ xe muộn", null, "Bạn muốn hỏi")]
    [InlineData("Đến bãi muộn", null, "kiểm tra trạng thái")]
    [InlineData("Đỗ xe quá giờ", "payment-guide", null)]
    public async Task DriverUsesGuestParkingTimeGuidance(string question, string? article, string? guidance)
    {
        await using var f = await Fixture.Create();
        f.EnableProvider();
        var id = await f.Conversation();
        var result = await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",
            new { clientMessageId = Guid.NewGuid(), expectedRevision = 0, message = question }));
        var answer = result.GetProperty("answer");
        if (article is null)
        {
            Assert.Equal("clarification", answer.GetProperty("outcome").GetString());
            Assert.Contains(guidance!, answer.GetProperty("content").GetString());
            Assert.Empty(answer.GetProperty("sources").EnumerateArray());
            Assert.Empty(f.Answers.Inputs);
        }
        else
        {
            Assert.Equal("answered", answer.GetProperty("outcome").GetString());
            Assert.Equal(article, Assert.Single(answer.GetProperty("sources").EnumerateArray()).GetProperty("articleId").GetString());
            Assert.Equal("Đỗ xe quá giờ đã đặt thì khoản phí phát sinh được xác định thế nào?", Assert.Single(f.Answers.Inputs));
        }
    }

    [Fact]
    public async Task ProductionRoutesUseJwtAccountAndHonestCapabilities()
    {
        await using var f=await Fixture.Create();
        var capabilities=await f.Data(await f.Get("/api/assistant/capabilities"));
        Assert.True(capabilities.GetProperty("features").GetProperty("faq").GetBoolean());
        Assert.All(capabilities.GetProperty("features").EnumerateObject().Where(x=>x.Name!="faq"),x=>Assert.False(x.Value.GetBoolean()));
        var id=await f.Conversation();
        foreach(var (account,user,role,status) in new[]{(Guid.NewGuid(),f.User,"driver",404),(Guid.NewGuid(),Guid.NewGuid(),"driver",404),(f.Account,f.User,"admin",403),(f.Account,f.User,"owner",403)})
        {
            var token=f.Token(account,user,role);
            foreach(var route in new[]{ $"/conversations/{id}",$"/conversations/{id}/messages",$"/conversations/{id}/requests/{Guid.NewGuid()}"})
                Assert.Equal(status,(int)(await f.Get("/api/assistant"+route,token)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get("/api/assistant/conversations","")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get("/api/assistant/conversations",f.Token(expired:true))).StatusCode);
        Assert.Empty((await f.Data(await f.Get("/api/assistant/conversations",f.Token(Guid.NewGuid(),f.User)))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,(await f.Get($"/api/assistant/conversations/{id}",f.Token())).StatusCode);
    }
    [Fact]
    public async Task ReplayRevisionHistoryAndUnknownOwnerFields()
    {
        await using var f=await Fixture.Create();var creation=Guid.NewGuid();var id=await f.Conversation(creation);
        Assert.Equal(HttpStatusCode.OK,(await f.Post("/api/assistant/conversations",new{clientConversationId=creation,locale="en",accountId=Guid.NewGuid()})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Post("/api/assistant/conversations",new{clientConversationId=creation,locale="vi"})).StatusCode);
        var key=Guid.NewGuid();var body=new{clientMessageId=key,expectedRevision=0,message="payment",accountId=Guid.NewGuid(),userId=Guid.NewGuid(),history="PRIVATE",tool="create_booking"};
        var route=$"/api/assistant/conversations/{id}/messages";
        var result=await f.Data(await f.Post(route,body));Assert.Equal("completed",result.GetProperty("state").GetString());Assert.Equal(2,result.GetProperty("revision").GetInt64());
        var count=f.Answers.Calls;
        Assert.Equal(HttpStatusCode.OK,(await f.Post(route,body)).StatusCode);Assert.Equal(count,f.Answers.Calls);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Post(route,new{clientMessageId=key,expectedRevision=0,message="booking"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="booking"})).StatusCode);
        Assert.Equal(count,f.Answers.Calls);
        var history=await f.Data(await f.Get(route));Assert.Equal(2,history.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain("PRIVATE",history.ToString());Assert.DoesNotContain("private-provider",history.ToString());
        f.Corpus.Value=null;
        var replay=await f.Data(await f.Get($"/api/assistant/conversations/{id}/requests/{key}"));
        Assert.Equal("withdrawn",replay.GetProperty("answer").GetProperty("sources")[0].GetProperty("status").GetString());Assert.Equal(count,f.Answers.Calls);
    }
    [Fact]
    public async Task RunningReplayBusyCancelAndLateCompletion()
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var key=Guid.NewGuid();f.Answers.Block=true;
        var route=$"/api/assistant/conversations/{id}";var body=new{clientMessageId=key,expectedRevision=0,message="payment"};
        var pending=f.Post(route+"/messages",body);await f.Answers.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var replay=await f.Post(route+"/messages",body);Assert.Equal(HttpStatusCode.Accepted,replay.StatusCode);Assert.NotNull(replay.Headers.Location);Assert.NotNull(replay.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Post(route+"/messages",new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="booking"})).StatusCode);
        var cancelled=await f.Data(await f.Post(route+$"/requests/{key}/cancel",new{}));Assert.Equal("cancelled",cancelled.GetProperty("state").GetString());
        f.Answers.Release.TrySetResult();await pending;
        var result=await f.Data(await f.Get(route+$"/requests/{key}"));Assert.Equal("cancelled",result.GetProperty("state").GetString());Assert.Equal(JsonValueKind.Null,result.GetProperty("answer").ValueKind);
        Assert.Single((await f.Data(await f.Get(route+"/messages"))).GetProperty("items").EnumerateArray());
        Assert.Equal("cancelled",(await f.Data(await f.Post(route+$"/requests/{key}/cancel",new{}))).GetProperty("state").GetString());Assert.Equal(1,f.Answers.Calls);
    }
    [Fact]
    public async Task ExpiryDuringGenerationKeepsResultForSameAccountRefresh()
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var key=Guid.NewGuid();f.Answers.Block=true;
        var pending=f.Post($"/api/assistant/conversations/{id}/messages",new{clientMessageId=key,expectedRevision=0,message="payment"});await f.Answers.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Expire credential but keep the durable processing deadline alive in the synthetic store.
        f.Clock.Now=f.Clock.Now.AddSeconds(3);f.Answers.Release.TrySetResult();
        Assert.Equal(HttpStatusCode.Unauthorized,(await pending).StatusCode);
        var result=await f.Data(await f.Get($"/api/assistant/conversations/{id}/requests/{key}",f.Token()));Assert.Equal("completed",result.GetProperty("state").GetString());Assert.Equal(1,f.Answers.Calls);
    }
    [Fact]
    public async Task OffStopsNewWorkButRetainsHistoryLookupAndReplay()
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var key=Guid.NewGuid();var body=new{clientMessageId=key,expectedRevision=0,message="payment"};var route=$"/api/assistant/conversations/{id}";
        await f.Data(await f.Post(route+"/messages",body));f.Settings.State=f.Settings.State with{Enabled=false};
        Assert.Equal(HttpStatusCode.OK,(await f.Get(route+"/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Get(route+$"/requests/{key}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Post(route+"/messages",body)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Post(route+"/messages",new{clientMessageId=Guid.NewGuid(),expectedRevision=2,message="payment"})).StatusCode);
        Assert.False((await f.Data(await f.Get("/api/assistant/capabilities"))).GetProperty("enabled").GetBoolean());Assert.Equal(1,f.Answers.Calls);
    }
    [Theory]
    [InlineData("off")]
    [InlineData("timeout")]
    [InlineData("invalid")]
    [InlineData("failure")]
    public async Task InterruptedGenerationNeverAppendsLateAnswer(string mode)
    {
        await using var f=await Fixture.Create(mode=="timeout"?100:20000);var id=await f.Conversation();var key=Guid.NewGuid();var route=$"/api/assistant/conversations/{id}";
        f.Answers.Block=mode is "off" or "timeout";f.Answers.Invalid=mode=="invalid";f.Answers.Fail=mode=="failure";
        var pending=f.Post(route+"/messages",new{clientMessageId=key,expectedRevision=0,message="payment"});
        if(mode=="off"){await f.Answers.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));f.Settings.State=f.Settings.State with{Enabled=false};}
        var response=await pending;Assert.Equal(mode=="off"?503:200,(int)response.StatusCode);
        f.Answers.Release.TrySetResult();var result=await f.Data(await f.Get(route+$"/requests/{key}"));
        Assert.Equal(mode=="off"?"cancelled":"failed",result.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null,result.GetProperty("answer").ValueKind);Assert.Single((await f.Data(await f.Get(route+"/messages"))).GetProperty("items").EnumerateArray());
    }
    [Fact]
    public async Task RetentionPagingAndCursorAccountBinding()
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();
        for(var i=0;i<3;i++){await f.Data(await f.Post($"/api/assistant/conversations/{id}/messages",new{clientMessageId=Guid.NewGuid(),expectedRevision=i*2,message="payment"}));}
        var route=$"/api/assistant/conversations/{id}/messages";
        var first=await f.Data(await f.Get(route+"?limit=2"));var cursor=first.GetProperty("nextCursor").GetString()!;
        Assert.Equal(5,first.GetProperty("items")[0].GetProperty("sequence").GetInt64());
        var second=await f.Data(await f.Get(route+"?limit=2&cursor="+Uri.EscapeDataString(cursor)));Assert.Equal(3,second.GetProperty("items")[0].GetProperty("sequence").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Get(route+"?limit=3&cursor="+Uri.EscapeDataString(cursor))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Get(route+"?limit=2&cursor="+Uri.EscapeDataString(cursor),f.Token(Guid.NewGuid(),f.User))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Get(route+"?limit=2&cursor=forged")).StatusCode);
        f.Clock.Now=f.Clock.Now.AddDays(30);
        Assert.Equal(HttpStatusCode.NotFound,(await f.Get($"/api/assistant/conversations/{id}",f.Token())).StatusCode);
        Assert.Empty((await f.Data(await f.Get("/api/assistant/conversations",f.Token()))).GetProperty("items").EnumerateArray());
    }
    [Fact]
    public async Task ContextRemainsLocalAndGuestTranscriptIsNotImported()
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var route=$"/api/assistant/conversations/{id}/messages";
        await f.Data(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="payment",transcript="SECRET",accountId=Guid.NewGuid()}));
        await f.Data(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=2,message="tell me more"}));
        Assert.Equal(new[]{"Payment guidance","Payment guidance"},f.Answers.Inputs);
        await f.Data(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=4,message="my account SECRET 12345"}));Assert.Equal(2,f.Answers.Calls);
        var another=await f.Conversation();Assert.Empty((await f.Data(await f.Get($"/api/assistant/conversations/{another}/messages"))).GetProperty("items").EnumerateArray());
        await f.Data(await f.Post($"/api/assistant/conversations/{another}/messages",new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="tell me more"}));Assert.Equal(2,f.Answers.Calls);
    }
    [Theory]
    [InlineData("{}",400)]
    [InlineData("{",400)]
    [InlineData("{\"clientMessageId\":\"11111111-1111-4111-8111-111111111111\",\"expectedRevision\":\"0\",\"message\":\"payment\"}",400)]
    [InlineData("{\"clientMessageId\":\"11111111-1111-4111-8111-111111111111\",\"message\":\"payment\"}",400)]
    public async Task BindingDoesNotAdmitInvalidInput(string body,int status)
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();
        using var req=new HttpRequestMessage(HttpMethod.Post,$"/api/assistant/conversations/{id}/messages"){Content=new StringContent(body,Encoding.UTF8,"application/json")};req.Headers.Authorization=new("Bearer",f.Token());
        Assert.Equal(status,(int)(await f.Client.SendAsync(req)).StatusCode);Assert.Equal(0,f.Answers.Calls);
    }
    [Fact]
    public async Task ManagedProviderReceivesOnlyPublicTopicAndLocalContinuation()
    {
        await using var f=await Fixture.Create();f.EnableProvider();var id=await f.Conversation();var route=$"/api/assistant/conversations/{id}/messages";
        await f.Data(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="payment",history="PRIVATE-HISTORY",accountId=f.Account,token="PRIVATE-TOKEN"}));
        await f.Data(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=2,message="tell me more"}));
        await f.Data(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=4,message="my account PRIVATE 1234"}));
        Assert.Equal(2,f.Answers.Generations);Assert.Equal(new[]{"Payment guidance","Payment guidance"},f.Answers.Inputs);
    }
    [Fact]
    public async Task ForeignAccountWritesAreHiddenAndCompletedCancelDoesNotChangeResult()
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var key=Guid.NewGuid();var route=$"/api/assistant/conversations/{id}";var body=new{clientMessageId=key,expectedRevision=0,message="payment"};
        var result=await f.Data(await f.Post(route+"/messages",body));
        foreach(var token in new[]{f.Token(Guid.NewGuid(),f.User),f.Token(Guid.NewGuid(),Guid.NewGuid())})
        {
            Assert.Equal(HttpStatusCode.NotFound,(await f.Post(route+"/messages",body,token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await f.Post(route+$"/requests/{key}/cancel",new{},token)).StatusCode);
        }
        var cancelled=await f.Data(await f.Post(route+$"/requests/{key}/cancel",new{}));Assert.Equal(result.ToString(),cancelled.ToString());Assert.Equal(1,f.Answers.Calls);
    }
    [Fact]
    public async Task CancellationDuringPreparationPreventsProviderDispatch()
    {
        await using var f=await Fixture.Create();f.EnableProvider();var id=await f.Conversation();var key=Guid.NewGuid();var route=$"/api/assistant/conversations/{id}";
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target=f.Corpus.Reads+3;
        f.Corpus.Hook=async n=>{if(n==target){entered.TrySetResult();await release.Task;}};
        var pending=f.Post(route+"/messages",new{clientMessageId=key,expectedRevision=0,message="payment"});await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("cancelled",(await f.Data(await f.Post(route+$"/requests/{key}/cancel",new{}))).GetProperty("state").GetString());
        release.TrySetResult();await pending;Assert.Equal(0,f.Answers.Generations);Assert.Equal(0,f.Answers.Calls);
    }
    [Theory]
    [InlineData("expiry",401)]
    [InlineData("publication",503)]
    public async Task FinalDeliveryRechecksCredentialAndAdmissionSnapshot(string change,int status)
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var key=Guid.NewGuid();var changed=false;
        f.Corpus.Hook=_=>
        {
            if(!changed && f.Store.Entries[id].Requests.TryGetValue(key,out var request) && request.Result.State=="completed")
            {changed=true;if(change=="expiry")f.Clock.Now=f.Clock.Now.AddSeconds(3);else f.Corpus.Value=f.Corpus.Value! with{Fingerprint="replacement",Version="test-v2"};}
            return Task.CompletedTask;
        };
        Assert.Equal(status,(int)(await f.Post($"/api/assistant/conversations/{id}/messages",new{clientMessageId=key,expectedRevision=0,message="payment"})).StatusCode);
        Assert.Equal("completed",(await f.Data(await f.Get($"/api/assistant/conversations/{id}/requests/{key}",f.Token()))).GetProperty("state").GetString());Assert.Equal(1,f.Answers.Calls);
    }
    [Fact]
    public async Task ChangedRoutingDuringPreparationDoesNotDispatch()
    {
        await using var f=await Fixture.Create();f.EnableProvider();var id=await f.Conversation();var target=f.Corpus.Reads+3;
        f.Corpus.Hook=n=>{if(n==target)f.Settings.State=f.Settings.State with{Revision=2};return Task.CompletedTask;};
        var response=await f.Post($"/api/assistant/conversations/{id}/messages",new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="payment"});
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);Assert.Equal(0,f.Answers.Generations);
    }
    [Fact]
    public async Task TransportAndPagingBoundsRejectBeforeAdmission()
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var route=$"/api/assistant/conversations/{id}/messages";
        foreach(var (body,media,status) in new[]{(new string('x',16385),"application/json",413),("{}","text/plain",415)})
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,route){Content=new StringContent(body,Encoding.UTF8,media)};request.Headers.Authorization=new("Bearer",f.Token());
            Assert.Equal(status,(int)(await f.Client.SendAsync(request)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message=new string('x',4001)})).StatusCode);
        foreach(var query in new[]{"?limit=0","?limit=101","?limit=2&limit=2","?sort=desc"})Assert.Equal(HttpStatusCode.BadRequest,(await f.Get(route+query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Get("/api/assistant/conversations?limit=51")).StatusCode);Assert.Equal(0,f.Answers.Calls);
    }
    [Fact]
    public async Task ConcurrentGenerationLimitDoesNotBlockRecovery()
    {
        await using var f=await Fixture.Create();var first=await f.Conversation();var second=await f.Conversation();var third=await f.Conversation();f.Answers.Block=true;
        var key=Guid.NewGuid();var one=f.Post($"/api/assistant/conversations/{first}/messages",new{clientMessageId=key,expectedRevision=0,message="payment"});
        await f.Answers.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var two=f.Post($"/api/assistant/conversations/{second}/messages",new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="payment"});
        using var deadline=new CancellationTokenSource(5000);while(f.Answers.Calls<2)await Task.Delay(10,deadline.Token);
        var rejected=await f.Post($"/API/assistant/conversations/{third}/messages/",new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="payment"});Assert.Equal(HttpStatusCode.TooManyRequests,rejected.StatusCode);Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal("running",(await f.Data(await f.Get($"/api/assistant/conversations/{first}/requests/{key}"))).GetProperty("state").GetString());
        f.Answers.Release.TrySetResult();await one;await two;Assert.Equal(2,f.Answers.Calls);
    }
    [Fact]
    public async Task CorpusStallBoundsAdmissionAndDoesNotBlockSavedResultLookup()
    {
        await using var f=await Fixture.Create(overall:800);var id=await f.Conversation();var key=Guid.NewGuid();
        // Use a synthetic saved result because an 800ms overall fixture reserves its budget for finalization.
        await f.Store.Admit(f.Account,new(id,key,0,"payment"),"synthetic",new(1,"synthetic-hash","test-v1"),25000,default);
        await f.Store.Finish(f.Account,id,key,new(Guid.NewGuid(),"knowledge","answered","saved",[new("payment-guide","test-v1","en","title","excerpt")],[],[]),null,false,default);
        f.Corpus.Stall=true;
        var result=await f.Data(await f.Get($"/api/assistant/conversations/{id}/requests/{key}"));Assert.Equal("completed",result.GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.GatewayTimeout,(await f.Post("/api/assistant/conversations",new{clientConversationId=Guid.NewGuid(),locale="en"})).StatusCode);Assert.Equal(0,f.Answers.Calls);
    }
    [Fact]
    public async Task OrphanLookupRecoversWithoutRedispatchAndOldKeyRemainsTerminal()
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var key=Guid.NewGuid();
        await f.Store.Admit(f.Account,new(id,key,0,"payment"),"synthetic",new(1,"synthetic-hash","test-v1"),100,default);
        f.Clock.Now=f.Clock.Now.AddSeconds(1);
        var result=await f.Data(await f.Get($"/api/assistant/conversations/{id}/requests/{key}"));
        Assert.Equal("failed",result.GetProperty("state").GetString());Assert.Equal("REQUEST_INTERRUPTED",result.GetProperty("requestError").GetProperty("code").GetString());
        Assert.Equal(0,f.Answers.Calls);Assert.Single((await f.Data(await f.Get($"/api/assistant/conversations/{id}/messages"))).GetProperty("items").EnumerateArray());
    }
    [Theory]
    [InlineData("turns")]
    [InlineData("characters")]
    public async Task ContextCannotReachBeyondApprovedBounds(string mode)
    {
        await using var f=await Fixture.Create();var id=await f.Conversation();var route=$"/api/assistant/conversations/{id}/messages";
        var valid=new ChatMessage(Guid.NewGuid(),2,"assistant","answer",f.Clock.Now,Guid.NewGuid(),"answered",[new("payment-guide","test-v1","en","title","excerpt")],[],"knowledge",[]);
        var invalid=valid with {Sources=[]};
        f.Store.ContextOverride=mode=="turns"?Enumerable.Repeat(new ContextTurn("question",invalid),6).Append(new("payment",valid)).ToArray():[new(new string('x',12000),valid),new("payment",valid)];
        await f.Data(await f.Post(route,new{clientMessageId=Guid.NewGuid(),expectedRevision=0,message="tell me more"}));Assert.Equal(0,f.Answers.Calls);
    }
    private sealed class ChatCorpus : IPublicKnowledge
    {
        public PublicKnowledge? Value=new GuestFaqTests.Corpus().Value;
        public bool Stall; public int Reads; public Func<int,Task>? Hook;
        public async Task<PublicKnowledge?> ReadAsync(CancellationToken ct)
        {var count=Interlocked.Increment(ref Reads);if(Hook is not null)await Hook(count);if(Stall)await new TaskCompletionSource().Task;return Value;}
    }
    internal sealed class Clock : TimeProvider {public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Answers : IGuestAnswerFactory
    {
        public int Calls;public int Generations;public bool Block,Invalid,Fail;public List<string> Inputs=[];
        public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IManagedAnswerProvider Create(PublicKnowledge corpus,PublicArticle article,string locale)=>new Adapter(this,corpus,article);
        private sealed class Adapter(Answers owner,PublicKnowledge corpus,PublicArticle article):IManagedAnswerProvider
        {
            public Task<GroundedReply> GenerateAsync(string provider,string model,string secret,string message,CancellationToken ct){Interlocked.Increment(ref owner.Generations);return KnowledgeAsync(message,ct);}
            public async Task<GroundedReply> KnowledgeAsync(string message,CancellationToken ct)
            {
                Interlocked.Increment(ref owner.Calls);owner.Inputs.Add(message);owner.Entered.TrySetResult();
                if(owner.Block)await owner.Release.Task; // Deliberately noncooperative to exercise timeout/late completion.
                if(owner.Fail)throw new DemoGroundedUnavailableException(DemoGroundedFailure.Network);
                return new("Synthetic public answer",[new(owner.Invalid?"foreign-source":article.Id,corpus.Version,article.TitleEn,article.En)],"private-provider","private-model","knowledge");
            }
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Account=Guid.NewGuid(),User=Guid.NewGuid();public Clock Clock=new();public ChatMemoryStore Store;
        public GuestFaqTests.Store Settings=new();public ChatCorpus Corpus=new();public Answers Answers=new();
        public HttpClient Client=null!;private WebApplication app=null!;private RSA key=RSA.Create(2048);private string initial="";
        public Meter Meter => app.Services.GetRequiredService<IMeterFactory>().Create("SmartPark.Assistant");
        private Fixture(){Store=new(Clock);}
        public static async Task<Fixture> Create(int generation=20000, int overall=25000, ReadOwner? owner=null,
            bool search=false, bool measure=false, IChatStore? chatStore=null)
        {
            var f=new Fixture();var fallback=new GuestFaqTests.Answers();
            var generator=new ManagedDemoGenerator(f.Settings,new GuestFaqTests.Protector(),fallback,f.Clock,NullLogger<ManagedDemoGenerator>.Instance);
            f.app=AssistantGuestHost.Build(["--environment","Production"],b=>
            {
                b.Configuration.Sources.Clear();b.WebHost.UseTestServer();b.Services.AddSingleton<TimeProvider>(f.Clock);
                if (measure)
                {
                    b.Logging.ClearProviders();
                    b.Services.AddSingleton(new GuestOptions(true));
                    b.Services.AddSingleton<IGuestAnswerFactory, GuestAnswerFactory>();
                }
                if (owner is not null)
                {
                    b.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                        ["Assistant:Reads:Search:MaxAgeSeconds"]="60", ["Assistant:Reads:Search:ClockSkewSeconds"]="5", ["Assistant:Reads:Search:Enabled"]=search.ToString(), ["Assistant:Reads:Search:Origin"]="https://synthetic-search.invalid",
                        ["Assistant:Reads:User:Enabled"]="true", ["Assistant:Reads:User:Origin"]="https://synthetic-user.invalid",
                        ["Assistant:Reads:Operations:Enabled"]="true", ["Assistant:Reads:Operations:Origin"]="https://synthetic-operations.invalid" });
                    foreach (var name in new[] { "assistant-user-read", "assistant-operations-read", "assistant-parking-search" })
                        b.Services.PostConfigure<Microsoft.Extensions.Http.HttpClientFactoryOptions>(name, o => o.HttpMessageHandlerBuilderActions.Add(h => h.PrimaryHandler=owner));
                }
                b.Services.AddAssistantAuthentication("synthetic-user","synthetic-assistant",f.key.ExportSubjectPublicKeyInfoPem(),f.Clock);
                b.Services.AddAuthorization(o=>o.AddPolicy("AssistantAdmin",p=>p.RequireAuthenticatedUser().RequireRole("admin")));
                b.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
                b.Services.AddSingleton<IChatStore>(chatStore ?? f.Store);b.Services.AddSingleton<IAssistantManagementStore>(f.Settings);b.Services.AddSingleton<IPublicKnowledge>(f.Corpus);
                if (!measure) b.Services.AddSingleton<IGuestAnswerFactory>(f.Answers);
                b.Services.AddSingleton(generator);b.Services.AddSingleton(new ChatOptions(true,generation,overall));
            });
            await f.app.StartAsync();f.Client=f.app.GetTestClient();f.initial=f.Token();return f;
        }
        public void EnableProvider() => Settings.State=Settings.State with {Providers=[new("openai","synthetic-model",true,1)],Keys=[new(Guid.NewGuid(),"openai","synthetic",true,1,Guid.NewGuid(),Clock.Now,null)]};
        public string Token(Guid? account=null,Guid? user=null,string role="driver",bool expired=false)
        {
            var header=Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");var payload=JsonSerializer.Serialize(new{iss="synthetic-user",aud="synthetic-assistant",sub=(user??User).ToString(),account_id=(account??Account).ToString(),jti=Guid.NewGuid().ToString("N"),role,nbf=Clock.Now.AddSeconds(-1).ToUnixTimeSeconds(),exp=Clock.Now.AddSeconds(expired?0:2).ToUnixTimeSeconds()});
            var unsigned=header+"."+Base64UrlEncoder.Encode(payload);return unsigned+"."+Base64UrlEncoder.Encode(key.SignData(Encoding.ASCII.GetBytes(unsigned),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
        }
        public Task<HttpResponseMessage> Get(string path,string? token=null)=>Send(HttpMethod.Get,path,null,token);
        public Task<HttpResponseMessage> Post(string path,object body,string? token=null)=>Send(HttpMethod.Post,path,body,token);
        private Task<HttpResponseMessage> Send(HttpMethod method,string path,object? body,string? token)
        {var request=new HttpRequestMessage(method,path);if(body is not null)request.Content=JsonContent.Create(body);if(token!="")request.Headers.Authorization=new("Bearer",token??initial);return Client.SendAsync(request);}
        public async Task<JsonElement> Data(HttpResponseMessage response){Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());Assert.Equal("no-store",response.Headers.CacheControl?.ToString());return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Clone();}
        public async Task<Guid> Conversation(Guid? creation=null)=> (await Data(await Post("/api/assistant/conversations",new{clientConversationId=creation??Guid.NewGuid(),locale="en",accountId=Guid.NewGuid()}))).GetProperty("id").GetGuid();
        public async ValueTask DisposeAsync(){Answers.Release.TrySetResult();Client.Dispose();await app.StopAsync();await app.DisposeAsync();key.Dispose();}
    }
}
