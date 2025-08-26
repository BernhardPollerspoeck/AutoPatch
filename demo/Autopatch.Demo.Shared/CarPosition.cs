using CommunityToolkit.Mvvm.ComponentModel;

namespace Autopatch.Demo.Shared;


public partial class CarPosition : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private double _latitude;

    [ObservableProperty]
    private double _longitude;
}
