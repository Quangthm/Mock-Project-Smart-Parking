namespace SmartParking.OperationService.Application.Discovery;

public sealed record CandidateBatch(DateTimeOffset AsOf, DateTimeOffset At, DateTimeOffset? StartAt, DateTimeOffset? EndAt, CandidateSite[] Items);
