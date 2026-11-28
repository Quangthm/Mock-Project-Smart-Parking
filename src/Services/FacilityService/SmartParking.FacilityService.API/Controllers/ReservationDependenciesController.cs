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
    [HttpGet("slots/{slotId}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<SelectedSlotMetadata> ResolveSlot(Guid slotId, CancellationToken ct) =>
        mediator.Send(new ResolveReservationSlotQuery(slotId), ct);
}
