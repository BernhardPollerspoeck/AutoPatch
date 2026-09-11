using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Autopatch.Server.Services;

/// <summary>
/// Manages tracked collections at runtime, allowing creation and retrieval of collections with specific keys.
/// </summary>
public interface ITrackedCollectionManager
{
    /// <summary>
    /// Gets or creates a tracked collection for the specified type and key.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection. If null, uses the default collection.</param>
    /// <returns>The tracked observable collection.</returns>
    ObservableCollection<TItem> GetOrCreateCollection<TItem>(string? key = null)
        where TItem : class, INotifyPropertyChanged;

    /// <summary>
    /// Gets an existing tracked collection for the specified type and key.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection. If null, uses the default collection.</param>
    /// <returns>The tracked observable collection, or null if it doesn't exist.</returns>
    ObservableCollection<TItem>? GetCollection<TItem>(string? key = null)
        where TItem : class, INotifyPropertyChanged;

    /// <summary>
    /// Removes a tracked collection for the specified type and key.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection. If null, uses the default collection.</param>
    /// <returns>True if the collection was found and removed, false otherwise.</returns>
    /// <remarks>Subscribers receive an empty collection; queued changes that were not flushed yet are discarded.</remarks>
    bool RemoveCollection<TItem>(string? key = null)
        where TItem : class, INotifyPropertyChanged;

    /// <summary>
    /// Sends all queued changes of a collection now, e.g. at the end of an application tick.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection. If null, uses the default collection.</param>
    /// <returns>A task that completes once the changes have been handed to SignalR.</returns>
    Task FlushAsync<TItem>(string? key = null)
        where TItem : class, INotifyPropertyChanged;

    /// <summary>
    /// Sends all queued changes of all collections now.
    /// </summary>
    /// <returns>A task that completes once the changes have been handed to SignalR.</returns>
    Task FlushAllAsync();

    /// <summary>
    /// Gets the tracker of a collection by its subscription key.
    /// </summary>
    /// <param name="subscriptionKey">The subscription key in format "TypeName" or "TypeName/Key".</param>
    /// <returns>The tracker, or null if the collection does not exist.</returns>
    IObjectTracker? FindTracker(string subscriptionKey);

    /// <summary>
    /// Gets all object trackers currently managed by this instance.
    /// </summary>
    /// <returns>An enumerable of all active object trackers.</returns>
    IEnumerable<IObjectTracker> GetAllTrackers();
}
