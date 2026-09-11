using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autopatch.Client.Services;

/// <summary>
/// Hosted service that connects the AutoPatch client in the background when the host starts and disconnects it when the host stops.
/// </summary>
/// <remarks>
/// Start-up never waits for the server: the first connection is retried with exponential backoff (up to 30 seconds) until it
/// succeeds. After that the client reconnects on its own. Register it with <c>AddAutoPatchHostedConnection()</c>; hosts that do
/// not run hosted services (e.g. Blazor WebAssembly) call <see cref="IAutoPatchClient.ConnectAsync"/> themselves.
/// </remarks>
/// <param name="autoPatchClient">The Autopatch client instance to manage connections for.</param>
/// <param name="logger">Logger for connection management operations.</param>
public class AutopatchConnectionManager(
    IAutoPatchClient autoPatchClient,
    ILogger<AutopatchConnectionManager> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _stopping = new();
    private Task? _connecting;

    /// <summary>
    /// Starts connecting in the background and returns right away.
    /// </summary>
    /// <param name="cancellationToken">Not used; the background connection runs until <see cref="StopAsync"/>.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _connecting = ConnectUntilSuccessAsync(_stopping.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops connecting and disconnects from the Autopatch service.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        if (_connecting is not null)
        {
            await _connecting;
        }

        try
        {
            await autoPatchClient.DisconnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during disconnect");
        }
    }

    private async Task ConnectUntilSuccessAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await autoPatchClient.ConnectAsync(cancellationToken);
                logger.LogInformation("Connected to AutoPatch server");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to connect to AutoPatch server (attempt {Attempt}); retrying in {Delay}", attempt, delay);
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping.Dispose();
        GC.SuppressFinalize(this);
    }
}
