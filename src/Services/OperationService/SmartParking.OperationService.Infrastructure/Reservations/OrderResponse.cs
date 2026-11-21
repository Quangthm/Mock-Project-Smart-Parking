using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record OrderResponse(string State, Guid? OrderId, DepositOrderRequest? AcceptedRequest, Guid? ReceiptId);
