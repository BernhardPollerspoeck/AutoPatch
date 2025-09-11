using System.Collections.ObjectModel;
using Autopatch.Client.Extensions;
using Autopatch.Client.Services;
using Autopatch.Demo.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();

builder.Services
    .AddAutoPatch(cfg =>
    {
        cfg.Endpoint = "http://localhost:5249";
    })
    .AddTrackedCollection<PizzaOrder>()
    .AddTrackedCollection<DeliveryDriver>()
    ;

var app = builder.Build();

await app.StartAsync();

var autoPatchClient = app.Services.GetRequiredService<IAutoPatchClient>();

Console.WriteLine("🍕 AutoPatch Demo Console Client");
Console.WriteLine("Demonstrating subscription validation...");

// Try different authentication scenarios
await TrySubscription(autoPatchClient, "admin", "store1", "Admin accessing store1");
await TrySubscription(autoPatchClient, "store1_user", "store1", "Store1 user accessing store1");
await TrySubscription(autoPatchClient, "store1_user", "store2", "Store1 user accessing store2 (should fail)");
await TrySubscription(autoPatchClient, "invalid_user", "store1", "Invalid user accessing store1 (should fail)");
await TrySubscription(autoPatchClient, null, "store1", "No auth string provided (should fail - security fix)");

// Subscribe to delivery drivers (no validation)
var driversSuccess = await autoPatchClient.SubscribeToTypeAsync<DeliveryDriver>();
Console.WriteLine($"\nSubscribed to DeliveryDriver (no validation): {(driversSuccess ? "✅ Success" : "❌ Failed")}");

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

static async Task TrySubscription(IAutoPatchClient client, string? authString, string collectionKey, string description)
{
    Console.WriteLine($"\n{description}:");
    Console.WriteLine($"  Auth: '{authString ?? "null"}', Collection: '{collectionKey}'");
    var success = await client.SubscribeToTypeAsync<PizzaOrder>(collectionKey, authString);
    Console.WriteLine($"  Result: {(success ? "✅ Success" : "❌ Rejected")}");
    
    if (success)
    {
        // Show some data if subscription was successful
        try
        {
            var collection = client.GetTrackedCollection<PizzaOrder>(collectionKey);
            Console.WriteLine($"  Collection items: {collection.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error getting collection: {ex.Message}");
        }
    }
}
