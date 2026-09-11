using Microsoft.AspNetCore.Http.Connections.Client;

namespace Autopatch.Client.Models;

/// <summary>
/// Configuration options for the AutoPatch client.
/// </summary>
public class AutoPatchConfiguration
{
    /// <summary>
    /// Gets or sets the URL of the AutoPatch hub, e.g. <c>https://host/autopatch</c>.
    /// </summary>
    /// <remarks>
    /// If the URL has no path (e.g. <c>https://host</c>), the default hub path <c>/autopatch</c> is appended.
    /// </remarks>
    public string Endpoint { get; set; } = null!;

    /// <summary>
    /// Gets or sets an optional dispatcher function for UI thread marshalling.
    /// Example: action => Application.Current.Dispatcher.Invoke(action)
    /// </summary>
    public Action<Action>? Dispatcher { get; set; }

    /// <summary>
    /// Gets or sets a function that provides the access token (e.g. a JWT) for the hub connection.
    /// </summary>
    /// <remarks>
    /// The token is sent as bearer token, so the hub sees an authenticated <c>Context.User</c> and subscription validators receive it.
    /// </remarks>
    public Func<Task<string?>>? AccessTokenProvider { get; set; }

    /// <summary>
    /// Gets or sets a callback to configure the underlying HTTP connection (headers, cookies, transports, ...).
    /// </summary>
    public Action<HttpConnectionOptions>? ConfigureHttpConnection { get; set; }

    /// <summary>
    /// Gets or sets the delays between reconnect attempts. The last delay is repeated forever.
    /// </summary>
    /// <value>Defaults to 0s, 1s, 2s, 5s, 10s.</value>
    public IReadOnlyList<TimeSpan>? ReconnectDelays { get; set; }
}
