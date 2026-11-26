using MediatR;
using SmartParking.OperationService.Application.PassPlans.DTOs;

namespace SmartParking.OperationService.Application.PassPlans.Commands;

public sealed record SavePassPlanDraftCommand(
    Guid SiteId,
    string ProductType,
    Guid? ActorUserId,
    SavePassPlanDraftRequest Request) : IRequest<SavePassPlanDraftResponseDto>;
