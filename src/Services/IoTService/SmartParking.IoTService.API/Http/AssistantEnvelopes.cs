namespace SmartParking.IoTService.API.Http;

public sealed record SuccessEnvelope<T>(bool Success, T Data, string TraceId);
public sealed record FieldError(string Field, string Code, string Message);
public sealed record ErrorEnvelope(bool Success, string Code, string Message, FieldError[] Errors, string TraceId);
