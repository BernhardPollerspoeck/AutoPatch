using System.Collections.Specialized;

namespace Autopatch.Server;

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

