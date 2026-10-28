namespace SmartParking.PaymentService.Domain.Pricing;

public sealed class ParkingPricingCalculator : IParkingPricingCalculator
{
    public PricingCalculationResult Calculate(PricingCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        if (request.Policy == null)
        {
            throw new PricingDomainException("INVALID_PRICING_POLICY", "Policy cannot be null.", "policy");
        }

        if (request.Policy.TariffId == Guid.Empty)
        {
            throw new PricingDomainException("INVALID_PRICING_POLICY", "TariffId cannot be empty.", "policy.tariffId");
        }

        if (string.IsNullOrWhiteSpace(request.Policy.TariffName))
        {
            throw new PricingDomainException("INVALID_PRICING_POLICY", "TariffName cannot be empty.", "policy.tariffName");
        }

        if (request.Policy.PolicyVersion < 1)
        {
            throw new PricingDomainException("INVALID_PRICING_POLICY", "PolicyVersion must be greater than or equal to 1.", "policy.policyVersion");
        }

        if (request.VehicleType != request.Policy.VehicleType)
        {
            throw new PricingDomainException("VEHICLE_TYPE_MISMATCH", $"Vehicle type '{request.VehicleType}' does not match policy vehicle type '{request.Policy.VehicleType}'.", "vehicleType");
        }

        if (request.Policy.FreeParkingGraceMinutes < 0)
        {
            throw new PricingDomainException("INVALID_PRICING_POLICY", "FreeParkingGraceMinutes cannot be negative.", "policy.freeParkingGraceMinutes");
        }

        if (request.Policy.TimeBlocks == null || request.Policy.TimeBlocks.Count == 0)
        {
            throw new PricingDomainException("INVALID_PRICING_POLICY", "Policy must contain at least one time block.", "policy.timeBlocks");
        }

        var timeZone = ResolveTimeZone(request.Policy.TimeZoneId);

        var localEntryTime = TimeZoneInfo.ConvertTime(request.EntryTime, timeZone);
        var localExitTime = TimeZoneInfo.ConvertTime(request.ExitTime, timeZone);

        if (localExitTime < localEntryTime)
        {
            throw new PricingDomainException("INVALID_TIME_RANGE", "ExitTime must be greater than or equal to EntryTime.", "exitTime");
        }

        var normalizedSlices = NormalizeAndValidateTimeBlocks(request.Policy.TimeBlocks);

        decimal totalParkedMinutes = (decimal)(localExitTime - localEntryTime).Ticks / TimeSpan.TicksPerMinute;
        int invoiceRoundedMinutes = (int)Math.Ceiling(totalParkedMinutes);

        if (totalParkedMinutes == 0m)
        {
            bool isFree = request.Policy.FreeParkingGraceMinutes > 0;
            return new PricingCalculationResult(
                Currency: "VND",
                LocalEntryTime: localEntryTime,
                LocalExitTime: localExitTime,
                TimeZoneId: request.Policy.TimeZoneId,
                TotalParkedMinutes: 0m,
                InvoiceRoundedMinutes: 0,
                BilledMinutes: 0m,
                FreeParkingGraceMinutes: request.Policy.FreeParkingGraceMinutes,
                IsFreeStay: isFree,
                UnroundedTotalVnd: 0m,
                FinalAmountVnd: 0L,
                RoundingMode: request.Policy.RoundingMode,
                AppliedTariffId: request.Policy.TariffId,
                AppliedTariffName: request.Policy.TariffName.Trim(),
                AppliedPolicyVersion: request.Policy.PolicyVersion,
                Segments: Array.Empty<PricingSegmentBreakdown>());
        }

        if (request.Policy.FreeParkingGraceMinutes > 0 && totalParkedMinutes <= request.Policy.FreeParkingGraceMinutes)
        {
            return new PricingCalculationResult(
                Currency: "VND",
                LocalEntryTime: localEntryTime,
                LocalExitTime: localExitTime,
                TimeZoneId: request.Policy.TimeZoneId,
                TotalParkedMinutes: totalParkedMinutes,
                InvoiceRoundedMinutes: invoiceRoundedMinutes,
                BilledMinutes: 0m,
                FreeParkingGraceMinutes: request.Policy.FreeParkingGraceMinutes,
                IsFreeStay: true,
                UnroundedTotalVnd: 0m,
                FinalAmountVnd: 0L,
                RoundingMode: request.Policy.RoundingMode,
                AppliedTariffId: request.Policy.TariffId,
                AppliedTariffName: request.Policy.TariffName.Trim(),
                AppliedPolicyVersion: request.Policy.PolicyVersion,
                Segments: Array.Empty<PricingSegmentBreakdown>());
        }

        var segments = new List<PricingSegmentBreakdown>();
        decimal unroundedTotalVnd = 0m;
        int segmentIndex = 1;

        var startDate = DateOnly.FromDateTime(localEntryTime.DateTime);
        var endDate = DateOnly.FromDateTime(localExitTime.DateTime);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var dayStart = date == startDate
                ? localEntryTime
                : new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), localEntryTime.Offset);

            var dayEnd = date == endDate
                ? localExitTime
                : new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), localEntryTime.Offset);

            foreach (var slice in normalizedSlices)
            {
                var sliceStartDto = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), localEntryTime.Offset).Add(slice.StartSpan);
                var sliceEndDto = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), localEntryTime.Offset).Add(slice.EndSpan);

                var segStart = dayStart > sliceStartDto ? dayStart : sliceStartDto;
                var segEnd = dayEnd < sliceEndDto ? dayEnd : sliceEndDto;

                if (segEnd > segStart)
                {
                    decimal overlapMinutes = (decimal)(segEnd - segStart).Ticks / TimeSpan.TicksPerMinute;
                    decimal rawChargeVnd = (overlapMinutes / 60m) * slice.Rule.RatePerHourVnd;

                    unroundedTotalVnd += rawChargeVnd;

                    segments.Add(new PricingSegmentBreakdown(
                        SegmentIndex: segmentIndex++,
                        LocalDate: date,
                        SegmentStart: segStart,
                        SegmentEnd: segEnd,
                        BlockCode: slice.Rule.BlockCode,
                        OverlapMinutes: overlapMinutes,
                        RatePerHourVnd: slice.Rule.RatePerHourVnd,
                        RawChargeVnd: rawChargeVnd));
                }
            }
        }

        long finalAmountVnd = ApplyRounding(unroundedTotalVnd, request.Policy.RoundingMode);

        return new PricingCalculationResult(
            Currency: "VND",
            LocalEntryTime: localEntryTime,
            LocalExitTime: localExitTime,
            TimeZoneId: request.Policy.TimeZoneId,
            TotalParkedMinutes: totalParkedMinutes,
            InvoiceRoundedMinutes: invoiceRoundedMinutes,
            BilledMinutes: totalParkedMinutes,
            FreeParkingGraceMinutes: request.Policy.FreeParkingGraceMinutes,
            IsFreeStay: false,
            UnroundedTotalVnd: unroundedTotalVnd,
            FinalAmountVnd: finalAmountVnd,
            RoundingMode: request.Policy.RoundingMode,
            AppliedTariffId: request.Policy.TariffId,
            AppliedTariffName: request.Policy.TariffName.Trim(),
            AppliedPolicyVersion: request.Policy.PolicyVersion,
            Segments: segments);
    }

    public static long ApplyRounding(decimal unroundedTotalVnd, MonetaryRoundingMode mode) => mode switch
    {
        MonetaryRoundingMode.HALF_UP_WHOLE_VND => (long)Math.Round(unroundedTotalVnd, 0, MidpointRounding.AwayFromZero),
        MonetaryRoundingMode.CEILING_WHOLE_VND => (long)Math.Ceiling(unroundedTotalVnd),
        MonetaryRoundingMode.HALF_UP_THOUSAND => (long)(Math.Round(unroundedTotalVnd / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m),
        MonetaryRoundingMode.CEILING_THOUSAND => (long)(Math.Ceiling(unroundedTotalVnd / 1000m) * 1000m),
        _ => (long)Math.Round(unroundedTotalVnd, 0, MidpointRounding.AwayFromZero)
    };

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new PricingDomainException("INVALID_TIMEZONE", "TimeZoneId cannot be empty.", "policy.timeZoneId");
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz))
        {
            return tz;
        }

        if (timeZoneId.Equals("Asia/Ho_Chi_Minh", StringComparison.OrdinalIgnoreCase) ||
            timeZoneId.Equals("SE Asia Standard Time", StringComparison.OrdinalIgnoreCase))
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById("SE Asia Standard Time", out tz)) return tz;
            if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Ho_Chi_Minh", out tz)) return tz;
        }

        throw new PricingDomainException("INVALID_TIMEZONE", $"Invalid or unsupported timeZoneId '{timeZoneId}'.", "policy.timeZoneId");
    }

    private sealed record NormalizedSlice(TimeSpan StartSpan, TimeSpan EndSpan, TimeBlockRule Rule);

    private static List<NormalizedSlice> NormalizeAndValidateTimeBlocks(IReadOnlyList<TimeBlockRule> timeBlocks)
    {
        var slices = new List<NormalizedSlice>();

        foreach (var block in timeBlocks)
        {
            if (string.IsNullOrWhiteSpace(block.BlockCode))
            {
                throw new PricingDomainException("INVALID_PRICING_POLICY", "BlockCode cannot be empty.", "policy.timeBlocks");
            }
            if (block.RatePerHourVnd < 0)
            {
                throw new PricingDomainException("INVALID_PRICING_POLICY", "RatePerHourVnd cannot be negative.", "policy.timeBlocks");
            }

            var startSpan = block.StartTime.ToTimeSpan();
            var endSpan = block.EndTime.ToTimeSpan();

            if (startSpan == TimeSpan.Zero && endSpan == TimeSpan.Zero)
            {
                if (timeBlocks.Count == 1)
                {
                    slices.Add(new NormalizedSlice(TimeSpan.Zero, TimeSpan.FromHours(24), block));
                }
                else
                {
                    throw new PricingDomainException("INVALID_PRICING_POLICY", "Invalid 24h block configuration.", "policy.timeBlocks");
                }
            }
            else if (startSpan == endSpan)
            {
                throw new PricingDomainException("INVALID_PRICING_POLICY", "TimeBlock cannot have zero length.", "policy.timeBlocks");
            }
            else if (startSpan > TimeSpan.Zero && endSpan == TimeSpan.Zero)
            {
                slices.Add(new NormalizedSlice(startSpan, TimeSpan.FromHours(24), block));
            }
            else if (startSpan < endSpan)
            {
                slices.Add(new NormalizedSlice(startSpan, endSpan, block));
            }
            else
            {
                slices.Add(new NormalizedSlice(TimeSpan.Zero, endSpan, block));
                slices.Add(new NormalizedSlice(startSpan, TimeSpan.FromHours(24), block));
            }
        }

        var sortedSlices = slices.OrderBy(s => s.StartSpan).ThenBy(s => s.EndSpan).ToList();

        for (int i = 0; i < sortedSlices.Count - 1; i++)
        {
            if (sortedSlices[i + 1].StartSpan < sortedSlices[i].EndSpan)
            {
                throw new PricingDomainException("OVERLAPPING_TIME_BLOCKS", "Time blocks overlap.", "policy.timeBlocks");
            }
        }

        if (sortedSlices[0].StartSpan > TimeSpan.Zero ||
            sortedSlices[^1].EndSpan < TimeSpan.FromHours(24))
        {
            throw new PricingDomainException("UNCOVERED_TIME_BLOCKS", "Time blocks do not cover 24 hours.", "policy.timeBlocks");
        }

        for (int i = 0; i < sortedSlices.Count - 1; i++)
        {
            if (sortedSlices[i + 1].StartSpan > sortedSlices[i].EndSpan)
            {
                throw new PricingDomainException("UNCOVERED_TIME_BLOCKS", "Time blocks do not cover 24 hours.", "policy.timeBlocks");
            }
        }

        return sortedSlices;
    }
}
