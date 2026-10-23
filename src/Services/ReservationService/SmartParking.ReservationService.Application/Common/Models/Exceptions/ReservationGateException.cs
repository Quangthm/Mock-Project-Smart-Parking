namespace SmartParking.ReservationService.Application.Common.Models.Exceptions;

public sealed class ReservationGateException(int statusCode, string code, string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
