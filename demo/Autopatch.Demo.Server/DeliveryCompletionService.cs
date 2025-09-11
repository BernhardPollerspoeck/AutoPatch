using System.Collections.ObjectModel;
using Autopatch.Demo.Shared;
using Autopatch.Server.Services;

namespace Autopatch.Demo.Server;

/// <summary>
/// Completes deliveries when drivers reach customers and removes delivered orders.
/// Handles the final stage of the order lifecycle and cleanup.
/// Thread-safe operations using shared locks to prevent conflicts with other services.
/// </summary>
public class DeliveryCompletionService(ITrackedCollectionManager collectionManager) : BackgroundService
{
    private readonly ObservableCollection<PizzaOrder> _orders = collectionManager.GetOrCreateCollection<PizzaOrder>();
    private readonly ObservableCollection<DeliveryDriver> _drivers = collectionManager.GetOrCreateCollection<DeliveryDriver>();

    /// <summary>
    /// Executes the background service loop for delivery completion and cleanup operations.
    /// Continuously monitors for old orders that need to be removed from the system.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Runs on a 25ms interval for responsive demo behavior. 
    /// Uses thread-safe operations with shared locks to coordinate with other services.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Check for deliveries every 200ms for faster demo and smooth response
            await Task.Delay(TimeSpan.FromMilliseconds(25), stoppingToken);

            CleanupOldOrders();
        }
    }

    /// <summary>
    /// Removes orders that are older than 5 minutes from the system and cleans up associated driver assignments.
    /// Performs two-stage cleanup: first removes old orders, then cleans orphaned driver assignments.
    /// </summary>
    /// <remarks>
    /// Uses shared locks to ensure thread safety when accessing collections.
    /// Orders older than 5 minutes are considered completed and removed to prevent memory growth.
    /// Automatically triggers driver assignment cleanup to maintain data consistency.
    /// </remarks>
    private void CleanupOldOrders()
    {
        List<PizzaOrder> ordersToCleanup;

        // Find old orders to cleanup using shared lock
        lock (CollectionLocks.OrdersLock)
        {
            ordersToCleanup = [.. _orders.Where(o => DateTime.Now - o.OrderTime > TimeSpan.FromMinutes(5))];
        }

        // Remove old orders using shared lock
        if (ordersToCleanup.Count != 0)
        {
            lock (CollectionLocks.OrdersLock)
            {
                foreach (var order in ordersToCleanup)
                {
                    _orders.Remove(order);
                }
            }
        }

        // Clean up driver assignments
        CleanupDriverAssignments();
    }

    /// <summary>
    /// Removes orphaned order assignments from delivery drivers when the referenced orders no longer exist.
    /// Ensures data consistency by cleaning up driver assignments that point to removed orders.
    /// </summary>
    /// <remarks>
    /// Uses shared locks to safely access both orders and drivers collections.
    /// Logs cleanup actions to the console for debugging and monitoring purposes.
    /// Maintains driver assignment lists by removing references to non-existent orders.
    /// </remarks>
    private void CleanupDriverAssignments()
    {
        HashSet<string> validOrderIds;

        // Get current valid order IDs using shared lock
        lock (CollectionLocks.OrdersLock)
        {
            validOrderIds = [.. _orders.Select(o => o.OrderId)];
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
