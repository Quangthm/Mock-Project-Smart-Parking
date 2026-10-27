namespace SmartParking.PaymentService.Domain.Exceptions;

public class PaymentDomainException : Exception
{
    public string Code { get; }
    public string? Field { get; }

    public PaymentDomainException(string code, string message, string? field = null)
        : base(message)
    {
        Code = code;
        Field = field;
    }
}
