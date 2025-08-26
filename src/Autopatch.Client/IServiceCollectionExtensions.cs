using System.Collections;
using Microsoft.AspNetCore.JsonPatch;
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

        return services;
    }

}

public interface IAutoPatchClient
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<string> SubscribeToTypeAsync<T>(IList values, CancellationToken cancellationToken = default) where T : class;
    Task RequestFullDataAsync(string subscriptionId, CancellationToken cancellationToken = default);
}

public class AutopatchSubscription
{
    public required IList Items { get; init; }
    //public required Type ItemType { get; init; }

}

public class AutoPatchClient(
    IOptions<AutoPatchConfiguration> options)
    : IAutoPatchClient
{
    private HubConnection? _connection;
    private Dictionary<string, AutopatchSubscription> _subscriptions = [];

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

    public async Task<string> SubscribeToTypeAsync<T>(IList values, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected.");
        var subscriptionId = await _connection.InvokeAsync<string>("SubscribeToType", typeof(T).Name, cancellationToken);

        _connection.On<string, JsonPatchDocument>(subscriptionId, HandleAutoPatchItem);
        _subscriptions[subscriptionId] = new AutopatchSubscription
        {
            Items = values,
            //ItemType = typeof(T)
        };

        return subscriptionId;
    }
    public Task RequestFullDataAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected.");
        return _connection.InvokeAsync("RequestFullData", subscriptionId, cancellationToken);
    }

    private void HandleAutoPatchItem(string subscriptionId, JsonPatchDocument changeSet)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            return;
        }

        changeSet.ApplyTo(subscription.Items);
    }
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
