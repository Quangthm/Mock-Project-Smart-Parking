using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMapper;
using FluentValidation;
using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class RecordAccountAccessDecisionHandler(IReservationRequestRepository requests, IUnitOfWork work, TimeProvider clock)
    : IRequestHandler<RecordAccountAccessDecisionCommand>
{
    public async Task Handle(RecordAccountAccessDecisionCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var decision = command.Decision;
        var request = await requests.LockAsync(decision.WorkflowId, ct);
        if (request is null || request.AccountCheck is not null ||
            request.UserId != decision.UserId || request.AccountId != decision.AccountId ||
            decision.Role != "driver" || request.Operation != decision.Operation) return;
        request.AcceptDecision(decision.AttemptId, decision.AttemptNumber, decision.DecisionId,
            decision.DecidedAt, decision.Allowed, clock.GetUtcNow());
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
