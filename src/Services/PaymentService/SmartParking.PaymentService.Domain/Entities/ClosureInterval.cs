namespace SmartParking.PaymentService.Domain.Entities;

public sealed record ClosureInterval(DateTimeOffset Start, DateTimeOffset End);
