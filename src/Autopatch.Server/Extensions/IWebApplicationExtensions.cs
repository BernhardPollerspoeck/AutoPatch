using Autopatch.Core;
using Autopatch.Server.SignalR;
using Microsoft.AspNetCore.Builder;

namespace Autopatch.Server.Extensions;

/// <summary>
/// Adds the AutoPatch SignalR hub to the specified <see cref="WebApplication"/> instance.
/// </summary>
public static class IWebApplicationExtensions
{
    /// <summary>
    /// Maps the <see cref="AutoPatchHub"/>.
    /// </summary>
    /// <param name="host">The <see cref="WebApplication"/> instance to configure.</param>
    /// <param name="pattern">The route of the hub. Defaults to <c>/autopatch</c>.</param>
    /// <returns>A <see cref="HubEndpointConventionBuilder"/> that can be used to further customize the endpoint.</returns>
    /// <remarks>
    /// Clients use the full hub URL (e.g. <c>https://host/autopatch</c>) as endpoint. The .NET client also accepts the bare host
    /// URL and appends the default path. To serve AutoPatch through an application hub, derive that hub from
    /// <see cref="AutoPatchHub"/> and map it instead.
    /// </remarks>
    public static HubEndpointConventionBuilder UseAutoPatch(this WebApplication host, string pattern = AutoPatchProtocol.DefaultHubPath)
    {
        return host.MapHub<AutoPatchHub>(pattern);
    }
}
