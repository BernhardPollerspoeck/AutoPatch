using System.Net;
using System.Net.Sockets;
using Autopatch.Server.Extensions;
using Autopatch.Server.Services;
using Autopatch.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autopatch.Server.Tests.Integration;

/// <summary>
/// A real Kestrel server with the AutoPatch hub on a local port.
/// </summary>
public sealed class AutoPatchTestServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private AutoPatchTestServer(WebApplication app, int port)
    {
        _app = app;
        Port = port;
    }

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public IServiceProvider Services => _app.Services;

    public ITrackedCollectionManager Manager => _app.Services.GetRequiredService<ITrackedCollectionManager>();

    public static async Task<AutoPatchTestServer> StartAsync(
        int? port = null,
        TimeSpan? throttleInterval = null,
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? configureApp = null)
    {
        var actualPort = port ?? GetFreePort();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{actualPort}");
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(1));
        builder.Services.AddSignalR();
        builder.Services
            .AddAutoPatch(o => o.DefaultThrottleInterval = TimeSpan.FromMilliseconds(20))
            .AddTrackedCollection<TestItem>(c => c.ThrottleInterval = throttleInterval);
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        if (configureApp is null)
        {
            app.UseAutoPatch();
        }
        else
        {
            configureApp(app);
        }

        await app.StartAsync();
        return new AutoPatchTestServer(app, actualPort);
    }

    public static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
