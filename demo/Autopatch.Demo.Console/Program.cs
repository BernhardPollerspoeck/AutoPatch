
using System.Collections.ObjectModel;
using Autopatch.Client;
using Autopatch.Demo.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();

builder.Services
    .AddAutoPatch(cfg =>
    {
        cfg.Endpoint = "http://localhost:5249";
    })

    ;

var app = builder.Build();

app.Start();

var autoPatchClient = app.Services.GetRequiredService<IAutoPatchClient>();

await autoPatchClient.ConnectAsync();

var cars = new ObservableCollection<CarPosition>();

var subscriptionId = await autoPatchClient.SubscribeToTypeAsync<CarPosition>(cars);
await autoPatchClient.RequestFullDataAsync(subscriptionId);

while (true)
{
    Console.Clear();
    Console.WriteLine($"Subscription ID: {subscriptionId}");
    Console.WriteLine("Cars:");
    foreach (var car in cars)
    {
        Console.WriteLine($"- {car.Id}: {car.Model} at ({car.Latitude}, {car.Longitude})");
    }
    Console.WriteLine("Press Ctrl+C to exit.");
    await Task.Delay(2000);
}
