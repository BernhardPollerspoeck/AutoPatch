using Microsoft.AspNetCore.Builder;

namespace Autopatch.Server;

/// <summary>
/// Adds the AutoPatch SignalR hub to the specified <see cref="WebApplication"/> instance.
/// </summary>
/// <remarks>This method maps the AutoPatch SignalR hub to the endpoint "/Autopatch".  It should be called during
/// the application's startup configuration to enable the AutoPatch functionality.</remarks>
public static class IWebApplicationExetensions
{
    /// <summary>
    /// Configures the application to use the AutoPatch hub at the specified endpoint.
    /// </summary>
    /// <remarks>This method maps the <see cref="AutoPatchHub"/> to the "/Autopatch" endpoint, enabling
    /// SignalR communication for the AutoPatch feature.</remarks>
    /// <param name="host">The <see cref="WebApplication"/> instance to configure.</param>
    /// <returns>A <see cref="HubEndpointConventionBuilder"/> that can be used to further customize the endpoint.</returns>
    public static HubEndpointConventionBuilder UseAutoPatch(this WebApplication host)
    {
        return host.MapHub<AutoPatchHub>("/Autopatch");
    }
}

