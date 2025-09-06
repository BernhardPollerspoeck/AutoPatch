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
        var carModels = new[]
        {
            "Tesla Model 3", "BMW i4", "Audi e-tron", "Mercedes EQS", "Volkswagen ID.4",
            "Nissan Leaf", "Hyundai Ioniq 5", "Ford Mustang Mach-E", "Polestar 2", "Lucid Air"
        };

        var basePositions = new[]
        {
            (48.2082, 16.3738), (48.2102, 16.3658), (48.2132, 16.3718),
            (48.2152, 16.3798), (48.2072, 16.3678), (48.2192, 16.3598),
            (48.2042, 16.3758), (48.2162, 16.3638), (48.2122, 16.3778)
        };

        var carLifetimes = new Dictionary<int, DateTime>();
        var nextCarId = 1;
        var minCarLifetimeSeconds = 3;
        var maxCars = 10;
        var minCars = 2;

        // Add initial cars
        for (int i = 0; i < minCars; i++)
        {
            var position = basePositions[_random.Next(basePositions.Length)];
            var car = new CarPosition 
            { 
                Id = nextCarId++, 
                Model = carModels[_random.Next(carModels.Length)], 
                Latitude = position.Item1 + (_random.NextDouble() - 0.5) * 0.01, 
                Longitude = position.Item2 + (_random.NextDouble() - 0.5) * 0.01 
            };
            _cars.Add(car);
            carLifetimes[car.Id] = DateTime.UtcNow;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);

            // Move existing cars
            foreach (var car in _cars)
            {
                car.Latitude += (_random.NextDouble() - 0.5) * 0.001;
                car.Longitude += (_random.NextDouble() - 0.5) * 0.001;
            }

            // Randomly add a car (20% chance per iteration)
            if (_cars.Count < maxCars && _random.NextDouble() < 0.2)
            {
                var position = basePositions[_random.Next(basePositions.Length)];
                var car = new CarPosition 
                { 
                    Id = nextCarId++, 
                    Model = carModels[_random.Next(carModels.Length)], 
                    Latitude = position.Item1 + (_random.NextDouble() - 0.5) * 0.01, 
                    Longitude = position.Item2 + (_random.NextDouble() - 0.5) * 0.01 
                };
                _cars.Add(car);
                carLifetimes[car.Id] = DateTime.UtcNow;
            }

            // Randomly remove a car (15% chance per iteration)
            if (_cars.Count > minCars && _random.NextDouble() < 0.15)
            {
                var eligibleCars = _cars.Where(c => 
                    carLifetimes.ContainsKey(c.Id) && 
                    DateTime.UtcNow.Subtract(carLifetimes[c.Id]).TotalSeconds >= minCarLifetimeSeconds
                ).ToList();

                if (eligibleCars.Any())
                {
                    var carToRemove = eligibleCars[_random.Next(eligibleCars.Count)];
                    _cars.Remove(carToRemove);
                    carLifetimes.Remove(carToRemove.Id);
                }
            }
        }
    }
}

