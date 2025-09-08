using Microsoft.Extensions.Hosting;
using Autopatch.Demo.WPF.ViewModels;

namespace Autopatch.Demo.WPF;

/// <summary>
/// Hosted service that automatically initializes AutoPatch subscriptions when the application starts.
/// Uses modern .NET hosting patterns for clean startup/shutdown lifecycle management.
/// </summary>
public class AutoPatchConnectionService : IHostedService
{
    private readonly MainViewModel _mainViewModel;

    public AutoPatchConnectionService(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Initialize all view models with AutoPatch subscriptions
        await _mainViewModel.OrdersViewModel.InitializeAsync();
        await _mainViewModel.DriversViewModel.InitializeAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // AutoPatch client cleanup will be handled by DI container disposal
        return Task.CompletedTask;
    }
}
