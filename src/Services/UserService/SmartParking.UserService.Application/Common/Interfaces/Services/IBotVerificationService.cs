namespace UserService.Application.Common.Interfaces.Services;

public interface IBotVerificationService
{
    Task VerifyAsync(string? token, string action, CancellationToken ct, string? remoteIp = null);
}
