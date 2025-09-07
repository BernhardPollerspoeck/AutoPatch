using System.Collections.ObjectModel;
using Autopatch.Demo.Shared;

namespace Autopatch.Demo.Server;

/// <summary>
/// Completes deliveries when drivers reach customers and removes delivered orders.
/// Handles the final stage of the order lifecycle and cleanup.
/// Thread-safe operations using shared locks to prevent conflicts with other services.
/// </summary>
public class DeliveryCompletionService : BackgroundService
{
    private readonly ObservableCollection<PizzaOrder> _orders;
    private readonly ObservableCollection<DeliveryDriver> _drivers;
    private readonly Random _random = new();

    public DeliveryCompletionService(
        ObservableCollection<PizzaOrder> orders,
        ObservableCollection<DeliveryDriver> drivers)
    {
        _orders = orders;
        _drivers = drivers;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Check for deliveries every 200ms for faster demo and smooth response
            await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);

            CompleteDeliveries();
            CleanupOldOrders();
        }
    }

    private void CompleteDeliveries()
    {
        List<DeliveryDriver> driversSnapshot;
        
        // Create thread-safe snapshot of drivers using shared lock
        lock (CollectionLocks.DriversLock)
        {
            driversSnapshot = _drivers
                .Where(d => d.Status == DriverStatus.Delivering && d.X >= 540) // Match updated delivery position
                .ToList();
        }

        // Process each driver that has reached customers
        foreach (var driver in driversSnapshot)
        {
            CompleteDriverDeliveries(driver);
        }
    }

    private void CompleteDriverDeliveries(DeliveryDriver driver)
    {
        List<string> assignedOrderIds;
        
        // Get assigned orders for this driver using shared lock
        lock (CollectionLocks.DriversLock)
        {
            var currentDriver = _drivers.FirstOrDefault(d => d.DriverId == driver.DriverId);
            if (currentDriver == null || currentDriver.Status != DriverStatus.Delivering || currentDriver.X < 540) // Match updated position
            {
                return; // Driver state changed or not at delivery location
            }
            
            assignedOrderIds = currentDriver.AssignedOrders.ToList();
        }

        // Complete orders assigned to this driver using shared lock
        var ordersToRemove = new List<PizzaOrder>();
        
        lock (CollectionLocks.OrdersLock)
        {
            foreach (var orderId in assignedOrderIds)
            {
                var order = _orders.FirstOrDefault(o => o.OrderId == orderId);
                if (order != null)
                {
                    // Mark order as delivered
                    order.Status = OrderStatus.Delivered;
                    ordersToRemove.Add(order);
                    Console.WriteLine($"✅ Order {order.OrderId} delivered by {driver.Name} to {order.CustomerName}");
                }
            }
            
            // Remove delivered orders from collection
            foreach (var order in ordersToRemove)
            {
                _orders.Remove(order);
            }
        }
    }

    private void CleanupOldOrders()
    {
        List<PizzaOrder> ordersToCleanup;
        
        // Find old orders to cleanup using shared lock
        lock (CollectionLocks.OrdersLock)
        {
            ordersToCleanup = _orders
                .Where(o => DateTime.Now - o.OrderTime > TimeSpan.FromMinutes(5)) // Much faster cleanup for demo - 5 minutes instead of 1 hour
                .ToList();
        }

        // Remove old orders using shared lock
        if (ordersToCleanup.Any())
        {
            lock (CollectionLocks.OrdersLock)
            {
                foreach (var order in ordersToCleanup)
                {
                    if (_orders.Contains(order)) // Double-check order still exists
                    {
                        _orders.Remove(order);
                        Console.WriteLine($"🗑️ Cleaned up old order: {order.OrderId}");
                    }
                }
            }
        }

        // Clean up driver assignments
        CleanupDriverAssignments();
    }

    private void CleanupDriverAssignments()
    {
        HashSet<string> validOrderIds;
        
        // Get current valid order IDs using shared lock
        lock (CollectionLocks.OrdersLock)
        {
            validOrderIds = _orders.Select(o => o.OrderId).ToHashSet();
        }

        // Clean up driver assignments using shared lock
        lock (CollectionLocks.DriversLock)
        {
            foreach (var driver in _drivers)
            {
                var orphanedAssignments = driver.AssignedOrders
                    .Where(orderId => !validOrderIds.Contains(orderId))
                    .ToList();

                foreach (var orphanedOrderId in orphanedAssignments)
                {
                    driver.AssignedOrders.Remove(orphanedOrderId);
                    Console.WriteLine($"🔧 Cleaned up orphaned assignment: {orphanedOrderId} from {driver.Name}");
                }
            }
        }
    }
}
