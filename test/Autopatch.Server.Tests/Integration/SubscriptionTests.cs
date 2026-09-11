using System.Collections.Concurrent;
using System.Text.Json;
using Autopatch.Server.Extensions;
using Autopatch.Server.Services;
using Autopatch.Server.SignalR;
using Autopatch.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR.Client;

namespace Autopatch.Server.Tests.Integration;

[TestClass]
public sealed class SubscriptionTests
{
    [TestMethod]
    [TestCategory("S4")]
    public async Task S4_ItemAddedJustBeforeSubscribing_IsNotDuplicatedOnTheClient()
    {
        await using var server = await AutoPatchTestServer.StartAsync(throttleInterval: TimeSpan.FromSeconds(3));
        await using var client = await TestClient.ConnectAsync(server.BaseUrl);

        server.Manager.GetOrCreateCollection<TestItem>().Add(TestItem.Create(1, "a"));
        (await client.Client.SubscribeToTypeAsync<TestItem>()).Should().BeTrue();

        await Eventually.WaitUntilAsync(() => client.Names().Count > 0, TimeSpan.FromSeconds(6));
        await Task.Delay(300);

        client.Names().Should().Equal(["a"],
            "the queued 'add' is already contained in the snapshot and is still sent to the group right after the full data");
    }

    [TestMethod]
    [TestCategory("S5")]
    public async Task S5_SubscribeBeforeTheCollectionExists_ReceivesItemsAddedLater()
    {
        await using var server = await AutoPatchTestServer.StartAsync();
        await using var client = await TestClient.ConnectAsync(server.BaseUrl);

        (await client.Client.SubscribeToTypeAsync<TestItem>("zone-1")).Should().BeTrue();
        server.Manager.GetOrCreateCollection<TestItem>("zone-1").Add(TestItem.Create(1, "created later"));

        var received = await Eventually.WaitUntilAsync(() => client.Names("zone-1").Count == 1, TimeSpan.FromSeconds(3));

        received.Should().BeTrue("no initial set is sent when the tracker does not exist yet, so the client ignores every later patch");
    }

    [TestMethod]
    [TestCategory("C3")]
    public async Task C3_SubscribingTwice_DoesNotDuplicateItems()
    {
        await using var server = await AutoPatchTestServer.StartAsync();
        var collection = server.Manager.GetOrCreateCollection<TestItem>();
        collection.Add(TestItem.Create(1, "a"));
        await using var client = await TestClient.ConnectAsync(server.BaseUrl);

        await client.Client.SubscribeToTypeAsync<TestItem>();
        await client.Client.SubscribeToTypeAsync<TestItem>();
        await Eventually.WaitUntilAsync(() => client.Names().Count > 0, TimeSpan.FromSeconds(3));
        collection.Add(TestItem.Create(2, "b"));
        await Eventually.WaitUntilAsync(() => client.Names().Contains("b"), TimeSpan.FromSeconds(3));
        await Task.Delay(300);

        client.Names().Should().Equal(["a", "b"],
            "every SubscribeToTypeAsync registers another handler, so each batch is applied once per call");
    }

    [TestMethod]
    [TestCategory("C3")]
    public async Task C3_UnsubscribingOneOfTwoSubscribers_KeepsUpdatesFlowing()
    {
        await using var server = await AutoPatchTestServer.StartAsync();
        var collection = server.Manager.GetOrCreateCollection<TestItem>();
        await using var client = await TestClient.ConnectAsync(server.BaseUrl);
        await client.Client.SubscribeToTypeAsync<TestItem>();
        await client.Client.SubscribeToTypeAsync<TestItem>();

        await client.Client.UnsubscribeFromTypeAsync<TestItem>();
        collection.Add(TestItem.Create(1, "after unsubscribe"));

        var received = await Eventually.WaitUntilAsync(() => client.Names().Contains("after unsubscribe"), TimeSpan.FromSeconds(3));

        received.Should().BeTrue("Unsubscribe removes all handlers and leaves the server group although one subscriber is left");
    }

    [TestMethod]
    [TestCategory("C3")]
    public async Task C3_SubscribingAgainWithInvalidCredentials_IsRejected()
    {
        await using var server = await AutoPatchTestServer.StartAsync(
            configureServices: services => services.AddTrackedCollection<SecureItem, AuthStringValidator<SecureItem>>());
        await using var client = await TestClient.ConnectAsync(server.BaseUrl);

        (await client.Client.SubscribeToTypeAsync<SecureItem>(authString: "valid")).Should().BeTrue();
        var second = await client.Client.SubscribeToTypeAsync<SecureItem>(authString: "invalid");

        second.Should().BeFalse("a repeated subscription shares the collection, but must still be validated with its own credentials");
    }

    [TestMethod]
    [TestCategory("S14")]
    public async Task S14_AutoPatchHostedInAnApplicationHub_DeliversData()
    {
        await using var server = await AutoPatchTestServer.StartAsync(configureApp: app => app.MapHub<ApplicationHub>("/app"));
        server.Manager.GetOrCreateCollection<TestItem>().Add(TestItem.Create(1, "a"));
        await using var connection = new HubConnectionBuilder().WithUrl($"{server.BaseUrl}/app").Build();
        var received = new ConcurrentQueue<JsonElement>();
        connection.On<string, JsonElement, bool, long>("AutoPatch/TestItem", (_, operations, _, _) => received.Enqueue(operations));
        await connection.StartAsync();

        (await connection.InvokeAsync<bool>("SubscribeToType", nameof(TestItem), null, null)).Should().BeTrue();
        var delivered = await Eventually.WaitUntilAsync(() => !received.IsEmpty, TimeSpan.FromSeconds(3));

        delivered.Should().BeTrue(
            "the trackers send through IHubContext<AutoPatchHub>, which does not know connections of any other hub, so an application needs a second connection");
    }

    public sealed class ApplicationHub(ITrackedCollectionManager collectionManager, IServiceProvider serviceProvider)
        : AutoPatchHub(collectionManager, serviceProvider);
}
