using System.Collections.ObjectModel;
using Autopatch.Demo.Shared;

namespace Autopatch.Demo.Server;

/// <summary>
/// Generates new pizza orders at random intervals to simulate customer orders.
/// Creates orders with random customer names, items, and assigns unique order IDs.
/// Thread-safe operations using shared locks to prevent conflicts with other services.
/// </summary>
public class OrderGeneratorService : BackgroundService
{
    private readonly ObservableCollection<PizzaOrder> _orders;
    private readonly Random _random = new();
    private int _orderCounter = 1000;

    private readonly string[] _customerNames = [
        "Max Mustermann", "Anna Schmidt", "Peter Weber", "Lisa Müller", "Tom Klein",
        "Sarah Wagner", "Michael Bauer", "Julia Fischer", "Daniel Richter", "Nina Wolf",
        "Alex Neumann", "Laura Koch", "Felix Zimmermann", "Emma Schmitt", "Leon Hoffmann"
    ];

    private readonly string[] _pizzaTypes = [
        "Margherita", "Salami", "Hawaii", "Quattro Stagioni", "Diavola",
        "Funghi", "Tonno", "Vegetariana", "Quattro Formaggi", "Prosciutto"
    ];

    private readonly string[] _drinks = [
        "Cola", "Fanta", "Sprite", "Wasser", "Bier"
    ];

    public OrderGeneratorService(ObservableCollection<PizzaOrder> orders)
    {
        _orders = orders;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let system start up
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Faster interval for demo - between 5-8 seconds for new orders
            var nextOrderDelay = TimeSpan.FromSeconds(_random.Next(5, 9));
            await Task.Delay(nextOrderDelay, stoppingToken);

            // Check if we should add a new order using shared lock
            bool shouldAddOrder;
            lock (CollectionLocks.OrdersLock)
            {
                shouldAddOrder = _orders.Count < 10; // Prevent too many orders accumulating (max 10 at once)
            }

            if (shouldAddOrder)
            {
                GenerateNewOrder();
            }
        }
    }

    private void GenerateNewOrder()
    {
        // Generate new order
        var order = new PizzaOrder
        {
            OrderId = $"PPP-{Interlocked.Increment(ref _orderCounter)}", // Thread-safe counter increment
            CustomerName = _customerNames[_random.Next(_customerNames.Length)],
            Items = GenerateRandomItems(),
            Status = OrderStatus.Received,
            OrderTime = DateTime.Now,
            EstimatedDelivery = DateTime.Now.AddMinutes(_random.Next(5, 10)) // Faster estimated delivery for demo
        };

        lock (CollectionLocks.OrdersLock)
        {
            // Double-check we're still under the limit (another thread might have added orders)
            if (_orders.Count < 10)
            {
                _orders.Add(order);
                Console.WriteLine($"🆕 New Order: {order.OrderId} for {order.CustomerName} - Items: {string.Join(", ", order.Items)}");
            }
        }
    }

    private List<string> GenerateRandomItems()
    {
        var items = new List<string>();
        
        // Always at least one pizza
        items.Add(_pizzaTypes[_random.Next(_pizzaTypes.Length)]);
        
        // 30% chance for second pizza
        if (_random.NextDouble() < 0.3)
        {
            var secondPizza = _pizzaTypes[_random.Next(_pizzaTypes.Length)];
            if (secondPizza != items[0]) // Avoid duplicates
            {
                items.Add(secondPizza);
            }
        }
        
        // 60% chance for drink
        if (_random.NextDouble() < 0.6)
        {
            items.Add(_drinks[_random.Next(_drinks.Length)]);
        }
        
        return items;
    }
}
