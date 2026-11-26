using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Reservations;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Persistence.Adapters;

namespace SmartParking.PaymentService.Persistence.Repositories;

public sealed class ReviewedTariffRepository(PaymentDbContext db) : IReviewedTariffRepository
{
    public async Task<(Guid RevisionId, PricingPolicySnapshot Policy)?> FindAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var active = db.Tariffs.AsNoTracking().Where(t => t.TenantId == tenantId && t.SiteId == siteId && t.VehicleType == "CAR")
            .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id).Take(1);
        var match = await (from tariff in active
            join revision in db.PolicyRevisions on tariff.Id equals revision.PolicyId
            where revision.PolicyType == "TARIFF" && revision.EffectiveFrom <= now &&
                (revision.EffectiveTo == null || now < revision.EffectiveTo)
            orderby revision.RevisionNumber descending
            select new { Tariff = tariff, Revision = revision }).FirstOrDefaultAsync(ct);
        if (match == null) return null;

        using var doc = JsonDocument.Parse(match.Revision.PolicyValuesJson);
        var rules = doc.RootElement;
        if (rules.ValueKind != JsonValueKind.Object ||
            !(rules.TryGetProperty("timeBlocks", out var blocks) || rules.TryGetProperty("time_blocks", out blocks)) ||
            blocks.ValueKind != JsonValueKind.Array || blocks.GetArrayLength() == 0 ||
            blocks.EnumerateArray().Any(block => !ValidBlock(block))) return null;
        match.Tariff.TariffRulesJson = match.Revision.PolicyValuesJson;
        return (match.Revision.Id, TariffSnapshotParser.Parse(match.Tariff, match.Revision.RevisionNumber));
    }

    private static bool ValidBlock(JsonElement block) => block.ValueKind == JsonValueKind.Object &&
        (block.TryGetProperty("startTime", out var start) || block.TryGetProperty("start_time", out start)) &&
        TimeOnly.TryParse(start.GetString(), out _) &&
        (block.TryGetProperty("endTime", out var end) || block.TryGetProperty("end_time", out end)) &&
        TimeOnly.TryParse(end.GetString(), out _) &&
        (block.TryGetProperty("ratePerHourVnd", out var rate) || block.TryGetProperty("rate_per_hour_vnd", out rate) ||
         block.TryGetProperty("pricePerHourVnd", out rate)) && rate.TryGetInt64(out var value) && value >= 0;
}
