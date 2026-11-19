using MassTransit;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.Contracts.ReservationAdmission;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class AdmissionMessages(ISendEndpointProvider endpoints) : IAdmissionMessages, IQuoteAdmissionStarter
{
    internal const string SagaQueue = "operations-reservation-admission-v1";
    internal const string EvaluationQueue = "operations-reservation-evaluate-v1";
    public Task RequestEvaluationAsync(Guid id, CancellationToken ct) =>
        SendAsync(EvaluationQueue, new EvaluateQuoteBooking(id), id, ct);
    public Task StartAsync(Guid id, CancellationToken ct) => SendAsync(SagaQueue, new StartQuoteAdmission(id), id, ct);
    public Task WakeAsync(Guid id, CancellationToken ct) => SendAsync(SagaQueue, new CheckAdmissionDeadline(id), id, ct);
    public Task EvaluateAsync(Guid id, CancellationToken ct) => SendAsync(SagaQueue, new EvaluateQuoteBooking(id), id, ct);
    public Task CheckPlateAsync(PlateCheckV1 check, CancellationToken ct) =>
        SendAsync("user-reservation-plate-v1", check, check.WorkflowId, ct);
    public Task CheckSlotAsync(SlotCheckV1 check, CancellationToken ct) =>
        SendAsync("facility-reservation-slot-v1", check, check.WorkflowId, ct);

    private async Task SendAsync<T>(string queue, T message, Guid workflow, CancellationToken ct) where T : class
    {
        var endpoint = await endpoints.GetSendEndpoint(new Uri($"queue:{queue}"));
        await endpoint.Send(message, send => send.CorrelationId = workflow, ct);
    }
}
