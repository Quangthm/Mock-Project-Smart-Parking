using SmartParking.IoTService.Application.Assistant.Knowledge;

namespace SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

// Document excerpts are first-party guidance, never a fabricated model answer or live business state.
public static class DemoKnowledgeGuidance
{
    public static GroundedReply Read(string directory, string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(message) || message.Length > 4000)
            throw new DemoGroundedUnavailableException(DemoGroundedFailure.Configuration);
        try
        {
            message = DemoGroundedGenerator.SanitizeMessage(message);
            var language = DemoKnowledgeCorpus.AnswerLanguage(message);
            var articles = DemoKnowledgeCorpus.Retrieve(DemoKnowledgeCorpus.Load(directory), message);
            ct.ThrowIfCancellationRequested();
            var text = articles.Length == 0 ? DemoGroundedGenerator.ScopeMessage(language) : language == "en"
                ? "The AI service is temporarily unavailable. The original Vietnamese SmartPark guidance is shown below. You can continue using the normal application workflow."
                : "Trợ lý AI đang tạm gián đoạn. Dưới đây là hướng dẫn gốc từ tài liệu SmartPark để bạn tiếp tục thao tác trên ứng dụng.";
            // Full validated sections: truncating a policy could omit a crucial condition/exception.
            var sources = articles.Select(a => new GroundedSource(a.Id, a.Version, a.Title, a.Content)).ToArray();
            return new(text, sources, null, null, "knowledge");
        }
        catch (DemoGroundedUnavailableException ex) when (ex.Failure == DemoGroundedFailure.Unspecified)
        { throw new DemoGroundedUnavailableException(DemoGroundedFailure.Knowledge); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        { throw new DemoGroundedUnavailableException(DemoGroundedFailure.Knowledge); }
    }
}
