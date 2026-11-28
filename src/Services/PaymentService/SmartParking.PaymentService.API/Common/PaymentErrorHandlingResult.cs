namespace SmartParking.PaymentService.API.Common;

public readonly record struct PaymentErrorHandlingResult(PaymentErrorResponse Response, bool IsUnexpected = false);
