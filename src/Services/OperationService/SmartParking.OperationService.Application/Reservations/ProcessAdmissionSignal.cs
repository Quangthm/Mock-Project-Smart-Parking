using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ProcessAdmissionSignal(Guid RequestId, object Signal) : IRequest<string>;
