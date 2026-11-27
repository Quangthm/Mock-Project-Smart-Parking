using MediatR;
using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.Application.PassPlans.Commands;

public sealed record ValidateAndFreezePassPlanCommand(ValidateAndFreezePassPlanRequest Request)
    : IRequest<FrozenPassPlanSnapshotDto>;
