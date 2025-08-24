using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Autopatch.Server;

public class AutopatchOptions
{
    public TimeSpan DefaultThrottleInterval { get; set; }
    public int MaxBatchSize { get; set; }
}

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAutoPatch(this IServiceCollection services, Action<AutopatchOptions>? configure = null)
    {
        var options = new AutopatchOptions();
        configure?.Invoke(options);
        // Register services here
        return services;
    }

    public static IServiceCollection AddObjectType<T>(this IServiceCollection services, Action<ObjectTypeConfiguration<T>> configure)
        where T : class
    {
        var config = new ObjectTypeConfiguration<T>();
        configure(config);
        // Register configuration here
        return services;
    }

    public static IServiceCollection AddChangeHandler<TObject, TChangeHandler>(this IServiceCollection services)
        where TObject : class
        where TChangeHandler : class, IChangeHandler<TObject>
    {
        services.AddTransient<IChangeHandler<TObject>, TChangeHandler>();
        return services;
    }
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

public class AutoPatchHub(IAutoPatchService autoPatchService) : Hub<IAutoPatchHubClient>
{
    public async Task SubscribeToType<T>()
        where T : class
    {
        // Create a group name based on the type name
        var groupName = $"type_{typeof(T).Name}";

        // Add the calling client to the group
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var fullData = autoPatchService.GetAllData<T>();
        await Clients.Caller.ReceiveFullData(fullData);
    }
    public async Task UnsubscribeFromType(string typeName)
    {
        // Create a group name based on the type name
        var groupName = $"type_{typeName}";

        // Remove the calling client from the group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

}
public interface IAutoPatchHubClient
{
    Task ReceiveFullData<T>(IEnumerable<T> allData) where T : class;
    Task ReceiveChanges<T>(IEnumerable<T> changes) where T : class;
}

public interface IAutoPatchService
{
    void NotifyChanged<T>(T changedObject) where T : class;
    void NotifyChangedBatch<T>(IEnumerable<T> changedObjects) where T : class;
    IEnumerable<T> GetAllData<T>() where T : class;
}

public interface IChangeHandler<T>
{
    Task HandleChangeAsync(T changedObject);
}

public class ObjectTypeConfiguration<T> where T : class
{
    public Expression<Func<T, object>> KeySelector { get; set; } = null!;
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

