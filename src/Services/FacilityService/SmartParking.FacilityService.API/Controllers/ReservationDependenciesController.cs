using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.API.Controllers;

[ApiController]
[Route("internal/reservation-dependencies/v1")]
[Authorize(Policy = ReservationServiceAuthentication.Scheme)]
public sealed class ReservationDependenciesController(IMediator mediator) : ControllerBase
{
    [HttpPost("acquire")]
    public Task<DependencyResult> Acquire(DependencyIntent request, CancellationToken ct) =>
        mediator.Send(new AcquireDependencyCommand(request), ct);
    [HttpPost("complete")]
    public Task<DependencyAcknowledgement> Complete(CompleteDependencyCommand request, CancellationToken ct) =>
        mediator.Send(request, ct);
    [HttpPost("abort")]
    public Task<DependencyAcknowledgement> Abort(DependencyIntent request, CancellationToken ct) =>
        mediator.Send(new AbortDependencyCommand(request), ct);
    [HttpGet("sites/{siteId:guid}/slots/{slotId:guid}")]
    public Task<BookingFacts> Read(Guid siteId, Guid slotId, CancellationToken ct) =>
        mediator.Send(new ReadBookingFactsQuery(siteId, slotId), ct);
}
