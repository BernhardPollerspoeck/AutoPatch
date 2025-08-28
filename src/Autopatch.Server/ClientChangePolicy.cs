namespace Autopatch.Server;

/// <summary>
/// Specifies the policy for handling client changes in a system.
/// </summary>
/// <remarks>This enumeration defines the possible policies for managing changes initiated by clients. Use these
/// values to configure how client changes are processed or restricted.</remarks>
public enum ClientChangePolicy
{
    /// <summary>
    /// Automatically accept and process client changes without requiring additional confirmation.
    /// </summary>
    AutoAccept,

    /// <summary>
    /// Indicates that confirmation is required before performing the associated action.
    /// </summary>
    ConfirmationRequired,

    /// <summary>
    /// Disallow any changes initiated by clients. All client changes will be rejected.
    /// </summary>
    Reject,
}

