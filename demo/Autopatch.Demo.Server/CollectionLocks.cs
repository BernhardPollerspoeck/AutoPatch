namespace Autopatch.Demo.Server;

/// <summary>
/// Shared synchronization objects for thread-safe access to collections across all services.
/// All services must use these same lock instances to ensure proper synchronization.
/// </summary>
public static class CollectionLocks
{
    /// <summary>
    /// Shared lock for the PizzaOrder collection.
    /// Use this lock for all read/write operations on the orders collection.
    /// </summary>
    public static readonly object OrdersLock = new();

    /// <summary>
    /// Shared lock for the DeliveryDriver collection.
    /// Use this lock for all read/write operations on the drivers collection.
    /// </summary>
    public static readonly object DriversLock = new();
}
