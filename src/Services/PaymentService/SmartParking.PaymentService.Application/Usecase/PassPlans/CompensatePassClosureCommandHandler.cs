using System.Security.Cryptography;
using System.Text;
using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Services;

namespace SmartParking.PaymentService.Application.Usecase.PassPlans;

public sealed class CompensatePassClosureCommandHandler(
    ISubscriptionClosureCompensationRepository repository,
    TimeProvider clock) : IRequestHandler<CompensatePassClosureCommand, CompensatePassClosureResponseDto>
{
    public async Task<CompensatePassClosureResponseDto> Handle(
        CompensatePassClosureCommand command,
        CancellationToken ct)
    {
        var req = command.Request;
        var trimmedKey = req.IdempotencyKey.Trim();
        var closureType = req.ClosureType.Trim().ToUpperInvariant();
        var intervals = req.UnavailableIntervals
            .Select(i => new ClosureInterval(i.Start, i.End))
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToArray();

        var payloadHash = ComputePayloadHash(
            req.ClosureId,
            closureType,
            req.SiteId,
            req.TenantId,
            req.PassId,
            req.PaymentOrderId,
            req.PaidPeriodStart,
            req.PaidPeriodEnd,
            intervals);

        await using var tx = await repository.BeginTransactionAsync(ct);

        var order = await repository.GetOrderForUpdateAsync(req.PaymentOrderId, ct);
        if (order is null)
        {
            throw new PaymentDomainException(
                PaymentDomainException.OrderNotFound,
                $"Payment order '{req.PaymentOrderId}' was not found.",
                nameof(req.PaymentOrderId));
        }

        var existing = await repository.GetByIdempotencyKeyAsync(trimmedKey, ct);
        if (existing is not null)
        {
            if (!string.Equals(existing.IdempotencyPayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new PaymentDomainException(
                    "IDEMPOTENCY_CONFLICT",
                    "IdempotencyKey was already used with a different closure compensation payload.");
            }

            var orderComps = await repository.GetByOrderIdAsync(existing.PaymentOrderId, ct);
            var totalOrderCompensated = orderComps.Sum(c => c.RefundAmount);

            return new CompensatePassClosureResponseDto(
                existing.Id,
                existing.RefundRequestId,
                existing.ClosureEventId,
                existing.ClosureType,
                existing.PassId,
                existing.PaymentOrderId,
                existing.RefundAmount,
                totalOrderCompensated,
                "VND",
                existing.RefundAmount > 0m ? "REFUND_REQUESTED" : "NO_REFUND_DUE",
                existing.CompensatedIntervals
                    .Select(i => new ClosureIntervalDto(i.Start, i.End))
                    .ToArray(),
                IsReplayed: true);
        }

        if (order.OrderType != OrderType.MONTHLY_PASS_FEE ||
            order.ReferenceType != ReferenceType.MONTHLY_PASS ||
            order.ReferenceId != req.PassId)
        {
            throw new PaymentDomainException(
                "PASS_ORDER_MISMATCH",
                $"Payment order '{req.PaymentOrderId}' does not match monthly pass '{req.PassId}'.",
                nameof(req.PaymentOrderId));
        }

        if (order.SiteId != req.SiteId || order.TenantId != req.TenantId)
        {
            throw new PaymentDomainException(
                "ORDER_SCOPE_MISMATCH",
                "Payment order does not belong to the specified tenant/site scope.");
        }

        if (order.Status != PaymentOrderStatus.PAID)
        {
            throw new PaymentDomainException(
                "ORDER_NOT_PAID",
                $"Payment order '{req.PaymentOrderId}' cannot be compensated in status '{order.Status}'.");
        }

        var priorCompensations = await repository.GetByOrderIdAsync(order.Id, ct);
        var existingRefundsTotal = await repository.GetNonRejectedRefundRequestsTotalAsync(order.Id, ct);

        var calc = PassClosureCompensationCalculator.Calculate(
            order.FinalAmount,
            req.PaidPeriodStart,
            req.PaidPeriodEnd,
            intervals,
            priorCompensations,
            existingRefundsTotal);

        var now = clock.GetUtcNow();
        RefundRequest? refundRequest = null;

        if (calc.NewlyCompensatedAmount > 0m)
        {
            refundRequest = new RefundRequest
            {
                Id = Guid.NewGuid(),
                TenantId = req.TenantId,
                PaymentOrderId = order.Id,
                Amount = calc.NewlyCompensatedAmount,
                Reason = $"PASS_CLOSURE_{closureType}:{req.ClosureId:D}",
                Status = "REFUND_REQUESTED",
                HandledByOwner = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            await repository.AddRefundRequestAsync(refundRequest, ct);

            var refundTransaction = new PaymentTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = req.TenantId,
                PaymentOrderId = order.Id,
                TransactionType = TransactionType.REFUND,
                PaymentMethod = PaymentMethod.PAYOS,
                IdempotencyKey = $"closure-refund:{trimmedKey}",
                Amount = calc.NewlyCompensatedAmount,
                Status = PaymentStatus.PENDING,
                CreatedAt = now
            };
            await repository.AddRefundTransactionAsync(refundTransaction, ct);
        }

        var compensation = new SubscriptionClosureCompensation
        {
            Id = Guid.NewGuid(),
            TenantId = req.TenantId,
            SiteId = req.SiteId,
            ClosureEventId = req.ClosureId,
            ClosureType = closureType,
            PassId = req.PassId,
            PaymentOrderId = order.Id,
            RefundRequestId = refundRequest?.Id,
            CompensatedFrom = calc.CompensatedFrom,
            CompensatedTo = calc.CompensatedTo,
            CompensatedIntervals = calc.NewlyCompensatedIntervals,
            RefundAmount = calc.NewlyCompensatedAmount,
            IdempotencyKey = trimmedKey,
            IdempotencyPayloadHash = payloadHash,
            CreatedAt = now
        };
        await repository.AddCompensationAsync(compensation, ct);

        await repository.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        return new CompensatePassClosureResponseDto(
            compensation.Id,
            refundRequest?.Id,
            compensation.ClosureEventId,
            compensation.ClosureType,
            compensation.PassId,
            compensation.PaymentOrderId,
            calc.NewlyCompensatedAmount,
            calc.TotalOrderCompensatedAmount,
            "VND",
            calc.NewlyCompensatedAmount > 0m ? "REFUND_REQUESTED" : "NO_REFUND_DUE",
            calc.NewlyCompensatedIntervals
                .Select(i => new ClosureIntervalDto(i.Start, i.End))
                .ToArray(),
            IsReplayed: false);
    }

    private static string ComputePayloadHash(
        Guid closureId,
        string closureType,
        Guid siteId,
        Guid tenantId,
        Guid passId,
        Guid paymentOrderId,
        DateTimeOffset paidPeriodStart,
        DateTimeOffset paidPeriodEnd,
        IReadOnlyList<ClosureInterval> intervals)
    {
        var intervalPart = string.Join(",", intervals.Select(i => $"{i.Start.UtcTicks}-{i.End.UtcTicks}"));
        var raw = string.Join(
            "|",
            closureId.ToString("N"),
            closureType,
            siteId.ToString("N"),
            tenantId.ToString("N"),
            passId.ToString("N"),
            paymentOrderId.ToString("N"),
            paidPeriodStart.UtcTicks,
            paidPeriodEnd.UtcTicks,
            intervalPart);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
