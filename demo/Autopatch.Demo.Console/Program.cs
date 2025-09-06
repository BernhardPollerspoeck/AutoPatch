
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
    .AddTrackedCollection<CarPosition>()
    ;

var app = builder.Build();


await app.StartAsync();



var autoPatchClient = app.Services.GetRequiredService<IAutoPatchClient>();
await autoPatchClient.SubscribeToTypeAsync<CarPosition>();




var collection = autoPatchClient.GetTrackedCollection<CarPosition>();

while (true)
{
    Console.Clear();
    Console.WriteLine("Cars:");
    foreach (var car in collection)
    {
        Console.WriteLine($"- {car.Id}: {car.Model} at ({car.Latitude}, {car.Longitude})");
    }
    Console.WriteLine("Press Ctrl+C to exit.");
    await Task.Delay(2000);
}


//await autoPatchClient.UnsubscribeFromTypeAsync<CarPosition>();
