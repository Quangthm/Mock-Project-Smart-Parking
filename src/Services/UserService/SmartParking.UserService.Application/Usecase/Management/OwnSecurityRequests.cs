using MediatR;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Application.Usecase.Management;

public sealed record SetupOwnMfaCommand : IRequest<object>;
public sealed record EnableOwnMfaCommand(string Code) : IRequest;
public sealed record GetWorkflowDeliveryQuery(Guid DeliveryId) : IRequest<object>;
public sealed record RetryWorkflowDeliveryCommand(Guid DeliveryId) : IRequest<object>;

public sealed class OwnSecurityRequestHandler(IAccountWorkflows workflows, ICurrentAccessService current)
    : IRequestHandler<SetupOwnMfaCommand, object>, IRequestHandler<EnableOwnMfaCommand>,
      IRequestHandler<GetWorkflowDeliveryQuery, object>, IRequestHandler<RetryWorkflowDeliveryCommand, object>
{
    public async Task<object> Handle(SetupOwnMfaCommand request, CancellationToken ct) =>
        await workflows.SetupMfaAsync((await current.RequireAsync(ct)).User.Id!.Value, ct);
    public async Task Handle(EnableOwnMfaCommand request, CancellationToken ct) =>
        await workflows.EnableMfaAsync((await current.RequireAsync(ct)).User.Id!.Value, request.Code, ct);
    public async Task<object> Handle(GetWorkflowDeliveryQuery request, CancellationToken ct) =>
        await workflows.DeliveryAsync((await current.RequireAsync(ct)).User.Id!.Value, request.DeliveryId, false, ct);
    public async Task<object> Handle(RetryWorkflowDeliveryCommand request, CancellationToken ct) =>
        await workflows.DeliveryAsync((await current.RequireAsync(ct)).User.Id!.Value, request.DeliveryId, true, ct);
}
