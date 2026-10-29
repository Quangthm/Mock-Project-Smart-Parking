using MediatR;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Management;

public sealed record CreateOperatorCommand(CreateOperatorDto Body) : IRequest<OperatorDto>;
public sealed record ListOperatorsQuery : IRequest<IReadOnlyList<OperatorDto>>;
public sealed record GetOperatorAssignmentsQuery : IRequest<IReadOnlyList<OperatorAssignment>>;

public sealed class OperatorRequestHandler(IOperatorProvisioningService operators, ICurrentAccessService current,
    IAccountAuthorizationService authorization)
    : IRequestHandler<CreateOperatorCommand, OperatorDto>, IRequestHandler<ListOperatorsQuery, IReadOnlyList<OperatorDto>>,
      IRequestHandler<GetOperatorAssignmentsQuery, IReadOnlyList<OperatorAssignment>>
{
    private async Task<Guid> RequireOwnerAsync(CancellationToken ct)
    {
        var id = (await current.RequireAsync(ct)).User.Id!.Value;
        await authorization.RequireOwnerManagementAsync(id, false, ct);
        return id;
    }
    public async Task<OperatorDto> Handle(CreateOperatorCommand request, CancellationToken ct) =>
        await operators.CreateAsync(await RequireOwnerAsync(ct), request.Body, ct);
    public async Task<IReadOnlyList<OperatorDto>> Handle(ListOperatorsQuery request, CancellationToken ct) =>
        await operators.ListAsync(await RequireOwnerAsync(ct), ct);
    public async Task<IReadOnlyList<OperatorAssignment>> Handle(GetOperatorAssignmentsQuery request, CancellationToken ct) =>
        await authorization.OperatorAssignmentsAsync((await current.RequireAsync(ct)).User.Id!.Value, ct);
}
