using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autopatch.Client.Services;

/// <summary>
/// Manages the connection lifecycle for the Autopatch client as a hosted service.
/// Automatically connects when the service starts and disconnects when the service stops.
/// Provides automatic reconnection with exponential backoff strategy.
/// </summary>
/// <param name="autoPatchClient">The Autopatch client instance to manage connections for.</param>
/// <param name="logger">Logger for connection management operations.</param>
public class AutopatchConnectionManager(
    IAutoPatchClient autoPatchClient, 
    ILogger<AutopatchConnectionManager> logger) : IHostedService
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// Starts the hosted service by establishing a connection to the Autopatch service.
    /// Configures automatic reconnection monitoring for connection resilience.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ConnectWithRetry(cancellationToken);
    }

    /// <summary>
    /// Stops the hosted service by disconnecting from the Autopatch service.
    /// Cancels any ongoing reconnection attempts and ensures clean shutdown.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        
        try
        {
            await autoPatchClient.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during disconnect");
        }
        
        _cancellationTokenSource.Dispose();
    }

    /// <summary>
    /// Attempts to establish a connection using exponential backoff retry strategy.
    /// Retries up to 5 times with increasing delays between attempts.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the connection attempts.</param>
    /// <returns>A task that completes when connected or all retries are exhausted.</returns>
    /// <exception cref="Exception">Thrown when all connection attempts fail.</exception>
    private async Task ConnectWithRetry(CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var maxRetries = 5;
        var baseDelay = TimeSpan.FromSeconds(1);

        while (retryCount < maxRetries && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Attempting to connect to AutoPatch server (attempt {Attempt}/{MaxRetries})", 
                    retryCount + 1, maxRetries);
                
                await autoPatchClient.ConnectAsync(cancellationToken);
                
                logger.LogInformation("Successfully connected to AutoPatch server");
                return;
            }
            catch (Exception ex) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, retryCount - 1));
                
                logger.LogWarning(ex, "Failed to connect to AutoPatch server. Retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})", 
                    delay.TotalMilliseconds, retryCount, maxRetries);
                
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to AutoPatch server after {MaxRetries} attempts", maxRetries);
                throw;
            }
        }
    }
}
