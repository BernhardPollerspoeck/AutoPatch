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
            await Task.Delay(TimeSpan.FromMilliseconds(25), stoppingToken);

            CleanupOldOrders();
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
                    var ordersWithout = driver.AssignedOrders.ToList();
                    ordersWithout.Remove(orphanedOrderId);
                    driver.AssignedOrders = ordersWithout;
                    Console.WriteLine($"🔧 Cleaned up orphaned assignment: {orphanedOrderId} from {driver.Name}");
                }
            }
        }
    }
}
