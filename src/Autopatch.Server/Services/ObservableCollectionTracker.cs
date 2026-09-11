using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Autopatch.Core;
using Autopatch.Server.Models;
using Autopatch.Server.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autopatch.Server.Services;

/// <summary>
/// Tracks changes in an observable collection and synchronizes them with clients via SignalR.
/// </summary>
/// <typeparam name="T">The type of items in the collection. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
/// <remarks>
/// <para>
/// Every change of the collection or of an item's property is turned into a JSON Patch operation right away, on the thread that
/// made the change, and appended to the queue in order. Values are serialized at that moment, so later changes of referenced
/// objects cannot leak into an earlier operation. Items are addressed by their index, which the tracker keeps in sync with the
/// collection change events (insert, remove, replace, move and reset).
/// </para>
/// <para>
/// Batches carry a sequence number. The full data sent to a new subscriber is the state after the last delivered batch, together
/// with that batch's sequence number, so the subscriber can ignore everything it already has and apply everything that follows.
/// If a batch cannot be delivered, all subscribers get the full data again.
/// </para>
/// </remarks>
public class ObservableCollectionTracker<T>
    : IObjectTracker<ObservableCollection<T>, T>, IDisposable
    where T : class, INotifyPropertyChanged
{
    private readonly BulkFlushQueue<OperationContainer<T>> _queue;
    private readonly ObjectTypeConfiguration<OperationContainer<T>> _options;
    private readonly ILogger _logger;
    private readonly AutoPatchHubClients _hubs;
    private readonly JsonSerializerOptions _json;
    private readonly string? _key;

    // Producer side: the collection as the tracker knows it, guarded by _gate.
    private readonly Lock _gate = new();
    private readonly List<T> _items = [];
    private readonly HashSet<T> _attached = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentDictionary<string, TrackedProperty?> _properties = new(StringComparer.Ordinal);

    // Sender side: the state the clients have after the last delivered batch. Only touched by the queue's delivery loop.
    private readonly List<JsonNode?> _sentState = [];
    // Starts at the current time in microseconds: keeps increasing across re-created collections and server restarts, and stays
    // below 2^53, so JavaScript clients can compare sequence numbers exactly.
    private long _sequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
    private int _resyncScheduled;
    private bool _retired;

    /// <summary>
    /// Creates a tracker that sends through the given <see cref="AutoPatchHub"/> context with the default SignalR JSON settings.
    /// </summary>
    /// <param name="queue">The queue for batching operations before sending to clients.</param>
    /// <param name="options">Options for configuring the object type tracking behavior.</param>
    /// <param name="logger">Logger for logging information and errors.</param>
    /// <param name="hubContext">The SignalR hub context for communicating with clients.</param>
    /// <param name="key">Optional key to identify a specific collection instance.</param>
    public ObservableCollectionTracker(
        BulkFlushQueue<OperationContainer<T>> queue,
        IOptions<ObjectTypeConfiguration<OperationContainer<T>>> options,
        ILogger<ObservableCollectionTracker<T>> logger,
        IHubContext<AutoPatchHub> hubContext,
        string? key = null)
        : this(queue, options, logger, new AutoPatchHubClients(hubContext), null, key)
    {
    }

    /// <summary>
    /// Creates a tracker.
    /// </summary>
    /// <param name="queue">The queue for batching operations before sending to clients.</param>
    /// <param name="options">Options for configuring the object type tracking behavior.</param>
    /// <param name="logger">Logger for logging information and errors.</param>
    /// <param name="hubs">The hubs to send through.</param>
    /// <param name="payloadSerializerOptions">The JSON settings of the SignalR hub protocol, or null for the defaults.</param>
    /// <param name="key">Optional key to identify a specific collection instance.</param>
    public ObservableCollectionTracker(
        BulkFlushQueue<OperationContainer<T>> queue,
        IOptions<ObjectTypeConfiguration<OperationContainer<T>>> options,
        ILogger<ObservableCollectionTracker<T>> logger,
        AutoPatchHubClients hubs,
        JsonSerializerOptions? payloadSerializerOptions,
        string? key = null)
    {
        _queue = queue;
        _options = options.Value;
        _logger = logger;
        _hubs = hubs;
        _key = key;
        _json = CreateSerializerOptions(payloadSerializerOptions, _options.ExcludedProperties);
    }

    /// <summary>
    /// Gets the name of the type being tracked.
    /// </summary>
    /// <value>The simple name of type <typeparamref name="T"/>.</value>
    public string TypeName => typeof(T).Name;

    /// <summary>
    /// Gets the key that identifies this specific collection instance.
    /// </summary>
    /// <value>A string representing the key for this collection, or null for the default collection.</value>
    public string? Key => _key;

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

    private string Target => AutoPatchProtocol.GetMethodName(AutoPatchProtocol.GetSubscriptionKey(TypeName, _key));

    /// <summary>
    /// Begins monitoring the tracked collection and its items for changes.
    /// </summary>
    public void StartTracking()
    {
        _queue.OnFlush += HandleQueueFlush;
        TrackedCollection.CollectionChanged += HandleCollectionChanged;

        lock (_gate)
        {
            if (TrackedCollection.Count > 0)
            {
                ResetFromCollection();
            }
        }
    }

    /// <summary>
    /// Stops monitoring the tracked collection and its items for changes.
    /// </summary>
    /// <remarks>
    /// This method unsubscribes from all events to disable synchronization and prevent memory leaks.
    /// </remarks>
    public void StopTracking()
    {
        StopObservingCollection();
        _queue.OnFlush -= HandleQueueFlush;
    }

    /// <summary>
    /// Sends the state of the collection after the last delivered batch to one connection.
    /// </summary>
    /// <param name="connectionId">The unique identifier of the connection to send the data to.</param>
    /// <remarks>
    /// The data is sent right away, without waiting for the next flush, and does not read the collection itself, so it is safe to
    /// call while the application changes the collection on another thread.
    /// </remarks>
    public void SendFullData(string connectionId) => _queue.SendImmediately(new FullDataRequest(connectionId));

    /// <summary>
    /// Sends all queued changes now.
    /// </summary>
    /// <returns>A task that completes once the changes have been handed to SignalR.</returns>
    public Task FlushAsync() => _queue.Flush();

    /// <summary>
    /// Stops tracking, tells all subscribers that the collection is now empty and releases the queue afterwards.
    /// </summary>
    internal Task RetireAsync()
    {
        StopObservingCollection();
        return _queue.SendImmediately(new RemovedNotice()).ContinueWith(
            _ =>
            {
                _queue.OnFlush -= HandleQueueFlush;
                _queue.Dispose();
            },
            TaskScheduler.Default);
    }

    private void StopObservingCollection()
    {
        TrackedCollection.CollectionChanged -= HandleCollectionChanged;
        lock (_gate)
        {
            foreach (var item in _attached)
            {
                item.PropertyChanged -= HandleItemPropertyChanged;
            }
            _attached.Clear();
            _items.Clear();
        }
    }

    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (_gate)
        {
            try
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                        var index = e.NewStartingIndex >= 0 ? e.NewStartingIndex : _items.Count;
                        foreach (T item in e.NewItems)
                        {
                            InsertItem(index++, item);
                        }
                        break;

                    case NotifyCollectionChangedAction.Remove when e.OldItems is not null && e.OldStartingIndex >= 0:
                        foreach (T item in e.OldItems)
                        {
                            RemoveItem(e.OldStartingIndex, item);
                        }
                        break;

                    case NotifyCollectionChangedAction.Replace when e.NewItems is not null && e.NewStartingIndex >= 0:
                        for (var i = 0; i < e.NewItems.Count; i++)
                        {
                            ReplaceItem(e.NewStartingIndex + i, (T)e.NewItems[i]!);
                        }
                        break;

                    case NotifyCollectionChangedAction.Move when e.OldItems is { Count: 1 } && e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0:
                        MoveItem(e.OldStartingIndex, e.NewStartingIndex);
                        break;

                    default:
                        ResetFromCollection();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not track a {Action} change of {TypeName}; sending the whole collection instead", e.Action, TypeName);
                ResetFromCollection();
            }
        }
    }

    private void InsertItem(int index, T item)
    {
        if (index > _items.Count)
        {
            throw new InvalidOperationException($"Insert at {index} does not match the tracked collection with {_items.Count} items.");
        }

        var value = SerializeItem(item);
        _items.Insert(index, item);
        Attach(item);
        Enqueue(new PatchOperation { Op = PatchOperation.Add, Path = $"/{index}", Value = value });
    }

    private void RemoveItem(int index, T item)
    {
        if (index >= _items.Count || !ReferenceEquals(_items[index], item))
        {
            throw new InvalidOperationException($"Remove at {index} does not match the tracked collection.");
        }

        _items.RemoveAt(index);
        DetachIfRemoved(item);
        Enqueue(new PatchOperation { Op = PatchOperation.Remove, Path = $"/{index}" });
    }

    private void ReplaceItem(int index, T item)
    {
        if (index >= _items.Count)
        {
            throw new InvalidOperationException($"Replace at {index} does not match the tracked collection with {_items.Count} items.");
        }

        var value = SerializeItem(item);
        var replaced = _items[index];
        _items[index] = item;
        Attach(item);
        DetachIfRemoved(replaced);
        Enqueue(new PatchOperation { Op = PatchOperation.Replace, Path = $"/{index}", Value = value });
    }

    private void MoveItem(int from, int to)
    {
        var item = _items[from];
        _items.RemoveAt(from);
        _items.Insert(to, item);
        Enqueue(new PatchOperation { Op = PatchOperation.Move, From = $"/{from}", Path = $"/{to}" });
    }

    /// <summary>
    /// Re-reads the whole collection (e.g. after <c>Clear()</c>) and replaces the content for all subscribers.
    /// </summary>
    private void ResetFromCollection()
    {
        foreach (var item in _attached)
        {
            item.PropertyChanged -= HandleItemPropertyChanged;
        }
        _attached.Clear();
        _items.Clear();

        try
        {
            _items.AddRange(TrackedCollection);
            foreach (var item in _items)
            {
                Attach(item);
            }
            _queue.Add(new ResetOperationContainer<T>([.. _items.Select(SerializeItem)]));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read the collection of {TypeName}", TypeName);
        }
    }

    private void Attach(T item)
    {
        if (item is not null && _attached.Add(item))
        {
            item.PropertyChanged += HandleItemPropertyChanged;
        }
    }

    private void DetachIfRemoved(T item)
    {
        if (item is not null && !_items.Any(i => ReferenceEquals(i, item)) && _attached.Remove(item))
        {
            item.PropertyChanged -= HandleItemPropertyChanged;
        }
    }

    private void HandleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not T item)
        {
            return;
        }

        try
        {
            string path;
            JsonElement value;
            if (string.IsNullOrEmpty(e.PropertyName))
            {
                path = string.Empty;
                value = SerializeItem(item);
            }
            else if (GetProperty(item, e.PropertyName) is { } property)
            {
                path = $"/{e.PropertyName}";
                value = JsonSerializer.SerializeToElement(property.Get(item), property.PropertyType, _json);
            }
            else
            {
                return; // Excluded, ignored for JSON or not a property.
            }

            lock (_gate)
            {
                // An item may be contained more than once; an item that is no longer contained produces no patch.
                for (var index = 0; index < _items.Count; index++)
                {
                    if (ReferenceEquals(_items[index], item))
                    {
                        Enqueue(new PatchOperation { Op = PatchOperation.Replace, Path = $"/{index}{path}", Value = value });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling property change for {TypeName}.{PropertyName}", TypeName, e.PropertyName);
        }
    }

    private TrackedProperty? GetProperty(T item, string propertyName)
        => _properties.GetOrAdd(propertyName, static (name, state) =>
        {
            var (itemType, json) = state;
            var jsonProperty = json.GetTypeInfo(itemType).Properties
                .FirstOrDefault(p => (p.AttributeProvider as MemberInfo)?.Name == name);
            return jsonProperty?.Get is { } getter ? new TrackedProperty(getter, jsonProperty.PropertyType, jsonProperty.Name) : null;
        }, (item.GetType(), _json));

    private JsonElement SerializeItem(T item) => JsonSerializer.SerializeToElement(item, item?.GetType() ?? typeof(T), _json);

    private void Enqueue(PatchOperation operation) => _queue.Add(new DefaultOperationContainer<T>(operation));

    /// <summary>
    /// Delivers a sealed batch. Runs on the queue's delivery loop, one batch at a time.
    /// </summary>
    private async Task HandleQueueFlush(List<OperationContainer<T>> containers)
    {
        var batch = new List<PatchOperation>();
        foreach (var container in containers)
        {
            switch (container)
            {
                case DefaultOperationContainer<T> change:
                    batch.Add(change.Operation);
                    break;

                case ResetOperationContainer<T> reset:
                    await SendBatchAsync(batch);
                    _sentState.Clear();
                    _sentState.AddRange(reset.Items.Select(item => ToNode(item)));
                    await SendInitialSetToGroupAsync();
                    break;

                case FullDataRequest request:
                    await SendBatchAsync(batch);
                    await SendFullDataAsync(request.ConnectionId);
                    break;

                case ResyncRequest:
                    await SendBatchAsync(batch);
                    Volatile.Write(ref _resyncScheduled, 0);
                    await SendInitialSetToGroupAsync();
                    break;

                case RemovedNotice:
                    batch.Clear();
                    _sentState.Clear();
                    _retired = true;
                    await SendInitialSetToGroupAsync();
                    break;
            }
        }
        await SendBatchAsync(batch);
    }

    private async Task SendBatchAsync(List<PatchOperation> batch)
    {
        if (batch.Count == 0 || _retired)
        {
            batch.Clear();
            return;
        }

        PatchOperation[] operations = [.. batch];
        batch.Clear();

        try
        {
            ApplyToSentState(operations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The operations for {TypeName} do not match the sent state; sending the whole collection instead", TypeName);
            lock (_gate)
            {
                ResetFromCollection();
            }
            return;
        }

        var sequence = ++_sequence;
        try
        {
            await _hubs.SendToGroupAsync(Target, Target, [Target, operations, false, sequence]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send batch {Sequence} of {TypeName}; subscribers will get the full data again", sequence, TypeName);
            ScheduleResync();
        }
    }

    private async Task SendInitialSetToGroupAsync()
    {
        var sequence = ++_sequence;
        try
        {
            await _hubs.SendToGroupAsync(Target, Target, [Target, SentStateAsOperations(), true, sequence]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send the full data of {TypeName} to its subscribers; retrying", TypeName);
            ScheduleResync();
        }
    }

    private async Task SendFullDataAsync(string connectionId)
    {
        try
        {
            await _hubs.SendToConnectionAsync(connectionId, Target, [Target, SentStateAsOperations(), true, _sequence]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send the full data of {TypeName} to connection {ConnectionId}", TypeName, connectionId);
        }
    }

    private void ScheduleResync()
    {
        if (!_retired && Interlocked.Exchange(ref _resyncScheduled, 1) == 0)
        {
            _ = ResyncLaterAsync();
        }
    }

    private async Task ResyncLaterAsync()
    {
        var delay = TimeSpan.FromMilliseconds(Math.Clamp(_queue.ThrottleInterval.TotalMilliseconds, 50, 5_000));
        await Task.Delay(delay);
        await _queue.SendImmediately(new ResyncRequest());
    }

    private PatchOperation[] SentStateAsOperations()
        => [.. _sentState.Select(node => new PatchOperation { Op = PatchOperation.Add, Path = "/-", Value = JsonSerializer.SerializeToElement(node) })];

    private void ApplyToSentState(PatchOperation[] operations)
    {
        foreach (var operation in operations)
        {
            if (!AutoPatchProtocol.TryParsePath(operation.Path, out var index, out var property))
            {
                throw new InvalidOperationException($"Unsupported path '{operation.Path}'.");
            }

            switch (operation.Op)
            {
                case PatchOperation.Add when index < 0:
                    _sentState.Add(ToNode(operation.Value));
                    break;
                case PatchOperation.Add:
                    _sentState.Insert(index, ToNode(operation.Value));
                    break;
                case PatchOperation.Remove:
                    _sentState.RemoveAt(index);
                    break;
                case PatchOperation.Replace when property is null:
                    _sentState[index] = ToNode(operation.Value);
                    break;
                case PatchOperation.Replace:
                    var jsonName = _properties.TryGetValue(property, out var tracked) && tracked is not null ? tracked.JsonName : property;
                    (_sentState[index] as JsonObject ?? throw new InvalidOperationException($"Item {index} is not an object."))[jsonName] = ToNode(operation.Value);
                    break;
                case PatchOperation.Move when AutoPatchProtocol.TryParsePath(operation.From, out var from, out _):
                    var moved = _sentState[from];
                    _sentState.RemoveAt(from);
                    _sentState.Insert(index, moved);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported operation '{operation.Op}' on '{operation.Path}'.");
            }
        }
    }

    private static JsonNode? ToNode(JsonElement? value) => value is { } element ? JsonSerializer.SerializeToNode(element) : null;

    private static JsonSerializerOptions CreateSerializerOptions(JsonSerializerOptions? payloadOptions, string[]? excludedProperties)
    {
        var source = payloadOptions ?? new JsonHubProtocolOptions().PayloadSerializerOptions;
        var options = new JsonSerializerOptions(source);
        var resolver = source.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = excludedProperties is { Length: > 0 }
            ? resolver.WithAddedModifier(typeInfo =>
            {
                if (!typeof(T).IsAssignableFrom(typeInfo.Type))
                {
                    return;
                }
                for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
                {
                    if (typeInfo.Properties[i].AttributeProvider is MemberInfo member && excludedProperties.Contains(member.Name))
                    {
                        typeInfo.Properties.RemoveAt(i);
                    }
                }
            })
            : resolver;
        return options;
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
        GC.SuppressFinalize(this);
        StopTracking();
        _queue.Dispose();
    }

    private sealed record TrackedProperty(Func<object, object?> Get, Type PropertyType, string JsonName);

    private sealed record FullDataRequest(string ConnectionId) : OperationContainer<T>;

    private sealed record ResyncRequest : OperationContainer<T>;

    private sealed record RemovedNotice : OperationContainer<T>;
}
