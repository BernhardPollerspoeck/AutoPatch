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
/// <param name="objectTrackers">The collection of object trackers that monitor changes in various data collections.</param>
public class AutoPatchHub(IEnumerable<IObjectTracker> objectTrackers) : Hub
{

    /// <summary>
    /// Subscribes the current connection to receive updates for objects of the specified type.
    /// </summary>
    /// <param name="typeName">The name of the type to subscribe to for receiving updates.</param>
    /// <returns>A task that represents the asynchronous subscription operation.</returns>
    /// <remarks>
    /// When a client subscribes to a type, they are added to a SignalR group named "AutoPatch/{typeName}".
    /// If a tracker exists for the specified type, the full current state of the data is immediately sent to the client.
    /// </remarks>
    public async Task SubscribeToType(string typeName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"AutoPatch/{typeName}");

        var tracker = objectTrackers.FirstOrDefault(t => t.TypeName == typeName);
        tracker?.SendFullData(Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribes the current connection from receiving updates for objects of the specified type.
    /// </summary>
    /// <param name="typeName">The name of the type to unsubscribe from.</param>
    /// <returns>A task that represents the asynchronous unsubscription operation.</returns>
    /// <remarks>
    /// This method removes the client from the SignalR group "AutoPatch/{typeName}", stopping them from
    /// receiving further updates for objects of the specified type.
    /// </remarks>
    public Task UnsubscribeFromType(string typeName)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"AutoPatch/{typeName}");
    }
}
