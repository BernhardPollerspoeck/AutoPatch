namespace Autopatch.Server.Models;

/// <summary>
/// Configuration options for the Autopatch server.
/// </summary>
public class AutopatchOptions
{
    /// <summary>
    /// Gets or sets the default interval between throttled operations.
    /// </summary>
    /// <value>The default throttle interval. Defaults to a system-defined value.</value>
    public TimeSpan DefaultThrottleInterval { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items that can be processed in a single batch.
    /// </summary>
    /// <value>The maximum batch size. Must be greater than zero.</value>
    public int MaxBatchSize { get; set; }
}

