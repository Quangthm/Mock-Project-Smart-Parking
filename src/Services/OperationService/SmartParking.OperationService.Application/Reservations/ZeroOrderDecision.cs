namespace SmartParking.OperationService.Application.Reservations;

/// <summary>A definitive rejection fences this operation at Payment. Uncertain responses remain recoverable.</summary>
public sealed record ZeroOrderDecision(string State, ZeroOrderBinding Binding, Guid? OrderId, Guid? ReceiptId, string? Code = null);
