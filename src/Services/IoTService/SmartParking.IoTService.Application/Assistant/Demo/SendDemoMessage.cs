using MediatR;
using SmartParking.IoTService.Application.Assistant.Knowledge;

namespace SmartParking.IoTService.Application.Assistant.Demo;

public sealed record DemoMessageRequest(string? Message);
public sealed record DemoMessageReply(bool IsDemo, string Notice, string Reply,
    string Mode, IReadOnlyList<GroundedSource> Sources, string? Provider = null, string? Model = null);
public sealed record SendDemoMessageCommand(string? Message) : IRequest<DemoMessageReply>;

public interface IDemoReplyGenerator
{
    Task<string> ReplyAsync(string message, CancellationToken cancellationToken);
}

public sealed class DemoMessageValidationException : Exception;

public sealed class SendDemoMessageHandler(IDemoReplyGenerator generator, IDemoGroundedGenerator? grounded = null)
    : IRequestHandler<SendDemoMessageCommand, DemoMessageReply>
{
    public async Task<DemoMessageReply> Handle(SendDemoMessageCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var message = request.Message?.Trim();
        if (string.IsNullOrEmpty(message) || message.Length > 4000) throw new DemoMessageValidationException();
        if (grounded is not null)
        {
            var answer = await grounded.GenerateAsync(message, cancellationToken);
            return new(true, answer.Mode == "knowledge"
                    ? "Hướng dẫn gốc từ tài liệu SmartPark, không phải câu trả lời do AI tạo. Chưa thực hiện đặt chỗ hoặc giao dịch."
                    : "FAQ thử nghiệm từ knowledge SmartPark. Không tra cứu dữ liệu nghiệp vụ trực tiếp.",
                answer.Reply, answer.Mode, answer.Sources, answer.Provider, answer.Model);
        }
        var reply = await generator.ReplyAsync(message, cancellationToken);
        return new(true, "Câu trả lời mô phỏng. Không phải kết quả từ AI hoặc dữ liệu thực tế.", reply, "mock", []);
    }
}
