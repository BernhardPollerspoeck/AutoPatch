using Microsoft.Extensions.Hosting;
using Autopatch.Client.Services;
using Autopatch.Demo.WPF.ViewModels;

namespace Autopatch.Demo.WPF;

/// <summary>
/// Background service that connects the AutoPatch client and initializes the subscriptions.
/// Keeps retrying until the server is reachable, so the app can be started before the server.
/// After the first connection the client reconnects and resubscribes on its own.
/// </summary>
public class AutoPatchConnectionService(IAutoPatchClient autoPatchClient, MainViewModel mainViewModel) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await autoPatchClient.ConnectAsync(stoppingToken);
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                mainViewModel.ShowConnectionAttemptFailed(ex);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        // Initialize all view models with AutoPatch subscriptions
        await mainViewModel.OrdersViewModel.InitializeAsync();
        await mainViewModel.DriversViewModel.InitializeAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            await autoPatchClient.DisconnectAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Never connected.
        }
    }
}
