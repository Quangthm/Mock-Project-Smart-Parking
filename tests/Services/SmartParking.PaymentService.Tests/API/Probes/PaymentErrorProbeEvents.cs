namespace SmartParking.PaymentService.Tests.API.Probes;

public sealed class PaymentErrorProbeEvents
{
    public int Challenges;
    public int Forbidden;
    public int Validated;
    public int ValidatedInputs;
    public int HandledInputs;
}
