using System.Security.Cryptography;
using System.Text;
using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class UpsertPassFinancialTermsCommandHandler(
    IOwnerSiteAccessPort siteAccessPort,
    ISubscriptionFinancialTermRepository repository,
    TimeProvider clock) : IRequestHandler<UpsertPassFinancialTermsCommand, UpsertPassFinancialTermsResponseDto>
{
    public async Task<UpsertPassFinancialTermsResponseDto> Handle(
        UpsertPassFinancialTermsCommand command,
        CancellationToken ct)
    {
        var req = command.Request;
        var productType = req.ProductType.Trim().ToUpperInvariant();
        var scope = await siteAccessPort.ValidateOwnerSiteAsync(command.SiteId, ct);
        var actorId = scope.UserId;

        if (productType == "YEARLY")
        {
            throw new PaymentDomainException(
                "YEARLY_POLICY_BLOCKED_Q05",
                "Yearly financial terms are currently blocked pending policy approval Q05.",
                "productType");
        }

        if (req.RefundPolicy is null || string.IsNullOrWhiteSpace(req.RefundPolicy.RoundingMode))
        {
            throw new PaymentDomainException(
                "INVALID_FINANCIAL_TERMS",
                "refundPolicy and roundingMode are required for MONTHLY pass financial terms.",
                "refundPolicy");
        }

        var domainRefundPolicy = new SubscriptionRefundPolicy(
            req.RefundPolicy.VoluntaryBeforeStartPercent,
            req.RefundPolicy.VoluntaryInPeriodPercent,
            req.RefundPolicy.FacilityClosurePercent,
            req.RefundPolicy.RoundingMode.Trim().ToUpperInvariant());

        if (!domainRefundPolicy.IsM05Compliant())
        {
            throw new PaymentDomainException(
                "INVALID_FINANCIAL_TERMS",
                "MONTHLY refundPolicy must enforce 100/50/100 percentages and HALF_UP_THOUSAND rounding.",
                "refundPolicy");
        }

        var normalizedPrices = req.VehiclePrices
            .Select(vp => new SubscriptionVehiclePrice(
                vp.VehicleType.Trim().ToUpperInvariant(),
                vp.FullPeriodPrice))
            .OrderBy(vp => vp.VehicleType, StringComparer.Ordinal)
            .ToArray();

        var currency = req.Currency.Trim().ToUpperInvariant();
        var trimmedKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? null
            : command.IdempotencyKey.Trim();
        var payloadHash = ComputePayloadHash(
            command.SiteId,
            command.PlanId,
            productType,
            req.PlanRevision,
            currency,
            normalizedPrices,
            domainRefundPolicy);

        await using var tx = await repository.BeginTransactionAsync(command.SiteId, command.PlanId, ct);

        var term = await repository.GetBySiteAndPlanAsync(command.SiteId, command.PlanId, ct);
        if (term is not null && trimmedKey is not null)
        {
            var existingRevision = await repository.GetRevisionByIdempotencyKeyAsync(term.Id, trimmedKey, ct);
            if (existingRevision is not null)
            {
                if (!string.Equals(existingRevision.IdempotencyPayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new PaymentDomainException(
                        "IDEMPOTENCY_CONFLICT",
                        "Idempotency-Key was already used with a different financial terms payload.");
                }

                return new UpsertPassFinancialTermsResponseDto(
                    term.Id,
                    term.PlanId,
                    existingRevision.PlanRevision,
                    existingRevision.RevisionNumber,
                    term.ProductType,
                    term.Currency,
                    existingRevision.VehiclePrices
                        .Select(vp => new PassVehiclePriceDto(vp.VehicleType, vp.FullPeriodPrice))
                        .ToArray(),
                    existingRevision.EffectiveFrom,
                    IsReplayed: true);
            }
        }

        var now = clock.GetUtcNow();
        int newRevisionNumber;

        if (term is null)
        {
            newRevisionNumber = 1;
            term = new SubscriptionFinancialTerm
            {
                Id = Guid.NewGuid(),
                TenantId = scope.TenantId,
                SiteId = command.SiteId,
                PlanId = command.PlanId,
                ProductType = productType,
                ActiveRevision = newRevisionNumber,
                Currency = currency,
                IsActive = true,
                CreatedBy = actorId,
                CreatedAt = now,
                UpdatedAt = now
            };
            await repository.AddTermAsync(term, ct);
        }
        else
        {
            var previousRev = await repository.GetRevisionAsync(term.Id, term.ActiveRevision, ct);
            if (previousRev is not null && previousRev.EffectiveTo is null)
            {
                previousRev.EffectiveTo = now;
                await repository.UpdateRevisionAsync(previousRev, ct);
            }

            var maxRev = term.Revisions.Count > 0
                ? term.Revisions.Max(r => r.RevisionNumber)
                : term.ActiveRevision;
            newRevisionNumber = maxRev + 1;
            term.ProductType = productType;
            term.ActiveRevision = newRevisionNumber;
            term.Currency = currency;
            term.IsActive = true;
            term.UpdatedAt = now;
            await repository.UpdateTermAsync(term, ct);
            await repository.CloseActivePolicyRevisionsAsync(term.Id, now, ct);
        }

        var revision = new SubscriptionFinancialTermRevision
        {
            Id = Guid.NewGuid(),
            FinancialTermId = term.Id,
            PlanId = command.PlanId,
            PlanRevision = req.PlanRevision,
            RevisionNumber = newRevisionNumber,
            VehiclePrices = normalizedPrices,
            RefundPolicy = domainRefundPolicy,
            EffectiveFrom = now,
            EffectiveTo = null,
            IdempotencyKey = trimmedKey,
            IdempotencyPayloadHash = payloadHash,
            CreatedBy = actorId,
            CreatedAt = now
        };
        await repository.AddRevisionAsync(revision, ct);

        await repository.AddSubscriptionPolicyRevisionAsync(
            term.Id,
            command.SiteId,
            command.PlanId,
            req.PlanRevision,
            newRevisionNumber,
            productType,
            currency,
            normalizedPrices,
            domainRefundPolicy,
            now,
            actorId,
            ct);

        await repository.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        return new UpsertPassFinancialTermsResponseDto(
            term.Id,
            term.PlanId,
            revision.PlanRevision,
            revision.RevisionNumber,
            term.ProductType,
            term.Currency,
            normalizedPrices
                .Select(vp => new PassVehiclePriceDto(vp.VehicleType, vp.FullPeriodPrice))
                .ToArray(),
            revision.EffectiveFrom,
            IsReplayed: false);
    }

    private static string ComputePayloadHash(
        Guid siteId,
        Guid planId,
        string productType,
        int planRevision,
        string currency,
        IReadOnlyList<SubscriptionVehiclePrice> prices,
        SubscriptionRefundPolicy refundPolicy)
    {
        var pricePart = string.Join(",", prices.Select(p => $"{p.VehicleType}:{p.FullPeriodPrice:0.00}"));
        var raw = string.Join(
            "|",
            siteId.ToString("N"),
            planId.ToString("N"),
            productType,
            planRevision,
            currency,
            pricePart,
            refundPolicy.VoluntaryBeforeStartPercent,
            refundPolicy.VoluntaryInPeriodPercent,
            refundPolicy.FacilityClosurePercent,
            refundPolicy.RoundingMode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
