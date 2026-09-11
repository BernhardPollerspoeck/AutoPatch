using Autopatch.Client.Services;
using Autopatch.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopatch.Server.Tests.Integration;

[TestClass]
public sealed class ConnectionTests
{
    private static readonly TimeSpan ReconnectTimeout = TimeSpan.FromSeconds(15);

    [TestMethod]
    [TestCategory("C1")]
    public async Task C1_ServerRestart_ClientReconnectsAutomatically()
    {
        var port = AutoPatchTestServer.GetFreePort();
        var server = await AutoPatchTestServer.StartAsync(port);
        await using var client = await TestClient.ConnectAsync(server.BaseUrl);

        await server.DisposeAsync();
        (await Eventually.WaitUntilAsync(() => client.ConnectionChanges.Contains(false), TimeSpan.FromSeconds(10)))
            .Should().BeTrue("the client notices that the server went away");
        await using var restarted = await AutoPatchTestServer.StartAsync(port);

        var reconnected = await Eventually.WaitUntilAsync(() => client.ConnectionChanges.LastOrDefault(), ReconnectTimeout);

        reconnected.Should().BeTrue("the HubConnection is built without WithAutomaticReconnect(), so after Closed nothing happens anymore");
    }

    [TestMethod]
    [TestCategory("C1")]
    public async Task C1_ConnectionManager_StartAsync_DoesNotBlockOrFailTheHostWhileTheServerIsDown()
    {
        var unusedPort = AutoPatchTestServer.GetFreePort();
        await using var client = new TestClient($"http://127.0.0.1:{unusedPort}");
        var manager = new AutopatchConnectionManager(client.Client, NullLogger<AutopatchConnectionManager>.Instance);
        using var shutdown = new CancellationTokenSource();

        var start = manager.StartAsync(shutdown.Token);
        var finished = await Task.WhenAny(start, Task.Delay(TimeSpan.FromSeconds(3))) == start;
        await shutdown.CancelAsync();

        finished.Should().BeTrue("StartAsync retries inside the host start-up (1s, 2s, 4s, 8s) and then throws, which stops the whole host");
        start.IsCompletedSuccessfully.Should().BeTrue("a hosted service that throws in StartAsync terminates the application");
    }

    [TestMethod]
    [TestCategory("C2")]
    public async Task C2_ServerRestart_SubscriptionsAreRestored()
    {
        var port = AutoPatchTestServer.GetFreePort();
        var server = await AutoPatchTestServer.StartAsync(port);
        server.Manager.GetOrCreateCollection<TestItem>().Add(TestItem.Create(1, "before restart"));
        await using var client = await TestClient.ConnectAsync(server.BaseUrl);
        (await client.Client.SubscribeToTypeAsync<TestItem>()).Should().BeTrue();
        (await Eventually.WaitUntilAsync(() => client.Names().Count == 1, TimeSpan.FromSeconds(5))).Should().BeTrue();

        await server.DisposeAsync();
        await using var restarted = await AutoPatchTestServer.StartAsync(port);
        restarted.Manager.GetOrCreateCollection<TestItem>().Add(TestItem.Create(2, "after restart"));

        var restored = await Eventually.WaitUntilAsync(() => client.Names().SequenceEqual(["after restart"]), ReconnectTimeout);

        restored.Should().BeTrue(
            "there is neither a reconnect nor a resubscribe, and full data would be appended to the old items (client has: {0})",
            string.Join(", ", client.Names()));
    }

    [TestMethod]
    [TestCategory("C9")]
    public async Task C9_ConnectAsyncTwice_KeepsASingleServerConnection()
    {
        var connections = new ConnectionCounter();
        await using var server = await AutoPatchTestServer.StartAsync(configureServices: services =>
        {
            services.AddSingleton(connections);
            services.AddSignalR(o => o.AddFilter<ConnectionCountingFilter>());
        });
        await using var client = await TestClient.ConnectAsync(server.BaseUrl);

        await client.Client.ConnectAsync();
        await Eventually.WaitUntilAsync(() => connections.Active == 2, TimeSpan.FromSeconds(2));

        connections.Active.Should().Be(1, "a second ConnectAsync builds a new HubConnection and leaves the old one open");
    }

    [TestMethod]
    [TestCategory("S14")]
    public async Task S14_EndpointIncludingTheHubPath_WorksLikeInTheJsClient()
    {
        await using var server = await AutoPatchTestServer.StartAsync();
        var jsStyleEndpoint = $"{server.BaseUrl}/autopatch";

        var connect = async () => await (await TestClient.ConnectAsync(jsStyleEndpoint)).DisposeAsync();

        await connect.Should().NotThrowAsync(
            "the JS client expects the full hub URL, the .NET client appends '/Autopatch' itself, so the same endpoint string cannot be used for both");
    }

    public sealed class ConnectionCounter
    {
        private int _active;

        public int Active => Volatile.Read(ref _active);

        public void Connected() => Interlocked.Increment(ref _active);

        public void Disconnected() => Interlocked.Decrement(ref _active);
    }

    private sealed class ConnectionCountingFilter(ConnectionCounter counter) : IHubFilter
    {
        public Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            counter.Connected();
            return next(context);
        }

        public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
        {
            counter.Disconnected();
            return next(context, exception);
        }
    }
}
