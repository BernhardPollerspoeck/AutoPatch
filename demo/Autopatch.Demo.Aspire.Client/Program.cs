using Autopatch.Client.Extensions;
using Autopatch.Client.Services;
using Autopatch.Demo.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configure AutoPatch with Aspire service discovery
// NOTE: For this demo to work with actual service discovery, you need to:
// 1. Configure service discovery providers (e.g., via configuration)
// 2. Or run the server with the same service name "autopatch"
builder.Services
    .AddAutoPatchWithServiceDiscovery("autopatch") // Discover the "autopatch" service
    .AddTrackedCollection<PizzaOrder>()
    .AddTrackedCollection<DeliveryDriver>();

var app = builder.Build();

await app.StartAsync();

var autoPatchClient = app.Services.GetRequiredService<IAutoPatchClient>();

Console.WriteLine("🍕 AutoPatch Aspire Demo Client");
Console.WriteLine("Using .NET Aspire service discovery to find AutoPatch server...");
Console.WriteLine("Note: This demo requires proper service discovery configuration or will fall back to manual endpoint setup.");

try
{
    // Connect using service discovery - no hardcoded URLs!
    // This will work if service discovery is properly configured
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
    Console.WriteLine("   • Service discovery enables automatic server discovery");
    Console.WriteLine("   • No hardcoded URLs needed when properly configured");
    Console.WriteLine("   • Real-time synchronization works seamlessly\n");

    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine("\nPossible solutions:");
    Console.WriteLine("   1. Configure service discovery properly");
    Console.WriteLine("   2. Run the AutoPatch server with matching service registration");
    Console.WriteLine("   3. Use traditional endpoint configuration for direct connection");
}

await app.StopAsync();