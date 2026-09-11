using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Autopatch.Client.Models;

/// <summary>
/// Represents a subscription to a specific type for real-time updates.
/// </summary>
/// <param name="collection">The observable collection being tracked.</param>
/// <param name="itemType">The type of items in the collection.</param>
internal class Subscription(object collection, Type itemType)
{
    private readonly ConcurrentDictionary<string, PropertyInfo?> _propertyCache = new(StringComparer.Ordinal);
    private MethodInfo? _move;
    private int _resyncPending;

    /// <summary>
    /// Gets the tracked observable collection.
    /// </summary>
    public object TrackedCollection { get; } = collection;

    /// <summary>
    /// Gets the tracked collection as a list.
    /// </summary>
    public IList Items => (IList)TrackedCollection;

    /// <summary>
    /// Gets the type of items in the collection.
    /// </summary>
    public Type ItemType { get; } = itemType;

    /// <summary>
    /// Gets or sets the type name the subscription was made with.
    /// </summary>
    public string TypeName { get; init; } = itemType.Name;

    /// <summary>
    /// Gets or sets the collection key the subscription was made with.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Gets or sets the authentication string the subscription was made with; it is sent again on every resubscribe.
    /// </summary>
    public string? AuthString { get; init; }

    /// <summary>
    /// Gets or sets the number of active subscribers to this type.
    /// </summary>
    public int Subscribers { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the subscription has received its initial data set.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of the last applied batch.
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// Marks that full data has been requested; returns false if a request is already pending.
    /// </summary>
    public bool TryBeginResync() => Interlocked.Exchange(ref _resyncPending, 1) == 0;

    /// <summary>
    /// Marks that full data has arrived.
    /// </summary>
    public void EndResync() => Volatile.Write(ref _resyncPending, 0);

    /// <summary>
    /// Gets property information for a given property name, using caching for performance.
    /// The exact name is preferred; otherwise the name is matched case-insensitively.
    /// </summary>
    /// <param name="propertyName">The name of the property to get information for.</param>
    /// <returns>The PropertyInfo for the specified property, or null if not found.</returns>
    public PropertyInfo? GetPropertyInfo(string propertyName)
        => _propertyCache.GetOrAdd(propertyName, static (name, type) =>
            type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase), ItemType);

    /// <summary>
    /// Moves an item, using <c>ObservableCollection.Move</c> when available so bound views see a single move.
    /// </summary>
    public void Move(int from, int to)
    {
        _move ??= TrackedCollection.GetType().GetMethod("Move", [typeof(int), typeof(int)]);
        if (_move is not null)
        {
            _move.Invoke(TrackedCollection, [from, to]);
            return;
        }

        var item = Items[from];
        Items.RemoveAt(from);
        Items.Insert(to, item);
    }
}
