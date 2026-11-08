using System.Net;
using System.Text;
using System.Text.Json;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

namespace SmartParking.IoTService.Tests;

public sealed class DemoKnowledgeCorpusTests
{
    private const string Header = "---\nversion: 0.2.0\nlocale: vi\npublication_state: draft\n---\n# Kiến thức SmartPark\nEDITORIAL_ONLY\n\n";
    private static string Section(string id = "booking-guide", string content = "Đặt chỗ bằng form. Giữ thanh toán mặc định 5 phút.",
        string terms = "booking; book; đặt chỗ") =>
        $"## [{id}] Đặt chỗ\n\nTừ khóa: {terms}\n\n### Nội dung\n\n{content}\n\n### Đối chiếu — không gửi đến AI\nPROVENANCE_ONLY\n";

    [Theory]
    [InlineData("How do I book a space?", "en")]
    [InlineData("Tôi muốn đặt chỗ", "vi")]
    [InlineData("toi muon dat cho", "vi")]
    [InlineData("BOOKING?", "en")]
    public async Task OneVietnameseSourceSupportsBothLanguagesWithoutAnotherProviderCall(string question, string language)
    {
        using var corpus = new Corpus(Header + Section());
        using var handler = new CapturingProvider();
        using var client = new HttpClient(handler);
        using var generator = Create(corpus.Path, client);
        var reply = await generator.GenerateAsync(question, default);
        var input = Assert.Single(handler.Inputs);
        Assert.Equal(language, input.GetProperty("answerLanguage").GetString());
        Assert.Equal("vi", input.GetProperty("sourceLanguage").GetString());
        var source = Assert.Single(input.GetProperty("sources").EnumerateArray());
        Assert.Contains("5 phút", source.GetProperty("Content").GetString());
        Assert.Equal("booking-guide", Assert.Single(reply.Sources).ArticleId);
        Assert.Equal("Đặt chỗ", reply.Sources[0].Title);
        Assert.DoesNotContain("EDITORIAL_ONLY", input.ToString());
        Assert.DoesNotContain("PROVENANCE_ONLY", input.ToString());
        Assert.DoesNotContain("Từ khóa", input.ToString());
        Assert.Contains("preserving all numbers", handler.Instructions);
        Assert.Contains("exceptions", handler.Instructions);
    }

