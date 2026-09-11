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
    AutoPatchHubClients hubs,
    IOptions<AutopatchOptions> autopatchOptions,
    IOptions<JsonHubProtocolOptions> jsonHubProtocolOptions)
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
        var queue = new BulkFlushQueue<OperationContainer<TItem>>(
            autopatchOptions,
            options,
            loggerFactory.CreateLogger<BulkFlushQueue<OperationContainer<TItem>>>());

        return new ObservableCollectionTracker<TItem>(
            queue,
            options,
            loggerFactory.CreateLogger<ObservableCollectionTracker<TItem>>(),
            hubs,
            jsonHubProtocolOptions.Value.PayloadSerializerOptions,
            key);
    }
}
