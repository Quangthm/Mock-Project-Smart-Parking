using UserService.Application.Chatbot;
namespace SmartParking.UserService.Tests;
public sealed class ChatbotScaffoldTests
{
    private sealed class Model:IChatModel{public int Calls;public string Intent="FAQ";public Task<string> ClassifyIntentAsync(ChatInput input,CancellationToken ct){Calls++;return Task.FromResult(Intent);}}
    private sealed class Backend:IChatBackendGateway{public int Calls;public Task<ChatReply> ExecuteReadAsync(string intent,ChatInput input,CancellationToken ct){Calls++;return Task.FromResult(new ChatReply("result","completed"));}}
    [Fact]
    public async Task DisabledScaffoldAndWriteIntentNeverInvokeBusinessBackend()
    {
        var options=new ChatbotOptions();var model=new Model();var backend=new Backend();var service=new ChatbotCoordinator(options,model,backend);var input=new ChatInput("hello",new(Guid.NewGuid(),"vi"));
        Assert.Equal("disabled",(await service.ReplyAsync(input,default)).Status);Assert.Equal(0,model.Calls);Assert.Equal(0,backend.Calls);
        options.Enabled=true;model.Intent="REFUND_APPROVE";Assert.Equal("handoff",(await service.ReplyAsync(input,default)).Status);Assert.Equal(0,backend.Calls);
        model.Intent="FAQ";Assert.Equal("completed",(await service.ReplyAsync(input,default)).Status);Assert.Equal(1,backend.Calls);
    }
}
