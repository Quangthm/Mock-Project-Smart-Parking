using MediatR;

namespace SmartParking.OperationService.Application.Discovery;

public sealed record ReadHistoricalLocationQuery : IRequest<HistoricalLocation?>;
