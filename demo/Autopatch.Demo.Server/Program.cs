using Autopatch.Demo.Server;
using Autopatch.Demo.Shared;
using Autopatch.Server.Extensions;
using Autopatch.Server.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults for service discovery
builder.AddServiceDefaults();

// Configure AutoPatch with Aspire support
builder.Services
    .AddAutoPatchWithAspire(cfg =>
    {
        cfg.DefaultThrottleInterval = TimeSpan.FromMilliseconds(500); // Balanced for demo
        cfg.MaxBatchSize = 50;
    })
    .AddTrackedCollection<PizzaOrder, PizzaOrderValidator>(cfg =>
    {
        cfg.ClientChangePolicy = ClientChangePolicy.Reject; // Read-only for demo
        cfg.ThrottleInterval = TimeSpan.FromMilliseconds(300);
        cfg.ExcludedProperties = [nameof(PizzaOrder.EstimatedDelivery)];
    })
    .AddTrackedCollection<DeliveryDriver>(cfg =>
    {
        cfg.ClientChangePolicy = ClientChangePolicy.Reject; // Read-only for demo
        cfg.ThrottleInterval = TimeSpan.FromMilliseconds(400);
    });

builder.Services.AddSignalR();

// Register simulation services
builder.Services.AddHostedService<OrderGeneratorService>();
builder.Services.AddHostedService<KitchenProcessorService>();
builder.Services.AddHostedService<DriverSimulatorService>();
builder.Services.AddHostedService<DeliveryCompletionService>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseAutoPatch();

Console.WriteLine("🍕 Poller's Pizza Palace Demo Server starting...");
Console.WriteLine("📊 AutoPatch Framework Demo - Live Order & Driver Tracking");
Console.WriteLine("🔍 Now discoverable via .NET Aspire service discovery!");

app.Run();
