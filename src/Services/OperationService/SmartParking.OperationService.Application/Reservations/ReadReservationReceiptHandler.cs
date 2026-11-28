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
