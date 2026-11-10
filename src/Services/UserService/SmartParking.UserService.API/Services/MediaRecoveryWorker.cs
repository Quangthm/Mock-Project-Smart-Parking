using UserService.Application.Media;

namespace UserService.API.Services;

public sealed class MediaRecoveryWorker(IServiceScopeFactory scopes, MediaSettings settings,
    ILogger<MediaRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MediaUseCases>().RecoverAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Media recovery pass incomplete; pending records remain recoverable."); }
        }
    }
}
