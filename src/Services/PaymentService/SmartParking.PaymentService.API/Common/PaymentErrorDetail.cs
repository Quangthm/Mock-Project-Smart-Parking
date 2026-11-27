namespace SmartParking.PaymentService.API.Common;

public readonly record struct PaymentErrorDetail(string Field, string Message, string? Code = null);
