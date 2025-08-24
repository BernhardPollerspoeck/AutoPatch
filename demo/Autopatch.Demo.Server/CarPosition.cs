using Autopatch.Demo.Shared;
using Autopatch.Server;

namespace Autopatch.Demo.Server;



public class DemoDataSimulator : BackgroundService
{
    private readonly IAutoPatchService _autoPatchService;
    private readonly Random _random = new();
    private readonly List<CarPosition> _cars = [];

    public DemoDataSimulator(IAutoPatchService autoPatchService)
    {
        _autoPatchService = autoPatchService;

        _cars.Add(new CarPosition { Id = 1, Model = "Tesla Model 3", Latitude = 48.2082, Longitude = 16.3738 });
        _cars.Add(new CarPosition { Id = 2, Model = "BMW i4", Latitude = 48.2102, Longitude = 16.3658 });
        _cars.Add(new CarPosition { Id = 3, Model = "Audi e-tron", Latitude = 48.2132, Longitude = 16.3718 });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            foreach (var car in _cars)
            {
                // Simulate car movement with small random changes
                car.Latitude += (_random.NextDouble() - 0.5) * 0.002;
                car.Longitude += (_random.NextDouble() - 0.5) * 0.002;
                await _autoPatchService.HandleBulkChangeAsync(
                    [
                        new ObjectChange<CarPosition>(car, nameof(CarPosition.Latitude), car.Latitude),
                        new ObjectChange<CarPosition>(car, nameof(CarPosition.Longitude), car.Longitude)
                    ]);
            }
        }
    }
}

