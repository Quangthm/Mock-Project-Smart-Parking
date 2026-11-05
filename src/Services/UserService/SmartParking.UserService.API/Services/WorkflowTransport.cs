using UserService.Application.Common.Interfaces.Services;
namespace UserService.API.Services;
public sealed class WorkflowDeliveryWorker(IServiceScopeFactory scopes,ILogger<WorkflowDeliveryWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(5));
        while(await timer.WaitForNextTickAsync(ct))
        {
            try {await using var scope=scopes.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<IAccountWorkflows>().DeliverOneAsync(scope.ServiceProvider.GetRequiredService<IWorkflowTransport>(),ct);}
            catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception e){logger.LogError("Workflow delivery unavailable ({Type})",e.GetType().Name);}
        }
    }
}
