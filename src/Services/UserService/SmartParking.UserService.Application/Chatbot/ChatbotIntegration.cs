namespace UserService.Application.Chatbot;

// Scaffold only: implementations call backend contracts, never duplicate business rules.
public sealed class ChatbotOptions
{
    public bool Enabled { get; set; }
    public string? Provider { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public string[] AllowedIntents { get; set; } = ["FAQ", "PARKING_SEARCH"];
}
public sealed record ChatContext(Guid UserId,string Locale);
public sealed record ChatInput(string Message,ChatContext Context);
public sealed record ChatReply(string Message,string Status);
public interface IChatModel { Task<string> ClassifyIntentAsync(ChatInput input,CancellationToken ct); }
public interface IChatBackendGateway { Task<ChatReply> ExecuteReadAsync(string intent,ChatInput input,CancellationToken ct); }
public sealed class ChatbotCoordinator(ChatbotOptions options,IChatModel model,IChatBackendGateway backend)
{
    public async Task<ChatReply> ReplyAsync(ChatInput input,CancellationToken ct)
    {
        if(!options.Enabled)return new("Chat assistant is unavailable.","disabled");
        if(string.IsNullOrWhiteSpace(input.Message) || input.Message.Length>4000)return new("Invalid request.","invalid");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        var intent=await model.ClassifyIntentAsync(input,timeout.Token);
        if(!options.AllowedIntents.Contains(intent,StringComparer.Ordinal))return new("This action requires the normal application workflow.","handoff");
        return await backend.ExecuteReadAsync(intent,input,timeout.Token);
    }
}
