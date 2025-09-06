using Microsoft.AspNetCore.SignalR;

namespace Autopatch.Server;

public class AutoPatchHub(IEnumerable<IObjectTracker> objectTrackers) : Hub
{
    /// <summary>
    /// Subscribes to a specific type and returns the associated group name.
    /// </summary>
    /// <param name="typeName">The name of the type to subscribe to. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the group name  associated with the
    /// specified type.</returns>
    public async Task SubscribeToType(string typeName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"AutoPatch/{typeName}");

        var tracker = objectTrackers.FirstOrDefault(t => t.TypeName == typeName);
        tracker?.SendFullData(Context.ConnectionId);
    }


    public Task UnsubscribeFromType(string typeName)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"AutoPatch/{typeName}");
    }



}
