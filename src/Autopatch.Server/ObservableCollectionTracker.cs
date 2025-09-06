using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using Autopatch.Core;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.SignalR;

namespace Autopatch.Server;

public class ObservableCollectionTracker<T>(
    BulkFlushQueue<OperationContainer<T>> queue,
    IHubContext<AutoPatchHub> hubContext)
    : IObjectTracker<ObservableCollection<T>, T>
    where T : class, INotifyPropertyChanged
{
    private const string ADD = "add";
    private const string REMOVE = "remove";
    private const string REPLACE = "replace";
    private const string PATH_ADD = "/-";

    private readonly Dictionary<string, PropertyInfo> _propertyCache = [];

    public string TypeName => typeof(T).Name;

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

    public void SendFullData(string connectionId)
    {
        var operation = new FullDataOperationContainer<T>(
            TrackedCollection.Select(item => new Operation<ObservableCollection<T>>
            {
                op = ADD,
                path = PATH_ADD,
                value = item,
            }).ToArray(),
            connectionId);
        queue.Add(operation);
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
                queue.Add(new DefaultOperationContainer<T>(operation));
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
                queue.Add(new DefaultOperationContainer<T>(operation));
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
        queue.Add(new DefaultOperationContainer<T>(operation));
    }

    private async Task HandleQueueFlush(List<OperationContainer<T>> containers)
    {
        var target = $"AutoPatch/{typeof(T).Name}";
        foreach (var container in containers)
        {
            if (container is FullDataOperationContainer<T> fullData)
            {
                await hubContext.Clients
                    .Client(fullData.ConnectionId)
                    .SendAsync(target, fullData.Operation);
            }
        }

        var operations = containers
            .Where(c => c is DefaultOperationContainer<T>)
            .Select(c => (DefaultOperationContainer<T>)c);
        await hubContext.Clients
            .All
            //.Groups(target)
            .SendAsync(target, operations);
    }
}

