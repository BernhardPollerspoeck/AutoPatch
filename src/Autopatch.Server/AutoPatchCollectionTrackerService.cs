using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autopatch.Server;

/// <summary>
/// Provides a hosted service that manages the lifecycle of object trackers for collections.
/// </summary>
/// <remarks>This service is designed to start and stop a set of <see cref="IObjectTracker"/> instances,  which
/// are responsible for tracking changes in their respective collections. The service  integrates with the application's
/// hosting environment and is started and stopped automatically  as part of the application's lifecycle.</remarks>
/// <param name="serviceProvider"></param>
/// <param name="logger"></param>
internal class AutoPatchCollectionTrackerService(
    IServiceProvider serviceProvider,
    ILogger<AutoPatchCollectionTrackerService> logger)
    : IHostedService
{
    /// <summary>
    /// Starts the AutoPatchCollectionTrackerService and initializes tracking for all configured object trackers.
    /// </summary>
    /// <remarks>This method initializes and starts tracking for each object tracker in the service.  It logs
    /// the start of the service and the initialization of each tracker.  Call this method to begin monitoring the
    /// collections managed by the service.</remarks>
    /// <param name="cancellationToken">A token that can be used to cancel the operation. This implementation does not currently support cancellation.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var objectTrackers = scope.ServiceProvider.GetServices<IObjectTracker>().ToArray();
        logger.LogInformation("Starting AutoPatchCollectionTrackerService with {Count} trackers", objectTrackers.Length);
        foreach (var tracker in objectTrackers)
        {
            logger.LogInformation("Starting tracker for collection of type {Type}", tracker.TrackedCollection.GetType().FullName);
            tracker.StartTracking();
        }
        logger.LogInformation("AutoPatchCollectionTrackerService started");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the AutoPatchCollectionTrackerService and halts tracking for all associated object trackers.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. This parameter is not used in the current implementation.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var objectTrackers = scope.ServiceProvider.GetServices<IObjectTracker>().ToArray();
        logger.LogInformation("Stopping AutoPatchCollectionTrackerService with {Count} trackers", objectTrackers.Length);
        foreach (var tracker in objectTrackers)
        {
            logger.LogInformation("Stopping tracker for collection of type {Type}", tracker.TrackedCollection.GetType().FullName);
            tracker.StopTracking();
        }
        logger.LogInformation("AutoPatchCollectionTrackerService stopped");
        return Task.CompletedTask;
    }
}

