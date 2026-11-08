using System.Diagnostics;
using MediatR;
namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed class ChatBudget(ChatOptions options)
{
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    public TimeSpan Remaining => TimeSpan.FromMilliseconds(Math.Max(1, options.DeadlineMilliseconds - elapsed.ElapsedMilliseconds));
}
public sealed class ChatDeadlineBehavior<TRequest,TResponse>(ChatBudget budget) : IPipelineBehavior<TRequest,TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not (CreateConversation or SendChat or ReadChat or ListChat or GetAssistantCapabilitiesQuery)) return await next(ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(budget.Remaining);
        try { return await next(deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ChatFailure(504,"REQUEST_TIMEOUT"); }
    }
}
