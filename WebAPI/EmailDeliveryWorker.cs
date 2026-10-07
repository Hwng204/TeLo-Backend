using Application.Services.Interface;

namespace WebAPI;

public sealed class EmailDeliveryWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["NotificationEmail:Enabled"], out var enabled) || !enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailNotificationService>().DeliverPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Notification delivery batch failed; retrying on the next cycle."); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
