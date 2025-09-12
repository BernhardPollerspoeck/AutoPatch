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
    bool RemoveCollection<TItem>(string? key = null) 
        where TItem : class, INotifyPropertyChanged;

    /// <summary>
    /// Gets all object trackers currently managed by this instance.
    /// </summary>
    /// <returns>An enumerable of all active object trackers.</returns>
    IEnumerable<IObjectTracker> GetAllTrackers();
}
