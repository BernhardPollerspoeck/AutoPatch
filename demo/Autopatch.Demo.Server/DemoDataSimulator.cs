using System.Collections.ObjectModel;
using Autopatch.Demo.Shared;
using Autopatch.Server;

namespace Autopatch.Demo.Server;



public class DemoDataSimulator : BackgroundService
{
    private readonly Random _random = new();
    private readonly ObservableCollection<CarPosition> _cars;

    public DemoDataSimulator(ObservableCollection<CarPosition> cars)
    {
        _cars = cars;


    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _cars.Add(new CarPosition { Id = 1, Model = "Tesla Model 3", Latitude = 48.2082, Longitude = 16.3738 });
        _cars.Add(new CarPosition { Id = 2, Model = "BMW i4", Latitude = 48.2102, Longitude = 16.3658 });
        _cars.Add(new CarPosition { Id = 3, Model = "Audi e-tron", Latitude = 48.2132, Longitude = 16.3718 });

        _cars.Remove(_cars.Last());
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);

            foreach (var car in _cars)
            {
                // Simulate car movement with small random changes
                car.Latitude += (_random.NextDouble() - 0.5) * 0.002;
                car.Longitude += (_random.NextDouble() - 0.5) * 0.002;
            }
        }
    }
}

