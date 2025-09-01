namespace Autopatch.Server;

/// <summary>
/// Provides functionality to manage subscriptions, track changes, and handle data synchronization  for specified types
/// in a distributed system.
/// </summary>
/// <remarks>This service is responsible for subscribing to data collections, tracking changes, and batching 
/// updates for efficient synchronization. It supports subscribing to specific types, unsubscribing  from active
/// subscriptions, and requesting full data for a given subscription.</remarks>
public interface IAutoPatchService
{
    /// <summary>
    /// Subscribes to updates for the specified type.
    /// </summary>
    /// <param name="typeName">The name of the type to subscribe to. This value cannot be null or empty.</param>
    /// <returns>A subscription identifier as a string, which can be used to manage the subscription.</returns>
    string SubscribeToType(string typeName);

    /// <summary>
    /// Unsubscribes from a subscription with the specified subscription ID.
    /// </summary>
    /// <param name="subscriptionId">The unique identifier of the subscription to unsubscribe from. Cannot be null or empty.</param>
    /// <returns>A message indicating the result of the unsubscription process.</returns>
    string Unsubscribe(string subscriptionId);

    /// <summary>
    /// Requests the full set of data for a specific subscription over a specified connection.
    /// </summary>
    /// <param name="subscriptionId">The unique identifier of the subscription for which data is requested. Cannot be null or empty.</param>
    /// <param name="connectionId">The unique identifier of the connection through which the data request is made. Cannot be null or empty.</param>
    void RequestFullData(string subscriptionId, string connectionId);
}

