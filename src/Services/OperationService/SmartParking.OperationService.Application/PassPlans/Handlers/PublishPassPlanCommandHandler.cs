using MediatR;
using SmartParking.OperationService.Application.PassPlans.Commands;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Application.PassPlans.Services;
using SmartParking.OperationService.Domain.PassPlans;

namespace SmartParking.OperationService.Application.PassPlans.Handlers;

public sealed class PublishPassPlanCommandHandler(
    IPassPlanFacilityScopePort scopePort,
    IPassPlanRepository repository,
    TimeProvider clock)
    : IRequestHandler<PublishPassPlanCommand, PublishPassPlanResponseDto>
{
    public async Task<PublishPassPlanResponseDto> Handle(PublishPassPlanCommand command, CancellationToken ct)
    {
        var productType = PassPlanHelper.ParseProductType(command.ProductType);
        var scope = await scopePort.ValidateOwnerSiteAsync(command.SiteId, ct);
        var actorId = command.ActorUserId ?? scope.UserId;
        var publishNote = command.Request?.PublishNote;
        var trimmedKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? null
            : command.IdempotencyKey.Trim();

        await using var tx = await repository.BeginSiteLockTransactionAsync(command.SiteId, ct);

        var plan = await repository.GetPlanAsync(command.SiteId, productType, ct);

        if (trimmedKey is not null && plan is not null)
        {
            var existingRevision = await repository.GetRevisionByIdempotencyKeyAsync(plan.Id, trimmedKey, ct);
            if (existingRevision is not null)
            {
                if (plan.DraftSpec is not null)
                {
                    var currentHash = PassPlanHelper.ComputePublishHash(
                        command.SiteId,
                        productType,
                        plan.DraftSpec,
                        publishNote);

                    if (!string.Equals(existingRevision.IdempotencyPayloadHash, currentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new PassPlanException(
                            409,
                            "IDEMPOTENCY_CONFLICT",
                            "Idempotency-Key was already used with a different draft payload.");
                    }
                }
                else
                {
                    var expectedNote = publishNote?.Trim() ?? string.Empty;
                    var existingNote = existingRevision.PublishNote ?? string.Empty;
                    if (!string.Equals(expectedNote, existingNote, StringComparison.Ordinal))
                    {
                        throw new PassPlanException(
                            409,
                            "IDEMPOTENCY_CONFLICT",
                            "Idempotency-Key was already used with a different publish request.");
                    }
                }

                return new PublishPassPlanResponseDto(
                    plan.Id,
                    plan.SiteId,
                    plan.ProductType.ToString(),
                    existingRevision.RevisionNumber,
                    existingRevision.EffectiveFrom,
                    plan.IsEnabled,
                    IsReplayed: true);
            }
        }

        if (plan is null || plan.DraftSpec is null)
        {
            throw new PassPlanException(
                404,
                "PASS_PLAN_DRAFT_NOT_FOUND",
                "No draft exists to publish for this pass plan.");
        }

        var timeZoneId = await repository.GetEffectiveTimeZoneAsync(command.SiteId, ct);
        var payloadHash = trimmedKey is null
            ? null
            : PassPlanHelper.ComputePublishHash(command.SiteId, productType, plan.DraftSpec, publishNote);

        var revision = plan.Publish(
            actorId,
            timeZoneId,
            publishNote,
            trimmedKey,
            payloadHash,
            clock.GetUtcNow());

        await repository.AddRevisionAsync(revision, ct);
        await repository.UpdatePlanAsync(plan, ct);
        await repository.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        return new PublishPassPlanResponseDto(
            plan.Id,
            plan.SiteId,
            plan.ProductType.ToString(),
            revision.RevisionNumber,
            revision.EffectiveFrom,
            plan.IsEnabled,
            IsReplayed: false);
    }
}
