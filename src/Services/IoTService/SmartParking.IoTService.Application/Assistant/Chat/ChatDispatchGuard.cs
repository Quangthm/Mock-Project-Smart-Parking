using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Application.Assistant.Chat;

internal sealed class ChatDispatchGuard(IManagedAnswerProvider inner, Func<CancellationToken, Task> guard) : IManagedAnswerProvider
{
    public async Task<GroundedReply> GenerateAsync(string provider, string model, string secret, string message, CancellationToken ct)
    { await guard(ct); return await inner.GenerateAsync(provider, model, secret, message, ct); }
    public async Task<GroundedReply> KnowledgeAsync(string message, CancellationToken ct)
    { await guard(ct); return await inner.KnowledgeAsync(message, ct); }
}
