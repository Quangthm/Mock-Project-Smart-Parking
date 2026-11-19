using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record CommitQuoteHoldCommand(Guid RequestId) : IRequest;
