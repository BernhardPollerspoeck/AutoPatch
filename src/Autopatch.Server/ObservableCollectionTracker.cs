using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.SignalR;

namespace Autopatch.Server;

public class ObservableCollectionTracker<T>(
    BulkFlushQueue<Operation<ObservableCollection<T>>> queue,
    IHubContext<AutoPatchHub> hubContext)
    : IObjectTracker<ObservableCollection<T>, T>
    where T : class, INotifyPropertyChanged
{
    private const string ADD = "add";
    private const string REMOVE = "remove";
    private const string REPLACE = "replace";
    private const string PATH_ADD = "/-";

    private readonly Dictionary<string, PropertyInfo> _propertyCache = [];

    public ObservableCollection<T> TrackedCollection { get; } = [];

    INotifyCollectionChanged IObjectTracker.TrackedCollection => TrackedCollection;

    public void StartTracking()
    {
        queue.OnFlush += HandleQueueFlush;
        TrackedCollection.CollectionChanged += HandleCollectionChanged;
    }
    public void StopTracking()
    {
        TrackedCollection.CollectionChanged -= HandleCollectionChanged;
        queue.OnFlush -= HandleQueueFlush;
    }

    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (T item in e.NewItems)
            {
                item.PropertyChanged += HandleItemPropertyChanged;

                var operation = new Operation<ObservableCollection<T>>
                {
                    op = ADD,
                    path = PATH_ADD,
                    value = item,
                };
                queue.Add(operation);
            }
        }
        if (e.OldItems != null)
        {
            foreach (T item in e.OldItems)
            {
                item.PropertyChanged -= HandleItemPropertyChanged;

                var operation = new Operation<ObservableCollection<T>>
                {
                    op = REMOVE,
                    path = $"/{e.OldStartingIndex}",
                };
                queue.Add(operation);
            }
        }
    }
    private void HandleItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not T item || string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        if (!_propertyCache.TryGetValue(e.PropertyName, out var propInfo))
        {
            propInfo = item.GetType().GetProperty(e.PropertyName);
            if (propInfo == null)
            {
                return;
            }
            _propertyCache[e.PropertyName] = propInfo;
        }

        var operation = new Operation<ObservableCollection<T>>
        {
            op = REPLACE,
            path = $"/{TrackedCollection.IndexOf(item)}/{e.PropertyName}",
            value = propInfo.GetValue(item)//TODO: we currently cant get the value without reflection here. Maybe later SourceGenerator can help?
        };
        queue.Add(operation);
    }

    private Task HandleQueueFlush(List<Operation<ObservableCollection<T>>> operations)
    {
        var document = new JsonPatchDocument<ObservableCollection<T>>();
        foreach (var op in operations)
        {
            document.Operations.Add(op);
        }

        var key = $"AutoPatch/{typeof(T).Name}";//TODO: key builder
        return hubContext.Clients.Groups(key).SendAsync(key, document);
    }
}

