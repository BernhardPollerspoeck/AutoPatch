using System.Collections.ObjectModel;
using Autopatch.Demo.Shared;

namespace Autopatch.Demo.Server;

/// <summary>
/// Manages delivery drivers: creates drivers, assigns orders, and moves them across the map.
/// Handles the complete driver lifecycle and position updates for live tracking.
/// Thread-safe operations using shared locks to prevent conflicts with other services.
/// </summary>
public class DriverSimulatorService(
    ObservableCollection<DeliveryDriver> drivers,
    ObservableCollection<PizzaOrder> orders) : BackgroundService
{
    private readonly Random _random = new();
    private DateTime _lastUpdate = DateTime.UtcNow;

    private readonly string[] _driverNames = [
        "Mario", "Luigi", "Tony", "Gino", "Enzo"
    ];

    // Driver lanes with 50px spacing to accommodate ~45px driver height
    private readonly double[] _driverLanes = [118, 174, 230, 286, 342];

    // Fixed positions for consistent movement
    private const double RestaurantX = 50.0;
    private const double CustomerX = 540.0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize with 3 drivers
        InitializeDrivers();

        while (!stoppingToken.IsCancellationRequested)
        {
            // Update every 50ms for smooth movement (20 FPS)
            await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);

            var currentTime = DateTime.UtcNow;
            var deltaTime = (currentTime - _lastUpdate).TotalSeconds;
            _lastUpdate = currentTime;

            AssignReadyOrdersToDrivers();
            UpdateDriverPositions(deltaTime);
        }
    }

    private void InitializeDrivers()
    {
        lock (CollectionLocks.DriversLock)
        {
            for (int i = 0; i < 5; i++)
            {
                var driver = new DeliveryDriver
                {
                    DriverId = $"DRV-{(char)('A' + i)}",
                    Name = _driverNames[i],
                    Status = DriverStatus.Available,
                    X = RestaurantX, // Fixed restaurant position
                    Y = _driverLanes[i], // Use lanes with proper spacing
                    DeliverySpeed = _random.Next(80, 150), // Pixels per second for smooth movement
                    AssignedOrders = []
                };

                drivers.Add(driver);
                Console.WriteLine($"🚗 Driver {driver.Name} ({driver.DriverId}) is now online");
            }
        }
    }

    private void AssignReadyOrdersToDrivers()
    {
        List<PizzaOrder> readyOrders;
        DeliveryDriver[] availableDrivers;

        // Create thread-safe snapshots of collections using shared locks
        lock (CollectionLocks.OrdersLock)
        {
            readyOrders = orders
                .Where(o => o.Status == OrderStatus.Ready && string.IsNullOrEmpty(o.AssignedDriverId))
                .OrderBy(o => o.OrderTime)
                .ToList();
        }

        lock (CollectionLocks.DriversLock)
        {
            availableDrivers = [.. drivers.Where(d => d.Status == DriverStatus.Available && d.AssignedOrders.Count < 2)];
        }
        Random.Shared.Shuffle(availableDrivers);

        // Process assignments outside of locks to minimize lock time
        foreach (var order in readyOrders)
        {
            var driver = availableDrivers.FirstOrDefault();
            if (driver == null) break;

            // Safely assign order to driver
            AssignOrderToDriver(order, driver);

            // Remove from available list for this iteration
            var cleanedList = availableDrivers.ToList();
            cleanedList.Remove(driver);
            availableDrivers = [.. cleanedList];
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

            driver.AssignedOrders = [.. driver.AssignedOrders, order.OrderId];
            driver.Status = DriverStatus.Assigned;
            driver.DeliverySpeed = _random.Next(80, 150); // Pixels per second for varied delivery speeds
        }

        Console.WriteLine($"📦 {driver.Name} assigned to {order.OrderId} (Speed: {driver.DeliverySpeed} px/s)");
    }

    private void UpdateDriverPositions(double deltaTime)
    {
        List<DeliveryDriver> driversSnapshot;

        // Create thread-safe snapshot using shared lock
        lock (CollectionLocks.DriversLock)
        {
            driversSnapshot = [.. drivers];
        }

        // Process movements outside of lock
        foreach (var driver in driversSnapshot)
        {
            UpdateSingleDriverPosition(driver, deltaTime);
        }
    }

    private void UpdateSingleDriverPosition(DeliveryDriver driver, double deltaTime)
    {
        lock (CollectionLocks.DriversLock)
        {
            // Verify driver is still in collection and get current status
            var currentDriver = drivers.FirstOrDefault(d => d.DriverId == driver.DriverId);
            if (currentDriver == null) return; // Driver was removed

            switch (currentDriver.Status)
            {
                case DriverStatus.Assigned:
                    // Start delivery faster - increased chance for demo
                    if (_random.NextDouble() < 0.3) // 30% chance per update (was 60% per 200ms)
                    {
                        currentDriver.Status = DriverStatus.Delivering;
                    }
                    break;

                case DriverStatus.Delivering:
                    // Move towards customer using time-based movement
                    var deliveryDistance = currentDriver.DeliverySpeed * deltaTime;
                    var newX = currentDriver.X + deliveryDistance;

                    // Check if reached or passed customer position
                    if (newX >= CustomerX)
                    {
                        currentDriver.X = CustomerX; // Fixed customer position
                        currentDriver.Status = DriverStatus.Returning;

                        lock (CollectionLocks.OrdersLock)
                        {
                            foreach (var orderId in currentDriver.AssignedOrders)
                            {
                                var order = orders.FirstOrDefault(o => o.OrderId == orderId);
                                if (order != null)
                                {
                                    // Mark order as delivered
                                    order.Status = OrderStatus.Delivered;
                                }
                            }
                        }
                    }
                    else
                    {
                        currentDriver.X = newX;
                    }
                    break;

                case DriverStatus.Returning:
                    // Move back to restaurant using time-based movement
                    var returnDistance = currentDriver.DeliverySpeed * deltaTime;
                    var newReturnX = currentDriver.X - returnDistance;

                    // Check if reached or passed restaurant position
                    if (newReturnX <= RestaurantX)
                    {
                        currentDriver.X = RestaurantX; // Fixed restaurant position
                        currentDriver.Status = DriverStatus.Available;
                        currentDriver.AssignedOrders = [];
                        Console.WriteLine($"🏪 {currentDriver.Name} returned to restaurant");
                    }
                    else
                    {
                        currentDriver.X = newReturnX;
                    }
                    break;
            }
        }
    }

}
