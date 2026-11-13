using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.IoTService.API;
using SmartParking.IoTService.Application.Assistant.Demo;
using SmartParking.IoTService.Application.Assistant.Knowledge;

namespace SmartParking.IoTService.Tests;

public sealed class AssistantDemoMessageTests
{
    [Theory]
    [InlineData("Làm sao tìm bãi đỗ xe?", "không tra cứu")]
    [InlineData("Tôi muốn đặt chỗ", "chưa có thao tác")]
    [InlineData("Thanh toán thế nào?", "không tạo giao dịch")]
    [InlineData("doanh thu tháng này?", "không có số liệu")]
    [InlineData("<script>alert(1)</script>", "nội dung mẫu cố định")]
    public async Task ReturnsLabeledFixturesWithoutInventingBusinessState(string message, string expected)
    {
        await using var app = Build(); await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/demo/assistant/messages", new { message, role = "admin", accountId = "spoof" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.GetProperty("data").GetProperty("isDemo").GetBoolean());
        Assert.Contains("mô phỏng", body.GetProperty("data").GetProperty("notice").GetString());
        Assert.Contains(expected, body.GetProperty("data").GetProperty("reply").GetString());
        Assert.Equal("demo", response.Headers.GetValues("X-Assistant-Mode").Single());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"message\":null}")]
    [InlineData("{\"message\":42}")]
    [InlineData("{\"message\":\"  \"}")]
    [InlineData("{bad")]
    public async Task InvalidInputHasSafeEnvelope(string json)
    {
        await using var app = Build(); await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.PostAsync("/api/demo/assistant/messages", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VALIDATION_FAILED", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CharacterAndBodyLimitsApplyBeforeGenerating()
    {
        var generator = new ProbeGenerator();
        await using var app = Build(generator); await app.StartAsync();
        using var client = app.GetTestClient();
        using var accepted = await client.PostAsJsonAsync("/api/demo/assistant/messages", new { message = "  " + new string('界', 4000) + "  " });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(4000, generator.LastMessage!.Length);
        using var tooLong = await client.PostAsJsonAsync("/api/demo/assistant/messages", new { message = new string('a', 4001) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        using var oversize = await client.PostAsync("/api/demo/assistant/messages", new StringContent(new string('x', 32769), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversize.StatusCode);
        using var chunkedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/demo/assistant/messages")
        {
            Content = new UnknownLengthContent(new string('x', 32769))
        };
        chunkedRequest.Content.Headers.ContentType = new("application/json");
        using var chunked = await client.SendAsync(chunkedRequest);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunked.StatusCode);
        Assert.Equal(1, generator.Calls);
    }

    [Fact]
    public async Task UnsupportedMediaQueryAndGeneratorFailureAreNotMockSuccess()
    {
        await using var app = Build(new ProbeGenerator { Fail = true }); await app.StartAsync();
        using var client = app.GetTestClient();
        using var type = await client.PostAsync("/api/demo/assistant/messages", new StringContent("hello"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, type.StatusCode);
        using var query = await client.PostAsJsonAsync("/api/demo/assistant/messages?role=driver", new { message = "hello" });
        Assert.Equal(HttpStatusCode.BadRequest, query.StatusCode);
        using var failure = await client.PostAsJsonAsync("/api/demo/assistant/messages", new { message = "hello" });
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        var body = await failure.Content.ReadAsStringAsync();
        Assert.Contains("INTERNAL_ERROR", body);
        Assert.DoesNotContain("sensitive", body);
    }

    [Fact]
    public async Task CancellationAndValidationDoNotInvokeGenerator()
    {
        var generator = new ProbeGenerator();
        var handler = new SendDemoMessageHandler(generator);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new("hello"), new CancellationToken(true)));
        await Assert.ThrowsAsync<DemoMessageValidationException>(() => handler.Handle(new(" "), default));
        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task ProviderModeMissingConfigurationReturnsSafe503NeverMock()
    {
        await using var app = AssistantDemoHost.Build(["--environment", "Demo"], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Assistant:Demo:Enabled"] = "true", ["Assistant:Demo:Mode"] = "provider"
            });
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        for (var i = 0; i < 10; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/demo/assistant/messages", new { message = "booking" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("ASSISTANT_NOT_CONFIGURED", await response.Content.ReadAsStringAsync());
        }
        using var limited = await client.PostAsJsonAsync("/api/demo/assistant/messages", new { message = "booking" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("RATE_LIMITED", await limited.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task KnowledgeGuidanceUsesRealSuccessContractWithoutClaimingAiGeneration()
    {
        await using var app = AssistantDemoHost.Build(["--environment", "Demo"], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Assistant:Demo:Enabled"] = "true" });
            builder.Services.AddSingleton<IDemoGroundedGenerator>(new DocumentGenerator());
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/demo/assistant/messages", new { message = "booking" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = body.RootElement.GetProperty("data");
        Assert.Equal("knowledge", data.GetProperty("mode").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("provider").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("model").ValueKind);
        Assert.Contains("không phải câu trả lời do AI tạo", data.GetProperty("notice").GetString());
        Assert.Equal("Full policy and final conditions.", data.GetProperty("sources")[0].GetProperty("excerpt").GetString());
    }
    private sealed class DocumentGenerator : IDemoGroundedGenerator
    {
        public Task<GroundedReply> GenerateAsync(string message, CancellationToken ct) => Task.FromResult(new GroundedReply(
            "Read original guidance", [new("booking", "1", "Đặt chỗ", "Full policy and final conditions.")], null, null, "knowledge"));
    }

    private static WebApplication Build(ProbeGenerator? generator = null) => AssistantDemoHost.Build(["--environment", "Demo"], builder =>
    {
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Assistant:Demo:Enabled"] = "true" });
        if (generator is not null) builder.Services.AddSingleton<IDemoReplyGenerator>(generator);
    });

    private sealed class ProbeGenerator : IDemoReplyGenerator
    {
        public int Calls;
        public string? LastMessage;
        public bool Fail;
        public Task<string> ReplyAsync(string message, CancellationToken cancellationToken)
        {
            Calls++; LastMessage = message;
            if (Fail) throw new InvalidOperationException("sensitive generator details");
            return Task.FromResult("fixture");
        }
    }
    private sealed class UnknownLengthContent(string content) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(content)).AsTask();
    }
}
