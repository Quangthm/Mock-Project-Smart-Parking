using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Tests;

public sealed partial class QuoteBookingDatabaseTests
{
    [QuoteDatabaseFact]
    public async Task InactiveZeroQuotaAllowsAdmissionAndActiveZeroRejects()
    {
        await using var services = Services();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var quota = NewQuota(0);
        db.Add(quota);
        await db.SaveChangesAsync();

        var blocked = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(blocked));
        var outcome = await db.ReservationRequests.AsNoTracking().SingleAsync(r => r.Id == blocked);
        Assert.Equal(("REJECTED", "RESERVATION_QUOTA_EXCEEDED"), (outcome.State, outcome.Code));
        Assert.False(await db.Reservations.AnyAsync(r => r.Id == blocked));

        quota.IsActive = false;
        await db.SaveChangesAsync();
        var allowed = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(allowed));
        Assert.Equal("PENDING_PAYMENT", await db.Reservations.Where(r => r.Id == allowed).Select(r => r.Status).SingleAsync());
    }

    [QuoteDatabaseFact]
    public async Task QuotaIgnoresOtherScopesAndExpiredHolds()
    {
        await using var services = Services();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var otherSite = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var siteQuota = NewQuota(0);
        siteQuota.SiteId = otherSite;
        var tenantQuota = NewQuota(0);
        tenantQuota.TenantId = otherTenant;
        var vehicleQuota = NewQuota(0);
        vehicleQuota.VehicleType = "MOTORCYCLE";
        db.AddRange(NewQuota(1), siteQuota, tenantQuota, vehicleQuota);

        ReservationRow Claim(Guid tenantId, Guid siteId, string vehicleType) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SiteId = siteId, AccountId = Guid.NewGuid(),
            TargetSlotId = Guid.NewGuid(), Status = "CONFIRMED", VehicleType = vehicleType,
            ExpectedStartTime = start, ExpectedEndTime = Intent.End, CreatedAt = now
        };
        var expired = Claim(tenant, site, "CAR");
        expired.Status = "PENDING_PAYMENT";
        expired.CreatedAt = now.AddMinutes(-5);
        expired.HoldExpiresAt = now.AddMinutes(-1);
        var remote = Claim(tenant, otherSite, "CAR");
        remote.NormalizedPlate = plate;
        db.AddRange(remote, Claim(otherTenant, site, "CAR"),
            Claim(tenant, site, "MOTORCYCLE"), expired);
        await db.SaveChangesAsync();

        var id = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(id));
        Assert.Equal("PENDING_PAYMENT", await db.Reservations.Where(r => r.Id == id).Select(r => r.Status).SingleAsync());
    }

    [QuoteDatabaseFact]
    public async Task QuotaSchemaEnforcesNonnegativeValuesAndUniqueScope()
    {
        await using var services = Services();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var invalid = NewQuota(-1);
        db.Add(invalid);
        var negative = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(negative.InnerException).SqlState);
        db.ChangeTracker.Clear();

        db.Add(NewQuota(10_000));
        await db.SaveChangesAsync();
        db.Add(NewQuota(1));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
    }

    [QuoteDatabaseFact]
    public async Task QuotaReadFailureLeavesAdmissionUncommittedAndRetryCanAdmit()
    {
        await using var services = Services();
        var id = await Prepare(services, Guid.NewGuid());
        quotaReadFault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => Send(services, new CommitQuoteHoldCommand(id)));
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        Assert.Equal("EVALUATING", await db.ReservationRequests.Where(r => r.Id == id).Select(r => r.State).SingleAsync());
        Assert.False(await db.Reservations.AnyAsync(r => r.Id == id));
        Assert.False(await db.Set<OutboxMessage>().AnyAsync(m => m.MessageType.Contains("EnsureReservationOrderV1") &&
            m.Body.Contains(id.ToString())));

        quotaReadFault.Enabled = false;
        await Send(services, new CommitQuoteHoldCommand(id));
        Assert.True(await db.Reservations.AnyAsync(r => r.Id == id && r.Status == "PENDING_PAYMENT"));
    }

    [QuoteDatabaseFact]
    public async Task LateSettlementWithQuotaReadFailureRollsBackAndRetryAppliesTheQuota()
    {
        await using var services = Services();
        var id = await SeedTimedHold(services, late: true, zero: false);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        db.Add(NewQuota(0));
        await db.SaveChangesAsync();
        var evidence = Evidence(id);
        var dispositions = db.Set<OutboxMessage>().Where(m => m.MessageType.Contains("RequestReservationDispositionV1") &&
            m.Body.Contains(id.ToString()));

        quotaReadFault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => Send(services, new RecordReservationPaymentCommand(evidence)));
        var unchanged = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal("AWAITING_COLLECTION", unchanged.ConfirmationState);
        Assert.Null(unchanged.CollectionId);
        Assert.False(await dispositions.AnyAsync());

        quotaReadFault.Enabled = false;
        await Send(services, new RecordReservationPaymentCommand(evidence));
        await Send(services, new RecordReservationPaymentCommand(evidence));
        var denied = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal("NO_ENTITLEMENT", denied.ConfirmationState);
        Assert.Equal("RESERVATION_QUOTA_EXCEEDED", denied.ConfirmationCode);
        Assert.Equal(evidence.PaymentTransactionId, denied.CollectionId);
        Assert.Single(await dispositions.ToListAsync());
    }

    private ReservationQuotaConfig NewQuota(int maximum)
    {
        var now = DateTimeOffset.UtcNow;
        return new() { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site, VehicleType = "CAR",
            MaxReservationQuota = maximum, IsActive = true, CreatedAt = now, UpdatedAt = now };
    }
}
