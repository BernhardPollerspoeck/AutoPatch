using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autopatch.Server.Services;

/// <summary>
/// Releases all object trackers when the application stops.
/// </summary>
/// <remarks>
/// Collections are created on demand by <see cref="ITrackedCollectionManager"/>, so there is nothing to do on start.
/// </remarks>
/// <param name="serviceProvider">Service provider used to resolve the collection manager.</param>
/// <param name="logger">Logger for shutdown diagnostics.</param>
internal class AutoPatchCollectionTrackerService(
    IServiceProvider serviceProvider,
    ILogger<AutoPatchCollectionTrackerService> logger)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Stops all trackers and releases their resources.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. This parameter is not used in the current implementation.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        var trackers = serviceProvider.GetRequiredService<ITrackedCollectionManager>().GetAllTrackers().ToArray();
        logger.LogDebug("Stopping {Count} AutoPatch trackers", trackers.Length);

        foreach (var tracker in trackers)
        {
            tracker.StopTracking();
            (tracker as IDisposable)?.Dispose();
        }

        return Task.CompletedTask;
    }
}
