namespace UserService.Application.Common.Interfaces.Services;

public interface IWorkflowProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
public sealed record WorkflowMessage(string Channel, string Destination, string Subject, string Body);
public interface IWorkflowTransport
{
    Task SendAsync(WorkflowMessage message, Guid deliveryId, CancellationToken ct);
}
