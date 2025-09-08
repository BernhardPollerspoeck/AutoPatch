namespace Autopatch.Client.Models;

/// <summary>
/// Defines the change tracking modes available for AutoPatch collections.
/// </summary>
public enum ChangeTrackingMode
{
    /// <summary>
    /// Automatic change tracking mode.
    /// </summary>
    Auto,

    /// <summary>
    /// Manual commit mode - changes must be explicitly committed.
    /// </summary>
    ManualCommit,

    /// <summary>
    /// Automatic commit mode - changes are committed automatically.
    /// </summary>
    AutoCommit,
}
