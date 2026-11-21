namespace SmartParking.OperationService.Application.Reservations;

// CREATED replays the same order. CLOSED_NO_ORDER is an authoritative, durable fence
// against later creation for this operation. UNKNOWN must never be translated into absence.
public sealed record DepositOrderResult(string State, Guid? OrderId, DepositOrderRequest AcceptedRequest,
    string? OutcomeCode = null);
