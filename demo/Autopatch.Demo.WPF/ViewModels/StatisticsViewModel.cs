using Autopatch.Demo.Shared;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Autopatch.Demo.WPF.ViewModels;

/// <summary>
/// View model for live statistics display.
/// Calculates real-time metrics from orders and drivers collections.
/// </summary>
public partial class StatisticsViewModel : ObservableObject
{
    private readonly OrdersViewModel _ordersViewModel;
    private readonly DriversViewModel _driversViewModel;
    private readonly Timer _updateTimer;

    [ObservableProperty]
    private int _totalOrders;

    [ObservableProperty]
    private int _activeDrivers;

    [ObservableProperty]
    private double _averageDeliveryTime;

    [ObservableProperty]
    private int _ordersPerHour;

    [ObservableProperty]
    private string _currentStats = string.Empty;

    public StatisticsViewModel(OrdersViewModel ordersViewModel, DriversViewModel driversViewModel)
    {
        _ordersViewModel = ordersViewModel;
        _driversViewModel = driversViewModel;

        // Update stats every 5 seconds
        _updateTimer = new Timer(UpdateStatistics, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private void UpdateStatistics(object? state)
    {
        // Calculate current statistics
        TotalOrders = _ordersViewModel.Orders.Count;
        ActiveDrivers = _driversViewModel.Drivers.Count(d => d.Status != DriverStatus.Offline);
        
        // Simple average delivery time calculation (demo purposes)
        var completedOrders = _ordersViewModel.Orders.Where(o => o.Status == OrderStatus.OutForDelivery).ToList();
        if (completedOrders.Any())
        {
            var avgMinutes = completedOrders.Average(o => 
                o.EstimatedDelivery.HasValue 
                    ? (o.EstimatedDelivery.Value - o.OrderTime).TotalMinutes 
                    : 25);
            AverageDeliveryTime = Math.Round(avgMinutes, 1);
        }
        else
        {
            AverageDeliveryTime = 0;
        }

        // Calculate orders per hour (simplified)
        var recentOrders = _ordersViewModel.Orders.Where(o => 
            DateTime.Now - o.OrderTime < TimeSpan.FromHours(1)).Count();
        OrdersPerHour = recentOrders;

        // Update summary string
        CurrentStats = $"📊 {TotalOrders} Orders | 🚗 {ActiveDrivers} Drivers | ⏱️ {AverageDeliveryTime}min avg | 📈 {OrdersPerHour}/hour";
    }
}
