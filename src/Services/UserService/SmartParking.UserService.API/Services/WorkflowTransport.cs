using UserService.Application.Common.Interfaces.Services;
using UserService.Persistence;
using Microsoft.EntityFrameworkCore;
namespace UserService.API.Services;
public sealed class QueuedOtpSender(AppDbContext db,IAccountWorkflows workflows):IOtpSender
{
    public async Task SendAsync(string channel,string destination,string code,CancellationToken ct)
    {
        var user=db.Users.Local.Single(u=>u.DeletedOn==null && u.Status==SmartParking.UserService.Domain.Enum.UserStatus.PendingVerification && (u.Email==destination || u.Phone==destination));
        await db.WorkflowDeliveries.Where(d=>d.UserId==user.Id && d.Kind=="DRIVER_OTP" && d.Status!="sent").ExecuteUpdateAsync(s=>s.SetProperty(d=>d.Status,"cancelled").SetProperty(d=>d.ProtectedPayload,""),ct);
        await workflows.QueueAsync(user.Id!.Value,user.Id.Value,"DRIVER_OTP",new(channel,destination,"SmartPark verification",$"Your code is {code}. Valid for 5 minutes."),ct);
    }
}
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
