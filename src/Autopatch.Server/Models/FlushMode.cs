namespace Autopatch.Server.Models;

/// <summary>
/// Specifies the mode for flushing data or operations.
/// </summary>
public enum FlushMode
{
    /// <summary>
    /// Flush operations are triggered based on a timer interval.
    /// </summary>
    Timed,

    /// <summary>
    /// Flush operations are triggered when the batch reaches its maximum size.
    /// </summary>
    MaxBatchSize,

    /// <summary>
    /// Flush operations are triggered manually by explicit calls.
    /// </summary>
    Manual
}

