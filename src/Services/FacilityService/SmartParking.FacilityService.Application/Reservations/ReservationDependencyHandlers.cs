using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed class DependencyIntentValidator : AbstractValidator<DependencyIntent>
{
    public DependencyIntentValidator()
    {
        RuleFor(x => x.Token).NotEmpty(); RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty(); RuleFor(x => x.SiteId).NotEmpty(); RuleFor(x => x.SlotId).NotEmpty();
        RuleFor(x => x.Purpose).Must(x => x is "ADMISSION" or "CONFIRMATION");
        RuleFor(x => x.IntentFingerprint).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Deadline).NotEmpty();
    }
}
public sealed class AcquireDependencyValidator : AbstractValidator<AcquireDependencyCommand>
{
    public AcquireDependencyValidator() { RuleFor(x => x.Intent).NotNull().SetValidator(new DependencyIntentValidator()); }
}
public sealed class ReadBookingFactsValidator : AbstractValidator<ReadBookingFactsQuery>
{
    public ReadBookingFactsValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.SlotId).NotEmpty();
    }
}
public sealed class AbortDependencyValidator : AbstractValidator<AbortDependencyCommand>
{
    public AbortDependencyValidator() { RuleFor(x => x.Intent).NotNull().SetValidator(new DependencyIntentValidator()); }
}
public sealed class CompleteDependencyValidator : AbstractValidator<CompleteDependencyCommand>
{
    public CompleteDependencyValidator()
    {
        RuleFor(x => x.Intent).NotNull().SetValidator(new DependencyIntentValidator());
        RuleFor(x => x.OutcomeJson).NotEmpty().MaximumLength(8192);
    }
}
public static class DependencyProtocol
{
    public static DependencyIntent Intent(ReservationDependency row) => new(row.Id, row.WorkflowId,
        row.Purpose, row.TenantId, row.SiteId, row.SlotId, row.IntentFingerprint, row.Deadline);
    public static void RequireSame(ReservationDependency row, DependencyIntent intent)
    {
        if (Intent(row) != intent) throw new StructureException("STRUCTURE_CONFLICT", "Token payload cannot change.");
    }
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static ReservationDependency New(DependencyIntent intent, DateTimeOffset now) =>
        ReservationDependency.Create(intent.Token, intent.WorkflowId, intent.TenantId, intent.SiteId,
            intent.SlotId, intent.Purpose, intent.IntentFingerprint, intent.Deadline, now);
}
public sealed class AcquireDependencyHandler(IReservationDependencyRepository repository, IUnitOfWork work)
    : IRequestHandler<AcquireDependencyCommand, DependencyResult>
{
    public async Task<DependencyResult> Handle(AcquireDependencyCommand command, CancellationToken ct)
    {
        var intent = command.Intent;
        await using var transaction = await work.BeginTransactionAsync(ct);
        await repository.LockAsync(intent.Token, intent.SiteId, ct);
        var row = await repository.FindAsync(intent.Token, ct);
        if (row is not null)
        {
            DependencyProtocol.RequireSame(row, intent);
            if (row.State == "ABORTED") return new(intent, "ABORTED", null);
            if (row.State is "HELD" or "COMPLETED")
                return new(intent, "HELD", JsonSerializer.Deserialize<ResourceSnapshot>(row.SnapshotJson!));
        }
        if (await repository.HasPendingStructureEditAsync(intent.SiteId, ct))
            throw new StructureException("STRUCTURE_BUSY", "A structure change is awaiting reconciliation.");
        var now = await repository.NowAsync(ct);
        if (row is null) { row = DependencyProtocol.New(intent, now); repository.Add(row); }
        if (now >= intent.Deadline) row.Abort(now);
        else
        {
            var snapshot = await repository.SnapshotAsync(intent.SiteId, intent.SlotId, ct);
            if (snapshot.Booking.TenantId != intent.TenantId)
                throw new StructureException("FORBIDDEN", "Dependency scope does not match the site.");
            row.Hold(JsonSerializer.Serialize(snapshot), await repository.NowAsync(ct));
        }
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(intent, row.State, row.SnapshotJson is null ? null : JsonSerializer.Deserialize<ResourceSnapshot>(row.SnapshotJson));
    }
}
public sealed class AbortDependencyHandler(IReservationDependencyRepository repository, IUnitOfWork work)
    : IRequestHandler<AbortDependencyCommand, DependencyAcknowledgement>
{
    public async Task<DependencyAcknowledgement> Handle(AbortDependencyCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await repository.LockAsync(command.Intent.Token, command.Intent.SiteId, ct);
        var row = await repository.FindAsync(command.Intent.Token, ct);
        var now = await repository.NowAsync(ct);
        if (row is null) { row = DependencyProtocol.New(command.Intent, now); repository.Add(row); }
        else DependencyProtocol.RequireSame(row, command.Intent);
        row.Abort(now);
        await work.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(command.Intent, "ABORTED", null);
    }
}
public sealed class CompleteDependencyHandler(IReservationDependencyRepository repository, IUnitOfWork work)
    : IRequestHandler<CompleteDependencyCommand, DependencyAcknowledgement>
{
    public async Task<DependencyAcknowledgement> Handle(CompleteDependencyCommand command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await repository.LockAsync(command.Intent.Token, command.Intent.SiteId, ct);
        var row = await repository.FindAsync(command.Intent.Token, ct)
            ?? throw new StructureException("STRUCTURE_CONFLICT", "Dependency must be acquired before completion.");
        DependencyProtocol.RequireSame(row, command.Intent);
        // Completion acknowledges a committed Operations effect. Deadline expiry cannot undo it.
        row.Complete(DependencyProtocol.Hash(command.OutcomeJson), await repository.NowAsync(ct));
        await work.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(command.Intent, "COMPLETED", row.OutcomeFingerprint);
    }
}
public sealed class ReadBookingFactsHandler(IReservationDependencyRepository repository, IUnitOfWork work)
    : IRequestHandler<ReadBookingFactsQuery, BookingFacts>
{
    public async Task<BookingFacts> Handle(ReadBookingFactsQuery query, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await repository.LockAsync(Guid.NewGuid(), query.SiteId, ct);
        return (await repository.SnapshotAsync(query.SiteId, query.SlotId, ct)).Booking;
    }
}
