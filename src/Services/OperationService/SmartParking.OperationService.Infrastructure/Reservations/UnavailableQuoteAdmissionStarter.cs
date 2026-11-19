using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class UnavailableQuoteAdmissionStarter : IQuoteAdmissionStarter
{
    // Production composition stays closed until immutable Payment proof, quote consumption and hold cutover are verified.
    public Task StartAsync(Guid requestId, CancellationToken ct) =>
        throw new ReservationGateException(503, "PAYMENT_QUOTE_PROOF_UNAVAILABLE", "Plate-based booking is unavailable.");
}
