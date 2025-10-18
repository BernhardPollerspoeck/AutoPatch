namespace Autopatch.Client.Models;

/// <summary>
/// Configuration options for the AutoPatch client.
/// </summary>
public class AutoPatchConfiguration
{
    /// <summary>
    /// Gets or sets the endpoint URL for the AutoPatch server.
    /// This is used for direct endpoint configuration (traditional approach).
    /// Mutually exclusive with ServiceName.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the service name for .NET Aspire service discovery.
    /// When specified, the endpoint will be resolved through service discovery.
    /// Mutually exclusive with Endpoint.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets an optional dispatcher function for UI thread marshalling.
    /// Example: action => Application.Current.Dispatcher.Invoke(action)
    /// </summary>
    public Action<Action>? Dispatcher { get; set; }
}

