using MediatR;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Ports;

namespace SmartParking.OperationService.Application.PassPlans.Services;

public sealed class PassPlanSnapshotService(IMediator mediator) : IPassPlanSnapshotService
{
    public Task<FrozenPassPlanSnapshotDto> ValidateAndFreezeAsync(
        ValidateAndFreezePassPlanRequest request,
        CancellationToken ct = default)
        => mediator.Send(new ValidateAndFreezePassPlanCommand(request), ct);
}
