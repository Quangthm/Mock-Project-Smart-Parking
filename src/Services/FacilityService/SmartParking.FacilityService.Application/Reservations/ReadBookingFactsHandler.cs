using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Domain.Entities;

namespace SmartParking.FacilityService.Application.Reservations;

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
