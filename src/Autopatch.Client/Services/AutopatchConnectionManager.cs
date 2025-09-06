using Microsoft.Extensions.Hosting;

namespace Autopatch.Client.Services;

/// <summary>
/// Manages the connection lifecycle for the Autopatch client as a hosted service.
/// Automatically connects when the service starts and disconnects when the service stops.
/// </summary>
/// <param name="autoPatchClient">The Autopatch client instance to manage connections for.</param>
public class AutopatchConnectionManager(IAutoPatchClient autoPatchClient) : IHostedService
{
    /// <summary>
    /// Starts the hosted service by establishing a connection to the Autopatch service.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return autoPatchClient.ConnectAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the hosted service by disconnecting from the Autopatch service.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return autoPatchClient.DisconnectAsync(cancellationToken);
    }
}
