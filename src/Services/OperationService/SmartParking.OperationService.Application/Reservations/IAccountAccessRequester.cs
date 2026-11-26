using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public interface IAccountAccessRequester
{
    Task RequestAsync(AccountAccessRequestV1 request, CancellationToken ct);
}
