using System.Collections.ObjectModel;
using Autopatch.Client.Models;

namespace Autopatch.Client.Services;

/// <summary>
/// Defines the contract for an AutoPatch client that manages real-time data synchronization.
/// </summary>
public interface IAutoPatchClient : IAsyncDisposable
{
    /// <summary>
    /// Occurs when the connection state changes.
    /// </summary>
    /// <remarks>The event is triggered whenever the connection status transitions between connected and
    /// disconnected states. The event handler receives a <see cref="bool"/> parameter indicating the new connection
    /// state: <see langword="true"/> if the connection is established; otherwise, <see langword="false"/>.</remarks>
    event EventHandler<bool> OnConnectionChanged;

    /// <summary>
    /// Occurs when the client had to recover from an error, e.g. a batch that could not be applied.
    /// </summary>
    /// <remarks>
    /// The client requests the full data of the affected collection again, so the event is informational.
    /// It is raised on the dispatcher, if one is configured.
    /// </remarks>
    event EventHandler<AutoPatchErrorEventArgs> OnError;

    /// <summary>
    /// Establishes a connection to the AutoPatch server. Calling it while already connected has no effect.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the connection operation.</param>
    /// <returns>A task that represents the asynchronous connection operation.</returns>
    /// <remarks>
    /// Once connected, the client reconnects automatically and subscribes to all collections again.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the AutoPatch server and stops reconnecting.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the disconnection operation.</param>
    /// <returns>A task that represents the asynchronous disconnection operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to real-time updates for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to subscribe to for updates.</typeparam>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <param name="authString">Optional authentication string for subscription validation.</param>
    /// <param name="cancellationToken">A token to cancel the subscription operation.</param>
    /// <returns>A task that returns true if subscription was successful, false if rejected by server validation.</returns>
    /// <remarks>
    /// Subscribing to a collection that is already subscribed shares the collection; the server still validates the given
    /// credentials, and a rejected call leaves the existing subscription untouched.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task<bool> SubscribeToTypeAsync<T>(string? key = null, string? authString = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Unsubscribes from real-time updates for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to unsubscribe from.</typeparam>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <param name="cancellationToken">A token to cancel the unsubscription operation.</param>
    /// <returns>A task that represents the asynchronous unsubscription operation.</returns>
    /// <remarks>
    /// The server subscription ends when the last subscriber of the collection unsubscribes.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
    Task UnsubscribeFromTypeAsync<T>(string? key = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Gets the tracked collection for the specified type.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <returns>An observable collection that is synchronized with the server data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is not subscribed.</exception>
    ObservableCollection<T> GetTrackedCollection<T>(string? key = null) where T : class;
}
