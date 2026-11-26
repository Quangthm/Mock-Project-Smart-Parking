namespace SmartParking.PaymentService.API.Authentication;

public sealed class PaymentJwksUnavailableException : Exception
{
    public PaymentJwksUnavailableException()
        : base("Trusted authentication keys are unavailable.")
    {
    }
}
