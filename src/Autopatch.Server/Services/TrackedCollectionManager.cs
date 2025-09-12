using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Autopatch.Server.Services;

/// <summary>
/// Manages tracked collections at runtime, allowing creation and retrieval of collections with specific keys.
/// </summary>
public class TrackedCollectionManager(IServiceProvider serviceProvider) : ITrackedCollectionManager
{
    private readonly ConcurrentDictionary<string, IObjectTracker> _trackers = new();

    /// <summary>
    /// Gets or creates a tracked collection for the specified type and key.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection. If null, uses the default collection.</param>
    /// <returns>The tracked observable collection.</returns>
    public ObservableCollection<TItem> GetOrCreateCollection<TItem>(string? key = null)
        where TItem : class, INotifyPropertyChanged
    {
        var subscriptionKey = GetSubscriptionKey<TItem>(key);

        var tracker = _trackers.GetOrAdd(subscriptionKey, _ =>
        {
            var factory = serviceProvider.GetRequiredService<ITrackedCollectionFactory<TItem>>();
            var newTracker = factory.CreateTracker(key);
            newTracker.StartTracking();
            return newTracker;
        });

        return ((ObservableCollectionTracker<TItem>)tracker).TrackedCollection;
    }

    /// <summary>
    /// Gets an existing tracked collection for the specified type and key.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection. If null, uses the default collection.</param>
    /// <returns>The tracked observable collection, or null if it doesn't exist.</returns>
    public ObservableCollection<TItem>? GetCollection<TItem>(string? key = null)
        where TItem : class, INotifyPropertyChanged
    {
        var subscriptionKey = GetSubscriptionKey<TItem>(key);

        return _trackers.TryGetValue(subscriptionKey, out var tracker)
            ? ((ObservableCollectionTracker<TItem>)tracker).TrackedCollection
            : null;
    }

    /// <summary>
    /// Removes a tracked collection for the specified type and key.
    /// </summary>
    /// <typeparam name="TItem">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection. If null, uses the default collection.</param>
    /// <returns>True if the collection was found and removed, false otherwise.</returns>
    public bool RemoveCollection<TItem>(string? key = null)
        where TItem : class, INotifyPropertyChanged
    {
        var subscriptionKey = GetSubscriptionKey<TItem>(key);

        if (_trackers.TryRemove(subscriptionKey, out var tracker))
        {
            tracker.StopTracking();
            if (tracker is IDisposable disposable)
            {
                disposable.Dispose();
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all object trackers currently managed by this instance.
    /// </summary>
    /// <returns>An enumerable of all active object trackers.</returns>
    public IEnumerable<IObjectTracker> GetAllTrackers()
    {
        return _trackers.Values;
    }

    /// <summary>
    /// Gets the subscription key for a type and optional key parameter.
    /// </summary>
    /// <typeparam name="TItem">The type to get the subscription key for.</typeparam>
    /// <param name="key">Optional key to identify a specific collection.</param>
    /// <returns>The subscription key in format "TypeName" or "TypeName/Key".</returns>
    private static string GetSubscriptionKey<TItem>(string? key)
    {
        return string.IsNullOrEmpty(key) ? typeof(TItem).Name : $"{typeof(TItem).Name}/{key}";
    }
}
