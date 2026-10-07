using UserService.Application.Chatbot;
namespace UserService.Infrastructure.Services;
public sealed class UnconfiguredChatModel:IChatModel
{
    public Task<string> ClassifyIntentAsync(ChatInput input,CancellationToken ct)=>Task.FromResult("UNCONFIGURED");
}
public sealed class UnconfiguredChatBackend:IChatBackendGateway
{
    public Task<ChatReply> ExecuteReadAsync(string intent,ChatInput input,CancellationToken ct)=>Task.FromResult(new ChatReply("Backend integration is pending.","pending"));
}
