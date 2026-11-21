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
