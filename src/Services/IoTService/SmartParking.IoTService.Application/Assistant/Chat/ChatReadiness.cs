using SmartParking.IoTService.Application.Assistant.Guest;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed class ChatReadiness(ChatOptions options, IChatStore store, IAssistantManagementStore settings,
    IPublicKnowledge knowledge, AssistantStageMetrics? metrics = null)
{
    public async Task<ChatSnapshot> Require(CancellationToken ct)
    {
        using var measurement = metrics?.Start("chat_readiness");
        if (!options.ReleaseEnabled) throw new ChatFailure(503, "ASSISTANT_DISABLED");
        try
        {
            var state = await settings.ReadAsync(ct).WaitAsync(ct);
            if (!state.Enabled) throw new ChatFailure(503, "ASSISTANT_DISABLED");
            if (!await store.Ready(ct)) throw new ChatFailure(503, "STORAGE_UNAVAILABLE");
            var corpus = await knowledge.ReadAsync(ct).WaitAsync(ct) ?? throw new ChatFailure(503, "RETRIEVAL_UNAVAILABLE");
            return new(state.Revision, corpus.Fingerprint, corpus.Version);
        }
        catch (ManagementUnavailableException) { throw new ChatFailure(503, "DEPENDENCY_UNAVAILABLE"); }
    }
}
