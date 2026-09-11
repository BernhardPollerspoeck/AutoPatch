using System.Reflection;
using System.Text.Json.Nodes;
using Autopatch.Server.Services;
using Autopatch.Server.Tests.Infrastructure;

namespace Autopatch.Server.Tests;

/// <summary>
/// Behaviour of <see cref="ObservableCollectionTracker{T}"/> as seen by a subscribed client.
/// Every test compares what a correctly behaving client ends up with against the server collection.
/// </summary>
[TestClass]
public sealed class ObservableCollectionTrackerTests
{
    private static async Task<TrackerHarness<TestItem>> CreateWithItemsAsync(params string[] names)
    {
        var harness = new TrackerHarness<TestItem>();
        for (var i = 0; i < names.Length; i++)
        {
            harness.Collection.Add(TestItem.Create(i + 1, names[i]));
        }
        await harness.FlushAsync(names.Length);
        return harness;
    }

    [TestMethod]
    [TestCategory("S1")]
    public async Task S1_RapidChangesOfOneProperty_ArriveInTheOrderTheyWereMade()
    {
        using var harness = new TrackerHarness<TestItem>(throttleInterval: TimeSpan.FromMilliseconds(20), maxBatchSize: 100);
        var item = TestItem.Create(1);
        harness.Collection.Add(item);
        await Eventually.WaitUntilAsync(() => harness.Hub.Messages.Count > 0, TimeSpan.FromSeconds(5));
        var client = harness.CreateSyncedClient();

        const int changes = 2_000;
        await Task.Run(() =>
        {
            for (var value = 1; value <= changes; value++)
            {
                item.Value = value;
            }
        });

        List<int> ReceivedValues() => [.. harness.Hub.MessagesTo(MessageTargetKind.Group)
            .SelectMany(m => m.Operations)
            .Select(o => o!.AsObject())
            .Where(o => o["path"]!.GetValue<string>().EndsWith("/Value", StringComparison.Ordinal))
            .Select(o => o["value"]!.GetValue<int>())];
        await Eventually.WaitUntilAsync(() => ReceivedValues().Count == changes, TimeSpan.FromSeconds(20));

        ReceivedValues().Should().HaveCount(changes);
        ReceivedValues().Should().BeInAscendingOrder(
            "every change is enqueued with its own Task.Run, so the replace operations reach the client in arbitrary order");
        client.Items.Single()["value"]!.GetValue<int>().Should().Be(changes,
            "the client must end up with the last value that was set on the server");
    }

    [TestMethod]
    [TestCategory("S2")]
    public async Task S2_Insert_PlacesItemAtTheSameIndexOnTheClient()
    {
        using var harness = await CreateWithItemsAsync("a", "b", "c");
        var client = harness.CreateSyncedClient();

        harness.Collection.Insert(1, TestItem.Create(99, "inserted"));
        await harness.FlushAsync(1);

        client.Snapshot().Should().Equal(harness.ServerSnapshot(),
            "Insert(1, x) is sent as 'add /-', so the client appends the item instead of inserting it at index 1");
    }

    [TestMethod]
    [TestCategory("S2")]
    public async Task S2_Move_KeepsTheClientOrderInSync()
    {
        using var harness = await CreateWithItemsAsync("a", "b", "c");
        var client = harness.CreateSyncedClient();

        harness.Collection.Move(2, 0);
        await harness.FlushAsync(2);

        client.Snapshot().Should().Equal(harness.ServerSnapshot(),
            "Move is sent as 'add /-' plus 'remove /oldIndex', which does not reproduce the new order");
    }

    [TestMethod]
    [TestCategory("S2")]
    public async Task S2_ReplaceViaIndexer_ReplacesTheItemInPlaceOnTheClient()
    {
        using var harness = await CreateWithItemsAsync("a", "b", "c");
        var client = harness.CreateSyncedClient();

        harness.Collection[1] = TestItem.Create(99, "replacement");
        await harness.FlushAsync(2);

        client.Snapshot().Should().Equal(harness.ServerSnapshot(),
            "collection[i] = x is sent as 'add /-' plus 'remove /i', so the new item ends up at the end");
    }

    [TestMethod]
    [TestCategory("S2")]
    public async Task S2_Clear_EmptiesTheClientCollection()
    {
        using var harness = await CreateWithItemsAsync("a", "b", "c");
        var client = harness.CreateSyncedClient();

        harness.Collection.Clear();
        await harness.FlushAsync();

        client.Snapshot().Should().Equal(harness.ServerSnapshot(),
            "Clear() raises Reset with NewItems/OldItems == null, so no patch is sent at all");
    }

