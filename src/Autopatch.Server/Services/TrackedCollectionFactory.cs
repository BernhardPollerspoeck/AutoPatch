using System.Collections.ObjectModel;
using System.ComponentModel;
using Autopatch.Core;
using Autopatch.Server.Models;
using Autopatch.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autopatch.Server.Services;

/// <summary>
/// Factory implementation for creating tracked collections with specific keys.
/// </summary>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
public class TrackedCollectionFactory<TItem>(
    IOptions<ObjectTypeConfiguration<OperationContainer<TItem>>> options,
    ILoggerFactory loggerFactory,
    IHubContext<AutoPatchHub> hubContext,
    IOptions<AutopatchOptions> autopatchOptions)
    : ITrackedCollectionFactory<TItem>
    where TItem : class, INotifyPropertyChanged
{

    /// <summary>
    /// Creates a new tracked collection with the specified key.
    /// </summary>
    /// <param name="key">Optional key to identify the collection. If null, creates the default collection.</param>
    /// <returns>A new object tracker for the collection.</returns>
    public ObservableCollectionTracker<TItem> CreateTracker(string? key = null)
    {
        // Create a new BulkFlushQueue for this specific collection
        var queueLogger = loggerFactory.CreateLogger<BulkFlushQueue<OperationContainer<TItem>>>();
        var trackerLogger = loggerFactory.CreateLogger<ObservableCollectionTracker<TItem>>();
        var queue = new BulkFlushQueue<OperationContainer<TItem>>(autopatchOptions, options, queueLogger);

        return new ObservableCollectionTracker<TItem>(queue, options, trackerLogger, hubContext, key);
    }
}
