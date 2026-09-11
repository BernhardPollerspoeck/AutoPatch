using System.Collections.Specialized;

namespace Autopatch.Server.Services;

/// <summary>
/// Defines an interface for tracking objects within a collection and managing their synchronization.
/// </summary>
/// <remarks>This interface provides methods to start and stop tracking changes in a collection, as well as 
/// functionality to send the full state of the tracked data to a specified connection. It is designed  to work with
/// collections that implement <see cref="INotifyCollectionChanged"/>  to monitor changes
/// in real time.</remarks>
public interface IObjectTracker
{
    /// <summary>
    /// Gets the name of the type being tracked.
    /// </summary>
    /// <value>A string representing the type name of the tracked objects.</value>
    string TypeName { get; }

    /// <summary>
    /// Gets the key that identifies this specific collection instance.
    /// </summary>
    /// <value>A string representing the key for this collection, or null for the default collection.</value>
    string? Key { get; }

    /// <summary>
    /// Gets the subscription key used for SignalR groups and client subscriptions.
    /// </summary>
    /// <returns>The subscription key in format "TypeName" or "TypeName/Key".</returns>
    string GetSubscriptionKey() => Core.AutoPatchProtocol.GetSubscriptionKey(TypeName, Key);

    /// <summary>
    /// Gets the collection that is being tracked for changes.
    /// </summary>
    /// <value>An <see cref="INotifyCollectionChanged"/> collection that notifies when items are added, removed, or modified.</value>
    INotifyCollectionChanged TrackedCollection { get; }

    /// <summary>
    /// Begins monitoring the tracked collection for changes.
    /// </summary>
    /// <remarks>This method subscribes to collection change events to enable real-time synchronization.</remarks>
    void StartTracking();

    /// <summary>
    /// Stops monitoring the tracked collection for changes.
    /// </summary>
    /// <remarks>This method unsubscribes from collection change events to disable synchronization.</remarks>
    void StopTracking();

    /// <summary>
    /// Sends the complete current state of the tracked collection to the specified connection.
    /// </summary>
    /// <param name="connectionId">The unique identifier of the connection to send the data to.</param>
    /// <remarks>This method is typically used for initial synchronization when a new connection is established.</remarks>
    void SendFullData(string connectionId);

    /// <summary>
    /// Sends all queued changes of the collection now.
    /// </summary>
    /// <returns>A task that completes once the changes have been handed to SignalR.</returns>
    Task FlushAsync();
}

/// <summary>
/// Defines a strongly-typed interface for tracking objects within a collection and managing their synchronization.
/// </summary>
/// <typeparam name="TCollection">The type of collection being tracked, which must implement <see cref="INotifyCollectionChanged"/> and <see cref="IList{T}"/>.</typeparam>
/// <typeparam name="TItem">The type of items contained within the tracked collection.</typeparam>
/// <remarks>This interface extends <see cref="IObjectTracker"/> to provide type-safe access to the tracked collection.</remarks>
public interface IObjectTracker<TCollection, TItem> : IObjectTracker
    where TCollection : INotifyCollectionChanged, IList<TItem>
{
    /// <summary>
    /// Gets the strongly-typed collection that is being tracked for changes.
    /// </summary>
    /// <value>A <typeparamref name="TCollection"/> that contains items of type <typeparamref name="TItem"/> and notifies when changes occur.</value>
    new TCollection TrackedCollection { get; }
}
