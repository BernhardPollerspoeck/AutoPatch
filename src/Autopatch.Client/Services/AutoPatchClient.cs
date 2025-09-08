using System.Collections.ObjectModel;
using System.Text.Json;
using Autopatch.Client.Models;
using Microsoft.AspNetCore.JsonPatch.Adapters;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Serialization;

namespace Autopatch.Client.Services;

/// <summary>
/// Implementation of the AutoPatch client that manages real-time data synchronization through SignalR.
/// </summary>
/// <param name="options">Configuration options for the AutoPatch client.</param>
/// <param name="serviceProvider">Service provider for dependency resolution.</param>
public class AutoPatchClient(
    IOptions<AutoPatchConfiguration> options,
    IServiceProvider serviceProvider)
    : IAutoPatchClient
{
    /// <summary>
    /// JSON serializer options configured for property name case insensitivity.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// The SignalR hub connection instance.
    /// </summary>
    private HubConnection? _connection;

    /// <summary>
    /// Dictionary of active subscriptions keyed by method name.
    /// </summary>
    private readonly Dictionary<string, Subscription> _subscriptions = [];

    /// <summary>
    /// Event raised when the connection state changes.
    /// </summary>
    public event EventHandler<bool>? OnConnectionChanged;

    /// <summary>
    /// Establishes a connection to the AutoPatch server using SignalR.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the connection operation.</param>
    /// <returns>A task that represents the asynchronous connection operation.</returns>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var builder = new HubConnectionBuilder()
            .WithUrl($"{options.Value.Endpoint}/Autopatch");

        _connection = builder.Build();
        _connection.Closed += HandleConnectionClosed;
        _connection.Reconnected += HandleReconnected;
        _connection.Reconnecting += HandleReconnecting;
        await _connection.StartAsync(cancellationToken);
        OnConnectionChanged?.Invoke(this, _connection.State is HubConnectionState.Connected);
    }

    /// <summary>
    /// Disconnects from the AutoPatch server.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the disconnection operation.</param>
    /// <returns>A task that represents the asynchronous disconnection operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return _connection != null
            ? _connection.StopAsync(cancellationToken)
            : throw new InvalidOperationException("Not connected.");
    }

    /// <summary>
    /// Subscribes to real-time updates for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to subscribe to for updates.</typeparam>
    /// <param name="cancellationToken">A token to cancel the subscription operation.</param>
    /// <returns>A task that represents the asynchronous subscription operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    public async Task SubscribeToTypeAsync<T>(CancellationToken cancellationToken = default)
        where T : class
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected.");

        var methodName = $"AutoPatch/{typeof(T).Name}";

        var collection = serviceProvider.GetRequiredService<ObservableCollection<T>>();
        if (!_subscriptions.TryAdd(methodName, new Subscription(collection, typeof(T)))
            && _subscriptions.TryGetValue(methodName, out var subscription))
        {
            subscription.Subscribers++;
        }

        _connection.On<string, Operation[], bool>(methodName, HandleAutoPatchItem);

        await _connection.InvokeAsync("SubscribeToType", typeof(T).Name, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from real-time updates for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to unsubscribe from.</typeparam>
    /// <param name="cancellationToken">A token to cancel the unsubscription operation.</param>
    /// <returns>A task that represents the asynchronous unsubscription operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    public Task UnsubscribeFromTypeAsync<T>(CancellationToken cancellationToken = default)
        where T : class
    {
        if (_subscriptions.TryGetValue($"AutoPatch/{typeof(T).Name}", out var subscription))
        {
            subscription.Subscribers--;
            if (subscription.Subscribers == 0)
                _subscriptions.Remove($"AutoPatch/{typeof(T).Name}");
        }


        if (_connection == null)
            throw new InvalidOperationException("Not connected.");

        _connection.Remove($"AutoPatch/{typeof(T).Name}");
        return _connection.InvokeAsync("UnsubscribeFromType", typeof(T).Name, cancellationToken);
    }

    /// <summary>
    /// Gets the tracked collection for the specified type.
    /// </summary>
    /// <typeparam name="T">The type of items in the collection.</typeparam>
    /// <returns>An observable collection that is synchronized with the server data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is not subscribed.</exception>
    public ObservableCollection<T> GetTrackedCollection<T>()
        where T : class
    {
        return _subscriptions.TryGetValue($"AutoPatch/{typeof(T).Name}", out var subscription)
            ? (ObservableCollection<T>)subscription.TrackedCollection
            : throw new InvalidOperationException($"Type {typeof(T).Name} is not subscribed.");
    }

    /// <summary>
    /// Handles incoming AutoPatch operations from the server and applies them to the tracked collection.
    /// </summary>
    /// <param name="methodName">The method name identifying the subscription.</param>
    /// <param name="changeSet">Array of JSON Patch operations to apply.</param>
    /// <param name="isInitialSet">Indicates whether this is the initial data set.</param>
    private void HandleAutoPatchItem(string methodName, Operation[] changeSet, bool isInitialSet)
    {
        if (!_subscriptions.TryGetValue(methodName, out var subscription))
            return;

        if (!isInitialSet && !subscription.IsInitialized)
        {
            return; //Ignore until initial set is done
        }

        // Apply operations, optionally using dispatcher for UI thread marshalling
        void ApplyOperations()
        {
            var contractResolver = new DefaultContractResolver();
            var adapter = new ObjectAdapter(contractResolver, null, new AdapterFactory());

            foreach (var operation in changeSet)
            {
                if (operation.value is JsonElement jsonElement)
                {
                    operation.value = ConvertJsonElementToTargetType(jsonElement, operation, subscription);
                }
                operation.Apply(subscription.TrackedCollection, adapter);
            }

            subscription.IsInitialized = true;
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
    /// Converts a JsonElement to the appropriate target type based on the operation context.
    /// </summary>
    /// <param name="jsonElement">The JSON element to convert.</param>
    /// <param name="operation">The JSON Patch operation being applied.</param>
    /// <param name="subscription">The subscription context for type information.</param>
    /// <returns>The converted object in the correct type.</returns>
    private static object? ConvertJsonElementToTargetType(
    JsonElement jsonElement,
    Operation operation,
    Subscription subscription)
    {
        // For property updates: /0/PropertyName
        var pathParts = operation.path.Split('/');
        if (pathParts.Length == 3 && int.TryParse(pathParts[1], out _))
        {
            var propertyName = pathParts[2];
            var propertyInfo = subscription.GetPropertyInfo(propertyName);

            if (propertyInfo != null)
            {
                return ConvertJsonElementToType(jsonElement, propertyInfo.PropertyType);
            }
        }

        // For add operations: entire object
        if (operation.op == "add")
        {
            var itm = jsonElement.Deserialize(subscription.ItemType, _jsonSerializerOptions);
            return itm;
        }

        // Fallback to basic conversion
        return ConvertJsonElementToProperType(jsonElement);
    }

    /// <summary>
    /// Converts a JsonElement to a specific .NET type.
    /// </summary>
    /// <param name="jsonElement">The JSON element to convert.</param>
    /// <param name="targetType">The target .NET type to convert to.</param>
    /// <returns>The converted object or null if conversion fails.</returns>
    private static object? ConvertJsonElementToType(JsonElement jsonElement, Type targetType)
    {
        if (targetType == typeof(string))
            return jsonElement.GetString();
        if (targetType == typeof(int) || targetType == typeof(int?))
            return jsonElement.GetInt32();
        if (targetType == typeof(double) || targetType == typeof(double?))
            return jsonElement.GetDouble();
        if (targetType == typeof(bool) || targetType == typeof(bool?))
            return jsonElement.GetBoolean();
        if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            return jsonElement.GetDateTime();
        if (targetType == typeof(decimal) || targetType == typeof(decimal?))
            return jsonElement.GetDecimal();

        // For complex types, deserialize
        return jsonElement.Deserialize(targetType);
    }

    /// <summary>
    /// Converts a JsonElement to its natural .NET type based on the JSON value kind.
    /// </summary>
    /// <param name="jsonElement">The JSON element to convert.</param>
    /// <returns>The converted object in its natural .NET type.</returns>
    private static object? ConvertJsonElementToProperType(JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number => jsonElement.TryGetInt32(out var intValue) ? intValue : jsonElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.Object => jsonElement,
            _ => jsonElement.ToString()
        };
    }

    /// <summary>
    /// Handles the SignalR connection reconnecting event.
    /// Notifies subscribers that the connection is temporarily unavailable.
    /// </summary>
    /// <param name="arg">Exception that caused the reconnection, if any.</param>
    /// <returns>A completed task.</returns>
    private Task HandleReconnecting(Exception? arg)
    {
        OnConnectionChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the SignalR connection reconnected event.
    /// Notifies subscribers that the connection has been re-established.
    /// </summary>
    /// <param name="arg">Connection ID after reconnection.</param>
    /// <returns>A completed task.</returns>
    private Task HandleReconnected(string? arg)
    {
        OnConnectionChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the SignalR connection closed event.
    /// Notifies subscribers that the connection has been lost.
    /// </summary>
    /// <param name="arg">Exception that caused the connection to close, if any.</param>
    /// <returns>A completed task.</returns>
    private Task HandleConnectionClosed(Exception? arg)
    {
        OnConnectionChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }
}
