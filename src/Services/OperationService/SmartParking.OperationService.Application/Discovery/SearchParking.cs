using MediatR;

namespace SmartParking.OperationService.Application.Discovery;

public sealed record SearchParking(SearchFilter Filter, string? Cursor) : IRequest<SearchPage>;
