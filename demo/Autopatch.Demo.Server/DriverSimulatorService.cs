using System.Collections.ObjectModel;
using Autopatch.Demo.Shared;

namespace Autopatch.Demo.Server;

/// <summary>
/// Manages delivery drivers: creates drivers, assigns orders, and moves them across the map.
/// Handles the complete driver lifecycle and position updates for live tracking.
/// Thread-safe operations using shared locks to prevent conflicts with other services.
/// </summary>
public class DriverSimulatorService : BackgroundService
{
    private readonly ObservableCollection<DeliveryDriver> _drivers;
    private readonly ObservableCollection<PizzaOrder> _orders;
    private readonly Random _random = new();

    private readonly string[] _driverNames = [
        "Mario", "Luigi", "Tony", "Gino", "Enzo", "Marco", "Luca", "Pietro"
    ];

    // Driver lanes with 50px spacing to accommodate ~45px driver height
    private readonly double[] _driverLanes = [80, 130, 180, 230, 280];
    private int _driverCounter = 1;

    public DriverSimulatorService(
        ObservableCollection<DeliveryDriver> drivers,
        ObservableCollection<PizzaOrder> orders)
    {
        _drivers = drivers;
        _orders = orders;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize with 3 drivers
        InitializeDrivers();

        while (!stoppingToken.IsCancellationRequested)
        {
            // Update every 200ms for very smooth movement
            await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);

            AssignReadyOrdersToDrivers();
            UpdateDriverPositions();
            ManageDriverAvailability();
        }
    }

    private void InitializeDrivers()
    {
        lock (CollectionLocks.DriversLock)
        {
            for (int i = 0; i < 3; i++)
            {
                var driver = new DeliveryDriver
                {
                    DriverId = $"DRV-{(char)('A' + i)}",
                    Name = _driverNames[i],
                    Status = DriverStatus.Available,
                    X = 50, // Restaurant position
                    Y = _driverLanes[i], // Use lanes with proper spacing
                    DeliverySpeed = _random.Next(12, 25), // 2-3x faster speeds for demo
                    AssignedOrders = []
                };
                
                _drivers.Add(driver);
                Console.WriteLine($"🚗 Driver {driver.Name} ({driver.DriverId}) is now online");
            }
        }
    }

    private void AssignReadyOrdersToDrivers()
    {
        List<PizzaOrder> readyOrders;
        List<DeliveryDriver> availableDrivers;

        // Create thread-safe snapshots of collections using shared locks
        lock (CollectionLocks.OrdersLock)
        {
            readyOrders = _orders
                .Where(o => o.Status == OrderStatus.Ready && string.IsNullOrEmpty(o.AssignedDriverId))
                .OrderBy(o => o.OrderTime)
                .ToList();
        }

        lock (CollectionLocks.DriversLock)
        {
            availableDrivers = _drivers
                .Where(d => d.Status == DriverStatus.Available && d.AssignedOrders.Count < 2)
                .ToList();
        }

        // Process assignments outside of locks to minimize lock time
        foreach (var order in readyOrders)
        {
            var driver = availableDrivers.FirstOrDefault();
            if (driver == null) break;

            // Safely assign order to driver
            AssignOrderToDriver(order, driver);
            
            // Remove from available list for this iteration
            availableDrivers.Remove(driver);
        }
    }

    private void AssignOrderToDriver(PizzaOrder order, DeliveryDriver driver)
    {
        // Use shared locks for assignment operations
        lock (CollectionLocks.OrdersLock)
        {
            // Double-check assignment is still valid (order might have been assigned by another service)
            if (order.Status != OrderStatus.Ready || !string.IsNullOrEmpty(order.AssignedDriverId))
            {
                return; // Order already assigned or status changed
            }
            
            order.AssignedDriverId = driver.DriverId;
            order.AssignedDriverName = driver.Name; // Store driver name for display
            order.Status = OrderStatus.OutForDelivery;
        }

        lock (CollectionLocks.DriversLock)
        {
            if (driver.Status != DriverStatus.Available)
            {
                return; // Driver status changed
            }
            
            driver.AssignedOrders.Add(order.OrderId);
            driver.Status = DriverStatus.Assigned;
            driver.DeliverySpeed = _random.Next(12, 25); // 2-3x faster speeds for demo
        }
        
        Console.WriteLine($"📦 {driver.Name} assigned to {order.OrderId} (Speed: {driver.DeliverySpeed})");
    }

    private void UpdateDriverPositions()
    {
        List<DeliveryDriver> driversSnapshot;
        
        // Create thread-safe snapshot using shared lock
        lock (CollectionLocks.DriversLock)
        {
            driversSnapshot = _drivers.ToList();
        }

        // Process movements outside of lock
        foreach (var driver in driversSnapshot)
        {
            UpdateSingleDriverPosition(driver);
        }
    }

    private void UpdateSingleDriverPosition(DeliveryDriver driver)
    {
        lock (CollectionLocks.DriversLock)
        {
            // Verify driver is still in collection and get current status
            var currentDriver = _drivers.FirstOrDefault(d => d.DriverId == driver.DriverId);
            if (currentDriver == null) return; // Driver was removed

            switch (currentDriver.Status)
            {
                case DriverStatus.Assigned:
                    // Start delivery faster - increased chance for demo
                    if (_random.NextDouble() < 0.6)
                    {
                        currentDriver.Status = DriverStatus.Delivering;
                        Console.WriteLine($"🚗 {currentDriver.Name} started delivery");
                    }
                    break;

                case DriverStatus.Delivering:
                    // Move towards customer (left to right)
                    currentDriver.X += currentDriver.DeliverySpeed;
                    
                    // Check if reached customer (X = 540 to match layout)
                    if (currentDriver.X >= 540)
                    {
                        currentDriver.X = 540;
                        currentDriver.Status = DriverStatus.Returning;
                        Console.WriteLine($"📍 {currentDriver.Name} reached customer, starting return");
                    }
                    break;

                case DriverStatus.Returning:
                    // Move back to restaurant (right to left)
                    currentDriver.X -= currentDriver.DeliverySpeed;
                    
                    // Check if reached restaurant (X = 50)
                    if (currentDriver.X <= 50)
                    {
                        currentDriver.X = 50;
                        currentDriver.Status = DriverStatus.Available;
                        currentDriver.AssignedOrders.Clear();
                        Console.WriteLine($"🏪 {currentDriver.Name} returned to restaurant");
                    }
                    break;
            }
        }
    }

    private void ManageDriverAvailability()
    {
        lock (CollectionLocks.DriversLock)
        {
            // Randomly add a new driver (5% chance if we have less than 5)
            if (_drivers.Count < 5 && _random.NextDouble() < 0.05)
            {
                var laneIndex = _drivers.Count % _driverLanes.Length;
                var driver = new DeliveryDriver
                {
                    DriverId = $"DRV-{_driverCounter++}",
                    Name = _driverNames[_random.Next(_driverNames.Length)],
                    Status = DriverStatus.Available,
                    X = 50,
                    Y = _driverLanes[laneIndex], // Use lane system for proper spacing
                    DeliverySpeed = _random.Next(12, 25), // 2-3x faster speeds for demo
                    AssignedOrders = []
                };
                
                _drivers.Add(driver);
                Console.WriteLine($"➕ New driver {driver.Name} came online");
            }

            // Rarely remove a driver (2% chance if we have more than 2)
            if (_drivers.Count > 2 && _random.NextDouble() < 0.02)
            {
                var availableDriver = _drivers.FirstOrDefault(d => d.Status == DriverStatus.Available);
                if (availableDriver != null)
                {
                    _drivers.Remove(availableDriver);
                    Console.WriteLine($"➖ Driver {availableDriver.Name} went offline");
                }
            }
        }
    }
}
