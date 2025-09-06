using Microsoft.Extensions.Hosting;

namespace Autopatch.Client;

public class AutopatchConnectionManager(IAutoPatchClient autoPatchClient) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return autoPatchClient.ConnectAsync(cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return autoPatchClient.DisconnectAsync(cancellationToken);
    }
}
