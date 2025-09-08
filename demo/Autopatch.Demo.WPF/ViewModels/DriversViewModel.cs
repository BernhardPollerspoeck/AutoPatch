using Autopatch.Client.Services;
using Autopatch.Demo.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Autopatch.Demo.WPF.ViewModels;

/// <summary>
/// View model for the driver map visualization.
/// Manages live driver tracking with position updates via AutoPatch.
/// </summary>
public partial class DriversViewModel : ObservableObject
{
    private readonly IAutoPatchClient _autoPatchClient;

    [ObservableProperty]
    private ObservableCollection<DeliveryDriver> _drivers = [];

    [ObservableProperty]
    private DeliveryDriver? _selectedDriver;

    public DriversViewModel(IAutoPatchClient autoPatchClient)
    {
        _autoPatchClient = autoPatchClient;
    }

    /// <summary>
    /// Initialize AutoPatch subscription for delivery drivers
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _autoPatchClient.SubscribeToTypeAsync<DeliveryDriver>();
            this.Drivers = _autoPatchClient.GetTrackedCollection<DeliveryDriver>();
        }
        catch (Exception ex)
        {
            // Handle subscription error - in real app would show user message
            System.Diagnostics.Debug.WriteLine($"Failed to subscribe to drivers: {ex.Message}");
        }
    }
}
