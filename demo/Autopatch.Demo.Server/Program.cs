using Autopatch.Demo.Server;
using Autopatch.Demo.Shared;
using Autopatch.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAutoPatch(cfg =>
    {
        cfg.DefaultThrottleInterval = TimeSpan.FromSeconds(1);
        cfg.MaxBatchSize = 100;
    })
    .AddTrackedCollection<CarPosition>(cfg =>
    {
        cfg.ClientChangePolicy = ClientChangePolicy.Disallow;
    });

builder.Services.AddSignalR();

builder.Services.AddHostedService<DemoDataSimulator>();

var app = builder.Build();

app.UseAutoPatch();


app.Run();
