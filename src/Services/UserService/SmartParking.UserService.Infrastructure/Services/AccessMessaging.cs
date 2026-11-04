using MassTransit;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public sealed class OperatorAccessChangedConsumer(IAccessChangeNotifier notifier)
    : IConsumer<OperatorAccessChanged>
{
    public async Task Consume(ConsumeContext<OperatorAccessChanged> context)
    {
        // Repeat invalidation is safe. Retrying after a notification failure must not lose the hint.
        // Clients reload authoritative state; they never apply event payloads as access decisions.
        await notifier.NotifyAsync(context.Message, context.CancellationToken);
    }
}
