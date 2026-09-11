using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using Autopatch.Core;
using Autopatch.Server.Models;
using Autopatch.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autopatch.Server.Tests.Infrastructure;

/// <summary>
/// Wires a single <see cref="ObservableCollectionTracker{T}"/> to a <see cref="RecordingHubContext"/>.
/// The throttle interval defaults to one hour so batches are only sent when a test calls <see cref="FlushAsync"/>.
/// </summary>
public sealed class TrackerHarness<T> : IDisposable where T : class, INotifyPropertyChanged
{
    private static readonly FieldInfo? QueueItemsField =
        typeof(BulkFlushQueue<OperationContainer<T>>).GetField("_queue", BindingFlags.Instance | BindingFlags.NonPublic);

    public TrackerHarness(
        TimeSpan? throttleInterval = null,
        int maxBatchSize = 1_000_000,
        string[]? excludedProperties = null,
        string? key = null)
    {
        var itemOptions = Options.Create(new ObjectTypeConfiguration<OperationContainer<T>>
        {
            ThrottleInterval = throttleInterval ?? TimeSpan.FromHours(1),
            MaxBatchSize = maxBatchSize,
            ExcludedProperties = excludedProperties,
        });

        Queue = new BulkFlushQueue<OperationContainer<T>>(
            Options.Create(new AutopatchOptions()),
            itemOptions,
            Logs.CreateLogger<BulkFlushQueue<OperationContainer<T>>>());
        Tracker = new ObservableCollectionTracker<T>(
            Queue,
            itemOptions,
            Logs.CreateLogger<ObservableCollectionTracker<T>>(),
            Hub,
            key);
        Tracker.StartTracking();
    }

    public RecordingHubContext Hub { get; } = new();

    public CapturingLoggerFactory Logs { get; } = new();

    public BulkFlushQueue<OperationContainer<T>> Queue { get; }

    public ObservableCollectionTracker<T> Tracker { get; }

    public ObservableCollection<T> Collection => Tracker.TrackedCollection;

    public string Target => $"AutoPatch/{((IObjectTracker)Tracker).GetSubscriptionKey()}";

    public IReadOnlyList<string> ServerSnapshot() => [.. Collection.Select(i => SignalRWire.SerializeItem(i).ToJsonString())];

    /// <summary>A client that is subscribed and already in sync with the current server state.</summary>
    public ClientMirror CreateSyncedClient(string connectionId = "synced-client")
    {
        Hub.AddToGroupAsync(connectionId, Target);
        var mirror = new ClientMirror(Hub, connectionId, requireInitialSet: true);
        mirror.Seed(Collection);
        return mirror;
    }

    /// <summary>Subscribes a new client the same way <c>AutoPatchHub.SubscribeToType</c> does: join the group, then request full data.</summary>
    public ClientMirror SubscribeNewClient(string connectionId)
    {
        Hub.AddToGroupAsync(connectionId, Target);
        var mirror = new ClientMirror(Hub, connectionId, requireInitialSet: true);
        Tracker.SendFullData(connectionId);
        return mirror;
    }

    public int QueuedCount => QueueItemsField?.GetValue(Queue) is ICollection items ? items.Count : -1;

    /// <summary>
    /// Waits until the fire-and-forget enqueue tasks of the tracker have landed in the queue.
    /// </summary>
    public async Task SettleAsync(int expectedQueued = 0)
    {
        if (QueueItemsField is null)
        {
            await Task.Delay(200);
            return;
        }

        await Eventually.WaitUntilAsync(() => QueuedCount >= expectedQueued, TimeSpan.FromSeconds(1));

        var last = -1;
        var stableSince = DateTime.UtcNow;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var current = QueuedCount;
            if (current != last)
            {
                last = current;
                stableSince = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - stableSince > TimeSpan.FromMilliseconds(80))
            {
                return;
            }
            await Task.Delay(10);
        }
    }

    public async Task FlushAsync(int expectedQueued = 0)
    {
        await SettleAsync(expectedQueued);
        await Queue.Flush();
    }

    public void Dispose() => Tracker.Dispose();
}
