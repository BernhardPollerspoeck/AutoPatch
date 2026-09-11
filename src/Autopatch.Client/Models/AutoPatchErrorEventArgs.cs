namespace Autopatch.Client.Models;

/// <summary>
/// Describes an error the AutoPatch client recovered from, e.g. a batch that could not be applied.
/// </summary>
/// <param name="exception">The error.</param>
/// <param name="subscriptionKey">The affected subscription ("TypeName" or "TypeName/Key"), or null for connection errors.</param>
public sealed class AutoPatchErrorEventArgs(Exception exception, string? subscriptionKey) : EventArgs
{
    /// <summary>
    /// Gets the error.
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Gets the affected subscription ("TypeName" or "TypeName/Key"), or null for connection errors.
    /// </summary>
    public string? SubscriptionKey { get; } = subscriptionKey;
}
