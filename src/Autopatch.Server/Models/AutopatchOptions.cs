namespace Autopatch.Server.Models;

/// <summary>
/// Configuration options for the Autopatch server.
/// </summary>
public class AutopatchOptions
{
    /// <summary>
    /// Gets or sets the default interval between throttled operations.
    /// </summary>
    /// <value>The default throttle interval. Defaults to 200 milliseconds.</value>
    public TimeSpan DefaultThrottleInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum number of items that can be processed in a single batch.
    /// </summary>
    /// <value>The maximum batch size. Must be greater than zero. Defaults to 100.</value>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets when queued changes are sent, unless a collection type overrides it.
    /// </summary>
    /// <value>The default flush mode. Defaults to <see cref="FlushMode.Timed"/>.</value>
    public FlushMode DefaultFlushMode { get; set; } = FlushMode.Timed;
}
