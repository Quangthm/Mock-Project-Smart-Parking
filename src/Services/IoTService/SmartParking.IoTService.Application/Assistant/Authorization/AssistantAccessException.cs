namespace SmartParking.IoTService.Application.Assistant.Authorization;

public sealed class AssistantAccessException(bool authenticated) : Exception
{
    public bool Authenticated { get; } = authenticated;
}