    [TestMethod]
    [TestCategory("S2")]
    public async Task S2_ItemRemovedByClear_NoLongerProducesPatches()
    {
        using var harness = await CreateWithItemsAsync("a", "b", "c");
        var removedItem = harness.Collection[0];
        harness.Collection.Clear();
        await harness.FlushAsync();
        harness.Hub.ClearMessages();

        removedItem.Name = "changed after clear";
        await harness.FlushAsync();

        harness.Hub.Messages.SelectMany(m => m.Operations).Select(o => o!["path"]!.GetValue<string>())
            .Should().BeEmpty("the item is no longer part of the collection, but its PropertyChanged handler still sends '/-1/Name'");
    }

    [TestMethod]
    [TestCategory("S3")]
    public async Task S3_FailedSend_IsRecoveredSoTheClientConverges()
    {
        using var harness = new TrackerHarness<TestItem>(throttleInterval: TimeSpan.FromMilliseconds(20));
        var item = TestItem.Create(1, "original");
        harness.Collection.Add(item);
        await Eventually.WaitUntilAsync(() => harness.Hub.Messages.Count > 0, TimeSpan.FromSeconds(5));
        var client = harness.CreateSyncedClient();

        var failures = 0;
        harness.Hub.BeforeSend = _ => Interlocked.Increment(ref failures) == 1
            ? throw new IOException("simulated transport failure")
            : Task.CompletedTask;

        item.Name = "changed";
        await Eventually.WaitUntilAsync(() => Volatile.Read(ref failures) > 0, TimeSpan.FromSeconds(5));

        var converged = await Eventually.WaitUntilAsync(
            () => client.Snapshot().SequenceEqual(harness.ServerSnapshot()),
            TimeSpan.FromSeconds(2));
        converged.Should().BeTrue(
            "after a failed flush the batch is discarded and neither retried nor followed by a resync (client still has {0})",
            string.Join(", ", client.Snapshot()));
    }

    [TestMethod]
    [TestCategory("S3")]
    public async Task S3_FailedFullDataForOneClient_DoesNotDropTheBatchForAllOtherClients()
    {
        using var harness = await CreateWithItemsAsync("original");
        var existingClient = harness.CreateSyncedClient("existing-client");
        harness.Hub.BeforeSend = message => message.Kind == MessageTargetKind.Client
            ? throw new IOException("simulated failure for the new client")
            : Task.CompletedTask;

        harness.SubscribeNewClient("new-client");
        harness.Collection[0].Name = "changed";
        await harness.FlushAsync(2);

        existingClient.Snapshot().Should().Equal(harness.ServerSnapshot(),
            "an exception while sending full data to one connection aborts HandleQueueFlush before the group broadcast");
    }

    [TestMethod]
    [TestCategory("S4")]
    public async Task S4_OperationsQueuedBeforeTheSnapshot_AreNotAppliedTwiceByANewClient()
    {
        using var harness = new TrackerHarness<TestItem>();
        harness.Collection.Add(TestItem.Create(1, "a"));
        await harness.SettleAsync(1);

        var newClient = harness.SubscribeNewClient("new-client");
        await harness.FlushAsync(2);

        newClient.Snapshot().Should().Equal(harness.ServerSnapshot(),
            "the snapshot already contains the item, and the queued 'add' is sent to the group afterwards, so the new client gets a duplicate");
    }

    [TestMethod]
    [TestCategory("S6")]
    public async Task S6_ExcludedProperty_IsNotSentWhenAnItemIsAdded()
    {
        using var harness = new TrackerHarness<TestItem>(excludedProperties: [nameof(TestItem.Secret)]);
        harness.Collection.Add(new TestItem { Id = 1, Name = "a", Secret = "top secret" });
        await harness.FlushAsync(1);

        var sent = harness.Hub.Messages.SelectMany(m => m.Operations).Single()!["value"]!.AsObject();
        sent.ContainsKey("secret").Should().BeFalse(
            "ExcludedProperties is only checked for 'replace', 'add' serializes the whole item: {0}", sent.ToJsonString());
    }

    [TestMethod]
    [TestCategory("S6")]
    public async Task S6_ExcludedProperty_IsNotSentWithFullData()
    {
        using var harness = new TrackerHarness<TestItem>(excludedProperties: [nameof(TestItem.Secret)]);
        harness.Collection.Add(new TestItem { Id = 1, Name = "a", Secret = "top secret" });
        await harness.FlushAsync(1);
        harness.Hub.ClearMessages();

        harness.Tracker.SendFullData("new-client");
        await harness.FlushAsync(1);

        var fullData = harness.Hub.MessagesTo(MessageTargetKind.Client).Single();
        var sent = fullData.Operations.Single()!["value"]!.AsObject();
        sent.ContainsKey("secret").Should().BeFalse(
            "full data serializes the whole item regardless of ExcludedProperties: {0}", sent.ToJsonString());
    }

