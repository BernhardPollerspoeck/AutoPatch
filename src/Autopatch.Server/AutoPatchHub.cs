using Microsoft.AspNetCore.SignalR;

namespace Autopatch.Server;

/// <summary>
/// Represents a SignalR hub that facilitates real-time communication for subscribing to data types, unsubscribing from
/// subscriptions, and requesting full data updates.
/// </summary>
/// <remarks>This hub interacts with an <see cref="IAutoPatchService"/> to manage subscriptions and data requests.
/// Clients can use this hub to subscribe to specific data types, unsubscribe from existing subscriptions, and request
/// full data updates for a given subscription.</remarks>
/// <param name="autoPatchService"></param>
public class AutoPatchHub(IAutoPatchService autoPatchService) : Hub
{
    /// <summary>
    /// Subscribes to a specific type and returns the associated group name.
    /// </summary>
    /// <param name="typeName">The name of the type to subscribe to. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the group name  associated with the
    /// specified type.</returns>
    public Task<string> SubscribeToType(string typeName)
    {
        var groupName = autoPatchService.SubscribeToType(typeName);
        return Task.FromResult(groupName);
    }
    /// <summary>
    /// Unsubscribes from a subscription and returns the associated group name.
    /// </summary>
    /// <remarks>This method removes the specified subscription and retrieves the name of the group it was
    /// associated with. The returned group name can be used for further processing or validation.</remarks>
    /// <param name="subscriptionId">The unique identifier of the subscription to unsubscribe from. Cannot be <see langword="null"/> or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the name of the group associated
    /// with the unsubscribed subscription.</returns>
    public Task<string> Unsubscribe(string subscriptionId)
    {
        var groupName = autoPatchService.Unsubscribe(subscriptionId);
        return Task.FromResult(groupName);
    }

    /// <summary>
    /// Requests the full data set for the specified subscription.
    /// </summary>
    /// <remarks>This method triggers a request to retrieve the full data set associated with the given
    /// subscription ID.  The operation is performed asynchronously, but the returned task is already completed when the
    /// method returns.</remarks>
    /// <param name="subscriptionId">The unique identifier of the subscription for which the full data set is requested. Cannot be null or empty.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task RequestFullData(string subscriptionId)
    {
        autoPatchService.RequestFullData(subscriptionId, Context.ConnectionId);
        return Task.CompletedTask;
    }

}

