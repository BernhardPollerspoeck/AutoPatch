using System.Collections.ObjectModel;
using Autopatch.Demo.Shared;
using Autopatch.Server.Services;

namespace Autopatch.Demo.Server;

/// <summary>
/// Processes orders through the kitchen pipeline: Received → Preparing → Baking → Ready.
/// Simulates kitchen workflow with realistic timing for each stage.
/// Thread-safe operations using shared locks to prevent conflicts with other services.
/// </summary>
public class KitchenProcessorService(ITrackedCollectionManager collectionManager) : BackgroundService
{
    private readonly ObservableCollection<PizzaOrder> _orders = collectionManager.GetOrCreateCollection<PizzaOrder>();
    private readonly Random _random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Process kitchen every 3 seconds for faster demo
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            ProcessKitchenOrders();
        }
    }

    private void ProcessKitchenOrders()
    {
        List<PizzaOrder> ordersToProcess;
        
        // Create thread-safe snapshot of orders to process using shared lock
        lock (CollectionLocks.OrdersLock)
        {
            ordersToProcess = [.. _orders
                .Where(o => o.Status is not OrderStatus.OutForDelivery and
                           not OrderStatus.Delivered)
                .OrderBy(o => o.OrderTime)];
        }

        // Process each order outside of lock to minimize lock time
        foreach (var order in ordersToProcess)
        {
            ProcessSingleOrder(order);
        }
    }

    private void ProcessSingleOrder(PizzaOrder order)
    {
        lock (CollectionLocks.OrdersLock)
        {
            // Verify order is still in collection and get current state
            var currentOrder = _orders.FirstOrDefault(o => o.OrderId == order.OrderId);
            if (currentOrder == null) return; // Order was removed

            var shouldAdvance = ShouldAdvanceOrder(currentOrder);
            if (shouldAdvance)
            {
                var newStatus = GetNextStatus(currentOrder.Status);
                var oldStatus = currentOrder.Status;
                currentOrder.Status = newStatus;

                // Update ETA based on new status
                UpdateEstimatedDelivery(currentOrder);
                
                Console.WriteLine($"🍳 Kitchen: {currentOrder.OrderId} {oldStatus} → {newStatus}");
            }
        }
    }

    private bool ShouldAdvanceOrder(PizzaOrder order)
    {
        // Random chance based on how long order has been in current status - faster for demo
        var timeInCurrentStatus = DateTime.Now - order.OrderTime;
        
        return order.Status switch
        {
            OrderStatus.Received => _random.NextDouble() < 0.7, // 70% chance to start preparing
            OrderStatus.Preparing => timeInCurrentStatus.TotalSeconds > 10 && _random.NextDouble() < 0.8, // 80% chance after 10 seconds
            OrderStatus.Baking => timeInCurrentStatus.TotalSeconds > 20 && _random.NextDouble() < 0.9, // 90% chance after 20 seconds
            OrderStatus.Ready => false, // Will be handled by delivery assignment
            _ => false
        };
    }

    private static OrderStatus GetNextStatus(OrderStatus currentStatus)
    {
        return currentStatus switch
        {
            OrderStatus.Received => OrderStatus.Preparing,
            OrderStatus.Preparing => OrderStatus.Baking,
            OrderStatus.Baking => OrderStatus.Ready,
            _ => currentStatus
        };
    }

    private static void UpdateEstimatedDelivery(PizzaOrder order)
    {
        var additionalMinutes = order.Status switch
        {
            OrderStatus.Preparing => 1, // 1 more minute
            OrderStatus.Baking => 1,    // 1 more minute  
            OrderStatus.Ready => 2,      // 2 more minutes
            OrderStatus.OutForDelivery => 0, // Driver will handle timing
            _ => 5
        };

        order.EstimatedDelivery = DateTime.Now.AddMinutes(additionalMinutes);
    }
}
