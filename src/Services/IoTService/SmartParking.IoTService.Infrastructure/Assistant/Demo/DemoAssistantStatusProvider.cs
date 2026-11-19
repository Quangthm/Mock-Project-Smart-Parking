using SmartParking.IoTService.Application.Assistant;
using SmartParking.IoTService.Application.Assistant.Demo;

namespace SmartParking.IoTService.Infrastructure.Assistant.Demo;

// A deterministic fixture, not a dependency health check or production readiness adapter.
public sealed class DemoAssistantStatusProvider : IDemoAssistantStatusProvider
{
    public AssistantCapabilities GetCapabilities() => new(false, "0.3.0", ["vi", "en"], 4000,
        new(false, false, false, false, false, false, false, false));
}
