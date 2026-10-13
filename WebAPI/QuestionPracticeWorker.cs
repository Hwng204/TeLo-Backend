using Application.Services.Implement;

namespace WebAPI;

// Reconcile persisted completed exams, including late attempts and questions held by another exam.
public sealed class QuestionPracticeWorker(IServiceScopeFactory scopes, ILogger<QuestionPracticeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<QuestionBankService>().ReleaseCompletedAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Moved {Count} used questions into the practice bank", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Question practice reconciliation failed; will retry"); }
        }
    }
}
