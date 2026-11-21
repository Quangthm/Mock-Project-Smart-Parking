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

public sealed class AdvanceReservationAuthorizationHandler(IReservationRequestRepository requests,
    IUnitOfWork work, IAccountAccessRequester access, TimeProvider clock, IAdmissionMessages? saga = null)
    : IRequestHandler<AdvanceReservationAuthorizationCommand>
{
    public async Task Handle(AdvanceReservationAuthorizationCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request is null) return;
        if (request.AccountCheck is not null)
        {
            if (request.State != "CHECKING" || saga is null) return;
            await saga.WakeAsync(request.Id, ct);
        }
        else if (request.Advance(clock.GetUtcNow())) await access.RequestAsync(AdmissionMessage.For(request), ct);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
