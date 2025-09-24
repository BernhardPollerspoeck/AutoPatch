using Autopatch.Client.Extensions;
using Autopatch.Client.Services;
using Autopatch.Demo.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add service discovery configuration
builder.AddServiceDefaults();

// Configure AutoPatch with Aspire service discovery
builder.Services
    .AddAutoPatchWithServiceDiscovery("autopatch") // Discover the "autopatch" service
    .AddTrackedCollection<PizzaOrder>()
    .AddTrackedCollection<DeliveryDriver>();

var app = builder.Build();

await app.StartAsync();

var autoPatchClient = app.Services.GetRequiredService<IAutoPatchClient>();

Console.WriteLine("🍕 AutoPatch Aspire Demo Client");
Console.WriteLine("Using .NET Aspire service discovery to find AutoPatch server...");

try
{
    // Connect using service discovery - no hardcoded URLs!
    await autoPatchClient.ConnectAsync();
    Console.WriteLine("✅ Successfully connected to AutoPatch server via service discovery!");

    // Subscribe to pizza orders
    var ordersSuccess = await autoPatchClient.SubscribeToTypeAsync<PizzaOrder>("store1", "admin");
    Console.WriteLine($"📋 Subscribed to PizzaOrders: {(ordersSuccess ? "✅ Success" : "❌ Failed")}");

    // Subscribe to delivery drivers
    var driversSuccess = await autoPatchClient.SubscribeToTypeAsync<DeliveryDriver>();
    Console.WriteLine($"🚗 Subscribed to DeliveryDrivers: {(driversSuccess ? "✅ Success" : "❌ Failed")}");

    if (ordersSuccess)
    {
        var orders = autoPatchClient.GetTrackedCollection<PizzaOrder>("store1");
        Console.WriteLine($"📊 Current orders count: {orders.Count}");
        
        // Monitor changes
        orders.CollectionChanged += (_, e) => Console.WriteLine($"🔄 Orders collection changed: {e.Action}");
    }

    Console.WriteLine("\n🎯 This demo shows AutoPatch working with .NET Aspire service discovery!");
    Console.WriteLine("   • No hardcoded URLs in client configuration");
    Console.WriteLine("   • Server automatically discovered through Aspire");
    Console.WriteLine("   • Real-time synchronization still works perfectly\n");

    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine("\nMake sure the AutoPatch server is running via the Aspire AppHost!");
}

await app.StopAsync();