using Autopatch.Client.Extensions;
using Autopatch.Client.Services;
using Autopatch.Demo.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

const string Endpoint = "http://localhost:5249";

var builder = Host.CreateApplicationBuilder();

builder.Services
    .AddAutoPatch(cfg =>
    {
        cfg.Endpoint = Endpoint;
    })
    .AddTrackedCollection<PizzaOrder>()
    .AddTrackedCollection<DeliveryDriver>()
    ;

var app = builder.Build();

await app.StartAsync();

var autoPatchClient = app.Services.GetRequiredService<IAutoPatchClient>();
autoPatchClient.OnConnectionChanged += (_, connected) => Console.WriteLine(connected ? "🔌 Connected" : "⚠️ Disconnected - reconnecting...");
autoPatchClient.OnError += (_, e) => Console.WriteLine($"⚠️ {e.SubscriptionKey ?? "connection"}: {e.Exception.Message} - resynchronizing");

Console.WriteLine("🍕 AutoPatch Demo Console Client");
while (true)
{
    try
    {
        await autoPatchClient.ConnectAsync();
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Server not reachable ({ex.Message}), retrying in 2 seconds...");
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}

Console.WriteLine("Demonstrating subscription validation...");

// Try different authentication scenarios
await TrySubscription(autoPatchClient, "admin", "store1", "Admin accessing store1");
await TrySubscription(autoPatchClient, "store2_user", "store2", "Store2 user accessing store2");
await TrySubscription(autoPatchClient, "store1_user", "store2", "Store1 user accessing store2 (should fail)");
await TrySubscription(autoPatchClient, "invalid_user", "store1", "Invalid user accessing store1 (should fail)");
await TrySubscription(autoPatchClient, null, "store1", "No auth string provided (should fail)");

// Subscribe to delivery drivers (no validation)
var driversSuccess = await autoPatchClient.SubscribeToTypeAsync<DeliveryDriver>();
Console.WriteLine($"\nSubscribed to DeliveryDriver (no validation): {(driversSuccess ? "✅ Success" : "❌ Failed")}");

await TryValidatorBypass();

Console.WriteLine("\nLive counts (press any key to exit). Use 'Seed store1/store2' on http://localhost:5249/test to see");
Console.WriteLine("collections appear that did not exist when this client subscribed. Restart the server to see the reconnect.\n");
while (Console.IsInputRedirected || !Console.KeyAvailable)
{
    Console.WriteLine(
        $"{DateTime.Now:T}  store1: {Count<PizzaOrder>("store1")}  store2: {Count<PizzaOrder>("store2")}  drivers: {Count<DeliveryDriver>(null)}");
    await Task.Delay(TimeSpan.FromSeconds(2));
}

await autoPatchClient.DisposeAsync();

string Count<T>(string? key) where T : class
{
    try
    {
        return autoPatchClient.GetTrackedCollection<T>(key).Count.ToString();
    }
    catch (InvalidOperationException)
    {
        return "-";
    }
}

static async Task TrySubscription(IAutoPatchClient client, string? authString, string collectionKey, string description)
{
    Console.WriteLine($"\n{description}:");
    Console.WriteLine($"  Auth: '{authString ?? "null"}', Collection: '{collectionKey}'");
    var success = await client.SubscribeToTypeAsync<PizzaOrder>(collectionKey, authString);
    Console.WriteLine($"  Result: {(success ? "✅ Success" : "❌ Rejected")}");
}

// A raw SignalR connection, as an attacker would use it: type names that are not registered must be rejected,
// including a type name that contains the collection key.
static async Task TryValidatorBypass()
{
    await using var connection = new HubConnectionBuilder().WithUrl($"{Endpoint}/autopatch").Build();
    await connection.StartAsync();

    Console.WriteLine("\nValidator bypass attempts over a raw connection (both must be rejected):");
    foreach (var typeName in new[] { "PizzaOrder/store1", "DoesNotExist" })
    {
        var accepted = await connection.InvokeAsync<bool>("SubscribeToType", typeName, null, null);
        Console.WriteLine($"  SubscribeToType(\"{typeName}\"): {(accepted ? "❌ ACCEPTED - security problem" : "✅ Rejected")}");
    }
}
