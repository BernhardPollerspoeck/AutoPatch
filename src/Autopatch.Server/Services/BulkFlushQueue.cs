using Autopatch.Server.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autopatch.Server.Services;

/// <summary>
/// A thread-safe queue that batches items and flushes them based on configurable criteria.
/// Items are flushed when the batch size limit is reached, after a timeout interval, or manually.
/// </summary>
/// <typeparam name="TQueueItem">The type of items stored in the queue.</typeparam>
/// <param name="options">Configuration options for queue behavior including batch size and throttle interval.</param>
/// <param name="logger">Logger instance for recording queue operations and errors.</param>
public class BulkFlushQueue<TQueueItem>(
    IOptions<AutopatchOptions> options,
    ILogger<BulkFlushQueue<TQueueItem>> logger) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<TQueueItem> _queue = [];
    private Timer? _timer;
    private bool _disposed = false;

    /// <summary>
    /// Event that is raised when the queue is flushed. Subscribers receive the list of items being flushed.
    /// </summary>
    public event Func<List<TQueueItem>, Task>? OnFlush;

    /// <summary>
    /// Adds an item to the queue. Optionally inserts at a specific index or forces immediate flush.
    /// </summary>
    /// <param name="item">The item to add to the queue.</param>
    /// <param name="index">Optional zero-based index to insert the item at. If null, item is added to the end.</param>
    /// <param name="forceFlush">If true, immediately flushes the queue after adding the item.</param>
    public async Task Add(TQueueItem item, int? index = null, bool forceFlush = false)
    {
        if (_disposed)
            return;

        await _semaphore.WaitAsync();
        try
        {
            _timer ??= new(
                async _ => await TimerCallback(),
                null,
                (int)options.Value.DefaultThrottleInterval.TotalMilliseconds,
                (int)options.Value.DefaultThrottleInterval.TotalMilliseconds);

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
                await InternalFlush(FlushMode.Manual);
                return;
            }

            if (_queue.Count >= options.Value.MaxBatchSize)
            {
                await InternalFlush(FlushMode.MaxBatchSize);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Manually flushes all items currently in the queue.
    /// </summary>
    public async Task Flush()
    {
        if (_disposed)
            return;

        await _semaphore.WaitAsync();
        try
        {
            await InternalFlush(FlushMode.Manual);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Internal method that performs the actual flush operation by invoking the OnFlush event
    /// and clearing the queue. Resets the timer interval after flushing.
    /// </summary>
    /// <param name="flushMode">The mode that triggered this flush operation.</param>
    private async Task InternalFlush(FlushMode flushMode)
    {
        if (_queue.Count == 0)
            return;

        logger.LogInformation("Flushing BulkFlushQueue with {Count} items (Mode: {Mode})", _queue.Count, flushMode);
        var itemsToFlush = _queue.ToList();
        
        try
        {
            if (OnFlush != null)
                await OnFlush.Invoke(itemsToFlush);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during flush of BulkFlushQueue");
        }
        
        _queue.Clear();

        _timer?.Change(
            (int)options.Value.DefaultThrottleInterval.TotalMilliseconds,
            (int)options.Value.DefaultThrottleInterval.TotalMilliseconds);
    }

    /// <summary>
    /// Timer callback method that triggers a timed flush when the throttle interval elapses.
    /// </summary>
    private async Task TimerCallback()
    {
        if (_disposed)
            return;

        await _semaphore.WaitAsync();
        try
        {
            await InternalFlush(FlushMode.Timed);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Releases all resources used by the BulkFlushQueue.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer?.Dispose();
        _timer = null;
        _semaphore.Dispose();
    }
}
