using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartParking.NotificationAuditService.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling request {RequestName}", requestName);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Handled request {RequestName}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request {RequestName} failed with error", requestName);
            throw;
        }
    }
}
