using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Autopatch.Server.SignalR;

/// <summary>
/// Sends AutoPatch messages through every hub that clients subscribe with.
/// </summary>
/// <remarks>
/// SignalR groups and connections belong to one hub type. Applications that derive their own hub from <see cref="AutoPatchHub"/>
/// (to use a single connection for everything) are registered automatically on the first subscription through that hub.
/// </remarks>
public sealed class AutoPatchHubClients
{
    private readonly ConcurrentDictionary<Type, IHubClients> _hubs = new();
    private readonly IServiceProvider? _services;

    /// <summary>
    /// Creates the hub registry with the default <see cref="AutoPatchHub"/>.
    /// </summary>
    /// <param name="defaultHub">The hub context of <see cref="AutoPatchHub"/>.</param>
    /// <param name="services">Root service provider used to resolve the hub contexts of derived hubs.</param>
    public AutoPatchHubClients(IHubContext<AutoPatchHub> defaultHub, IServiceProvider? services = null)
    {
        _hubs[typeof(AutoPatchHub)] = defaultHub.Clients;
        _services = services;
    }

    /// <summary>
    /// Makes sure messages are also sent through the hub of the given type.
    /// </summary>
    /// <param name="hubType">A type deriving from <see cref="AutoPatchHub"/>.</param>
    public void Register(Type hubType)
    {
        if (_services is null || _hubs.ContainsKey(hubType))
        {
            return;
        }

        var context = (IHubContext)_services.GetRequiredService(typeof(IHubContext<>).MakeGenericType(hubType));
        _hubs.TryAdd(hubType, context.Clients);
    }

    /// <summary>
    /// Sends a message to all members of a group in every registered hub.
    /// </summary>
    public async Task SendToGroupAsync(string groupName, string method, object?[] arguments)
    {
        foreach (var hub in _hubs.Values)
        {
            await hub.Group(groupName).SendCoreAsync(method, arguments);
        }
    }

    /// <summary>
    /// Sends a message to one connection. Only the hub that owns the connection delivers it.
    /// </summary>
    public async Task SendToConnectionAsync(string connectionId, string method, object?[] arguments)
    {
        foreach (var hub in _hubs.Values)
        {
            await hub.Client(connectionId).SendCoreAsync(method, arguments);
        }
    }
}