    [TestMethod]
    [TestCategory("S9")]
    public async Task S9_Batches_CarryAMonotonicSequenceNumber()
    {
        using var harness = await CreateWithItemsAsync("a");
        harness.Hub.ClearMessages();

        harness.Collection[0].Name = "first";
        await harness.FlushAsync(1);
        harness.Collection[0].Name = "second";
        await harness.FlushAsync(1);

        var batches = harness.Hub.MessagesTo(MessageTargetKind.Group);
        batches.Should().HaveCount(2);
        var first = IntegerCandidates(batches[0].Arguments);
        var second = IntegerCandidates(batches[1].Arguments);

        first.Keys.Intersect(second.Keys).Any(key => second[key] > first[key]).Should().BeTrue(
            "a batch only contains (target, operations, isInitialSet), so a client cannot detect lost, duplicated or reordered batches. Arguments: {0}",
            batches[0].Arguments.ToJsonString());
    }

    [TestMethod]
    [TestCategory("S10")]
    public async Task S10_SendFullData_WhileTheApplicationChangesTheCollection_DoesNotThrow()
    {
        using var harness = new TrackerHarness<TestItem>();
        for (var i = 0; i < 50; i++)
        {
            harness.Collection.Add(TestItem.Create(i));
        }

        using var stop = new CancellationTokenSource();
        var writer = Task.Run(() =>
        {
            var id = 1_000;
            while (!stop.IsCancellationRequested)
            {
                harness.Collection.Add(TestItem.Create(id++));
                harness.Collection.RemoveAt(0);
            }
        });

        var failures = new List<Exception>();
        await Task.Run(() =>
        {
            for (var i = 0; i < 2_000 && failures.Count == 0; i++)
            {
                try
                {
                    harness.Tracker.SendFullData($"client-{i}");
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }
        });
        await stop.CancelAsync();
        await writer;

        failures.Should().BeEmpty(
            "SendFullData enumerates the ObservableCollection on the hub thread while the application thread modifies it");
    }

    [TestMethod]
    [TestCategory("S10")]
    public void S10_PropertyCache_IsSafeForConcurrentPropertyChangedEvents()
    {
        var dictionaryFields = typeof(ObservableCollectionTracker<TestItem>)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            .Select(f => f.Name);

        dictionaryFields.Should().BeEmpty(
            "the reflection cache is a plain Dictionary that is written from PropertyChanged handlers, which may run on any thread");
    }

    [TestMethod]
    [TestCategory("S14")]
    public async Task S14_BatchWithOnlyFullData_DoesNotBroadcastAnEmptyOperationList()
    {
        using var harness = await CreateWithItemsAsync("a");
        harness.Hub.ClearMessages();

        harness.Tracker.SendFullData("new-client");
        await harness.FlushAsync(1);

        harness.Hub.MessagesTo(MessageTargetKind.Group).Where(m => m.Operations.Count == 0)
            .Should().BeEmpty("a batch that only contains full data still sends an empty operations array to the whole group");
    }

    [TestMethod]
    [TestCategory("S14")]
    public async Task S14_ReferenceTypePropertyValue_IsCapturedWhenTheChangeHappens()
    {
        using var harness = await CreateWithItemsAsync("a");
        harness.Hub.ClearMessages();
        var position = new TestPosition { X = 1 };

        harness.Collection[0].Position = position;
        await harness.SettleAsync(1);
        position.X = 2;
        await harness.FlushAsync();

        var sent = harness.Hub.Messages.SelectMany(m => m.Operations).Single()!["value"]!.AsObject();
        sent["x"]!.GetValue<double>().Should().Be(1,
            "the operation keeps a reference and is serialized at flush time, so it sends the state of the flush, not of the change");
    }

    private static Dictionary<string, long> IntegerCandidates(JsonArray arguments)
    {
        var result = new Dictionary<string, long>();
        for (var i = 0; i < arguments.Count; i++)
        {
            switch (arguments[i])
            {
                case JsonValue value when value.TryGetValue<long>(out var number):
                    result[$"arg{i}"] = number;
                    break;
                case JsonObject envelope:
                    foreach (var (name, node) in envelope)
                    {
                        if (node is JsonValue property && property.TryGetValue<long>(out var propertyNumber))
                        {
                            result[$"arg{i}.{name}"] = propertyNumber;
                        }
                    }
                    break;
            }
        }
        return result;
    }
}
