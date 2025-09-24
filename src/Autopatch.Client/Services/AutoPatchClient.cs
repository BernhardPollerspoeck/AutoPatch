using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Autopatch.Client.Models;
using Microsoft.AspNetCore.JsonPatch.Adapters;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.ServiceDiscovery;
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
        var endpoint = await ResolveEndpointAsync(cancellationToken);
        var builder = new HubConnectionBuilder()
            .WithUrl($"{endpoint}/Autopatch");

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
    /// <param name="key">Optional key to identify a specific collection of this type. If null, uses the default collection.</param>
    /// <param name="authString">Optional authentication string for subscription validation.</param>
    /// <param name="cancellationToken">A token to cancel the subscription operation.</param>
    /// <returns>A task that returns true if subscription was successful, false if rejected by server validation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not connected to the server.</exception>
    public async Task<bool> SubscribeToTypeAsync<T>(string? key = null, string? authString = null, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected.");

        var subscriptionKey = GetSubscriptionKey<T>(key);
        var methodName = $"AutoPatch/{subscriptionKey}";

        var collection = serviceProvider.GetRequiredService<ObservableCollection<T>>();
        if (!_subscriptions.TryAdd(methodName, new Subscription(collection, typeof(T)))
            && _subscriptions.TryGetValue(methodName, out var subscription))
        {
            subscription.Subscribers++;
        }

        _connection.On<string, Operation[], bool>(methodName, HandleAutoPatchItem);

        var result = await _connection.InvokeAsync<bool>("SubscribeToType", typeof(T).Name, key, authString, cancellationToken);

        // If subscription was rejected, clean up local subscription
        if (!result && _subscriptions.TryGetValue(methodName, out var failedSubscription))
        {
            failedSubscription.Subscribers--;
            if (failedSubscription.Subscribers == 0)
            {
                _subscriptions.Remove(methodName);
                _connection.Remove(methodName);
            }
        }

        return result;
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
        var subscriptionKey = GetSubscriptionKey<T>(key);
        var methodName = $"AutoPatch/{subscriptionKey}";

        if (_subscriptions.TryGetValue(methodName, out var subscription))
        {
            subscription.Subscribers--;
            if (subscription.Subscribers == 0)
                _subscriptions.Remove(methodName);
        }

        if (_connection == null)
            throw new InvalidOperationException("Not connected.");

        _connection.Remove(methodName);
        return _connection.InvokeAsync("UnsubscribeFromType", typeof(T).Name, key, cancellationToken);
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
        var subscriptionKey = GetSubscriptionKey<T>(key);
        var methodName = $"AutoPatch/{subscriptionKey}";

        return _subscriptions.TryGetValue(methodName, out var subscription)
            ? (ObservableCollection<T>)subscription.TrackedCollection
            : throw new InvalidOperationException($"Type {typeof(T).Name} with key '{key ?? "default"}' is not subscribed.");
    }

    /// <summary>
    /// Gets the subscription key for a type and optional key parameter.
    /// </summary>
    /// <typeparam name="T">The type to get the subscription key for.</typeparam>
    /// <param name="key">Optional key to identify a specific collection.</param>
    /// <returns>The subscription key in format "TypeName" or "TypeName/Key".</returns>
    private static string GetSubscriptionKey<T>(string? key)
    {
        return string.IsNullOrEmpty(key) ? typeof(T).Name : $"{typeof(T).Name}/{key}";
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
            //TODO: while we apply, client changes need to be paused in a smart way
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

    /// <summary>
    /// Resolves the endpoint URL either from direct configuration or through service discovery.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the resolution operation.</param>
    /// <returns>The resolved endpoint URL.</returns>
    /// <exception cref="InvalidOperationException">Thrown when neither endpoint nor service name is configured, or both are configured.</exception>
    private async Task<string> ResolveEndpointAsync(CancellationToken cancellationToken = default)
    {
        var config = options.Value;

        // Validate configuration
        if (string.IsNullOrEmpty(config.Endpoint) && string.IsNullOrEmpty(config.ServiceName))
        {
            throw new InvalidOperationException("Either Endpoint or ServiceName must be configured.");
        }

        if (!string.IsNullOrEmpty(config.Endpoint) && !string.IsNullOrEmpty(config.ServiceName))
        {
            throw new InvalidOperationException("Endpoint and ServiceName are mutually exclusive. Configure only one.");
        }

        // Use direct endpoint if configured
        if (!string.IsNullOrEmpty(config.Endpoint))
        {
            return config.Endpoint;
        }

        // Use service discovery if service name is configured
        var serviceEndpointResolver = serviceProvider.GetService<ServiceEndpointResolver>();
        if (serviceEndpointResolver == null)
        {
            throw new InvalidOperationException(
                "Service discovery is not configured. Add service discovery to your service collection using AddServiceDiscovery().");
        }

        var endpoints = await serviceEndpointResolver.GetEndpointsAsync(config.ServiceName!, cancellationToken);

        if (!endpoints.Endpoints.Any())
        {
            throw new InvalidOperationException($"No endpoints found for service '{config.ServiceName}'.");
        }

        var endpoint = endpoints.Endpoints.First();
        return $"{endpoint.EndPoint}";
    }
}
