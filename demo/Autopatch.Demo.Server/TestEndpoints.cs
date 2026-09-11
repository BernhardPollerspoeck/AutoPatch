using Autopatch.Demo.Shared;
using Autopatch.Server.Services;

namespace Autopatch.Demo.Server;

/// <summary>
/// Endpoints and a small page (<c>/test</c>) to trigger the collection changes that are hard to observe in the simulation:
/// insert, move, replace, clear, bursts of property changes and keyed collections that are created after clients subscribed.
/// </summary>
public static class TestEndpoints
{
    private static int _testOrderCounter;

    public static void MapTestEndpoints(this WebApplication app)
    {
        app.MapGet("/test", () => Results.Content(Page, "text/html"));

        var orders = app.MapGroup("/test/orders");
        orders.MapPost("/insert", (ITrackedCollectionManager manager) => ChangeOrders(manager, o =>
        {
            o.Insert(0, CreateOrder("Inserted at top"));
            return "Inserted a new order at index 0";
        }));
        orders.MapPost("/move", (ITrackedCollectionManager manager) => ChangeOrders(manager, o =>
        {
            if (o.Count < 2)
            {
                return "Need at least two orders";
            }
            o.Move(o.Count - 1, 0);
            return "Moved the last order to the top";
        }));
        orders.MapPost("/replace", (ITrackedCollectionManager manager) => ChangeOrders(manager, o =>
        {
            if (o.Count == 0)
            {
                return "No order to replace";
            }
            o[0] = CreateOrder("Replaced first order");
            return "Replaced the first order via indexer";
        }));
        orders.MapPost("/clear", (ITrackedCollectionManager manager) => ChangeOrders(manager, o =>
        {
            o.Clear();
            return "Cleared all orders";
        }));
        orders.MapPost("/burst", (ITrackedCollectionManager manager) => ChangeOrders(manager, o =>
        {
            if (o.Count == 0)
            {
                return "No order for the burst";
            }
            var order = o[0];
            for (var i = 1; i <= 200; i++)
            {
                order.CustomerName = $"Burst {i}";
            }
            return $"Changed the name of {order.OrderId} 200 times - clients must show 'Burst 200'";
        }));

        app.MapPost("/test/stores/seed", (ITrackedCollectionManager manager) =>
        {
            foreach (var store in new[] { "store1", "store2" })
            {
                var collection = manager.GetOrCreateCollection<PizzaOrder>(store);
                collection.Add(CreateOrder($"{store} customer"));
            }
            return Results.Text("Added an order to the collections 'store1' and 'store2' (created if needed)");
        });
    }

    private static IResult ChangeOrders(ITrackedCollectionManager manager, Func<System.Collections.ObjectModel.ObservableCollection<PizzaOrder>, string> change)
    {
        var orders = manager.GetOrCreateCollection<PizzaOrder>();
        lock (CollectionLocks.OrdersLock)
        {
            return Results.Text(change(orders));
        }
    }

    private static PizzaOrder CreateOrder(string customerName) => new()
    {
        OrderId = $"TEST-{Interlocked.Increment(ref _testOrderCounter)}",
        CustomerName = customerName,
        Items = ["Margherita"],
        Status = OrderStatus.Received,
        OrderTime = DateTime.Now,
        EstimatedDelivery = DateTime.Now.AddMinutes(10),
    };

    private const string Page = """
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8">
          <title>AutoPatch test page</title>
          <style>
            body { font-family: system-ui, sans-serif; margin: 2rem; max-width: 50rem; }
            button { margin: .25rem .5rem .25rem 0; padding: .5rem 1rem; }
            #log { margin-top: 1rem; white-space: pre-wrap; background: #f4f4f4; padding: 1rem; min-height: 4rem; }
            li { margin: .25rem 0; }
          </style>
        </head>
        <body>
          <h1>🍕 AutoPatch test page</h1>
          <p>Every button changes the <code>PizzaOrder</code> collection on the server. All clients (WPF, web, console) must show exactly the server state afterwards.</p>
          <ul>
            <li><button data-url="/test/orders/insert">Insert at top</button> new order appears at the top, not at the end</li>
            <li><button data-url="/test/orders/move">Move last to top</button> order moves, nothing is duplicated</li>
            <li><button data-url="/test/orders/replace">Replace first</button> first order is replaced in place</li>
            <li><button data-url="/test/orders/clear">Clear</button> all clients become empty, new orders keep arriving afterwards</li>
            <li><button data-url="/test/orders/burst">200 changes</button> first order ends with name "Burst 200"</li>
            <li><button data-url="/test/stores/seed">Seed store1/store2</button> keyed collections, also for clients that subscribed before they existed</li>
          </ul>
          <div id="log"></div>
          <script>
            document.querySelectorAll('button').forEach(button => button.addEventListener('click', async () => {
              const response = await fetch(button.dataset.url, { method: 'POST' });
              document.getElementById('log').textContent = new Date().toLocaleTimeString() + '  ' + await response.text();
            }));
          </script>
        </body>
        </html>
        """;
}
