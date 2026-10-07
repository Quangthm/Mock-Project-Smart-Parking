using UserService.Application.DTOs;

namespace UserService.Application.Common.Interfaces.Services;

public interface IDriverRegistrationService
{
    Task<DriverRegistrationResult> RegisterAsync(DriverRegistrationDto request, CancellationToken ct);
    Task VerifyAsync(VerifyDriverOtpDto request, CancellationToken ct);
    Task<DriverRegistrationResult> ResendAsync(Guid registrationId, CancellationToken ct);
    Task<DriverRegistrationResult> RecoverAsync(string contact,CancellationToken ct);
}

public interface IOtpSender
{
    Task SendAsync(string channel, string destination, string code, CancellationToken ct);
}
