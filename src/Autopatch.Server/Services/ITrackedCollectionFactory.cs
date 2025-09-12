using System.ComponentModel;

namespace Autopatch.Server.Services;

/// <summary>
/// Factory interface for creating tracked collections with specific keys.
/// </summary>
/// <typeparam name="TItem">The type of items in the collection.</typeparam>
public interface ITrackedCollectionFactory<TItem> 
    where TItem : class, INotifyPropertyChanged
{
    /// <summary>
    /// Creates a new tracked collection with the specified key.
    /// </summary>
    /// <param name="key">Optional key to identify the collection. If null, creates the default collection.</param>
    /// <returns>A new object tracker for the collection.</returns>
    ObservableCollectionTracker<TItem> CreateTracker(string? key = null);
}
