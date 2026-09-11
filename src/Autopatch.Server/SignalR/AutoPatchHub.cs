using Autopatch.Core;
using Autopatch.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Autopatch.Server.SignalR;

/// <summary>
/// Provides a SignalR hub for managing real-time object patch synchronization between clients and tracked collections.
/// </summary>
/// <remarks>
/// This hub allows clients to subscribe to specific object types and receive real-time updates when changes occur
/// in the tracked collections. It leverages SignalR groups to manage subscriptions efficiently.
/// Applications that want a single connection for their own hub methods and AutoPatch can derive their hub from this class
/// and map it instead of calling <c>UseAutoPatch()</c>.
/// </remarks>
/// <param name="collectionManager">The collection manager that handles dynamic creation and retrieval of tracked collections.</param>
/// <param name="serviceProvider">Service provider for resolving validators.</param>
public class AutoPatchHub(ITrackedCollectionManager collectionManager, IServiceProvider serviceProvider) : Hub
{
    /// <summary>
    /// Subscribes the current connection to receive updates for objects of the specified type.
    /// </summary>
    /// <param name="typeName">The name of the type to subscribe to for receiving updates.</param>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <param name="authString">Optional authentication string for subscription validation.</param>
    /// <returns>A task that returns true if subscription was successful, false if rejected.</returns>
    /// <remarks>
    /// <para>
    /// Only collection types registered with <c>AddTrackedCollection</c> can be subscribed. If a validator is registered for the
    /// type, it is always called, regardless of whether an auth string is provided.
    /// </para>
    /// <para>
    /// The client is added to the group "AutoPatch/{typeName}" or "AutoPatch/{typeName}/{key}" and receives the full data right
    /// away - an empty collection if it has not been created yet.
    /// </para>
    /// </remarks>
    public async Task<bool> SubscribeToType(string typeName, string? key = null, string? authString = null)
    {
        var registry = serviceProvider.GetRequiredService<TrackedCollectionRegistry>();
        if (!registry.TryGet(typeName, out var registration))
        {
            return false;
        }

        if (!await registration.ValidateAsync(serviceProvider, Context.User, authString, key ?? string.Empty))
        {
            return false;
        }

        var hubs = serviceProvider.GetRequiredService<AutoPatchHubClients>();
        hubs.Register(GetType());

        var subscriptionKey = AutoPatchProtocol.GetSubscriptionKey(typeName, key);
        var target = AutoPatchProtocol.GetMethodName(subscriptionKey);
        await Groups.AddToGroupAsync(Context.ConnectionId, target);

        if (collectionManager.FindTracker(subscriptionKey) is { } tracker)
        {
            tracker.SendFullData(Context.ConnectionId);
        }
        else
        {
            // Sequence 0: the first batch of a collection created later is detected as a gap, so the client asks for full data.
            await hubs.SendToConnectionAsync(Context.ConnectionId, target, [target, Array.Empty<PatchOperation>(), true, 0L]);
        }

        return true;
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
        var target = AutoPatchProtocol.GetMethodName(AutoPatchProtocol.GetSubscriptionKey(typeName, key));
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, target);
    }
}
