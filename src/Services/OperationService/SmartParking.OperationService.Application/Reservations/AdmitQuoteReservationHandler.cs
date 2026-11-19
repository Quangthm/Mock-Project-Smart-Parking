using MediatR;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class AdmitQuoteReservationHandler(ICurrentActor actor, IReservationRequestRepository requests,
    IUnitOfWork work, IQuoteAdmissionStarter starter, TimeProvider clock)
    : IRequestHandler<AdmitQuoteReservation, ReservationReceipt>
{
    public async Task<ReservationReceipt> Handle(AdmitQuoteReservation command, CancellationToken ct)
    {
        var (user, account) = AdmissionActor.RequireDriver(actor);
        var intent = command.Intent with
        {
            LicensePlate = ReservationCarPlate.Canonicalize(command.Intent.LicensePlate)!,
            Start = command.Intent.Start.ToUniversalTime(), End = command.Intent.End.ToUniversalTime()
        };
        var json = JsonSerializer.Serialize(intent);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        await using var transaction = await work.BeginTransactionAsync(ct);
        await requests.LockAdmissionAsync(account, command.IdempotencyKey, ct);
        var prior = await requests.FindByKeyAsync(account, command.IdempotencyKey, ct);
        if (prior is not null)
        {
            if (prior.AccountCheck is null || prior.Fingerprint != fingerprint)
                throw new ReservationGateException(409, "IDEMPOTENCY_KEY_REUSED", "The key is bound to another request.");
            return new(prior.Id, prior.State, prior.ReservationId, prior.Code, 3);
        }
        var now = clock.GetUtcNow();
        if (intent.Start <= now) throw new ReservationGateException(400, "INVALID_REQUEST", "Start must be in the future.");
        var request = ReservationRequest.AdmitQuote(user, account, command.IdempotencyKey, json, fingerprint, now);
        requests.Add(request);
        await starter.StartAsync(request.Id, ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(request.Id, request.State, null, null, 3);
    }
}
