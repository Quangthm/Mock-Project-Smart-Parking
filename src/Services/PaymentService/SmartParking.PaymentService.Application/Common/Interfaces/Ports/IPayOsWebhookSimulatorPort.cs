namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public interface IPayOsWebhookSimulatorPort
{
    SimulatedPayOsWebhookPayload BuildSignedSuccessWebhook(DemoPayOsWebhookRequest request);
}
