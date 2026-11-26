using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Domain.Services;

public static class PassClosureCompensationCalculator
{
    public static PassClosureCompensationResult Calculate(
        decimal orderFinalAmount,
        DateTimeOffset paidPeriodStart,
        DateTimeOffset paidPeriodEnd,
        IReadOnlyList<ClosureInterval> requestedIntervals,
        IReadOnlyList<SubscriptionClosureCompensation> priorCompensations,
        decimal existingNonRejectedRefundsTotal)
    {
        if (paidPeriodEnd <= paidPeriodStart)
        {
            throw new PaymentDomainException(
                "INVALID_CLOSURE_INTERVALS",
                "paidPeriodEnd must be greater than paidPeriodStart.",
                nameof(paidPeriodEnd));
        }

        if (orderFinalAmount <= 0m)
        {
            throw new PaymentDomainException(
                "CLOSURE_COMPENSATION_EXCEEDS_ORDER",
                "Paid order amount must be positive.",
                nameof(orderFinalAmount));
        }

        var clippedRequested = ClipAndMerge(requestedIntervals, paidPeriodStart, paidPeriodEnd);

        var priorRaw = new List<ClosureInterval>();
        foreach (var prior in priorCompensations)
        {
            if (prior.CompensatedIntervals is { Count: > 0 })
            {
                priorRaw.AddRange(prior.CompensatedIntervals);
            }
            else if (prior.CompensatedFrom.HasValue && prior.CompensatedTo.HasValue)
            {
                priorRaw.Add(new ClosureInterval(prior.CompensatedFrom.Value, prior.CompensatedTo.Value));
            }
        }

        var clippedPrior = ClipAndMerge(priorRaw, paidPeriodStart, paidPeriodEnd);
        var newlyCompensated = SubtractIntervals(clippedRequested, clippedPrior);

        var priorClosuresTotal = priorCompensations.Sum(c => c.RefundAmount);
        var baselineRefunded = Math.Max(priorClosuresTotal, existingNonRejectedRefundsTotal);

        if (newlyCompensated.Count == 0)
        {
            return new PassClosureCompensationResult(
                [],
                0m,
                priorClosuresTotal,
                null,
                null);
        }

        decimal totalPeriodTicks = (paidPeriodEnd - paidPeriodStart).Ticks;
        decimal uncoveredTicks = newlyCompensated.Sum(i => (i.End - i.Start).Ticks);
        var unrounded = orderFinalAmount * (uncoveredTicks / totalPeriodTicks);
        decimal newlyAmount = ParkingPricingCalculator.ApplyRounding(
            unrounded,
            MonetaryRoundingMode.HALF_UP_THOUSAND);

        if (newlyAmount <= 0m)
        {
            return new PassClosureCompensationResult(
                [],
                0m,
                priorClosuresTotal,
                null,
                null);
        }

        if (baselineRefunded + newlyAmount > orderFinalAmount)
        {
            throw new PaymentDomainException(
                "CLOSURE_COMPENSATION_EXCEEDS_ORDER",
                $"Cumulative compensation {baselineRefunded + newlyAmount:0.##} exceeds paid order amount {orderFinalAmount:0.##}.");
        }

        return new PassClosureCompensationResult(
            newlyCompensated,
            newlyAmount,
            priorClosuresTotal + newlyAmount,
            newlyCompensated[0].Start,
            newlyCompensated[^1].End);
    }

    private static List<ClosureInterval> ClipAndMerge(
        IReadOnlyList<ClosureInterval> raw,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        var clipped = new List<ClosureInterval>(raw.Count);
        foreach (var item in raw)
        {
            if (item.End <= item.Start)
            {
                continue;
            }

            var start = item.Start < windowStart ? windowStart : item.Start;
            var end = item.End > windowEnd ? windowEnd : item.End;
            if (end > start)
            {
                clipped.Add(new ClosureInterval(start, end));
            }
        }

        if (clipped.Count <= 1)
        {
            return clipped;
        }

        clipped.Sort((a, b) =>
        {
            var cmp = a.Start.CompareTo(b.Start);
            return cmp != 0 ? cmp : a.End.CompareTo(b.End);
        });

        var merged = new List<ClosureInterval>(clipped.Count) { clipped[0] };
        for (var i = 1; i < clipped.Count; i++)
        {
            var last = merged[^1];
            var next = clipped[i];
            if (next.Start <= last.End)
            {
                if (next.End > last.End)
                {
                    merged[^1] = new ClosureInterval(last.Start, next.End);
                }
            }
            else
            {
                merged.Add(next);
            }
        }

        return merged;
    }

    private static List<ClosureInterval> SubtractIntervals(
        IReadOnlyList<ClosureInterval> targets,
        IReadOnlyList<ClosureInterval> subtracts)
    {
        if (targets.Count == 0)
        {
            return [];
        }

        if (subtracts.Count == 0)
        {
            return [.. targets];
        }

        var result = new List<ClosureInterval>();
        foreach (var target in targets)
        {
            var cursor = target.Start;
            foreach (var sub in subtracts)
            {
                if (sub.End <= cursor)
                {
                    continue;
                }

                if (sub.Start >= target.End)
                {
                    break;
                }

                if (sub.Start > cursor)
                {
                    result.Add(new ClosureInterval(cursor, sub.Start));
                }

                if (sub.End > cursor)
                {
                    cursor = sub.End;
                }

                if (cursor >= target.End)
                {
                    break;
                }
            }

            if (cursor < target.End)
            {
                result.Add(new ClosureInterval(cursor, target.End));
            }
        }

        return result;
    }
}
