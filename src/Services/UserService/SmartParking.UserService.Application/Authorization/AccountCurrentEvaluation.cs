using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Application.Authorization;

public interface IAccountAccessDecisionRepository
{
    Task LockWorkflowAsync(Guid workflowId, CancellationToken ct);
    Task<AccountAccessDecision?> FindAsync(Guid workflowId, CancellationToken ct);
    void Add(AccountAccessDecision decision);
}

public interface IAccountAuthorityRepository
{
    Task<CurrentAccountAuthority?> ReadAsync(Guid userId, Guid accountId, DateTimeOffset now, CancellationToken ct);
}

public interface IAccountAccessDecisionPublisher
{
    Task SendAsync(AccountAccessDecisionV1 decision, CancellationToken ct);
}

public sealed record EvaluateAccountCurrentCommand(AccountAccessRequestV1 Request) : IRequest;

public sealed class EvaluateAccountCurrentHandler(IAccountAuthorityRepository accounts,
    IAccountAccessDecisionRepository decisions, IAccountAccessDecisionPublisher publisher,
    IUnitOfWork work, TimeProvider clock) : IRequestHandler<EvaluateAccountCurrentCommand>
{
    public async Task Handle(EvaluateAccountCurrentCommand command, CancellationToken ct)
    {
        var request = command.Request;
        if (request.WorkflowId == Guid.Empty || request.AttemptId == Guid.Empty || request.UserId == Guid.Empty ||
            request.AccountId == Guid.Empty || request.AttemptNumber is < 1 or > 3 || request.Role != "driver" ||
            request.Operation is not ("CREATE_RESERVATION" or "CANCEL_OWN_RESERVATION") ||
            request.AttemptDeadline > request.OverallDeadline || request.AdmittedAt >= request.AttemptDeadline ||
            request.OverallDeadline - request.AdmittedAt > TimeSpan.FromSeconds(90))
            throw new ArgumentException("Invalid current-account message contract.");

        // Delivery attempt IDs/deadlines may change. The workflow's actor, operation and original
        // time budget may not. Exactly one persisted result is reused across those deliveries.
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            request.WorkflowId, request.UserId, request.AccountId, request.Role, request.Operation,
            AdmittedAt = request.AdmittedAt.ToUniversalTime(), OverallDeadline = request.OverallDeadline.ToUniversalTime()
        }))));
        await using var transaction = await work.BeginTransactionAsync(ct);
        await decisions.LockWorkflowAsync(request.WorkflowId, ct);
        var decision = await decisions.FindAsync(request.WorkflowId, ct);
        if (decision is not null && decision.RequestFingerprint != fingerprint)
            throw new ArgumentException("Authorization workflow identity was reused with different input.");
        var now = clock.GetUtcNow();
        // Expired delivery is not a denial and must not trigger a new evaluation or replay.
        if (now < request.AdmittedAt || now >= request.AttemptDeadline || now >= request.OverallDeadline) return;
        if (decision is null)
        {
            var authority = await accounts.ReadAsync(request.UserId, request.AccountId, now, ct);
            decision = AccountAccessDecision.Evaluate(request.WorkflowId,
                request.UserId, request.AccountId, request.Role, request.Operation, fingerprint, authority, clock.GetUtcNow());
            // Persist a completed first evaluation even if this delivery timed out while reading.
            // A later timely delivery can replay it; never replace it with a second result.
            decisions.Add(decision);
        }
        if (clock.GetUtcNow() < request.AttemptDeadline && clock.GetUtcNow() < request.OverallDeadline)
            await publisher.SendAsync(new(decision.Id, decision.WorkflowId, request.AttemptId, request.AttemptNumber,
                decision.UserId, decision.AccountId, decision.Role, decision.Operation, decision.DecidedAt,
                decision.Allowed, decision.Allowed ? "ALLOWED" : "CURRENT_ACCESS_DENIED"), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
