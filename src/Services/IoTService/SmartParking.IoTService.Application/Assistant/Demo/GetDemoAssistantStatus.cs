using MediatR;

namespace SmartParking.IoTService.Application.Assistant.Demo;

public interface IDemoAssistantStatusProvider
{
    AssistantCapabilities GetCapabilities();
}

public sealed record DemoAssistantStatus(bool IsDemo, string Notice, AssistantCapabilities Capabilities);
public sealed record GetDemoAssistantStatusQuery : IRequest<DemoAssistantStatus>;

public sealed class GetDemoAssistantStatusHandler(IDemoAssistantStatusProvider provider)
    : IRequestHandler<GetDemoAssistantStatusQuery, DemoAssistantStatus>
{
    public Task<DemoAssistantStatus> Handle(GetDemoAssistantStatusQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DemoAssistantStatus(true,
            "Dữ liệu demo. Chưa kết nối tài khoản, hội thoại hoặc dịch vụ AI.", provider.GetCapabilities()));
    }
}
