using System.Reflection;

namespace Autopatch.Client.Models;

/// <summary>
/// Represents a subscription to a specific type for real-time updates.
/// </summary>
/// <param name="collection">The observable collection being tracked.</param>
/// <param name="itemType">The type of items in the collection.</param>
internal class Subscription(object collection, Type itemType)
{
    /// <summary>
    /// Gets the tracked observable collection.
    /// </summary>
    public object TrackedCollection { get; } = collection;

    /// <summary>
    /// Gets the type of items in the collection.
    /// </summary>
    public Type ItemType { get; } = itemType;

    /// <summary>
    /// Gets or sets the number of active subscribers to this type.
    /// </summary>
    public int Subscribers { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the subscription has received its initial data set.
    /// </summary>
    public bool IsInitialized { get; set; } = false;

    private readonly Dictionary<string, PropertyInfo> _propertyCache = [];

    /// <summary>
    /// Gets property information for a given property name, using caching for performance.
    /// </summary>
    /// <param name="propertyName">The name of the property to get information for.</param>
    /// <returns>The PropertyInfo for the specified property, or null if not found.</returns>
    public PropertyInfo? GetPropertyInfo(string propertyName)
    {
        if (!_propertyCache.TryGetValue(propertyName, out var propInfo))
        {
            propInfo = ItemType.GetProperty(propertyName);
            if (propInfo != null)
                _propertyCache[propertyName] = propInfo;
        }
        return propInfo;
    }
}
