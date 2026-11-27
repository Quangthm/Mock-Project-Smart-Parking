using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.SearchAuditProjections;

namespace SmartParking.NotificationAuditService.Tests.Application;

public class SearchAuditProjectionsQueryValidatorTests
{
    private readonly SearchAuditProjectionsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuery_ShouldPass()
    {
        var query = new SearchAuditProjectionsQuery(
            Action: "USER_LOCKED",
            From: DateTimeOffset.UtcNow.AddHours(-1),
            To: DateTimeOffset.UtcNow,
            Page: 1,
            PageSize: 20);

        var result = _validator.Validate(query);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenPageLessThanOne_ShouldFail(int page)
    {
        var result = _validator.Validate(new SearchAuditProjectionsQuery(Page: page));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchAuditProjectionsQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenPageSizeOutOfRange_ShouldFail(int pageSize)
    {
        var result = _validator.Validate(new SearchAuditProjectionsQuery(PageSize: pageSize));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchAuditProjectionsQuery.PageSize));
    }

    [Fact]
    public void Validate_WhenFromGreaterThanTo_ShouldFail()
    {
        var now = DateTimeOffset.UtcNow;
        var result = _validator.Validate(new SearchAuditProjectionsQuery(From: now.AddMinutes(5), To: now));
        Assert.False(result.IsValid);
    }
}
