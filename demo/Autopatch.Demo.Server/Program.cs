using Autopatch.Demo.Server;
using Autopatch.Demo.Shared;
using Autopatch.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure AutoPatch with optimized settings for pizza delivery demo
builder.Services
    .AddAutoPatch(cfg =>
    {
        cfg.DefaultThrottleInterval = TimeSpan.FromMilliseconds(500); // Balanced for demo
        cfg.MaxBatchSize = 50;
    })
    .AddTrackedCollection<PizzaOrder, PizzaOrderValidator>(cfg =>
    {
        cfg.ThrottleInterval = TimeSpan.FromMilliseconds(300);
    })
    .AddTrackedCollection<DeliveryDriver>(cfg =>
    {
        cfg.ThrottleInterval = TimeSpan.FromMilliseconds(400);
    });

builder.Services.AddSignalR();

// Add CORS support for React demo
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactDemo", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173") // React/Vite dev servers
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register simulation services
builder.Services.AddHostedService<OrderGeneratorService>();
builder.Services.AddHostedService<KitchenProcessorService>();
builder.Services.AddHostedService<DriverSimulatorService>();
builder.Services.AddHostedService<DeliveryCompletionService>();

var app = builder.Build();

// Use CORS
app.UseCors("AllowReactDemo");

app.UseAutoPatch();
app.MapTestEndpoints();

Console.WriteLine("🍕 Poller's Pizza Palace Demo Server starting...");
Console.WriteLine("📊 AutoPatch Framework Demo - Live Order & Driver Tracking");
Console.WriteLine("🧪 Test page: http://localhost:5249/test");

app.Run();
