using Autopatch.Client.Extensions;
using Autopatch.Client.Models;
using Autopatch.Demo.Shared;
using Autopatch.Demo.WPF.ViewModels;
using Autopatch.Demo.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace Autopatch.Demo.WPF;

/// <summary>
/// Pizza Palace WPF Demo Application using modern .NET 10 Host Builder pattern.
/// Demonstrates AutoPatch Framework with live order tracking and driver visualization.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// Application startup - configure services and show main window
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        _host = CreateHostBuilder().Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    /// <summary>
    /// Application shutdown - stop host gracefully
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    /// <summary>
    /// Configure services using modern Host Builder pattern
    /// </summary>
    private static HostApplicationBuilder CreateHostBuilder()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddAutoPatch(cfg =>
        {
            cfg.Endpoint = "http://localhost:5249";
            
            cfg.Dispatcher = action => Current.Dispatcher.Invoke(action);
        })
            .AddTrackedCollection<PizzaOrder>()
            .AddTrackedCollection<DeliveryDriver>();


        // ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<OrdersViewModel>();
        builder.Services.AddSingleton<DriversViewModel>();
        builder.Services.AddSingleton<StatisticsViewModel>();

        // Views
        builder.Services.AddSingleton<MainWindow>();

        // Auto-start AutoPatch connection
        builder.Services.AddHostedService<AutoPatchConnectionService>();

        return builder;
    }
}
