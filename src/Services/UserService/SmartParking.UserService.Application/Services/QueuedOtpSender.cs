using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
namespace UserService.Application.Services;
public sealed class QueuedOtpSender(IIdentityWorkflowStore store, IAccountWorkflows workflows) : IOtpSender
{
    public async Task SendAsync(string channel, string destination, string code, CancellationToken ct)
    {
        var userId = store.PendingOtpRecipient(destination);
        await store.CancelDriverOtpDeliveriesAsync(userId, ct);
        await workflows.QueueAsync(userId, userId, "DRIVER_OTP", OtpWorkflowMessage.Create(channel, destination, code), ct);
    }
}
