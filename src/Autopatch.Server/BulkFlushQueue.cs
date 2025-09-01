using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autopatch.Server;

public enum FlushMode
{
    Timed,
    MaxBatchSize,
    Manual
}

public class BulkFlushQueue<TQueueItem>(
    IOptions<AutopatchOptions> options,
    ILogger<BulkFlushQueue<TQueueItem>> logger)
{
    private readonly Lock _lock = new();
    private readonly List<TQueueItem> _queue = [];
    private Timer? _timer; //TODO: lock timer invocation

    public event Func<List<TQueueItem>, Task>? OnFlush;

    public void Add(TQueueItem item)
    {
        lock (_lock)
        {
            _timer ??= new(
                _ => InternalFlush(FlushMode.Timed),
                null,
                (int)options.Value.DefaultThrottleInterval.TotalMilliseconds,
                (int)options.Value.DefaultThrottleInterval.TotalMilliseconds);

            _queue.Add(item);
            if (_queue.Count >= options.Value.MaxBatchSize)
            {
                InternalFlush(FlushMode.MaxBatchSize);
            }
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            InternalFlush(FlushMode.Manual);
        }
    }


    private void InternalFlush(FlushMode flushMode)
    {
        logger.LogInformation("Flushing BulkFlushQueue with {Count} items (Mode: {Mode})", _queue.Count, flushMode);
        var itemsToFlush = _queue.ToList();
        try
        {
            OnFlush?.Invoke(itemsToFlush);
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
}

