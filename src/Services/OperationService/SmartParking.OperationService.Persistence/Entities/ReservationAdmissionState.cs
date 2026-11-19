using MassTransit;

namespace SmartParking.OperationService.Persistence.Entities;

public sealed class ReservationAdmissionState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "";

    // The receipt owns the durable decision. Used only within the current MassTransit activity.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? Outcome { get; set; }
}
