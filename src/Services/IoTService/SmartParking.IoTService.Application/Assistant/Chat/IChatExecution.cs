namespace SmartParking.IoTService.Application.Assistant.Chat;

public interface IChatExecution
{
    IDisposable Admit(Guid account);
    Task<RequestResult> Run(Func<CancellationToken, Task<RequestResult>> work);
}
