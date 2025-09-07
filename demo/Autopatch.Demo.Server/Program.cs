using Autopatch.Demo.Server;
using Autopatch.Demo.Shared;
using Autopatch.Server.Extensions;
using Autopatch.Server.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure AutoPatch with optimized settings for pizza delivery demo
builder.Services
    .AddAutoPatch(cfg =>
    {
        cfg.DefaultThrottleInterval = TimeSpan.FromMilliseconds(500); // Balanced for demo
        cfg.MaxBatchSize = 50;
    })
    .AddTrackedCollection<PizzaOrder>(cfg =>
    {
        cfg.ClientChangePolicy = ClientChangePolicy.Reject; // Read-only for demo
    })
    .AddTrackedCollection<DeliveryDriver>(cfg =>
    {
        cfg.ClientChangePolicy = ClientChangePolicy.Reject; // Read-only for demo
    });

builder.Services.AddSignalR();

// Register simulation services
builder.Services.AddHostedService<OrderGeneratorService>();
builder.Services.AddHostedService<KitchenProcessorService>();
builder.Services.AddHostedService<DriverSimulatorService>();
builder.Services.AddHostedService<DeliveryCompletionService>();

var app = builder.Build();

app.UseAutoPatch();

Console.WriteLine("🍕 Poller's Pizza Palace Demo Server starting...");
Console.WriteLine("📊 AutoPatch Framework Demo - Live Order & Driver Tracking");

app.Run();
