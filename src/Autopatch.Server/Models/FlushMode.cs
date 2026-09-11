namespace Autopatch.Server.Models;

/// <summary>
/// Specifies when queued changes of a collection are sent to the clients.
/// </summary>
public enum FlushMode
{
    /// <summary>
    /// Changes are sent at the latest one throttle interval after the first queued change, or earlier when the maximum batch size is reached.
    /// </summary>
    Timed,

    /// <summary>
    /// Changes are sent when the maximum batch size is reached or when the application flushes explicitly.
    /// </summary>
    MaxBatchSize,

    /// <summary>
    /// Changes are only sent when the application calls <see cref="Services.ITrackedCollectionManager.FlushAsync{TItem}(string?)"/>,
    /// e.g. once per simulation tick. Neither a timer nor the maximum batch size splits a batch.
    /// </summary>
    Manual
}
