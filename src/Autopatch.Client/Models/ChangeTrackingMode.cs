namespace Autopatch.Client.Models;

/// <summary>
/// Defines the change tracking modes for client-side changes, as described in the specification.
/// </summary>
/// <remarks>
/// Client-initiated changes are not supported yet; collections are always synchronized from the server only (<see cref="Disabled"/>).
/// </remarks>
public enum ChangeTrackingMode
{
    /// <summary>
    /// Client-side changes are not tracked. This is the default.
    /// </summary>
    Disabled,

    /// <summary>
    /// Manual commit mode - changes must be explicitly committed.
    /// </summary>
    ManualCommit,

    /// <summary>
    /// Automatic commit mode - changes are committed automatically.
    /// </summary>
    AutoCommit,
}
