using Autopatch.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace Autopatch.Server.SignalR;

/// <summary>
/// Provides a SignalR hub for managing real-time object patch synchronization between clients and tracked collections.
/// </summary>
/// <remarks>
/// This hub allows clients to subscribe to specific object types and receive real-time updates when changes occur
/// in the tracked collections. It leverages SignalR groups to manage subscriptions efficiently.
/// </remarks>
/// <param name="collectionManager">The collection manager that handles dynamic creation and retrieval of tracked collections.</param>
public class AutoPatchHub(ITrackedCollectionManager collectionManager) : Hub
{

    /// <summary>
    /// Subscribes the current connection to receive updates for objects of the specified type.
    /// </summary>
    /// <param name="typeName">The name of the type to subscribe to for receiving updates.</param>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <returns>A task that represents the asynchronous subscription operation.</returns>
    /// <remarks>
    /// When a client subscribes to a type, they are added to a SignalR group named "AutoPatch/{typeName}" or "AutoPatch/{typeName}/{key}".
    /// If a tracker exists for the specified type and key, the full current state of the data is immediately sent to the client.
    /// </remarks>
    public async Task SubscribeToType(string typeName, string? key = null)
    {
        var subscriptionKey = GetSubscriptionKey(typeName, key);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"AutoPatch/{subscriptionKey}");

        var tracker = collectionManager.GetAllTrackers().FirstOrDefault(t => t.GetSubscriptionKey() == subscriptionKey);
        tracker?.SendFullData(Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribes the current connection from receiving updates for objects of the specified type.
    /// </summary>
    /// <param name="typeName">The name of the type to unsubscribe from.</param>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <returns>A task that represents the asynchronous unsubscription operation.</returns>
    /// <remarks>
    /// This method removes the client from the SignalR group "AutoPatch/{typeName}" or "AutoPatch/{typeName}/{key}", stopping them from
    /// receiving further updates for objects of the specified type and key.
    /// </remarks>
    public Task UnsubscribeFromType(string typeName, string? key = null)
    {
        var subscriptionKey = GetSubscriptionKey(typeName, key);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"AutoPatch/{subscriptionKey}");
    }

    /// <summary>
    /// Gets the subscription key for a type and optional key parameter.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <param name="key">Optional key to identify a specific collection.</param>
    /// <returns>The subscription key in format "TypeName" or "TypeName/Key".</returns>
    private static string GetSubscriptionKey(string typeName, string? key)
    {
        return string.IsNullOrEmpty(key) ? typeName : $"{typeName}/{key}";
    }

    //public Task<ClientChangeResult> SubmitClientChange(string typeName, Operation[] operations)
    //{

    //}
}
