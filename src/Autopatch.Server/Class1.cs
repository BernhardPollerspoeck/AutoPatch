using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Autopatch.Server;

public class AutopatchOptions
{
    public TimeSpan DefaultThrottleInterval { get; set; }
    public int MaxBatchSize { get; set; }
}

public class ObjectTypeConfiguration<T> where T : class
{
    public string[] ExcludedProperties { get; set; } = [];
    public ClientChangePolicy ClientChangePolicy { get; set; } = ClientChangePolicy.Auto;
    public TimeSpan? ThrottleInterval { get; set; }
}
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAutoPatch(this IServiceCollection services, Action<AutopatchOptions> configure)
    {
        services.Configure(configure);

        //services.AddSingleton<IAutoPatchService, AutoPatchService>();
        return services;
    }

    public static IServiceCollection AddTrackedCollection<TItem>(
            this IServiceCollection services,
            Action<ObjectTypeConfiguration<ObservableCollectionTracker<TItem>>>? configure = null)
            where TItem : class, INotifyPropertyChanged
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<ObservableCollectionTracker<TItem>>();
        services.AddSingleton<IObjectTracker<ObservableCollection<TItem>, TItem>>(sp => sp.GetRequiredService<ObservableCollectionTracker<TItem>>());
        services.AddSingleton<IObjectTracker>(sp => sp.GetRequiredService<ObservableCollectionTracker<TItem>>());
        services.AddSingleton(sp => sp.GetRequiredService<IObjectTracker<ObservableCollection<TItem>, TItem>>().TrackedCollection);

        return services;
    }

}

public interface IObjectTracker
{
    INotifyCollectionChanged TrackedCollection { get; }
}

public interface IObjectTracker<TCollection, TItem> : IObjectTracker
    where TCollection : INotifyCollectionChanged, IList<TItem>
{
    new TCollection TrackedCollection { get; }
}


public class ObservableCollectionTracker<T> : IObjectTracker<ObservableCollection<T>, T>
    where T : class, INotifyPropertyChanged
{
    public ObservableCollection<T> TrackedCollection { get; } = [];

    INotifyCollectionChanged IObjectTracker.TrackedCollection => TrackedCollection;
}


public static class IWebApplicationExetensions
{
    public static HubEndpointConventionBuilder UseAutoPatch(this WebApplication host)
    {
        return host.MapHub<AutoPatchHub>("/Autopatch");
    }
}




public class AutoPatchHub(IAutoPatchService autoPatchService) : Hub
{
    public Task<string> SubscribeToType(string typeName)
    {
        var groupName = autoPatchService.SubscribeToType(typeName);
        return Task.FromResult(groupName);
    }
    public Task RequestFullData(string subscriptionId)
    {
        autoPatchService.RequestFullData(subscriptionId, Context.ConnectionId);
        return Task.CompletedTask;
    }
}



//we now need some service that is subscribed to all the collections and tracks changes
//then it puts all changes into a queue and sends them out in batches based on the throttle interval












public interface IAutoPatchService
{
    string SubscribeToType(string typeName);
    void RequestFullData(string subscriptionId, string connectionId);
}
public class AutoPatchService
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
    public void RequestFullData(string subscriptionId, string connectionId)
    {
        if(!_typeSubscriptions.ContainsValue(subscriptionId))
        {
            return;
        }
        //here we need to add all objects for the collection on first place in the queue
        //only send it to the requesting connection
    }
      
}


public enum ClientChangePolicy
{
    Auto,
    Allow,
    Disallow
}

