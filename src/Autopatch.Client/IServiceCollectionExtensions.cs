using System.Text.Json;
using Autopatch.Core;
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

    public static IServiceCollection AddObjectType<T>(this IServiceCollection services, Action<ObjectTypeConfiguration<T>> configure)
        where T : class
    {
        services.Configure(configure);

        return services;
    }
}

public interface IAutoPatchClient
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<string> SubscribeToTypeAsync<T>(IList<T> values, CancellationToken cancellationToken = default) where T : class;
}

public class AutoPatchClient(
    IOptions<AutoPatchConfiguration> options,
    IServiceProvider serviceProvider)
    : IAutoPatchClient
{
    private HubConnection? _connection;
    private Dictionary<string, (IList<object>, ObjectTypeConfiguration, Type)> _subscriptions = [];

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

    public async Task<string> SubscribeToTypeAsync<T>(IList<T> values, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_connection == null)
            throw new InvalidOperationException("Not connected.");
        var subscriptionId = await _connection.InvokeAsync<string>("SubscribeToType", typeof(T).Name, cancellationToken);

        _connection.On<string, AutoPatchItem>(subscriptionId, HandleAutoPatchItem);
        _subscriptions[subscriptionId] = (values.Cast<object>().ToList(), serviceProvider.GetRequiredService<IOptions<ObjectTypeConfiguration<T>>>().Value!, typeof(T));

        return subscriptionId;
    }

    private void HandleAutoPatchItem(string subscriptionId, AutoPatchItem changeSet)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out var data))
        {
            return;
        }

        switch (changeSet.Action)
        {
            case AutoPatchAction.Add when changeSet.Data is not null:
                var addData = JsonSerializer.Deserialize<AutoPatchAddDocument>(changeSet.Data);
                if (addData is null)
                {
                    return;
                }
                var addItem = JsonSerializer.Deserialize(addData.Json, data.Item3);
                data.Item1.Insert(addData.Index, addData.Json);
                break;
            case AutoPatchAction.Remove:
                var removeItem = data.Item1.FirstOrDefault(d => data.Item2.KeySelector(d) == changeSet.ItemId);
                if (removeItem is null)
                {
                    return;
                }
                data.Item1.Remove(removeItem);
                break;
            case AutoPatchAction.Update when changeSet.Data is not null:
                var updateItem = data.Item1.FirstOrDefault(d => data.Item2.KeySelector(d) == changeSet.ItemId);
                if (updateItem is null)
                {
                    return;
                }
                var jsonPatch = JsonSerializer.Deserialize<JsonPatchDocument>(changeSet.Data);
                if (jsonPatch is null)
                {
                    return;
                }
                jsonPatch.ApplyTo(updateItem);
                break;
            default:
                //logging
                break;
        }





    }
}

public abstract class ObjectTypeConfiguration
{
    public Func<object, object> KeySelector { get; set; } = null!;
    public ChangeTrackingMode ChangeTracking { get; set; } = ChangeTrackingMode.Auto;
}
public class ObjectTypeConfiguration<T> : ObjectTypeConfiguration where T : class
{
    public new Func<T, object> KeySelector { get; set; } = null!;
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
