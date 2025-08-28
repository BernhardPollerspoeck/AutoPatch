using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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


        return services;
    }

}

public interface IObjectTracker
{
    INotifyCollectionChanged TrackedCollection { get; }

    void StartTracking();
    void StopTracking();
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

    public void StartTracking()
    {
        TrackedCollection.CollectionChanged += HandleCollectionChange;
    }
    public void StopTracking()
    {
        TrackedCollection.CollectionChanged -= HandleCollectionChange;
    }

    private void HandleCollectionChange(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (T item in e.NewItems)
            {
                //TODO: notify add
                item.PropertyChanged += HandleItemPropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (T item in e.OldItems)
            {
                //TODO: notify remove
                item.PropertyChanged -= HandleItemPropertyChanged;
            }
        }
    }
    private void HandleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        //TODO: notify change
    }

    //maybe some queue or bucket that triggers on some debounce and max size

}


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

