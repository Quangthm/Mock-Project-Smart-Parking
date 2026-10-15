using SmartParking.UserService.Domain.Entities;
using UserService.Application.DTOs;
using UserService.Application.Services;
namespace UserService.Application.Common.Interfaces.Services;
public interface IAccountWorkflows
{
    Task<ChallengeResult> RequestLoginAsync(string contact,CancellationToken ct);
    Task<UserSessionDto> CompleteLoginAsync(CompleteOtpLoginDto body,AuthSessionService sessions,CancellationToken ct);
    Task VerifyOwnerAsync(Guid id,string code,CancellationToken ct);
    Task<ChallengeResult> ResendOwnerAsync(Guid id,CancellationToken ct);
    Task<object> RecoverOwnerAsync(string contact,CancellationToken ct);
    Task<MfaStatusDto> MfaStatusAsync(Guid id, CancellationToken ct);
    Task<MfaSetupDto> SetupMfaAsync(Guid id, MfaVerificationDto body, CancellationToken ct);
    Task<MfaRecoveryCodesDto> EnableMfaAsync(Guid id, string code, CancellationToken ct);
    Task DisableMfaAsync(Guid id, MfaVerificationDto body, CancellationToken ct);
    Task<MfaRecoveryCodesDto> RegenerateRecoveryCodesAsync(Guid id, MfaVerificationDto body, CancellationToken ct);
    Task<UserSessionDto> CompleteMfaLoginAsync(CompleteMfaLoginDto body, AuthSessionService sessions, CancellationToken ct);
    Task<object> DeliveryAsync(Guid actor,Guid id,bool retry,CancellationToken ct);
    Task<Guid> QueueAsync(Guid userId,Guid actorId,string kind,WorkflowMessage message,CancellationToken ct,Guid? challengeId=null);
    Task DeliverOneAsync(IWorkflowTransport transport,CancellationToken ct);
}
