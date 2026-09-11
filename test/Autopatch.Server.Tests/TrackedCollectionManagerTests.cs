using Autopatch.Server.Extensions;
using Autopatch.Server.Models;
using Autopatch.Server.Services;
using Autopatch.Server.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Autopatch.Server.Tests;

[TestClass]
public sealed class TrackedCollectionManagerTests
{
    [TestMethod]
    [TestCategory("S8")]
    public void S8_Manager_OffersAManualFlush()
    {
        typeof(ITrackedCollectionManager).GetMethods().Select(m => m.Name)
            .Should().Contain(name => name.Contains("Flush"),
                "BulkFlushQueue.Flush() exists but is not reachable through the public API, so batches cannot be bound to an application tick");
    }

    [TestMethod]
    [TestCategory("S8")]
    public void S8_Configuration_OffersAManualOnlyFlushMode()
    {
        var configurationTypes = new[] { typeof(AutopatchOptions), typeof(ObjectTypeConfiguration<object>) };

        configurationTypes.SelectMany(t => t.GetProperties()).Select(p => p.PropertyType)
            .Should().Contain(typeof(FlushMode),
                "FlushMode.Manual is only an enum value; there is no option that turns off the timer-based flush");
    }

    [TestMethod]
    [TestCategory("S14")]
    public async Task S14_RemoveCollection_SubscribersDoNotKeepStaleItems()
    {
        using var harness = new HubHarness(s => s.AddTrackedCollection<TestItem>());
        harness.Manager.GetOrCreateCollection<TestItem>("zone-1").Add(TestItem.Create(1, "old"));
        // Let the 'add' go out before subscribing, otherwise S4 delivers the item twice.
        (await Eventually.WaitUntilAsync(() => harness.Hub.Messages.Count > 0, TimeSpan.FromSeconds(2))).Should().BeTrue();
        var client = harness.Client("client");
        await harness.CreateHub("client").SubscribeToType(nameof(TestItem), "zone-1");
        (await Eventually.WaitUntilAsync(() => client.Items.Count == 1, TimeSpan.FromSeconds(2))).Should().BeTrue();

        harness.Manager.RemoveCollection<TestItem>("zone-1");
        var recreated = harness.Manager.GetOrCreateCollection<TestItem>("zone-1");
        recreated.Add(TestItem.Create(2, "new"));
        await Eventually.WaitUntilAsync(() => client.Items.Count == 2, TimeSpan.FromSeconds(1));

        client.Items.Select(i => i["name"]!.GetValue<string>()).Should().Equal(["new"],
            "RemoveCollection neither notifies the subscribers nor removes the group, so patches for the recreated collection are applied on top of stale data");
    }

    [TestMethod]
    [TestCategory("S14")]
    public async Task S14_TrackerService_StartAsync_DoesNotOnlyLog()
    {
        var logs = new CapturingLoggerFactory();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(logs);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddAutoPatch(_ => { });
        using var provider = services.BuildServiceProvider();

        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        logs.Entries.Where(e => e.Level >= LogLevel.Information).Select(e => e.Message)
            .Should().BeEmpty("AutoPatchCollectionTrackerService.StartAsync does nothing except resolving the manager and logging two Information messages");
    }
}