    [Fact]
    public async Task SameGeneratorReloadsEditsAndVersionsWithoutRestartAndDoesNotReuseBadOrDeletedKnowledge()
    {
        using var corpus = new Corpus(Header + Section());
        using var handler = new CapturingProvider();
        using var client = new HttpClient(handler);
        using var generator = Create(corpus.Path, client);
        var first = await generator.GenerateAsync("booking", default);
        Assert.Equal("0.2.0", first.Sources[0].Version);
        corpus.Save((Header + Section(content: "Đặt chỗ: giữ thanh toán mặc định 8 phút.")).Replace("0.2.0", "0.2.1"));
        var updated = await generator.GenerateAsync("booking", default);
        Assert.Equal("0.2.1", updated.Sources[0].Version);
        Assert.Contains("8 phút", updated.Sources[0].Excerpt);
        Assert.DoesNotContain("5 phút", updated.Sources[0].Excerpt);
        corpus.Save("incomplete save");
        var invalid = await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => generator.GenerateAsync("booking", default));
        Assert.Equal(DemoGroundedFailure.Knowledge, invalid.Failure);
        Assert.Equal(2, handler.Inputs.Count);
        File.Delete(corpus.FilePath);
        await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => generator.GenerateAsync("booking", default));
        Assert.Equal(2, handler.Inputs.Count);
        corpus.Save(Header + Section(content: "Đặt chỗ đã cập nhật lại."));
        Assert.Contains("cập nhật", (await generator.GenerateAsync("booking", default)).Sources[0].Excerpt);
        Assert.Equal(3, handler.Inputs.Count);
    }

    [Fact]
    public async Task InFlightAnswerKeepsItsOwnSourceSnapshotWhenTheDocumentIsEdited()
    {
        using var corpus = new Corpus(Header + Section());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingProvider { BeforeReply = () => { entered.SetResult(); return release.Task; } };
        using var client = new HttpClient(handler);
        using var generator = Create(corpus.Path, client);
        var pending = generator.GenerateAsync("booking", default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        corpus.Save((Header + Section(content: "Đặt chỗ: giữ mặc định 8 phút.")).Replace("0.2.0", "0.2.1"));
        release.SetResult();
        var answer = await pending;
        Assert.Equal("0.2.0", answer.Sources[0].Version);
        Assert.Contains("5 phút", answer.Sources[0].Excerpt);
        handler.BeforeReply = null;
        var next = await generator.GenerateAsync("booking", default);
        Assert.Equal("0.2.1", next.Sources[0].Version);
        Assert.Contains("8 phút", next.Sources[0].Excerpt);
    }

    [Fact]
    public async Task DecomposedVietnameseAccentsDoNotTurnConnectionLossIntoAccountSecurity()
    {
        using var corpus = new Corpus(Header + Section("account-security-guide", "Bảo mật tài khoản.", "bảo mật")
            + Section("troubleshooting-guide", "Báo mất kết nối.", "mất kết nối"));
        using var handler = new CapturingProvider();
        using var client = new HttpClient(handler);
        using var generator = Create(corpus.Path, client);
        var answer = await generator.GenerateAsync("Ứng dụng báo mất kết nối".Normalize(NormalizationForm.FormD), default);
        Assert.Equal("troubleshooting-guide", answer.Sources[0].ArticleId);
        Assert.Equal("vi", handler.Inputs[0].GetProperty("answerLanguage").GetString());
    }

    [Theory]
    [InlineData("missing-file")]
    [InlineData("duplicate-id")]
    [InlineData("invalid-id")]
    [InlineData("missing-version")]
    [InlineData("duplicate-version")]
    [InlineData("english-source")]
    [InlineData("empty-terms")]
    [InlineData("missing-content")]
    [InlineData("duplicate-content")]
    [InlineData("empty-content")]
    [InlineData("oversize-section")]
    [InlineData("oversize-title")]
    [InlineData("unknown-section-heading")]
    [InlineData("unexpected-content-subheading")]
    [InlineData("too-many-sections")]
    [InlineData("oversize-file")]
    [InlineData("invalid-utf8")]
    public async Task MalformedKnowledgeFailsBeforeSpendingAndNeverLeaksItsBody(string scenario)
    {
        var text = Header + Section();
        text = scenario switch
        {
            "duplicate-id" => text + Section(),
            "invalid-id" => Header + Section(id: "../bad"),
            "missing-version" => text.Replace("version:", "version-missing:"),
            "duplicate-version" => text.Replace("version: 0.2.0", "version: 0.2.0\nversion: 0.2.1"),
            "english-source" => text.Replace("locale: vi", "locale: en"),
            "empty-terms" => Header + Section(terms: "; ;"),
            "missing-content" => text.Replace("### Nội dung", "### Missing"),
            "duplicate-content" => text + "\n### Nội dung\nDuplicate",
            "empty-content" => Header + Section(content: " "),
            "oversize-section" => Header + Section(content: new string('x', 16001)),
            "oversize-title" => text.Replace("] Đặt chỗ", "] " + new string('x', 256)),
            "unknown-section-heading" => text + "\n## Invalid section\n",
            "unexpected-content-subheading" => text.Replace("5 phút.", "5 phút.\n### Điều kiện\nKhông được bỏ mất điều kiện này."),
            "too-many-sections" => Header + string.Concat(Enumerable.Range(0, 101).Select(i => Section(id: $"section-{i}"))),
            "oversize-file" => new string('x', 256 * 1024 + 1),
            _ => text,
        };
        using var corpus = new Corpus(text);
        if (scenario == "missing-file") File.Delete(corpus.FilePath);
        if (scenario == "invalid-utf8") File.WriteAllBytes(corpus.FilePath, [0xFF, 0xFE, 0xFF]);
        using var handler = new CapturingProvider();
        using var client = new HttpClient(handler);
        using var generator = Create(corpus.Path, client);
        var error = await Assert.ThrowsAsync<DemoGroundedUnavailableException>(() => generator.GenerateAsync("booking", default));
        Assert.Equal(DemoGroundedFailure.Knowledge, error.Failure);
        Assert.Null(error.InnerException);
        Assert.Empty(handler.Inputs);
        Assert.DoesNotContain("PROVENANCE_ONLY", error.ToString());
    }

    [Fact]
    public async Task ReadsOnlyCanonicalFileAndAcceptsBomCrLfAndContentSubheadings()
    {
        using var corpus = new Corpus((Header + Section(content: "Đặt chỗ.\n\n#### Điều kiện\nMặc định 5 phút.")).Replace("\n", "\r\n"));
        File.WriteAllText(corpus.FilePath, File.ReadAllText(corpus.FilePath), new UTF8Encoding(true));
        File.WriteAllText(System.IO.Path.Combine(corpus.Path, "notes.md"), "not a knowledge document");
        using var handler = new CapturingProvider();
        using var client = new HttpClient(handler);
        using var generator = Create(corpus.Path, client);
        Assert.Contains("#### Điều kiện", (await generator.GenerateAsync("booking", default)).Sources[0].Excerpt);
    }

    [Theory]
    [InlineData("How does astronomy work?", "I can help")]
    [InlineData("Payments?", "I can help")]
    [InlineData("Refunds?", "I can help")]
    [InlineData("Tôi hỏi thiên văn học", "Mình hiện hỗ trợ")]
    [InlineData("bookkeeping", "Mình hiện hỗ trợ")]
    public async Task NoMatchIsLocalizedDoesNotMatchPartialWordsAndMakesNoProviderCall(string question, string expected)
    {
        using var corpus = new Corpus(Header + Section());
        using var handler = new CapturingProvider();
        using var client = new HttpClient(handler);
        using var generator = Create(corpus.Path, client);
        var reply = await generator.GenerateAsync(question, default);
        Assert.Empty(reply.Sources);
        Assert.Contains(expected, reply.Reply);
        Assert.Empty(handler.Inputs);
    }

    [Theory]
    [InlineData("How do I find parking?", "discovery-guide")]
    [InlineData("Where is nearby parking on the map?", "discovery-guide")]
    [InlineData("Tôi tìm bãi đỗ xe", "discovery-guide")]
    [InlineData("How do I book a parking space?", "booking-guide")]
    [InlineData("toi muon dat cho", "booking-guide")]
    [InlineData("What is the minimum booking duration?", "booking-guide")]
    [InlineData("What is the cancellation policy?", "cancellation-refund-guide")]
    [InlineData("Tôi muốn hủy và hoàn tiền", "cancellation-refund-guide")]
    [InlineData("Can I get a refund?", "cancellation-refund-guide")]
    [InlineData("How much does parking cost?", "payment-guide")]
    [InlineData("How do I pay?", "payment-guide")]
    [InlineData("How do parking payments work?", "payment-guide")]
    [InlineData("Thanh toán phí thế nào?", "payment-guide")]
    [InlineData("Does the monthly pass guarantee a space?", "monthly-yearly-guide")]
    [InlineData("How does yearly parking renewal work?", "monthly-yearly-guide")]
    [InlineData("Tôi muốn mua gói tháng", "monthly-yearly-guide")]
    [InlineData("What does OCCUPIED and RESERVED mean?", "parking-rules-guide")]
    [InlineData("I lost my ticket at the gate", "parking-rules-guide")]
    [InlineData("Tôi bị mất vé", "parking-rules-guide")]
    [InlineData("How do I change my password?", "account-security-guide")]
    [InlineData("Tôi muốn đổi mật khẩu", "account-security-guide")]
    [InlineData("Tôi muốn bảo mật tài khoản", "account-security-guide")]
    [InlineData("How can I manage my vehicles?", "account-security-guide")]
    [InlineData("How do I contact support?", "support-guide")]
    [InlineData("Tôi cần hỗ trợ", "support-guide")]
    [InlineData("My booking is not loading due to a connection error", "troubleshooting-guide")]
    [InlineData("Ứng dụng báo mất kết nối", "troubleshooting-guide")]
    [InlineData("ung dung mat ket noi", "troubleshooting-guide")]
    [InlineData("What is SmartPark?", "smartpark-overview")]
    [InlineData("Xin chào SmartPark", "smartpark-overview")]
    public async Task ActualDocumentRanksRelevantVietnameseTopicFirstForBothLanguages(string question, string id)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var handler = new CapturingProvider();
        using var client = new HttpClient(handler);
        using var generator = Create(System.IO.Path.Combine(root.FullName, "docs", "03-design", "ai", "p1", "knowledge"), client);
        var reply = await generator.GenerateAsync(question, default);
        Assert.Equal(id, Assert.Single(reply.Sources).ArticleId);
        var input = Assert.Single(handler.Inputs);
        Assert.InRange(input.GetProperty("sources").GetArrayLength(), 1, 3);
        Assert.DoesNotContain("Đối chiếu", input.ToString());
        Assert.DoesNotContain("publication_state", input.ToString());
    }

    private static DemoGroundedGenerator Create(string path, HttpClient client) => new(new(path, "openai", "model-test", "synthetic-key"), client);
    [Theory]
    [InlineData("booking", "original Vietnamese")]
    [InlineData("đặt chỗ", "hướng dẫn gốc")]
    public void KnowledgeFallbackUsesFullCurrentSectionsWithoutGeneratingOrTranslating(string question, string expected)
    {
        var content = "Đặt chỗ. " + new string('x', 1200) + " Điều kiện cuối không được bỏ qua.";
        using var corpus = new Corpus(Header + Section(content: content));
        var answer = DemoKnowledgeGuidance.Read(corpus.Path, question, default);
        Assert.Equal("knowledge", answer.Mode);
        Assert.Null(answer.Provider);
        Assert.Null(answer.Model);
        Assert.Contains(expected, answer.Reply);
        Assert.Equal(content, Assert.Single(answer.Sources).Excerpt);
        Assert.DoesNotContain("PROVENANCE_ONLY", answer.Sources[0].Excerpt);
        corpus.Save((Header + Section(content: "Đặt chỗ: điều kiện mới.")).Replace("0.2.0", "0.2.1"));
        var updated = DemoKnowledgeGuidance.Read(corpus.Path, question, default);
        Assert.Equal("0.2.1", updated.Sources[0].Version);
        Assert.Contains("điều kiện mới", updated.Sources[0].Excerpt);
        corpus.Save("invalid document");
        var error = Assert.Throws<DemoGroundedUnavailableException>(() => DemoKnowledgeGuidance.Read(corpus.Path, question, default));
        Assert.Equal(DemoGroundedFailure.Knowledge, error.Failure);
        Assert.ThrowsAny<OperationCanceledException>(() => DemoKnowledgeGuidance.Read(corpus.Path, question, new CancellationToken(true)));
    }
    [Theory]
    [InlineData("buồn ngủ quá bạn eie")]
    [InlineData("mình đang buồn quá")]
    [InlineData("bạn giúp mình với")]
    [InlineData("I'm very sleepy today, my friend")]
    [InlineData("I'm sleepy")]
    public async Task ActualCorpusDoesNotSendConversationalFillerToProvider(string question)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "SmartParking.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var handler = new CapturingProvider();
        using var client = new HttpClient(handler);
        using var generator = Create(System.IO.Path.Combine(root.FullName, "docs", "03-design", "ai", "p1", "knowledge"), client);
        var answer = await generator.GenerateAsync(question, default);
        Assert.Empty(handler.Inputs.Select(_ => "unexpected model call")); // Do not dump full knowledge on failure.
        Assert.Empty(answer.Sources);
        Assert.Contains("SmartPark", answer.Reply);
        Assert.StartsWith(question.StartsWith("I'm", StringComparison.Ordinal) ? "I can help" : "Mình hiện hỗ trợ", answer.Reply);
    }

    private sealed class Corpus : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "smartpark-one-knowledge-" + Guid.NewGuid().ToString("N"));
        public string FilePath => System.IO.Path.Combine(Path, "smartpark-knowledge.md");
        public Corpus(string text) { Directory.CreateDirectory(Path); Save(text); }
        public void Save(string text) => File.WriteAllText(FilePath, text);
        public void Dispose()
        {
            foreach (var file in Directory.GetFiles(Path)) File.Delete(file);
            Directory.Delete(Path);
        }
    }
    private sealed class CapturingProvider : HttpMessageHandler
    {
        public List<JsonElement> Inputs { get; } = [];
        public string Instructions { get; private set; } = "";
        public Func<Task>? BeforeReply { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Instructions = body.RootElement.GetProperty("instructions").GetString()!;
            using var input = JsonDocument.Parse(body.RootElement.GetProperty("input").GetString()!);
            Inputs.Add(input.RootElement.Clone());
            if (BeforeReply is not null) await BeforeReply();
            var id = input.RootElement.GetProperty("sources")[0].GetProperty("id").GetString()!;
            var text = JsonSerializer.Serialize(new { reply = "Synthetic answer", sourceIds = new[] { id } });
            return new(HttpStatusCode.OK) { Content = JsonContent(JsonSerializer.Serialize(new {
                status = "completed", output = new[] { new { type = "message", role = "assistant", content = new[] { new { type = "output_text", text } } } },
            })) };
        }
        private static StringContent JsonContent(string text) => new(text, Encoding.UTF8, "application/json");
    }
}
