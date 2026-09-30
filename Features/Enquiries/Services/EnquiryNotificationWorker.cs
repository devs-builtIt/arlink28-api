using Arlink28.Api.Features.Enquiries.Services.Interfaces;

namespace Arlink28.Api.Features.Enquiries.Services;

/// <summary>
/// Sends each queued enquiry's email. A failure is retried a couple of times, then logged: the enquiry is
/// already saved and waiting in the admin, so nothing is lost either way.
/// </summary>
public class EnquiryNotificationWorker(
    IEnquiryNotificationQueue queue,
    IServiceScopeFactory scopes,
    ILogger<EnquiryNotificationWorker> logger) : BackgroundService
{
    public const int Attempts = 3;
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(45);
    /// <summary>Wait before a retry, times the attempt number. Replaceable so tests needn't wait.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        try
        {
            await foreach (var id in queue.ReadAllAsync(stopping))
                await SendWithRetriesAsync(id, stopping);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task SendWithRetriesAsync(Guid id, CancellationToken stopping)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                timeout.CancelAfter(SendTimeout);
                await scope.ServiceProvider.GetRequiredService<IEnquiryNotifier>().SendAsync(id, timeout.Token);
                return;
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (attempt < Attempts)
            {
                logger.LogWarning(ex, "The email for enquiry {EnquiryId} failed (attempt {Attempt} of {Attempts}); trying again.",
                    id, attempt, Attempts);
                try { await Task.Delay(RetryDelay * attempt, stopping); }
                catch (OperationCanceledException) { return; }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "The email for enquiry {EnquiryId} could not be sent after {Attempts} attempts. It is saved and waiting in the admin.",
                    id, Attempts);
            }
        }
    }
}
