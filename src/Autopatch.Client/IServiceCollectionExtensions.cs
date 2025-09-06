using System.Collections;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Autopatch.Client;
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAutoPatch(this IServiceCollection services, Action<AutoPatchConfiguration> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IAutoPatchClient, AutoPatchClient>();
        services.AddHostedService<AutopatchConnectionManager>();

        return services;
    }

    public static IServiceCollection AddTrackedCollection<TItem>(this IServiceCollection services)
    {
        services.AddSingleton<ObservableCollection<TItem>>();


        return services;
    }
}

public interface IAutoPatchClient
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task SubscribeToTypeAsync<T>(CancellationToken cancellationToken = default) where T : class;
    Task UnsubscribeFromTypeAsync<T>(CancellationToken cancellationToken = default) where T : class;

    ObservableCollection<T> GetTrackedCollection<T>() where T : class;
}


public class AutoPatchClient(
    IOptions<AutoPatchConfiguration> options,
    IServiceProvider serviceProvider)
    : IAutoPatchClient
{
    private HubConnection? _connection;
    private readonly Dictionary<string, Subscription> _subscriptions = [];

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var builder = new HubConnectionBuilder()
            .WithUrl($"{options.Value.Endpoint}/Autopatch");

        _connection = builder.Build();
        return _connection.StartAsync(cancellationToken);

    }
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected.");
        return _connection.StopAsync(cancellationToken);
    }


    public async Task SubscribeToTypeAsync<T>(CancellationToken cancellationToken = default)
        where T : class
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected.");

        var methodName = $"AutoPatch/{typeof(T).Name}";

        var collection = serviceProvider.GetRequiredService<ObservableCollection<T>>();
        if (!_subscriptions.TryAdd(methodName, new Subscription(collection))
            && _subscriptions.TryGetValue(methodName, out var subscription))
        {
            subscription.Subscribers++;
        }

        _connection.On<Operation[]>(methodName, HandleAutoPatchItem);

        await _connection.InvokeAsync("SubscribeToType", typeof(T).Name, cancellationToken);
    }
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

    public ObservableCollection<T> GetTrackedCollection<T>()
        where T : class
    {
        if (!_subscriptions.TryGetValue($"AutoPatch/{typeof(T).Name}", out var subscription))
            throw new InvalidOperationException($"Type {typeof(T).Name} is not subscribed.");

        return (ObservableCollection<T>)subscription.TrackedCollection;
    }


    private void HandleAutoPatchItem(Operation[] changeSet)
    {
        // TODO: Apply patches to the tracked collection
    }
}

internal class Subscription(object collection)
{
    public object TrackedCollection { get; } = collection;
    public int Subscribers { get; set; } = 1;
}

public class AutoPatchConfiguration
{
    public string Endpoint { get; set; } = null!;

}

public enum ChangeTrackingMode
{
    Auto,
    ManualCommit,
    AutoCommit,
}
