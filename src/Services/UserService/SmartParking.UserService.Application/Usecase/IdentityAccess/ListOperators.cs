using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.IdentityAccess;

public sealed record ListOperatorsQuery(Guid Actor) : IRequest<IReadOnlyList<OperatorDto>>;
public sealed class ListOperatorsHandler(IIdentityAccessRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<ListOperatorsQuery, IReadOnlyList<OperatorDto>>
{
    public async Task<IReadOnlyList<OperatorDto>> Handle(ListOperatorsQuery request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var tenantIds = await repository.OwnerTenantsAsync(request.Actor, "OPERATOR_MANAGE", cancellationToken);
        if (tenantIds.Length == 0)
            throw new AuthException("FORBIDDEN", "Current Owner permission is required.", 403);
        var operators = await repository.ListOperatorsAsync(request.Actor, tenantIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return operators;
    }
}
