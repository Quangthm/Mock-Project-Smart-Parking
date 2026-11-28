using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Domain.Services;

public sealed record PassClosureCompensationResult(
    IReadOnlyList<ClosureInterval> NewlyCompensatedIntervals,
    decimal NewlyCompensatedAmount,
    decimal TotalOrderCompensatedAmount,
    DateTimeOffset? CompensatedFrom,
    DateTimeOffset? CompensatedTo);
