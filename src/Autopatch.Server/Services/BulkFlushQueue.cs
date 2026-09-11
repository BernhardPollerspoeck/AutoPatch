using System.Threading.Channels;
using Autopatch.Server.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autopatch.Server.Services;

/// <summary>
/// Collects items into batches and hands the batches, strictly in order, to <see cref="OnFlush"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Add"/> only appends to the pending batch and never waits for a flush, so producers are never blocked by a slow
/// transport. A batch is sealed when the throttle interval since its first item has elapsed, when it reaches the maximum batch
/// size or when <see cref="Flush"/> is called, depending on the <see cref="FlushMode"/>.
/// </para>
/// <para>
/// Sealed batches are delivered by a single background loop, one after the other, in the order they were sealed.
/// </para>
/// </remarks>
/// <typeparam name="TQueueItem">The type of items stored in the queue.</typeparam>
/// <param name="options">Configuration options for queue behavior including batch size and throttle interval.</param>
/// <param name="itemOptions">Options specific to the type of items in the queue, such as maximum batch size.</param>
/// <param name="logger">Logger instance for recording queue operations and errors.</param>
public class BulkFlushQueue<TQueueItem>(
    IOptions<AutopatchOptions> options,
    IOptions<ObjectTypeConfiguration<TQueueItem>> itemOptions,
    ILogger<BulkFlushQueue<TQueueItem>> logger)
    : IDisposable
    where TQueueItem : class
{
    private readonly Lock _gate = new();
    private readonly List<TQueueItem> _queue = [];
    private readonly Channel<PendingFlush> _flushes = Channel.CreateUnbounded<PendingFlush>(new UnboundedChannelOptions { SingleReader = true });
    private Timer? _timer;
    private bool _timerArmed;
    private Task? _sender;
    private bool _disposed;

    /// <summary>
    /// Event that is raised for every sealed batch. Handlers are awaited one after the other; the next batch is not delivered
    /// before all handlers of the previous one have completed.
    /// </summary>
    public event Func<List<TQueueItem>, Task>? OnFlush;

    /// <summary>
    /// Gets the flush mode that is in effect for this queue.
    /// </summary>
    public FlushMode FlushMode => itemOptions.Value.FlushMode ?? options.Value.DefaultFlushMode;

    /// <summary>
    /// Gets the throttle interval that is in effect for this queue.
    /// </summary>
    public TimeSpan ThrottleInterval => itemOptions.Value.ThrottleInterval ?? options.Value.DefaultThrottleInterval;

    /// <summary>
    /// Appends an item to the pending batch.
    /// </summary>
    /// <param name="item">The item to add to the queue.</param>
    /// <param name="index">Optional zero-based index to insert the item at. If null, item is added to the end.</param>
    /// <param name="forceFlush">If true, seals the pending batch right away.</param>
    /// <returns>
    /// A completed task, or with <paramref name="forceFlush"/> a task that completes once the batch has been delivered.
    /// </returns>
    public Task Add(TQueueItem item, int? index = null, bool forceFlush = false)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            if (index.HasValue)
            {
                _queue.Insert(index.Value, item);
            }
            else
            {
                _queue.Add(item);
            }

            if (forceFlush)
            {
                return SealLocked(FlushMode.Manual);
            }

            if (FlushMode != FlushMode.Manual && _queue.Count >= GetMaxBatchSize())
            {
                SealLocked(FlushMode.MaxBatchSize);
            }
            else if (FlushMode == FlushMode.Timed)
            {
                ArmTimerLocked();
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Delivers an item as a batch of its own, after all batches sealed so far and without sealing the pending batch.
    /// </summary>
    /// <param name="item">The item to deliver.</param>
    /// <returns>A task that completes once the item has been delivered.</returns>
    public Task SendImmediately(TQueueItem item)
    {
        lock (_gate)
        {
            return _disposed ? Task.CompletedTask : EnqueueLocked([item], FlushMode.Manual);
        }
    }

    /// <summary>
    /// Seals the pending batch.
    /// </summary>
    /// <returns>A task that completes once this batch and all batches before it have been delivered.</returns>
    public Task Flush()
    {
        lock (_gate)
        {
            return _disposed ? Task.CompletedTask : SealLocked(FlushMode.Manual);
        }
    }

    private Task SealLocked(FlushMode flushMode)
    {
        List<TQueueItem> items = [.. _queue];
        _queue.Clear();
        return EnqueueLocked(items, flushMode);
    }

    private Task EnqueueLocked(List<TQueueItem> items, FlushMode flushMode)
    {
        var flush = new PendingFlush(items, flushMode);
        _flushes.Writer.TryWrite(flush);
        _sender ??= Task.Run(SendLoopAsync);
        return flush.Completion.Task;
    }

    private void ArmTimerLocked()
    {
        if (_timerArmed)
        {
            return;
        }
        _timer ??= new Timer(OnTimer);
        _timer.Change(GetThrottleIntervalMilliseconds(), Timeout.Infinite);
        _timerArmed = true;
    }

    private void OnTimer(object? state)
    {
        lock (_gate)
        {
            _timerArmed = false;
            if (!_disposed && _queue.Count > 0)
            {
                SealLocked(FlushMode.Timed);
            }
        }
    }

    private async Task SendLoopAsync()
    {
        await foreach (var flush in _flushes.Reader.ReadAllAsync())
        {
            try
            {
                if (flush.Items.Count > 0 && OnFlush is { } handlers)
                {
                    logger.LogDebug("Flushing BulkFlushQueue with {Count} items (Mode: {Mode})", flush.Items.Count, flush.Mode);
                    foreach (var handler in handlers.GetInvocationList().Cast<Func<List<TQueueItem>, Task>>())
                    {
                        await handler(flush.Items);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during flush of BulkFlushQueue");
            }
            finally
            {
                flush.Completion.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Stops the queue. Pending items are discarded; a batch that is currently being delivered completes normally.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queue.Clear();
            _timer?.Dispose();
            _timer = null;
            _flushes.Writer.TryComplete();
        }
        GC.SuppressFinalize(this);
    }

    private int GetMaxBatchSize() => itemOptions.Value.MaxBatchSize ?? options.Value.MaxBatchSize;

    private int GetThrottleIntervalMilliseconds() => (int)ThrottleInterval.TotalMilliseconds;

    private sealed class PendingFlush(List<TQueueItem> items, FlushMode mode)
    {
        public List<TQueueItem> Items { get; } = items;

        public FlushMode Mode { get; } = mode;

        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
