using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using Autopatch.Core;
using Autopatch.Server.SignalR;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.SignalR;

namespace Autopatch.Server.Services;

/// <summary>
/// Tracks changes in an observable collection and synchronizes them with clients via SignalR.
/// </summary>
/// <typeparam name="T">The type of items in the collection. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
/// <param name="queue">The queue for batching operations before sending to clients.</param>
/// <param name="hubContext">The SignalR hub context for communicating with clients.</param>
/// <remarks>
/// This tracker monitors changes to both the collection itself (add/remove operations) and properties 
/// of individual items within the collection. All changes are converted to JSON Patch operations
/// and sent to connected clients through SignalR.
/// </remarks>
public class ObservableCollectionTracker<T>(
    BulkFlushQueue<OperationContainer<T>> queue,
    IHubContext<AutoPatchHub> hubContext)
    : IObjectTracker<ObservableCollection<T>, T>, IDisposable
    where T : class, INotifyPropertyChanged
{
    /// <summary>
    /// JSON Patch operation type for adding items.
    /// </summary>
    private const string ADD = "add";

    /// <summary>
    /// JSON Patch operation type for removing items.
    /// </summary>
    private const string REMOVE = "remove";

    /// <summary>
    /// JSON Patch operation type for replacing values.
    /// </summary>
    private const string REPLACE = "replace";

    /// <summary>
    /// JSON Patch path for adding items to the end of an array.
    /// </summary>
    private const string PATH_ADD = "/-";

    /// <summary>
    /// Cache for reflection PropertyInfo objects to improve performance.
    /// </summary>
    private readonly Dictionary<string, PropertyInfo> _propertyCache = [];

    /// <summary>
    /// Gets the name of the type being tracked.
    /// </summary>
    /// <value>The simple name of type <typeparamref name="T"/>.</value>
    public string TypeName => typeof(T).Name;

    /// <summary>
    /// Gets the observable collection that is being tracked for changes.
    /// </summary>
    /// <value>An <see cref="ObservableCollection{T}"/> that contains items of type <typeparamref name="T"/>.</value>
    public ObservableCollection<T> TrackedCollection { get; } = [];

    /// <summary>
    /// Gets the tracked collection as an <see cref="INotifyCollectionChanged"/> interface.
    /// </summary>
    /// <value>The tracked collection implementing <see cref="INotifyCollectionChanged"/>.</value>
    INotifyCollectionChanged IObjectTracker.TrackedCollection => TrackedCollection;

    /// <summary>
    /// Begins monitoring the tracked collection and its items for changes.
    /// </summary>
    /// <remarks>
    /// This method subscribes to both collection change events and the flush queue events
    /// to enable real-time synchronization with connected clients.
    /// </remarks>
    public void StartTracking()
    {
        queue.OnFlush += HandleQueueFlush;
        TrackedCollection.CollectionChanged += HandleCollectionChanged;
    }

    /// <summary>
    /// Stops monitoring the tracked collection and its items for changes.
    /// </summary>
    /// <remarks>
    /// This method unsubscribes from all events to disable synchronization and prevent memory leaks.
    /// It also cleans up all PropertyChanged event subscriptions from items in the collection.
    /// </remarks>
    public void StopTracking()
    {
        TrackedCollection.CollectionChanged -= HandleCollectionChanged;
        queue.OnFlush -= HandleQueueFlush;
        
        // Clean up PropertyChanged events from all items to prevent memory leaks
        foreach (T item in TrackedCollection)
        {
            item.PropertyChanged -= HandleItemPropertyChanged;
        }
    }

    /// <summary>
    /// Sends the complete current state of the tracked collection to the specified connection.
    /// </summary>
    /// <param name="connectionId">The unique identifier of the connection to send the data to.</param>
    /// <remarks>
    /// This method creates a series of "add" operations for each item in the collection
    /// and queues them as a full data operation for the specified connection.
    /// </remarks>
    public void SendFullData(string connectionId)
    {
        var operation = new FullDataOperationContainer<T>(
            [.. TrackedCollection.Select(item => new Operation<ObservableCollection<T>>
            {
                op = ADD,
                path = PATH_ADD,
                value = item,
            })],
            connectionId);
        
        _ = Task.Run(async () => await queue.Add(operation));
    }

    /// <summary>
    /// Handles collection change events by creating appropriate JSON Patch operations.
    /// </summary>
    /// <param name="sender">The collection that raised the event.</param>
    /// <param name="e">Event arguments containing details about the collection change.</param>
    /// <remarks>
    /// For added items, this method subscribes to their PropertyChanged events and creates "add" operations.
    /// For removed items, this method unsubscribes from their PropertyChanged events and creates "remove" operations.
    /// </remarks>
    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (T item in e.NewItems)
            {
                item.PropertyChanged += HandleItemPropertyChanged;

                var operation = new Operation<ObservableCollection<T>>
                {
                    op = ADD,
                    path = PATH_ADD,
                    value = item,
                };
                
                _ = Task.Run(async () => await queue.Add(new DefaultOperationContainer<T>(operation)));
            }
        }
        if (e.OldItems != null)
        {
            foreach (T item in e.OldItems)
            {
                item.PropertyChanged -= HandleItemPropertyChanged;

                var operation = new Operation<ObservableCollection<T>>
                {
                    op = REMOVE,
                    path = $"/{e.OldStartingIndex}",
                };
                
                _ = Task.Run(async () => await queue.Add(new DefaultOperationContainer<T>(operation)));
            }
        }
    }

    /// <summary>
    /// Handles property change events from items within the tracked collection.
    /// </summary>
    /// <param name="sender">The item that raised the PropertyChanged event.</param>
    /// <param name="e">Event arguments containing the name of the changed property.</param>
    /// <remarks>
    /// This method creates "replace" operations for changed properties and uses reflection
    /// to retrieve the new property value. PropertyInfo objects are cached for performance.
    /// </remarks>
    private void HandleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (sender is not T item || string.IsNullOrEmpty(e.PropertyName))
            {
                return;
            }

            if (!_propertyCache.TryGetValue(e.PropertyName, out var propInfo))
            {
                propInfo = item.GetType().GetProperty(e.PropertyName);
                if (propInfo == null)
                {
                    return;
                }
                _propertyCache[e.PropertyName] = propInfo;
            }

            var operation = new Operation<ObservableCollection<T>>
            {
                op = REPLACE,
                path = $"/{TrackedCollection.IndexOf(item)}/{e.PropertyName}",
                value = propInfo.GetValue(item)//TODO: we currently cant get the value without reflection here. Maybe later SourceGenerator can help?
            };
            
            _ = Task.Run(async () => await queue.Add(new DefaultOperationContainer<T>(operation)));
        }
        catch (Exception)
        {
            // TODO: Add proper logging here
        }
    }

    /// <summary>
    /// Handles the queue flush event by sending batched operations to SignalR clients.
    /// </summary>
    /// <param name="containers">The list of operation containers to be sent to clients.</param>
    /// <returns>A task that represents the asynchronous operation of sending data to clients.</returns>
    /// <remarks>
    /// Full data operations are sent only to the specific connection that requested them,
    /// while regular operations are broadcast to all connected clients.
    /// </remarks>
    private async Task HandleQueueFlush(List<OperationContainer<T>> containers)
    {
        var target = $"AutoPatch/{typeof(T).Name}";
        foreach (var container in containers)
        {
            if (container is FullDataOperationContainer<T> fullData)
            {
                await hubContext.Clients
                    .Client(fullData.ConnectionId)
                    .SendAsync(target, target, fullData.Operation, true);
            }
        }

        var operations = containers
            .Where(c => c is DefaultOperationContainer<T>)
            .Select(c => (DefaultOperationContainer<T>)c)
            .Select(c => c.Operation)
            .ToArray();
        await hubContext.Clients
            .All
            //.Groups(target)
            .SendAsync(target, target, operations, false);
    }

    /// <summary>
    /// Releases all resources used by the ObservableCollectionTracker and ensures proper cleanup.
    /// </summary>
    /// <remarks>
    /// This method ensures that all event subscriptions are properly removed and resources are cleaned up
    /// to prevent memory leaks. It should be called when the tracker is no longer needed.
    /// </remarks>
    public void Dispose()
    {
        StopTracking();
        _propertyCache.Clear();
    }
}
