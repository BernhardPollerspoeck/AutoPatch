using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using Autopatch.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Autopatch.Server;

public class AutopatchOptions
{
    public TimeSpan DefaultThrottleInterval { get; set; }
    public int MaxBatchSize { get; set; }
}

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAutoPatch(this IServiceCollection services, Action<AutopatchOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IAutoPatchService, AutoPatchService>();
        return services;
    }

    public static IServiceCollection AddObjectType<T>(this IServiceCollection services, Action<ObjectTypeConfiguration<T>> configure)
        where T : class
    {
        services.Configure(configure);

        // Register configuration here
        return services;
    }

    //public static IServiceCollection AddChangeHandler<TObject, TChangeHandler>(this IServiceCollection services)
    //    where TObject : class
    //    where TChangeHandler : class, IChangeHandler<TObject>
    //{
    //    services.AddSingleton<IChangeHandler<TObject>, TChangeHandler>();
    //    return services;
    //}
}

public static class ISignalRServerBuilderExtensions
{
    public static ISignalRServerBuilder AddAutoPatch(this ISignalRServerBuilder builder)
    {
        builder.AddHubOptions<AutoPatchHub>(cfg =>
        {
        });
        return builder;
    }


}

public static class IWebApplicationExetensions
{
    public static WebApplication UseAutoPatch(this WebApplication host)
    {
        host.MapHub<AutoPatchHub>("/Autopatch");
        return host;
    }

}
public class AutoPatchHub(IAutoPatchService autoPatchService) : Hub
{


    public Task<string> SubscribeToType(string typeName)
    {
        var groupName = autoPatchService.SubscribeToType(typeName);
        return Task.FromResult(groupName);
    }


}

public interface IAutoPatchService
{
    string SubscribeToType(string typeName);
    Task HandleChangeAsync<T>(ObjectChange<T> objectChange) where T : class;
    Task HandleBulkChangeAsync<T>(IEnumerable<ObjectChange<T>> objectChanges) where T : class;
}
public class AutoPatchService(
    IHubContext<AutoPatchHub> hubContext,
    IServiceProvider serviceProvider)
    : IAutoPatchService
{
    private Dictionary<string, string> _typeSubscriptions = [];

    public string SubscribeToType(string typeName)
    {
        if (!_typeSubscriptions.TryGetValue(typeName, out var subscriptionId))
        {
            subscriptionId = Guid.NewGuid().ToString();
            _typeSubscriptions[typeName] = subscriptionId;
        }
        return subscriptionId;
    }

    public async Task HandleBulkChangeAsync<T>(IEnumerable<ObjectChange<T>> objectChanges) where T : class
    {
        foreach (var change in objectChanges)
        {
            await HandleChangeAsync(change);
        }
    }

    public async Task HandleChangeAsync<T>(ObjectChange<T> objectChange) where T : class
    {
        var typeName = typeof(T).Name;
        var subscriptionId = _typeSubscriptions.GetValueOrDefault(typeName);
        if (subscriptionId is null)
        {
            return;
        }

        var objTypeConfiguration = serviceProvider.GetService<IOptions<ObjectTypeConfiguration<T>>>();
        if (objTypeConfiguration is null)
        {
            throw new InvalidOperationException($"No configuration found for type {typeName}. Please register it using AddObjectType<{typeName}>.");
        }

        var patchDoc = new JsonPatchDocument();
        patchDoc.Replace(objectChange.PropertyName, objectChange.NewValue);
        var patchData = JsonSerializer.Serialize(patchDoc);

        var changeSet = new AutoPatchItem
        {
            Action = AutoPatchAction.Update,
            ItemId = objTypeConfiguration.Value.KeySelector(objectChange.ChangedObject),
            Data = patchData,
        };

        await hubContext.Clients.All.SendCoreAsync(subscriptionId, [subscriptionId, changeSet]);

    }

}

public record struct ObjectChange<T>(T ChangedObject, string PropertyName, object? NewValue) where T : class;

public class ObjectTypeConfiguration<T> where T : class
{
    public Func<T, object> KeySelector { get; set; } = null!;
    public string[] ExcludedProperties { get; set; } = [];
    public ClientChangePolicy ClientChangePolicy { get; set; } = ClientChangePolicy.Auto;
    public TimeSpan? ThrottleInterval { get; set; }
}

public enum ClientChangePolicy
{
    Auto,
    Allow,
    Disallow
}

