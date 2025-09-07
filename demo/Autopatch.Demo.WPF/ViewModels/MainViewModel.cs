using Autopatch.Client.Services;
using Autopatch.Demo.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Autopatch.Demo.WPF.ViewModels;

/// <summary>
/// Main view model that orchestrates all sub-view models and manages application state.
/// Uses modern MVVM pattern with CommunityToolkit.Mvvm for property change notifications.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAutoPatchClient _autoPatchClient;
    private readonly Timer _clockTimer;

    [ObservableProperty]
    private string _statusMessage = "Starting Pizza Palace Demo...";

    [ObservableProperty]
    private string _connectionStatus = "Connecting...";

    [ObservableProperty]
    private string _connectionStatusColor = "Orange";

    [ObservableProperty]
    private DateTime _currentTime = DateTime.Now;

    public OrdersViewModel OrdersViewModel { get; }
    public DriversViewModel DriversViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }

    public MainViewModel(
        IAutoPatchClient autoPatchClient,
        OrdersViewModel ordersViewModel,
        DriversViewModel driversViewModel,
        StatisticsViewModel statisticsViewModel)
    {
        _autoPatchClient = autoPatchClient;
        OrdersViewModel = ordersViewModel;
        DriversViewModel = driversViewModel;
        StatisticsViewModel = statisticsViewModel;

        // Setup event handlers
        //_autoPatchClient.OnConnectionChanged += OnConnectionChanged;
        //_autoPatchClient.OnError += OnError;

        // Start clock timer
        _clockTimer = new Timer(UpdateClock, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        StatusMessage = "🍕 Welcome to Poller's Pizza Palace - Live Demo Running";
    }

    private void UpdateClock(object? state)
    {
        CurrentTime = DateTime.Now;
    }

    private void OnConnectionChanged(bool isConnected)
    {
        ConnectionStatus = isConnected ? "Connected" : "Disconnected";
        ConnectionStatusColor = isConnected ? "LimeGreen" : "Red";
        
        StatusMessage = isConnected 
            ? "🚀 AutoPatch Connected - Live data flowing"
            : "⚠️ AutoPatch Disconnected - Attempting reconnection";
    }

    private void OnError(Exception ex)
    {
        StatusMessage = $"❌ Error: {ex.Message}";
        ConnectionStatusColor = "Red";
    }
}
