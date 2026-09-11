using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Autopatch.Client.Models;
using Autopatch.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Autopatch.Client.Services;

/// <summary>
/// Implementation of the AutoPatch client that manages real-time data synchronization through SignalR.
/// </summary>
/// <remarks>
/// <para>
/// Every batch from the server carries a sequence number. Batches that are older than the current state are ignored; if a batch
/// is missing, or a batch cannot be applied, the client asks the server for the full data again.
/// </para>
/// <para>
/// The client reconnects automatically and subscribes to all collections again after a reconnect.
/// </para>
/// </remarks>
/// <param name="options">Configuration options for the AutoPatch client.</param>
/// <param name="serviceProvider">Service provider for dependency resolution.</param>
public class AutoPatchClient(
    IOptions<AutoPatchConfiguration> options,
    IServiceProvider serviceProvider)
    : IAutoPatchClient
{
    private static readonly TimeSpan[] DefaultReconnectDelays =
        [TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>
    /// Active subscriptions keyed by method name. Read on the SignalR receive thread, written by the caller.
    /// </summary>
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new(StringComparer.Ordinal);

    private HubConnection? _connection;
    private volatile bool _stopped;
    private volatile bool _disposed;
    private int _restarting;

    /// <inheritdoc />
    public event EventHandler<bool>? OnConnectionChanged;

    /// <inheritdoc />
    public event EventHandler<AutoPatchErrorEventArgs>? OnError;

    /// <summary>
    /// Establishes a connection to the AutoPatch server using SignalR. Calling it while already connected has no effect.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the connection operation.</param>
    /// <returns>A task that represents the asynchronous connection operation.</returns>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            _stopped = false;
            _connection ??= CreateConnection();
            if (_connection.State != HubConnectionState.Disconnected)
            {
                return;
            }
            await _connection.StartAsync(cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }

        OnConnectionChanged?.Invoke(this, true);
        await ResubscribeAllAsync();
    }

    /// <summary>
    /// Disconnects from the AutoPatch server and stops reconnecting.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the disconnection operation.</param>
    /// <returns>A task that represents the asynchronous disconnection operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _stopped = true;
        return _connection != null
            ? _connection.StopAsync(cancellationToken)
            : throw new InvalidOperationException("Not connected.");
    }

    /// <summary>
    /// Subscribes to real-time updates for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to subscribe to for updates.</typeparam>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <param name="authString">Optional authentication string for subscription validation.</param>
    /// <param name="cancellationToken">A token to cancel the subscription operation.</param>
    /// <returns>A task that returns true if subscription was successful, false if rejected by server validation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    public async Task<bool> SubscribeToTypeAsync<T>(string? key = null, string? authString = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var connection = _connection ?? throw new InvalidOperationException("Not connected.");
        var methodName = GetMethodName<T>(key);

        var subscription = new Subscription(serviceProvider.GetRequiredService<ObservableCollection<T>>(), typeof(T))
        {
            Key = key,
            AuthString = authString,
        };
        var current = _subscriptions.GetOrAdd(methodName, subscription);
        if (!ReferenceEquals(current, subscription))
        {
            // The collection and its handler are shared, but every call is validated with its own credentials.
            if (!await connection.InvokeAsync<bool>(AutoPatchProtocol.SubscribeMethod, current.TypeName, key, authString, cancellationToken))
            {
                return false;
            }
            lock (current)
            {
                current.Subscribers++;
            }
            return true;
        }

        RegisterHandler(connection, methodName);
        bool accepted;
        try
        {
            accepted = await connection.InvokeAsync<bool>(AutoPatchProtocol.SubscribeMethod, subscription.TypeName, key, authString, cancellationToken);
        }
        catch
        {
            RemoveSubscription(connection, methodName);
            throw;
        }

        if (!accepted)
        {
            RemoveSubscription(connection, methodName);
        }
        return accepted;
    }

    /// <summary>
    /// Unsubscribes from real-time updates for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to unsubscribe from.</typeparam>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <param name="cancellationToken">A token to cancel the unsubscription operation.</param>
    /// <returns>A task that represents the asynchronous unsubscription operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    public Task UnsubscribeFromTypeAsync<T>(string? key = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var connection = _connection ?? throw new InvalidOperationException("Not connected.");
        var methodName = GetMethodName<T>(key);

        if (!_subscriptions.TryGetValue(methodName, out var subscription))
        {
            return Task.CompletedTask;
        }

        lock (subscription)
        {
            if (--subscription.Subscribers > 0)
            {
                return Task.CompletedTask;
            }
        }

        RemoveSubscription(connection, methodName);
        return connection.InvokeAsync(AutoPatchProtocol.UnsubscribeMethod, typeof(T).Name, key, cancellationToken);
    }

    /// <summary>
    /// Gets the tracked collection for the specified type.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <returns>An observable collection that is synchronized with the server data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is not subscribed.</exception>
    public ObservableCollection<T> GetTrackedCollection<T>(string? key = null)
        where T : class
    {
        return _subscriptions.TryGetValue(GetMethodName<T>(key), out var subscription)
            ? (ObservableCollection<T>)subscription.TrackedCollection
            : throw new InvalidOperationException($"Type {typeof(T).Name} with key '{key ?? "default"}' is not subscribed.");
    }

    /// <summary>
    /// Stops the connection and releases it.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopped = true;
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }

    private static string GetMethodName<T>(string? key)
        => AutoPatchProtocol.GetMethodName(AutoPatchProtocol.GetSubscriptionKey(typeof(T).Name, key));

    private HubConnection CreateConnection()
    {
        var config = options.Value;
        var connection = new HubConnectionBuilder()
            .WithUrl(ResolveHubUrl(config.Endpoint), http =>
            {
                if (config.AccessTokenProvider is { } accessTokenProvider)
                {
                    http.AccessTokenProvider = accessTokenProvider;
                }
                config.ConfigureHttpConnection?.Invoke(http);
            })
            .WithAutomaticReconnect(new ReconnectPolicy(config.ReconnectDelays ?? DefaultReconnectDelays))
            .Build();

        connection.Closed += HandleConnectionClosed;
        connection.Reconnected += HandleReconnected;
        connection.Reconnecting += HandleReconnecting;
        return connection;
    }

    /// <summary>
    /// Uses the endpoint as hub URL; a URL without path gets the default hub path.
    /// </summary>
    private static Uri ResolveHubUrl(string endpoint)
    {
        var uri = new Uri(endpoint, UriKind.Absolute);
        return uri.AbsolutePath is "" or "/" ? new Uri(uri, AutoPatchProtocol.DefaultHubPath) : uri;
    }

    private void RegisterHandler(HubConnection connection, string methodName)
        => connection.On<string, PatchOperation[], bool, long>(methodName, (_, operations, isInitialSet, sequence)
            => HandleAutoPatchItem(methodName, operations, isInitialSet, sequence));

    private void RemoveSubscription(HubConnection connection, string methodName)
    {
        _subscriptions.TryRemove(methodName, out _);
        connection.Remove(methodName);
    }

    /// <summary>
    /// Handles incoming AutoPatch operations from the server and applies them to the tracked collection.
    /// </summary>
    /// <param name="methodName">The method name identifying the subscription.</param>
    /// <param name="changeSet">Array of JSON Patch operations to apply.</param>
    /// <param name="isInitialSet">Indicates whether this is the initial data set.</param>
    /// <param name="sequence">The sequence number of the batch.</param>
    private void HandleAutoPatchItem(string methodName, PatchOperation[] changeSet, bool isInitialSet, long sequence)
    {
        if (!_subscriptions.TryGetValue(methodName, out var subscription))
        {
            return;
        }

        void ApplyOperations()
        {
            try
            {
                lock (subscription)
                {
                    if (isInitialSet)
                    {
                        var items = CollectionPatcher.PrepareInitialSet(subscription, changeSet);
                        subscription.Items.Clear();
                        foreach (var item in items)
                        {
                            subscription.Items.Add(item);
                        }
                        subscription.Sequence = sequence;
                        subscription.IsInitialized = true;
                        subscription.EndResync();
                        return;
                    }

                    if (!subscription.IsInitialized || sequence <= subscription.Sequence)
                    {
                        return; // Not initialized yet, or already contained in the current state.
                    }

                    if (sequence != subscription.Sequence + 1)
                    {
                        RequestFullData(subscription);
                        return;
                    }

                    foreach (var step in CollectionPatcher.Prepare(subscription, changeSet))
                    {
                        step(subscription);
                    }
                    subscription.Sequence = sequence;
                }
            }
            catch (Exception ex)
            {
                RaiseError(ex, subscription);
                RequestFullData(subscription);
            }
        }

        // Use dispatcher if configured (for WPF/UI scenarios)
        if (options.Value.Dispatcher != null)
        {
            options.Value.Dispatcher(ApplyOperations);
        }
        else
        {
            ApplyOperations();
        }
    }

    /// <summary>
    /// Ignores further batches of the subscription until the server has sent the full data again.
    /// </summary>
    private void RequestFullData(Subscription subscription)
    {
        subscription.IsInitialized = false;
        if (subscription.TryBeginResync() && _connection is { State: HubConnectionState.Connected } connection)
        {
            _ = SubscribeOnServerAsync(connection, subscription);
        }
    }

    private async Task ResubscribeAllAsync()
    {
        if (_connection is not { } connection)
        {
            return;
        }

        foreach (var subscription in _subscriptions.Values)
        {
            subscription.IsInitialized = false;
            subscription.TryBeginResync();
        }
        await Task.WhenAll(_subscriptions.Values.Select(s => SubscribeOnServerAsync(connection, s)));
    }

    private async Task SubscribeOnServerAsync(HubConnection connection, Subscription subscription)
    {
        try
        {
            if (!await connection.InvokeAsync<bool>(AutoPatchProtocol.SubscribeMethod, subscription.TypeName, subscription.Key, subscription.AuthString))
            {
                RaiseError(new InvalidOperationException("The server rejected the subscription."), subscription);
            }
        }
        catch (Exception ex)
        {
            subscription.EndResync();
            RaiseError(ex, subscription);
        }
    }

    private void RaiseError(Exception exception, Subscription? subscription)
    {
        try
        {
            OnError?.Invoke(this, new AutoPatchErrorEventArgs(
                exception,
                subscription is null ? null : AutoPatchProtocol.GetSubscriptionKey(subscription.TypeName, subscription.Key)));
        }
        catch (Exception)
        {
            // An error handler must not break the receive loop.
        }
    }

    private Task HandleReconnecting(Exception? arg)
    {
        OnConnectionChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    private Task HandleReconnected(string? arg)
    {
        OnConnectionChanged?.Invoke(this, true);
        _ = ResubscribeAllAsync();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the SignalR connection closed event. Unless the client was stopped, it keeps trying to connect again.
    /// </summary>
    private Task HandleConnectionClosed(Exception? arg)
    {
        OnConnectionChanged?.Invoke(this, false);
        if (!_stopped && !_disposed && Interlocked.Exchange(ref _restarting, 1) == 0)
        {
            _ = RestartAsync();
        }
        return Task.CompletedTask;
    }

    private async Task RestartAsync()
    {
        var policy = new ReconnectPolicy(options.Value.ReconnectDelays ?? DefaultReconnectDelays);
        try
        {
            for (var attempt = 0; !_stopped && !_disposed; attempt++)
            {
                await Task.Delay(policy.GetDelay(attempt));
                try
                {
                    await ConnectAsync();
                    return;
                }
                catch (Exception ex) when (!_disposed)
                {
                    RaiseError(ex, null);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Disposed while reconnecting.
        }
        finally
        {
            Volatile.Write(ref _restarting, 0);
        }
    }

    /// <summary>
    /// Retries forever, using the configured delays and repeating the last one.
    /// </summary>
    private sealed class ReconnectPolicy(IReadOnlyList<TimeSpan> delays) : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) => GetDelay(retryContext.PreviousRetryCount);

        public TimeSpan GetDelay(long attempt) => delays.Count == 0 ? TimeSpan.FromSeconds(5) : delays[(int)Math.Min(attempt, delays.Count - 1)];
    }
}
