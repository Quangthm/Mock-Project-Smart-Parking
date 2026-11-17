using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record GetPolicyRevisionByIdQuery(Guid Id) : IRequest<PolicyRevisionListItemDto?>;

public sealed class GetPolicyRevisionByIdHandler(IReservationPolicyRepository policies)
    : IRequestHandler<GetPolicyRevisionByIdQuery, PolicyRevisionListItemDto?>
{
    public Task<PolicyRevisionListItemDto?> Handle(GetPolicyRevisionByIdQuery query, CancellationToken ct) =>
        policies.GetRevisionByIdAsync(query.Id, ct);
}
