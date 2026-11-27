using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationQuartzDeadlineTests : IRequestHandler<CheckAdmissionDeadline, ReservationDeadlineResult>
{
    private int calls;
    private volatile bool repaired;
    private DateTimeOffset expected;
    private readonly TaskCompletionSource<DateTimeOffset> firstDecision = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ReservationDeadlineResult> Handle(CheckAdmissionDeadline command, CancellationToken ct)
    {
        Assert.Equal(expected, command.ExpectedAt);
        var attempt = Interlocked.Increment(ref calls);
        if (attempt == 1)
        {
            firstDecision.TrySetResult(DateTimeOffset.UtcNow);
            return Task.FromResult(ReservationDeadlineResult.Early(TimeSpan.FromSeconds(2)));
        }
        if (!repaired) throw new IOException("Injected local deadline execution failure.");
        return Task.FromResult(ReservationDeadlineResult.Completed);
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<CheckAdmissionDeadline>());
        services.AddSingleton<IRequestHandler<CheckAdmissionDeadline, ReservationDeadlineResult>>(this);
        services.AddQuartz(q => q.ConfigureScheduler(s => s.InstanceName = Guid.NewGuid().ToString()));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SchedulerClockAlreadyPastDeadlineUsesRemainingDatabaseInterval()
    {
        await using var provider = BuildProvider();
        var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = limit.Token;
        var subject = Guid.NewGuid();
        expected = DateTimeOffset.UtcNow.AddMinutes(-1);
        repaired = true;
        var key = new JobKey($"Admission-{subject:N}-{expected.UtcTicks}", ReservationDeadlineJob.Group);
        var job = JobBuilder.Create<ReservationDeadlineJob>().WithIdentity(key).StoreDurably()
            .UsingJobData("Kind", "Admission").UsingJobData("SubjectId", subject.ToString("D"))
            .UsingJobData("ExpectedAt", expected.ToString("O")).Build();
        try
        {
            await scheduler.ScheduleJob(job, TriggerBuilder.Create().ForJob(key).StartNow().Build(), cancellationToken: ct);
            await scheduler.Start(ct);
            var first = await firstDecision.Task.WaitAsync(ct);
            while (await scheduler.GetJobDetail(key, ct) is not null) await Task.Delay(20, ct);
            Assert.Equal(2, calls);
            Assert.True(DateTimeOffset.UtcNow - first >= TimeSpan.FromSeconds(1.5));
        }
        finally { await scheduler.Shutdown(); }
    }

    [Fact]
    public async Task NativeOneShotRearmsThenRetainsExhaustedJobForExactKeyRedrive()
    {
        await using var provider = BuildProvider();
        var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = limit.Token;
        var subject = Guid.NewGuid();
        expected = DateTimeOffset.UtcNow.AddSeconds(2);
        var key = new JobKey($"Admission-{subject:N}-{expected.UtcTicks}", ReservationDeadlineJob.Group);
        var job = JobBuilder.Create<ReservationDeadlineJob>().WithIdentity(key).StoreDurably().RequestRecovery()
            .UsingJobData("Kind", "Admission").UsingJobData("SubjectId", subject.ToString("D"))
            .UsingJobData("ExpectedAt", expected.ToString("O")).Build();
        try
        {
            await scheduler.ScheduleJob(job, TriggerBuilder.Create().ForJob(key).StartNow().Build(), cancellationToken: ct);
            await scheduler.Start(ct);
            while (Volatile.Read(ref calls) < 5 || (await scheduler.GetTriggersOfJob(key, ct)).Count > 0)
                await Task.Delay(100, ct);
            Assert.NotNull(await scheduler.GetJobDetail(key, ct));
            Assert.Equal(5, calls); // one early firing, then one failure plus three retries
            repaired = true;
            await scheduler.TriggerJob(key, cancellationToken: ct);
            while (await scheduler.GetJobDetail(key, ct) is not null) await Task.Delay(50, ct);
            Assert.Equal(6, calls);
        }
        finally { await scheduler.Shutdown(); }
    }

    [Fact]
    public void SchedulerAndDeadlineConsumerRemainRegisteredOnAdmissionOnlyHost()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservationAdmission:Enabled"] = "true",
            ["ConnectionStrings:Reservation"] = "Host=127.0.0.1;Port=1;Database=unused",
            ["ReservationAdmission:BrokerPassword"] = "isolated-test",
        }).Build();
        var services = new ServiceCollection();
        services.AddReservationMessaging(config);
        Assert.Contains(services, s => s.ServiceType == typeof(ISchedulerFactory));
        Assert.Contains(services, s => s.ServiceType == typeof(RegisterReservationDeadlineConsumer));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(ReservationAdmissionMachine));
    }

    [Theory]
    [InlineData(null, 15)]
    [InlineData("", 15)]
    [InlineData("1", 1)]
    [InlineData("90", 90)]
    [InlineData("0", 0)]
    [InlineData("91", 0)]
    public void AdmissionTimeoutBindingAndDisabledScheduler(string? setting, int expectedSeconds)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ReservationAdmission:OverallTimeoutSeconds"] = setting }).Build();
        var services = new ServiceCollection();
        services.AddReservationMessaging(config);
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<ISchedulerFactory>());
        var options = provider.GetRequiredService<IOptions<ReservationAdmissionOptions>>();
        if (expectedSeconds == 0) Assert.Throws<OptionsValidationException>(() => options.Value);
        else Assert.Equal(expectedSeconds, options.Value.OverallTimeoutSeconds);
    }
}
