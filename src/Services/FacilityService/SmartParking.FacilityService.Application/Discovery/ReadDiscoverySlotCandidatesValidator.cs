using FluentValidation;

namespace SmartParking.FacilityService.Application.Discovery;

public sealed class ReadDiscoverySlotCandidatesValidator : AbstractValidator<ReadDiscoverySlotCandidatesQuery>
{
    public ReadDiscoverySlotCandidatesValidator()
    {
        RuleFor(q => q.VehicleType).Equal("CAR");
        RuleFor(q => q.Sites).Must(sites => sites is { Length: > 0 and <= 100 } &&
            sites.All(s => s is not null && s.SiteId != Guid.Empty && s.ConfigurationVersion != Guid.Empty) &&
            sites.Select(s => s.SiteId).Distinct().Count() == sites.Length)
            .WithMessage("Provide 1 to 100 distinct published sites and their configuration versions.");
        RuleFor(q => q.At).Must(ValidUtc);
        RuleFor(q => q).Must(q => (q.StartAt, q.EndAt) switch
        {
            (null, null) => true,
            ({ } start, { } end) => ValidUtc(start) && ValidUtc(end) && end > start &&
                end - start <= TimeSpan.FromDays(1),
            _ => false
        }).WithMessage("Supply a complete UTC stay of up to one day.");
    }

    private static bool ValidUtc(DateTimeOffset time) =>
        time != default && time != DateTimeOffset.MaxValue && time.Offset == TimeSpan.Zero;
}
