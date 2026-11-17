using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Repositories;

namespace SmartParking.OperationService.Tests;

public sealed class PlatformArrivalEnablementValidationTests
{
    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDbContext<ReservationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IReservationPolicyRepository, ReservationPolicyRepository>();
        return services.BuildServiceProvider();
    }

    private static CreatePolicyDraftCommand Create(bool? enabled) => new(null, null,
        15, 15, 24, 7, 0, 4, 60, 15, 4, 15, 2, 15, 180, 5, enabled, "Asia/Ho_Chi_Minh", null);

    private static UpdatePolicyDraftCommand Update(CreatePolicyDraftResultDto draft, bool? enabled) => new(
        draft.Id, draft.UpdatedAt, null, null, 16, 15, 24, 7, 0, 4, 60, 15, 4, 15, 2, 15, 180, 5,
        enabled, "Asia/Ho_Chi_Minh", null);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_ExplicitSiteEnablement_IsRejectedBeforePersistence(bool enabled)
    {
        await using var provider = Provider();
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var error = await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(Create(enabled)));

        Assert.Contains(error.Errors, failure => failure.PropertyName == "ArrivalLateToleranceEnabled");
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        Assert.Empty(await db.Set<OperationalPolicyRow>().ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_ExplicitSiteEnablement_IsRejectedWithoutChangingDraft(bool enabled)
    {
        await using var provider = Provider();
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var draft = await mediator.Send(Create(null));
        var error = await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(Update(draft, enabled)));

        Assert.Contains(error.Errors, failure => failure.PropertyName == "ArrivalLateToleranceEnabled");
        var row = await scope.ServiceProvider.GetRequiredService<ReservationDbContext>()
            .Set<OperationalPolicyRow>().SingleAsync();
        Assert.Equal(15, row.PaymentHoldDurationMinutes);
        Assert.Null(row.ArrivalLateToleranceEnabled);
        Assert.Equal(draft.UpdatedAt, row.UpdatedAt);
    }

    [Fact]
    public void NullEnablement_RemainsValidForCreateAndUpdate()
    {
        var command = Create(null);
        Assert.True(new CreatePolicyDraftCommandValidator().Validate(command).IsValid);
        var draft = new CreatePolicyDraftResultDto(Guid.NewGuid(), 1, "PLATFORM", "DRAFT", false,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Assert.True(new UpdatePolicyDraftCommandValidator().Validate(Update(draft, null)).IsValid);
    }
}
