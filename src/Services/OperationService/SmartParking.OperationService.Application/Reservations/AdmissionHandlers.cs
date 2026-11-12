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

public sealed class AdmissionMapperProfile : Profile
{
    public AdmissionMapperProfile() => CreateMap<ReservationRequest, ReservationReceipt>()
        .ForCtorParam(nameof(ReservationReceipt.RequestId), opt => opt.MapFrom(x => x.Id))
        .ForCtorParam(nameof(ReservationReceipt.ReservationId), opt => opt.MapFrom(x => x.ReservationId));
}

public sealed class AdmitReservationValidator : AbstractValidator<AdmitReservationCommand>
{
    public AdmitReservationValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Intent).NotNull();
        When(x => x.Intent is not null, () =>
        {
            RuleFor(x => x.Intent.VehicleId).NotEmpty();
            RuleFor(x => x.Intent.SiteId).NotEmpty();
            RuleFor(x => x.Intent.RequestedSlotId).NotEmpty();
            RuleFor(x => x.Intent.TermsId).NotEmpty();
            RuleFor(x => x.Intent.TermsVersion).NotEmpty().MaximumLength(256);
            RuleFor(x => x.Intent.Mode).Equal("SPECIFIC_SLOT");
            RuleFor(x => x.Intent.PaymentMethod).NotEmpty().MaximumLength(32);
            RuleFor(x => x.Intent.End).GreaterThan(x => x.Intent.Start);
        });
    }
}

public sealed class AdmitReservationHandler(ICurrentActor actor, IReservationRequestRepository requests,
    IUnitOfWork work, IAccountAccessRequester access, TimeProvider clock, IMapper mapper)
    : IRequestHandler<AdmitReservationCommand, ReservationReceipt>
{
    public async Task<ReservationReceipt> Handle(AdmitReservationCommand command, CancellationToken ct)
    {
        var (userId, accountId) = AdmissionActor.RequireDriver(actor);
        var intent = command.Intent with { Start = command.Intent.Start.ToUniversalTime(), End = command.Intent.End.ToUniversalTime() };
        var json = JsonSerializer.Serialize(intent);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        await using var transaction = await work.BeginTransactionAsync(ct);
        await requests.LockAdmissionAsync(accountId, command.IdempotencyKey, ct);
        var prior = await requests.FindByKeyAsync(accountId, command.IdempotencyKey, ct);
        if (prior is not null)
        {
            if (prior.Fingerprint != fingerprint)
                throw new ReservationGateException(409, "IDEMPOTENCY_KEY_REUSED", "The key is already bound to another request.");
            return mapper.Map<ReservationReceipt>(prior);
        }
        var now = clock.GetUtcNow();
        if (intent.Start <= now)
            throw new ReservationGateException(400, "INVALID_REQUEST", "Reservation start must be in the future.");
        var request = ReservationRequest.Admit(userId, accountId, command.IdempotencyKey, json, fingerprint, now);
        requests.Add(request);
        await access.RequestAsync(AdmissionMessage.For(request), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return mapper.Map<ReservationReceipt>(request);
    }
}

public sealed class ReadReservationReceiptHandler(ICurrentActor actor, IReservationRequestRepository requests, IMapper mapper)
    : IRequestHandler<ReadReservationReceiptQuery, ReservationReceipt>
{
    public async Task<ReservationReceipt> Handle(ReadReservationReceiptQuery query, CancellationToken ct)
    {
        var (_, accountId) = AdmissionActor.RequireDriver(actor);
        var request = await requests.FindOwnAsync(accountId, query.RequestId, ct)
            ?? throw new ReservationGateException(404, "REQUEST_NOT_FOUND", "Request was not found.");
        return mapper.Map<ReservationReceipt>(request);
    }
}

public sealed class AdvanceReservationAuthorizationHandler(IReservationRequestRepository requests,
    IUnitOfWork work, IAccountAccessRequester access, TimeProvider clock)
    : IRequestHandler<AdvanceReservationAuthorizationCommand>
{
    public async Task Handle(AdvanceReservationAuthorizationCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request is null) return;
        if (request.Advance(clock.GetUtcNow())) await access.RequestAsync(AdmissionMessage.For(request), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

public sealed class RecordAccountAccessDecisionHandler(IReservationRequestRepository requests, IUnitOfWork work, TimeProvider clock)
    : IRequestHandler<RecordAccountAccessDecisionCommand>
{
    public async Task Handle(RecordAccountAccessDecisionCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var decision = command.Decision;
        var request = await requests.LockAsync(decision.WorkflowId, ct);
        if (request is null || request.UserId != decision.UserId || request.AccountId != decision.AccountId ||
            decision.Role != "driver" || request.Operation != decision.Operation) return;
        request.AcceptDecision(decision.AttemptId, decision.AttemptNumber, decision.DecisionId,
            decision.DecidedAt, decision.Allowed, clock.GetUtcNow());
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

internal static class AdmissionActor
{
    public static (Guid User, Guid Account) RequireDriver(ICurrentActor actor) =>
        actor.UserId is { } user && user != Guid.Empty && actor.AccountId is { } account && account != Guid.Empty && actor.Role == "driver"
            ? (user, account) : throw new ReservationGateException(403, "FORBIDDEN", "Driver access is required.");
}

internal static class AdmissionMessage
{
    public static AccountAccessRequestV1 For(ReservationRequest request) => new(request.Id, request.AttemptId,
        request.AttemptNumber, request.UserId, request.AccountId, "driver", request.Operation,
        request.AdmittedAt, request.AttemptDeadline, request.OverallDeadline);
}
