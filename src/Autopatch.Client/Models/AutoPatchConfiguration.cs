namespace Autopatch.Client.Models;

/// <summary>
/// Configuration options for the AutoPatch client.
/// </summary>
public class AutoPatchConfiguration
{
    /// <summary>
    /// Gets or sets the endpoint URL for the AutoPatch server.
    /// </summary>
    public string Endpoint { get; set; } = null!;

    /// <summary>
    /// Gets or sets an optional dispatcher function for UI thread marshalling.
    /// Example: action => Application.Current.Dispatcher.Invoke(action)
    /// </summary>
    public Action<Action>? Dispatcher { get; set; }
}

