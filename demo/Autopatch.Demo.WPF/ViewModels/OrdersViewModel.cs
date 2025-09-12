using Autopatch.Client.Services;
using Autopatch.Demo.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Autopatch.Demo.WPF.ViewModels;

/// <summary>
/// View model for the orders list display.
/// Manages live pizza order tracking with automatic updates via AutoPatch.
/// </summary>
public partial class OrdersViewModel : ObservableObject
{
    private readonly IAutoPatchClient _autoPatchClient;

    [ObservableProperty]
    private ObservableCollection<PizzaOrder> _orders = [];

    [ObservableProperty]
    private PizzaOrder? _selectedOrder;

    public OrdersViewModel(IAutoPatchClient autoPatchClient)
    {
        _autoPatchClient = autoPatchClient;
    }

    /// <summary>
    /// Initialize AutoPatch subscription for pizza orders
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _autoPatchClient.SubscribeToTypeAsync<PizzaOrder>(null, "admin");
            this.Orders = _autoPatchClient.GetTrackedCollection<PizzaOrder>();
        }
        catch (Exception ex)
        {
            // Handle subscription error - in real app would show user message
            System.Diagnostics.Debug.WriteLine($"Failed to subscribe to orders: {ex.Message}");
        }
    }
}
