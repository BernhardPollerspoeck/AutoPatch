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
    .AddObjectType<CarPosition>(cfg =>
    {
        cfg.KeySelector = cp => cp.Id;
        cfg.ClientChangePolicy = ClientChangePolicy.Disallow;
    });

builder.Services.AddSignalR()
    .AddAutoPatch();

builder.Services.AddHostedService<DemoDataSimulator>();

var app = builder.Build();

app.UseAutoPatch();


app.Run();
