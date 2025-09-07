using Autopatch.Demo.WPF.ViewModels;
using System.Windows;

namespace Autopatch.Demo.WPF.Views;

/// <summary>
/// Main window for Poller's Pizza Palace demo application.
/// Shows live order tracking and driver visualization using AutoPatch Framework.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
