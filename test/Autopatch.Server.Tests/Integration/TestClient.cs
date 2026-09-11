using System.Collections.Concurrent;
using Autopatch.Client.Models;
using Autopatch.Client.Services;
using Autopatch.Server.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ClientRegistration = Autopatch.Client.Extensions.IServiceCollectionExtensions;

namespace Autopatch.Server.Tests.Integration;

/// <summary>
/// The real <see cref="AutoPatchClient"/>, registered the way applications do it.
/// All patches are applied under <see cref="_gate"/> so tests can read the collections safely.
/// </summary>
public sealed class TestClient : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly ServiceProvider _services;

    public TestClient(string endpoint, Action<AutoPatchConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        ClientRegistration.AddAutoPatch(services, config =>
        {
            config.Endpoint = endpoint;
            config.Dispatcher = apply =>
            {
                lock (_gate) apply();
            };
            configure?.Invoke(config);
        });
        ClientRegistration.AddTrackedCollection<TestItem>(services);
        ClientRegistration.AddTrackedCollection<SecureItem>(services);
        _services = services.BuildServiceProvider();
        Client.OnConnectionChanged += (_, connected) => ConnectionChanges.Enqueue(connected);
    }

    public IAutoPatchClient Client => _services.GetRequiredService<IAutoPatchClient>();

    public IServiceProvider Services => _services;

    public ConcurrentQueue<bool> ConnectionChanges { get; } = new();

    public static async Task<TestClient> ConnectAsync(string endpoint, Action<AutoPatchConfiguration>? configure = null)
    {
        var client = new TestClient(endpoint, configure);
        await client.Client.ConnectAsync();
        return client;
    }

    public IReadOnlyList<string> Names(string? key = null)
    {
        lock (_gate)
        {
            try
            {
                return [.. Client.GetTrackedCollection<TestItem>(key).Select(i => i.Name)];
            }
            catch (InvalidOperationException)
            {
                return [];
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Client.DisconnectAsync();
        }
        catch (Exception)
        {
            // Already disconnected.
        }
        await _services.DisposeAsync();
    }
}
