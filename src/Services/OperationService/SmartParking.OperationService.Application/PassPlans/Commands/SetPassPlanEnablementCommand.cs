using MediatR;
using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.Application.PassPlans.Commands;

public sealed record SetPassPlanEnablementCommand(
    Guid SiteId,
    string ProductType,
    Guid? ActorUserId,
    SetPassPlanEnablementRequest Request) : IRequest<SetPassPlanEnablementResponseDto>;
