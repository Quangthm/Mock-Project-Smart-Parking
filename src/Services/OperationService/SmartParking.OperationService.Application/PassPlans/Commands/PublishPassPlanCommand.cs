using MediatR;
using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.Application.PassPlans.Commands;

public sealed record PublishPassPlanCommand(
    Guid SiteId,
    string ProductType,
    Guid? ActorUserId,
    string? IdempotencyKey,
    PublishPassPlanRequest? Request) : IRequest<PublishPassPlanResponseDto>;
