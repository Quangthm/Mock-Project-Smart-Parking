namespace UserService.Application.Common.Interfaces.Services;

public interface IWorkflowProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
public sealed record WorkflowMessage(string Channel, string Destination, string Subject, string Body, string? OtpCode = null, string? EmailTemplate = null);
public static class WorkflowEmailTemplates
{
    public const string OwnerApproved = "owner-approved";
    public const string OwnerRejected = "owner-rejected";
    public const string MfaSecurityChange = "mfa-security-change";
}
public interface IWorkflowTransport
{
    Task SendAsync(WorkflowMessage message, Guid deliveryId, CancellationToken ct);
}
