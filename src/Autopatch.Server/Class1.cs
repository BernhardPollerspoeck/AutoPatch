using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autopatch.Server;
public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddAutoPatch(
        this IServiceCollection services,
        Action<AutopatchOptions> configure)
    {
        services.Configure(configure);

        services.AddHostedService<AutoPatchCollectionTrackerService>();

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
        services.AddSingleton<BulkFlushQueue<Operation<ObservableCollection<TItem>>>>();

        return services;
    }

}

//connection flow idea:
//- if a client subscribes 
//- the client gets all data he also gets a timestamp
//- while the client does not have all the data, all incoming changes are queued
//- once the client has all data, the queued changes are processed in order if the timestamp is newer than the initial data timestamp


internal class AutoPatchService : IAutoPatchService
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
    public string Unsubscribe(string subscriptionId)
    {
        var item = _typeSubscriptions.FirstOrDefault(kv => kv.Value == subscriptionId);
        if (item.Key != null)
        {
            _typeSubscriptions.Remove(item.Key);
        }
        return item.Key ?? "";
    }
    public void RequestFullData(string subscriptionId, string connectionId)
    {
        if (!_typeSubscriptions.ContainsValue(subscriptionId))
        {
            return;
        }
        //here we need to add all objects for the collection on first place in the queue
        //only send it to the requesting connection
    }

}

