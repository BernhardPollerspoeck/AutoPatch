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
}
